using Xenasys.Core.Entities.Common;

namespace Xenasys.Core.Entities;

public class UserTenantMembership : AuditableEntity
{
    public int Id { get; set; }

    public int UserId { get; set; }

    /// <summary>
    /// NULL = platform-scoped (Super Admin). Non-null = tenant workspace membership.
    /// </summary>
    public int? CompanyId { get; set; }

    public int RoleId { get; set; }

    public bool IsActive { get; set; } = true;

    public User? User { get; set; }

    public Tenant? Company { get; set; }

    public Role? Role { get; set; }
}
