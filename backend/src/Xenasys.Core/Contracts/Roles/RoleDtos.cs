using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Xenasys.Core.Contracts.Roles;

// ==========================================================================================
// WHAT: Wire DTOs for company-scoped Roles (Name, RoleCode, Description, IsActive).
// WHY: BRD §3.1 — custom roles belong to a company; IsSystem derived from RoleCode.
// EXAMPLE: CreateRoleRequest { name, description }.
// ==========================================================================================

public sealed class RoleDto
{
    public int Id { get; set; }

    public int? CompanyId { get; set; }

    /// <summary>Owning company display name; null/empty for platform SUPER_ADMIN.</summary>
    public string? CompanyName { get; set; }

    public string RoleCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Post-login landing path (e.g. /dashboard).</summary>
    public string? DefaultRoute { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>True for SUPER_ADMIN / TENANT_ADMIN / TENANT_STAFF (computed).</summary>
    public bool IsSystem { get; set; }

    public IReadOnlyList<string>? PermissionCodes { get; set; }
}

public sealed class PermissionOptionDto
{
    public string Code { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

public sealed class CreateRoleRequest
{
    /// <summary>
    /// Super Admin may target a company; Tenant Admin ignores (JWT company wins).
    /// Wire key: company_id.
    /// </summary>
    [JsonPropertyName("company_id")]
    public int? CompanyId { get; init; }

    [Required, MinLength(2), MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Optional landing path after login (must start with /).</summary>
    [MaxLength(200)]
    public string? DefaultRoute { get; set; }

    public IReadOnlyList<string>? PermissionCodes { get; set; }
}

public sealed class UpdateRoleRequest
{
    [Required, MinLength(2), MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Landing path after login (must start with /). System roles may update this only.</summary>
    [MaxLength(200)]
    public string? DefaultRoute { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class UpdateRolePermissionsRequest
{
    public IReadOnlyList<string> PermissionCodes { get; set; } = [];
}
