namespace Xenasys.Core.Entities;

/// <summary>
/// Natural-key constants for idempotent seeding (no hardcoded entity PKs).
/// Primary keys are SQL Server IDENTITY — leave Id = 0 for DB generation.
/// </summary>
public static class SeedConstants
{
    public const string MasterTenantCode = "MASTER";

    public const string MasterTenantName = "Xenasys Master Tenant";

    public const string SuperAdminUserName = "superadmin";

    public const string SuperAdminEmail = "superadmin@xenasys.local";

    public const string SuperAdminUserNn = "00";

    /// <summary>Dev-only placeholder — rotate before any shared environment.</summary>
    public const string SuperAdminPlaceholderPassword = "SuperAdmin@12345";

    public const string TenantAdminUserName = "tenantadmin";

    public const string TenantAdminEmail = "tenantadmin@xenasys.local";

    public const string TenantAdminUserNn = "01";

    public const string TenantAdminPlaceholderPassword = "TenantAdmin@12345";

    public const string RoleSuperAdmin = "SUPER_ADMIN";

    public const string RoleTenantAdmin = "TENANT_ADMIN";

    public const string RoleTenantStaff = "TENANT_STAFF";

    /// <summary>Finance operational template — Control + Subsidiary full; Masters read.</summary>
    public const string RoleFinance = "FINANCE";

    /// <summary>True when RoleCode is a seeded platform role (not editable/deletable).</summary>
    public static bool IsSystemRoleCode(string? roleCode) =>
        string.Equals(roleCode, RoleSuperAdmin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleCode, RoleTenantAdmin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleCode, RoleTenantStaff, StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleCode, RoleFinance, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Shared TENANT_ADMIN (CompanyId NULL) — not SUPER_ADMIN.
    /// Do not call inside IQueryable; EF cannot translate this method — inline
    /// <c>CompanyId == null &amp;&amp; RoleCode == RoleTenantAdmin</c> instead.
    /// </summary>
    public static bool IsPlatformTenantSystemRole(int? companyId, string? roleCode) =>
        companyId is null
        && string.Equals(roleCode, RoleTenantAdmin, StringComparison.OrdinalIgnoreCase);

    /// <summary>Legacy Chart_of_Account 001 → 4-digit platform code.</summary>
    public const string MasterAssetsCode = "1000";

    /// <summary>Legacy Chart_of_Account 002.</summary>
    public const string MasterEquityCode = "2000";

    /// <summary>Legacy Chart_of_Account 003 (was seed "2000 Current Liabilities" before T1.61).</summary>
    public const string MasterLiabilitiesCode = "3000";

    /// <summary>Legacy Chart_of_Account 004.</summary>
    public const string MasterIncomeCode = "4000";

    /// <summary>Legacy Chart_of_Account 005.</summary>
    public const string MasterExpenseCode = "5000";

    public const string InventoryControlCode = "AST-CTL-INV";

    public const string AccountsPayableControlCode = "LIA-CTL-AP";

    public const string CashControlCode = "AST-CTL-CASH";

    public const string BankControlCode = "AST-CTL-BANK";

    public const string SalesControlCode = "INC-CTL-SAL";

    public const string CogsControlCode = "EXP-CTL-COGS";
}
