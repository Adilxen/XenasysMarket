using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Xenasys.Core.Validation;

namespace Xenasys.Core.Contracts.Users;

// ==========================================================================================
// WHAT: Tenant user list / create / update wire DTOs (BRD Point 8 + T1.38 multi-role).
// WHY: Tenant Admin manages users + memberships; RoleIds[] maps to UserRoles (newSalon).
// EXAMPLE: CreateTenantUserRequest { userName, password, roleIds: [5, 7] }.
// ==========================================================================================

public sealed class UserRoleRefDto
{
    public int RoleId { get; set; }

    public string RoleCode { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;
}

public sealed class UserCompanyRefDto
{
    public int CompanyId { get; set; }

    public string CompanyName { get; set; } = string.Empty;

    public string? TenantCode { get; set; }

    public bool MembershipIsActive { get; set; }

    /// <summary>Roles active for this company (company-scoped + shared TENANT_ADMIN).</summary>
    public IReadOnlyList<UserRoleRefDto> Roles { get; set; } = [];
}

/// <summary>Super Admin: add another company membership to an existing user (T1.56).</summary>
public sealed class AddUserMembershipRequest
{
    [JsonPropertyName("company_id")]
    public int CompanyId { get; init; }

    [Required(ErrorMessage = "At least one role is required.")]
    [MinLength(1, ErrorMessage = "At least one role is required.")]
    [JsonPropertyName("roleIds")]
    public int[] RoleIds { get; init; } = [];
}

/// <summary>Update roles / active flag for one company membership.</summary>
public sealed class UpdateUserMembershipRequest
{
    [Required(ErrorMessage = "At least one role is required.")]
    [MinLength(1, ErrorMessage = "At least one role is required.")]
    [JsonPropertyName("roleIds")]
    public int[] RoleIds { get; init; } = [];

    public bool MembershipIsActive { get; init; } = true;
}

public sealed class TenantUserDto
{
    public int Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public bool IsActive { get; set; }

    public int MembershipId { get; set; }

    /// <summary>Primary role (membership.RoleId / first RoleIds) for JWT display.</summary>
    public int RoleId { get; set; }

    public string RoleCode { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public IReadOnlyList<UserRoleRefDto> Roles { get; set; } = [];

    public bool MembershipIsActive { get; set; }

    public int CompanyId { get; set; }

    public string CompanyName { get; set; } = string.Empty;

    public string? TenantCode { get; set; }

    /// <summary>All company memberships for this user (detail / listing context).</summary>
    public IReadOnlyList<UserCompanyRefDto> Companies { get; set; } = [];
}

public sealed class CreateTenantUserRequest
{
    /// <summary>
    /// Super Admin may target a company on create (provisioning wire key company_id).
    /// Tenant Admin ignores — JWT company wins (T1.60 BRD §2 accepted exception).
    /// </summary>
    [JsonPropertyName("company_id")]
    public int? CompanyId { get; init; }

    [Required(ErrorMessage = "User Name is mandatory.")]
    [MaxLength(50)]
    [NoHtmlOrScript]
    [JsonPropertyName("userName")]
    public string UserName { get; init; } = string.Empty;

    [Required(ErrorMessage = "Password is mandatory.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; init; } = string.Empty;

    [EmailAddress]
    [MaxLength(100)]
    [NoHtmlOrScript]
    public string? Email { get; init; }

    [Required(ErrorMessage = "At least one role is required.")]
    [MinLength(1, ErrorMessage = "At least one role is required.")]
    [JsonPropertyName("roleIds")]
    public int[] RoleIds { get; init; } = [];
}

public sealed class UpdateTenantUserRequest
{
    /// <summary>
    /// Super Admin: which membership roles/active apply to. Omit = profile-only (email/password).
    /// Tenant Admin: ignored (JWT company wins). Wire key: company_id.
    /// </summary>
    [JsonPropertyName("company_id")]
    public int? CompanyId { get; init; }

    [EmailAddress]
    [MaxLength(100)]
    [NoHtmlOrScript]
    public string? Email { get; init; }

    /// <summary>Required when updating a membership (company_id set / Tenant Admin). Profile-only SA may omit.</summary>
    [JsonPropertyName("roleIds")]
    public int[]? RoleIds { get; init; }

    /// <summary>Membership active flag for this company (soft enable/disable).</summary>
    public bool MembershipIsActive { get; init; } = true;

    /// <summary>Optional password change (min 8 when provided). Omit/blank = keep existing.</summary>
    public string? Password { get; init; }
}
