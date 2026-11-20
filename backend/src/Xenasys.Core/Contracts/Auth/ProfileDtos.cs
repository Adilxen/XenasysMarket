using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Xenasys.Core.Validation;

namespace Xenasys.Core.Contracts.Auth;

// ==========================================================================================
// WHAT: Authenticated self-service profile wire DTOs (header Profile update).
// WHY: User may change User Name / Password; Role + Company are display-only (JWT-bound).
// EXAMPLE: PUT /api/auth/me { userName, password? } — role/company ignored if sent.
// ==========================================================================================

public sealed class CurrentUserProfileDto
{
    public int UserId { get; set; }

    [JsonPropertyName("userName")]
    public string UserName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string Role { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public int CompanyId { get; set; }

    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Role landing path after login (normalized).</summary>
    public string DefaultRoute { get; set; } = "/dashboard";
}

public sealed class UpdateCurrentUserProfileRequest
{
    [Required(ErrorMessage = "User Name is mandatory.")]
    [MaxLength(50)]
    [NoHtmlOrScript]
    [JsonPropertyName("userName")]
    public string UserName { get; init; } = string.Empty;

    /// <summary>Optional password change (min 8 when provided). Omit/blank = keep existing.</summary>
    public string? Password { get; init; }
}
