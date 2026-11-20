using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xenasys.Core.Entities;
using Xenasys.Core.Services;
using Xenasys.Core.Time;
using Xenasys.Persistence.Coding;
using Xenasys.Persistence.Data;

namespace Xenasys.Persistence.Seeding;

// ==========================================================================================
// WHAT: Per-company COA baseline — INV/AP/Cash/Bank/Sales/COGS controls + Cash/Bank leaves.
// WHY: BRD §3.2 tenant-owned Controls; company create + seed engine share one path.
// EXAMPLE: EnsureCompanyBaselineAsync(companyId:12) → AST-CTL-INV + ACM007-CASH under Cash ctrl.
// ==========================================================================================
public static class TenantCoaBootstrap
{
    public static async Task EnsureCompanyBaselineAsync(
        ApplicationDbContext db,
        int companyId,
        int actorId,
        DateTime utcNow,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        if (companyId <= 0)
        {
            return;
        }

        var assets = await db.MasterAccounts.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Code == SeedConstants.MasterAssetsCode && m.IsActive, cancellationToken);
        var liabilities = await db.MasterAccounts.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Code == SeedConstants.MasterLiabilitiesCode && m.IsActive, cancellationToken);
        var income = await db.MasterAccounts.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Code == SeedConstants.MasterIncomeCode && m.IsActive, cancellationToken);
        var expense = await db.MasterAccounts.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Code == SeedConstants.MasterExpenseCode && m.IsActive, cancellationToken);

        if (assets is null || liabilities is null || income is null || expense is null)
        {
            logger?.LogWarning("Skip tenant COA baseline — master accounts missing");
            return;
        }

        var tenantCode = await db.Tenants.AsNoTracking()
            .Where(t => t.Id == companyId)
            .Select(t => t.TenantCode)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(tenantCode))
        {
            return;
        }

        var inv = await EnsureControlAsync(
            db, companyId, SeedConstants.InventoryControlCode, "Inventory Control Account",
            ControlAccountKinds.Inventory, assets.Id, actorId, utcNow, cancellationToken);
        var ap = await EnsureControlAsync(
            db, companyId, SeedConstants.AccountsPayableControlCode, "Accounts Payable Control Account",
            ControlAccountKinds.AccountsPayable, liabilities.Id, actorId, utcNow, cancellationToken);
        var cashCtrl = await EnsureControlAsync(
            db, companyId, SeedConstants.CashControlCode, "Cash Control Account",
            ControlAccountKinds.Other, assets.Id, actorId, utcNow, cancellationToken);
        var bankCtrl = await EnsureControlAsync(
            db, companyId, SeedConstants.BankControlCode, "Bank Control Account",
            ControlAccountKinds.Other, assets.Id, actorId, utcNow, cancellationToken);
        await EnsureControlAsync(
            db, companyId, SeedConstants.SalesControlCode, "Sales Control Account",
            ControlAccountKinds.Other, income.Id, actorId, utcNow, cancellationToken);
        await EnsureControlAsync(
            db, companyId, SeedConstants.CogsControlCode, "Cost of Goods Sold Control Account",
            ControlAccountKinds.Other, expense.Id, actorId, utcNow, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        await EnsureBaselineSubsidiaryAsync(
            db, companyId, tenantCode, cashCtrl.Id, "CASH", "Cash", actorId, utcNow, cancellationToken);
        await EnsureBaselineSubsidiaryAsync(
            db, companyId, tenantCode, bankCtrl.Id, "BANK", "Bank", actorId, utcNow, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        _ = inv;
        _ = ap;
    }

    private static async Task<ControlAccount> EnsureControlAsync(
        ApplicationDbContext db,
        int companyId,
        string code,
        string title,
        string controlKind,
        int masterAccountId,
        int actorId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        ControlAccount? row = null;
        if (controlKind is ControlAccountKinds.Inventory or ControlAccountKinds.AccountsPayable)
        {
            row = await db.ControlAccounts.IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    c => c.CompanyId == companyId && c.ControlKind == controlKind,
                    cancellationToken);
        }

        row ??= await db.ControlAccounts.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.Code == code, cancellationToken);

        if (row is null)
        {
            row = new ControlAccount
            {
                CompanyId = companyId,
                MasterAccountId = masterAccountId,
                Code = code,
                Title = title,
                ControlKind = controlKind,
                IsActive = true,
                InsertDate = utcNow,
                InsertUserId = actorId,
            };
            db.ControlAccounts.Add(row);
            return row;
        }

        var dirty = false;
        if (row.MasterAccountId != masterAccountId)
        {
            row.MasterAccountId = masterAccountId;
            dirty = true;
        }

        if (!string.Equals(row.Code, code, StringComparison.Ordinal))
        {
            row.Code = code;
            dirty = true;
        }

        if (!string.Equals(row.Title, title, StringComparison.Ordinal))
        {
            row.Title = title;
            dirty = true;
        }

        if (!string.Equals(row.ControlKind, controlKind, StringComparison.Ordinal))
        {
            row.ControlKind = controlKind;
            dirty = true;
        }

        if (!row.IsActive)
        {
            row.IsActive = true;
            dirty = true;
        }

        if (dirty)
        {
            row.UpdateDate = utcNow;
            row.UpdateUserId = actorId;
        }

        return row;
    }

    private static async Task EnsureBaselineSubsidiaryAsync(
        ApplicationDbContext db,
        int companyId,
        string tenantCode,
        int controlId,
        string suffix,
        string title,
        int actorId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var code = SubsidiaryAccountCoding.FormatBaselineCode(tenantCode, suffix);
        var exists = await db.SubsidiaryAccounts.IgnoreQueryFilters()
            .AnyAsync(
                s => s.CompanyId == companyId
                    && (s.SubsidiaryAccountCode == code || s.SubsidiaryAccountCode == $"OTH-{suffix}"),
                cancellationToken);
        if (exists)
        {
            return;
        }

        var accountNo = await AllocateGlobalAccountNoAsync(db, cancellationToken);

        db.SubsidiaryAccounts.Add(new SubsidiaryAccount
        {
            CompanyId = companyId,
            ControlAccountId = controlId,
            SubsidiaryAccountCode = code,
            SubsidiaryAccountTitle = title,
            AccountNo = accountNo,
            IsActive = true,
            IsSystemGenerated = true,
            InsertDate = utcNow,
            InsertUserId = actorId,
        });
    }

    /// <summary>Platform-year sequence → globally unique ACC-yyyy-seq5 (same SSoT as allocator).</summary>
    private static async Task<string> AllocateGlobalAccountNoAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var year = AuditClock.Now.Year;
        var sequenceKey = SubsidiaryAccountNoAllocator.SequenceKey(year);

        var exists = await db.PlatformSequences
            .AsNoTracking()
            .AnyAsync(s => s.SequenceKey == sequenceKey, cancellationToken);
        if (!exists)
        {
            var existingNos = await db.SubsidiaryAccounts
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

            db.PlatformSequences.Add(new PlatformSequence
            {
                SequenceKey = sequenceKey,
                LastValue = bootstrap,
            });
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
            }
        }

        const string sql =
            """
            UPDATE [PlatformSequences] WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
            SET [LastValue] = [LastValue] + 1
            OUTPUT INSERTED.[LastValue] AS [LastValue]
            WHERE [SequenceKey] = @key;
            """;

        var key = new SqlParameter("@key", sequenceKey);
        var rows = await db.Database
            .SqlQueryRaw<SequenceAdvanceResult>(sql, key)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            throw new InvalidOperationException($"Platform sequence '{sequenceKey}' is missing.");
        }

        return SubsidiaryAccountCoding.FormatAccountNo(year, rows[0].LastValue);
    }

    private sealed class SequenceAdvanceResult
    {
        public long LastValue { get; set; }
    }
}
