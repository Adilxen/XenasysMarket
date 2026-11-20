using Xenasys.Core.Entities.Common;

namespace Xenasys.Core.Entities;

public class User : AuditableEntity, IActivatable
{
    public int Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Exactly 2 digit chars — product_code creator segment.</summary>
    public string UserNn { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool IsSuperAdmin { get; set; }

    public ICollection<UserTenantMembership> Memberships { get; set; } = new List<UserTenantMembership>();

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
