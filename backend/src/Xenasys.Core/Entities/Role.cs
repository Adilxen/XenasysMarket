using Xenasys.Core.Entities.Common;

namespace Xenasys.Core.Entities;

// ==========================================================================================
// WHAT: Company-scoped Role catalog (BRD §3.1) — module fields + audit.
// WHY: T1.28 — restore CompanyId; custom roles belong to one tenant; SUPER_ADMIN is platform.
// EXAMPLE: CompanyId=5, Name="Buyer", RoleCode="BU0012", IsActive=true.
// ==========================================================================================
public class Role : BaseAuditableEntity, IActivatable
{
    public int Id { get; set; }

    /// <summary>Null = platform Super Admin only; otherwise owning company.</summary>
    public int? CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Custom roles: Prefix2+Seq4 (CHAR(6)). System: SUPER_ADMIN / TENANT_ADMIN / TENANT_STAFF.
    /// </summary>
    public string RoleCode { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Post-login landing path (e.g. /dashboard). Null → /dashboard.</summary>
    public string? DefaultRoute { get; set; }

    public bool IsActive { get; set; } = true;

    public Tenant? Company { get; set; }

    public ICollection<UserTenantMembership> Memberships { get; set; } = new List<UserTenantMembership>();
}
