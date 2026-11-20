using Xenasys.Core.Entities.Common;

namespace Xenasys.Core.Entities;

// ==========================================================================================
// WHAT: User × Role assignment junction (newSalon UserRoles) — multi-role per user.
// WHY: T1.38 — Users module can grant multiple company roles; PermissionGate unions grants.
// EXAMPLE: User 12 → TENANT_STAFF + custom Buyer roles for Company 5.
// ==========================================================================================
public class UserRole : BaseAuditableEntity, IActivatable
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int RoleId { get; set; }

    public bool IsActive { get; set; } = true;

    public User? User { get; set; }

    public Role? Role { get; set; }
}
