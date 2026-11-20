using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions.Coding;
using Xenasys.Core.Entities;
using Xenasys.Core.Services;
using Xenasys.Core.Time;
using Xenasys.Persistence.Data;

namespace Xenasys.Persistence.Coding;

// ==========================================================================================
// WHAT: Platform-year sequence → globally unique BRD AccountNo + tenant AccountCode (M/P/V).
// WHY: ACC-yyyy-seq5 must never collide across companies (SA grid / BRD unique example).
// EXAMPLE: Seq 42 → ACC-2026-00042 + ACM007-M00042
// ==========================================================================================
public sealed class SubsidiaryAccountNoAllocator : ISubsidiaryAccountNoAllocator
{
    private const int MaxAttempts = 8;

    private readonly ApplicationDbContext _db;

    public SubsidiaryAccountNoAllocator(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<SubsidiaryAccountCodes> AllocateManualAsync(
        int companyId,
        CancellationToken cancellationToken = default) =>
        AllocateAsync(companyId, SubsidiaryAccountCoding.KindManual, cancellationToken);

    public Task<SubsidiaryAccountCodes> AllocateProductAsync(
        int companyId,
        CancellationToken cancellationToken = default) =>
        AllocateAsync(companyId, SubsidiaryAccountCoding.KindProduct, cancellationToken);

    public Task<SubsidiaryAccountCodes> AllocateVendorAsync(
        int companyId,
        CancellationToken cancellationToken = default) =>
        AllocateAsync(companyId, SubsidiaryAccountCoding.KindVendor, cancellationToken);

    /// <summary>Next globally unique AccountNo only (baseline Cash/Bank leaves).</summary>
    public async Task<string> AllocateAccountNoAsync(CancellationToken cancellationToken = default)
    {
        var year = AuditClock.Now.Year;
        var sequenceKey = SequenceKey(year);
        await EnsureSequenceRowAsync(sequenceKey, year, cancellationToken);

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var next = await NextValueAsync(sequenceKey, cancellationToken);
            var accountNo = SubsidiaryAccountCoding.FormatAccountNo(year, next);
            var taken = await _db.SubsidiaryAccounts
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(s => s.AccountNo == accountNo, cancellationToken);

            if (!taken)
            {
                return accountNo;
            }
        }

        throw new InvalidOperationException(
            "Unable to allocate unique Account No after multiple sequence advances.");
    }

    private async Task<SubsidiaryAccountCodes> AllocateAsync(
        int companyId,
        string codeKind,
        CancellationToken cancellationToken)
    {
        if (companyId <= 0)
        {
            throw new InvalidOperationException("Company context is required to allocate Account codes.");
        }

        var tenantCode = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == companyId)
            .Select(t => t.TenantCode)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(tenantCode))
        {
            throw new InvalidOperationException(
                $"Company {companyId} has no TenantCode — cannot allocate Account codes.");
        }

        var year = AuditClock.Now.Year;
        var sequenceKey = SequenceKey(year);
        await EnsureSequenceRowAsync(sequenceKey, year, cancellationToken);

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var next = await NextValueAsync(sequenceKey, cancellationToken);
            var accountNo = SubsidiaryAccountCoding.FormatAccountNo(year, next);
            var accountCode = SubsidiaryAccountCoding.FormatSequencedCode(tenantCode, codeKind, next);

            var taken = await _db.SubsidiaryAccounts
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(
                    s => s.AccountNo == accountNo
                        || (s.CompanyId == companyId && s.SubsidiaryAccountCode == accountCode),
                    cancellationToken);

            if (!taken)
            {
                return new SubsidiaryAccountCodes(accountNo, accountCode, next);
            }
        }

        throw new InvalidOperationException(
            "Unable to allocate unique Account No / Account Code after multiple sequence advances.");
    }

    public static string SequenceKey(int year) => $"SUB_ACCNO_{year}";

    private async Task EnsureSequenceRowAsync(
        string sequenceKey,
        int year,
        CancellationToken cancellationToken)
    {
        var bootstrap = await ComputeAccountNoBootstrapAsync(year, cancellationToken);

        var existing = await _db.PlatformSequences
            .FirstOrDefaultAsync(s => s.SequenceKey == sequenceKey, cancellationToken);

        if (existing is not null)
        {
            // Seed / load-test inserts can leave LastValue behind max ACC-yyyy-#####.
            if (bootstrap > existing.LastValue)
            {
                existing.LastValue = bootstrap;
                await _db.SaveChangesAsync(cancellationToken);
                _db.ChangeTracker.Clear();
            }

            return;
        }

        // Adopt max of any legacy per-company year keys.
        var legacyMax = await _db.PlatformSequences
            .AsNoTracking()
            .Where(s => s.SequenceKey.StartsWith("SUB_ACCNO_"))
            .Select(s => (long?)s.LastValue)
            .MaxAsync(cancellationToken);
        if (legacyMax is long life && life > bootstrap)
        {
            bootstrap = life;
        }

        _db.PlatformSequences.Add(new PlatformSequence
        {
            SequenceKey = sequenceKey,
            LastValue = bootstrap,
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
        }
    }

    private async Task<long> ComputeAccountNoBootstrapAsync(
        int year,
        CancellationToken cancellationToken)
    {
        var existingNos = await _db.SubsidiaryAccounts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(s => s.AccountNo)
            .ToListAsync(cancellationToken);

        long bootstrap = 0;
        foreach (var no in existingNos)
        {
            if (!SubsidiaryAccountCoding.TryParseAccountNoSequence(no, out var seq))
            {
                continue;
            }

            var yearOk = !SubsidiaryAccountCoding.TryParseAccountNoYear(no, out var noYear)
                || noYear == year;
            if (yearOk && seq > bootstrap)
            {
                bootstrap = seq;
            }
        }

        return bootstrap;
    }

    private async Task<long> NextValueAsync(string sequenceKey, CancellationToken cancellationToken)
    {
        const string sql =
            """
            UPDATE [PlatformSequences] WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
            SET [LastValue] = [LastValue] + 1
            OUTPUT INSERTED.[LastValue] AS [LastValue]
            WHERE [SequenceKey] = @key;
            """;

        var key = new SqlParameter("@key", sequenceKey);
        var rows = await _db.Database
            .SqlQueryRaw<SequenceAdvanceResult>(sql, key)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            throw new InvalidOperationException($"Platform sequence '{sequenceKey}' is missing.");
        }

        return rows[0].LastValue;
    }

    private sealed class SequenceAdvanceResult
    {
        public long LastValue { get; set; }
    }
}
