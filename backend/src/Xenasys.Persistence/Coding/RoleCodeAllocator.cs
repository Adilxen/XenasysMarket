using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions.Coding;
using Xenasys.Core.Entities;
using Xenasys.Core.Services;
using Xenasys.Persistence.Data;

namespace Xenasys.Persistence.Coding;

// ==========================================================================================
// WHAT: Allocates Role Code via UPDLOCK sequence + per-company uniqueness check.
// WHY: BRD company-scoped roles (T1.28) — RoleCode unique within CompanyId.
// EXAMPLE: AllocateAsync(5, "Buyer") → UPDATE…OUTPUT → "BU0042".
// ==========================================================================================
public sealed class RoleCodeAllocator : IRoleCodeAllocator
{
    private const int MaxAttempts = 8;

    private readonly ApplicationDbContext _db;

    public RoleCodeAllocator(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<string> AllocateAsync(
        int companyId,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        await EnsureSequenceRowAsync(cancellationToken);

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var next = await NextValueAsync(cancellationToken);
            var code = RoleCodeGenerator.Generate(roleName, next);

            var taken = await _db.Roles
                .AsNoTracking()
                .AnyAsync(
                    r => r.CompanyId == companyId && r.RoleCode == code,
                    cancellationToken);

            if (!taken)
            {
                return code;
            }
        }

        throw new InvalidOperationException(
            "Unable to allocate a unique Role Code after multiple sequence advances. " +
            "Sequence space may be exhausted (max 9999) or flooded with collisions.");
    }

    private async Task EnsureSequenceRowAsync(CancellationToken cancellationToken)
    {
        var exists = await _db.PlatformSequences
            .AsNoTracking()
            .AnyAsync(s => s.SequenceKey == RoleCodeGenerator.SequenceKey, cancellationToken);

        if (exists)
        {
            return;
        }

        var bootstrap = await _db.Roles
            .AsNoTracking()
            .CountAsync(
                r => r.CompanyId != null
                    && r.RoleCode != SeedConstants.RoleTenantAdmin
                    && r.RoleCode != SeedConstants.RoleTenantStaff,
                cancellationToken);

        _db.PlatformSequences.Add(new PlatformSequence
        {
            SequenceKey = RoleCodeGenerator.SequenceKey,
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

    private async Task<long> NextValueAsync(CancellationToken cancellationToken)
    {
        const string sql =
            """
            UPDATE [PlatformSequences] WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
            SET [LastValue] = [LastValue] + 1
            OUTPUT INSERTED.[LastValue] AS [LastValue]
            WHERE [SequenceKey] = @key;
            """;

        var key = new SqlParameter("@key", RoleCodeGenerator.SequenceKey);
        var rows = await _db.Database
            .SqlQueryRaw<SequenceAdvanceResult>(sql, key)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            throw new InvalidOperationException(
                $"Platform sequence '{RoleCodeGenerator.SequenceKey}' is missing.");
        }

        return rows[0].LastValue;
    }

    private sealed class SequenceAdvanceResult
    {
        public long LastValue { get; set; }
    }
}
