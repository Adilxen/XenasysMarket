using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Entities;
using Xenasys.Core.Security;
using Xenasys.Core.Time;
using Xenasys.Persistence.Data;

namespace Xenasys.Persistence.Seeding;

// ==========================================================================================
// WHAT: Seeds FormActions catalog + RoleFormActions for system / backfilled custom roles.
// WHY: T1.34 — newSalon Form×Action SSoT with company isolation via Roles.CompanyId.
// EXAMPLE: EnsureFormActionCatalog → every Form gets Create/Read/Update/Delete/View links.
// ==========================================================================================
public static class FormActionRbacSeed
{
    private static readonly string[] DefaultActionNames =
    [
        "Create",
        "Read",
        "Update",
        "Delete",
        "View",
    ];

    /// <summary>
    /// Platform catalog / multi-tenant admin surfaces — Super Admin only (not Tenant Admin).
    /// </summary>
    public static readonly HashSet<string> PlatformAdminOnlyFormPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/settings/company",
        "/settings/actions",
        "/settings/forms",
        "/settings/permission-audit",
        "/settings/system",
        "/super/dashboard",
    };

    /// <summary>Staff operational form paths (Read-heavy + limited writes).</summary>
    private static readonly HashSet<string> StaffFormPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/dashboard",
        "/categories",
        "/sub-categories",
        "/brands",
        "/products",
        "/stock",
        "/sales",
        "/vendors",
        "/subsidiary-accounts",
        "/chart-of-accounts",
        "/control-accounts",
        "/finance/coa",
    };

    private static readonly HashSet<string> StaffWritePaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/categories",
        "/sub-categories",
        "/brands",
        "/products",
        "/stock",
        "/sales",
        "/vendors",
    };

    /// <summary>Finance role — Masters read + Control/Subsidiary full + COA tree.</summary>
    private static readonly HashSet<string> FinanceFormPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/dashboard",
        "/chart-of-accounts",
        "/control-accounts",
        "/subsidiary-accounts",
        "/finance/coa",
        "/company-profile",
    };

    private static readonly HashSet<string> FinanceWritePaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/control-accounts",
        "/subsidiary-accounts",
    };

    public static async Task EnsureFormActionCatalogAsync(
        ApplicationDbContext db,
        int actorId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var forms = await db.Forms.AsNoTracking()
            .Where(f => f.IsActive)
            .Select(f => new { f.Id, f.Path })
            .ToListAsync(cancellationToken);

        var actions = await db.Actions.AsNoTracking()
            .Where(a => a.IsActive && DefaultActionNames.Contains(a.Name))
            .ToListAsync(cancellationToken);

        if (forms.Count == 0 || actions.Count == 0)
        {
            return;
        }

        var existing = await db.FormActions.AsNoTracking()
            .Where(fa => fa.IsActive)
            .Select(fa => new { fa.FormId, fa.ActionId })
            .ToListAsync(cancellationToken);
        var existingSet = existing
            .Select(e => (e.FormId, e.ActionId))
            .ToHashSet();

        foreach (var form in forms)
        {
            foreach (var action in actions)
            {
                if (existingSet.Contains((form.Id, action.Id)))
                {
                    continue;
                }

                db.FormActions.Add(new FormAction
                {
                    FormId = form.Id,
                    ActionId = action.Id,
                    IsActive = true,
                    InsertDate = utcNow,
                    InsertUserId = actorId,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public static async Task EnsureSystemRoleFormActionsAsync(
        ApplicationDbContext db,
        int? companyId,
        CancellationToken cancellationToken = default)
    {
        var catalog = await (
                from fa in db.FormActions.AsNoTracking()
                join f in db.Forms.AsNoTracking() on fa.FormId equals f.Id
                join a in db.Actions.AsNoTracking() on fa.ActionId equals a.Id
                where fa.IsActive && f.IsActive && a.IsActive
                select new { fa.FormId, fa.ActionId, f.Path, a.Name })
            .ToListAsync(cancellationToken);

        if (catalog.Count == 0)
        {
            return;
        }

        var rolesQuery = db.Roles.AsQueryable();
        if (companyId is int)
        {
            // Per-company bootstrap no longer creates TENANT_* — nothing to map here.
            return;
        }

        rolesQuery = rolesQuery.Where(r =>
            (r.RoleCode == SeedConstants.RoleSuperAdmin && r.CompanyId == null)
            || (r.RoleCode == SeedConstants.RoleTenantAdmin && r.CompanyId == null)
            || (r.RoleCode == SeedConstants.RoleFinance && r.CompanyId == null));

        var roles = await rolesQuery.ToListAsync(cancellationToken);
        foreach (var role in roles)
        {
            IEnumerable<(int FormId, int ActionId)> desired = role.RoleCode switch
            {
                var c when string.Equals(c, SeedConstants.RoleSuperAdmin, StringComparison.OrdinalIgnoreCase)
                    => catalog.Select(x => (x.FormId, x.ActionId)),
                var c when string.Equals(c, SeedConstants.RoleTenantAdmin, StringComparison.OrdinalIgnoreCase)
                    => catalog
                        .Where(x => IsTenantAdminFormAction(x.Path, x.Name))
                        .Select(x => (x.FormId, x.ActionId)),
                var c when string.Equals(c, SeedConstants.RoleFinance, StringComparison.OrdinalIgnoreCase)
                    => catalog
                        .Where(x => IsFinanceFormAction(x.Path, x.Name))
                        .Select(x => (x.FormId, x.ActionId)),
                _ => [],
            };

            await SyncRoleFormActionsAsync(db, role.Id, desired.ToHashSet(), cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Tenant Admin: all non-platform forms + header-only /company-profile Read/Update/View (T1.57).
    /// Chart of Accounts (masters) is read-only for tenants (BRD Task 1.1 / T1.61).
    /// Does not grant /settings/company (listing stays Super Admin / PlatformAdminOnly).
    /// </summary>
    private static bool IsTenantAdminFormAction(string path, string actionName)
    {
        if (string.Equals(path, "/company-profile", StringComparison.OrdinalIgnoreCase))
        {
            return actionName is "Read" or "View" or "Update";
        }

        if (string.Equals(path, "/chart-of-accounts", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, "/finance/coa", StringComparison.OrdinalIgnoreCase))
        {
            return actionName is "Read" or "View";
        }

        // Control + Subsidiary — BRD §3.2 Tenant Admin Full Control (Own Tenant).
        return !PlatformAdminOnlyFormPaths.Contains(path);
    }

    private static bool IsFinanceFormAction(string path, string actionName)
    {
        if (!FinanceFormPaths.Contains(path))
        {
            return false;
        }

        if (string.Equals(path, "/chart-of-accounts", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, "/finance/coa", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, "/dashboard", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, "/company-profile", StringComparison.OrdinalIgnoreCase))
        {
            return actionName is "Read" or "View"
                || (string.Equals(path, "/company-profile", StringComparison.OrdinalIgnoreCase)
                    && actionName is "Update");
        }

        if (FinanceWritePaths.Contains(path))
        {
            return actionName is "Create" or "Read" or "Update" or "Delete" or "View";
        }

        return actionName is "Read" or "View";
    }

    // Keep StaffFormPaths / StaffWritePaths available for custom role templates.
    private static bool IsStaffFormAction(string path, string actionName)
    {
        if (StaffWritePaths.Contains(path))
        {
            return actionName is "Create" or "Read" or "Update" or "Delete" or "View";
        }

        if (StaffFormPaths.Contains(path))
        {
            return actionName is "Read" or "View";
        }

        return false;
    }

    /// <summary>
    /// Seeds a new company custom role with staff operational Form×Action grants (M6.3).
    /// TA can tighten/widen via PUT /api/roles/{id}/form-actions.
    /// </summary>
    public static async Task ApplyStaffTemplateAsync(
        ApplicationDbContext db,
        int roleId,
        CancellationToken cancellationToken = default)
    {
        var catalog = await (
                from fa in db.FormActions.AsNoTracking()
                join f in db.Forms.AsNoTracking() on fa.FormId equals f.Id
                join a in db.Actions.AsNoTracking() on fa.ActionId equals a.Id
                where fa.IsActive && f.IsActive && a.IsActive
                select new { fa.FormId, fa.ActionId, f.Path, a.Name })
            .ToListAsync(cancellationToken);

        var desired = catalog
            .Where(x => IsStaffFormAction(x.Path, x.Name))
            .Select(x => (x.FormId, x.ActionId))
            .ToHashSet();

        await SyncRoleFormActionsAsync(db, roleId, desired, cancellationToken);
    }

    public static async Task SyncRoleFormActionsAsync(
        ApplicationDbContext db,
        int roleId,
        HashSet<(int FormId, int ActionId)> desired,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.RoleFormActions
            .Where(rfa => rfa.RoleId == roleId)
            .ToListAsync(cancellationToken);

        var now = AuditClock.Now;
        foreach (var row in existing)
        {
            var key = (row.FormId, row.ActionId);
            if (desired.Contains(key))
            {
                if (!row.IsActive)
                {
                    row.IsActive = true;
                    row.UpdateDate = now;
                }

                desired.Remove(key);
            }
            else if (row.IsActive)
            {
                row.IsActive = false;
                row.UpdateDate = now;
            }
        }

        foreach (var (formId, actionId) in desired)
        {
            db.RoleFormActions.Add(new RoleFormAction
            {
                RoleId = roleId,
                FormId = formId,
                ActionId = actionId,
                IsActive = true,
                InsertDate = now,
            });
        }
    }
}
