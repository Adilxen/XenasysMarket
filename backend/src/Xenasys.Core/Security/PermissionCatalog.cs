using Xenasys.Core.Entities;

namespace Xenasys.Core.Security;

/// <summary>BRD §3.2 permission codes seeded as system-locked rows.</summary>
public static class PermissionCatalog
{
    public static readonly (string Code, string Module, string Action, string Description)[] All =
    [
        ("categories.create", "categories", "Create", "Create categories / sub-categories"),
        ("categories.read", "categories", "Read", "View categories / sub-categories"),
        ("categories.update", "categories", "Update", "Edit categories / sub-categories"),
        ("categories.delete", "categories", "Delete", "Delete categories / sub-categories"),

        ("brands.create", "brands", "Create", "Create brands"),
        ("brands.read", "brands", "Read", "View brands"),
        ("brands.update", "brands", "Update", "Edit brands"),
        ("brands.delete", "brands", "Delete", "Delete / deactivate brands"),

        ("products.create", "products", "Create", "Create product listings"),
        ("products.read", "products", "Read", "View product listings"),
        ("products.update", "products", "Update", "Edit product listings"),
        ("products.delete", "products", "Delete", "Delete / deactivate products"),

        ("stock.create", "stock", "Create", "Create stock movements"),
        ("stock.read", "stock", "Read", "View stock movements / balances"),
        ("stock.update", "stock", "Update", "Adjust stock movements"),
        ("stock.delete", "stock", "Delete", "Void stock movements"),

        ("platform.read", "platform", "Read", "View Super Admin cross-tenant aggregations"),

        ("dashboard.read", "dashboard", "Read", "View company / platform dashboard"),

        ("coa.masters.create", "coa.masters", "Create", "Create master accounts"),
        ("coa.masters.read", "coa.masters", "Read", "View master accounts"),
        ("coa.masters.update", "coa.masters", "Update", "Edit master accounts"),
        ("coa.masters.delete", "coa.masters", "Delete", "Deactivate master accounts"),

        ("coa.controls.create", "coa.controls", "Create", "Create control accounts"),
        ("coa.controls.read", "coa.controls", "Read", "View control accounts"),
        ("coa.controls.update", "coa.controls", "Update", "Edit control accounts"),
        ("coa.controls.delete", "coa.controls", "Delete", "Deactivate control accounts"),

        ("coa.subsidiaries.create", "coa.subsidiaries", "Create", "Create subsidiary accounts"),
        ("coa.subsidiaries.read", "coa.subsidiaries", "Read", "View subsidiary accounts"),
        ("coa.subsidiaries.update", "coa.subsidiaries", "Update", "Edit subsidiary accounts"),
        ("coa.subsidiaries.delete", "coa.subsidiaries", "Delete", "Delete subsidiary accounts"),

        ("vendors.create", "vendors", "Create", "Create vendors"),
        ("vendors.read", "vendors", "Read", "View vendors"),
        ("vendors.update", "vendors", "Update", "Edit vendors"),
        ("vendors.delete", "vendors", "Delete", "Delete / deactivate vendors"),

        ("sales.create", "sales", "Create", "Create sales orders"),
        ("sales.read", "sales", "Read", "View sales orders"),
        ("sales.update", "sales", "Update", "Edit sales order header notes"),
        ("sales.delete", "sales", "Delete", "Cancel sales orders and restore stock"),

        ("company.read", "company", "Read", "View company / tenant profile"),
        ("company.create", "company", "Create", "Register a new tenant company"),
        ("company.update", "company", "Update", "Edit company / tenant profile"),
        ("company.delete", "company", "Delete", "Deactivate a tenant company"),

        ("roles.read", "roles", "Read", "View roles and permission maps"),
        ("roles.create", "roles", "Create", "Create custom operational roles"),
        ("roles.update", "roles", "Update", "Edit roles and permission maps"),
        ("roles.delete", "roles", "Delete", "Deactivate custom operational roles"),
        // Legacy alias kept for existing FE / seeds until clients migrate to roles.update
        ("roles.manage", "roles", "Update", "Manage custom operational role permissions"),

        ("users.read", "users", "Read", "View tenant users and memberships"),
        ("users.manage", "users", "Update", "Create tenant users and assign roles"),

        ("forms.read", "forms", "Read", "View Forms navigation catalog"),
        ("forms.manage", "forms", "Update", "Create / edit / deactivate Forms catalog"),

        ("actions.read", "actions", "Read", "View Actions catalog"),
        ("actions.manage", "actions", "Update", "Create / edit / deactivate Actions catalog"),
    ];

    /// <summary>Tenant Staff default operational subset (no destructive catalog deletes / role manage).</summary>
    public static readonly HashSet<string> TenantStaffDefaults = new(StringComparer.OrdinalIgnoreCase)
    {
        "categories.read",
        "categories.create",
        "categories.update",
        "brands.read",
        "brands.create",
        "brands.update",
        "products.create",
        "products.read",
        "products.update",
        "stock.create",
        "stock.read",
        "coa.masters.read",
        "coa.controls.read",
        "coa.subsidiaries.read",
        "vendors.read",
        "vendors.create",
        "vendors.update",
        "sales.read",
        "sales.create",
        "sales.update",
        "company.read",
    };

    /// <summary>Finance ops — BRD §3.2 configurable Control/Subsidiary beyond TA defaults.</summary>
    public static readonly HashSet<string> FinanceDefaults = new(StringComparer.OrdinalIgnoreCase)
    {
        "coa.masters.read",
        "coa.controls.create",
        "coa.controls.read",
        "coa.controls.update",
        "coa.controls.delete",
        "coa.subsidiaries.create",
        "coa.subsidiaries.read",
        "coa.subsidiaries.update",
        "coa.subsidiaries.delete",
        "company.read",
    };

    public static IEnumerable<string> CodesForRole(string roleCode) =>
        roleCode switch
        {
            SeedConstants.RoleSuperAdmin => All.Select(p => p.Code),
            SeedConstants.RoleTenantAdmin => All.Select(p => p.Code)
                .Where(c =>
                    !c.StartsWith("platform.", StringComparison.OrdinalIgnoreCase)
                    && (!c.StartsWith("roles.", StringComparison.OrdinalIgnoreCase)
                     || c is "roles.read" or "roles.create" or "roles.update"
                         or "roles.delete" or "roles.manage")
                    && (!c.StartsWith("users.", StringComparison.OrdinalIgnoreCase)
                        || c is "users.read" or "users.manage")
                    && (!c.StartsWith("company.", StringComparison.OrdinalIgnoreCase)
                        || c is "company.read" or "company.update" or "company.delete")
                    && (!c.StartsWith("coa.masters.", StringComparison.OrdinalIgnoreCase)
                        || c is "coa.masters.read")),
            SeedConstants.RoleTenantStaff => TenantStaffDefaults,
            SeedConstants.RoleFinance => FinanceDefaults,
            _ => [],
        };
}
