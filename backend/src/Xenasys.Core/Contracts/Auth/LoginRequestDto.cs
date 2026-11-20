using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Xenasys.Core.Abstractions;
using Xenasys.Core.Validation;

namespace Xenasys.Core.Contracts.Auth;

// ==========================================================================================
// WHAT: POST /api/auth/login body — userName, password, optional company_id (T0.3 / T0.5).
// WHY: company_id is required for multi-membership users; after login isolation uses JWT tenant_id.
// EXAMPLE: { "userName": "jdoe", "password": "Secret123!", "company_id": 5 }.
// ==========================================================================================
public sealed class LoginRequestDto
{
    /// <summary>Wire name <c>userName</c> for SPA / API spec compatibility.</summary>
    [Required(ErrorMessage = "User Name is mandatory.")]
    [MaxLength(50, ErrorMessage = "User Name must be at most 50 characters.")]
    [NoHtmlOrScript]
    [JsonPropertyName("userName")]
    public string Username { get; init; } = string.Empty;

    [Required(ErrorMessage = "Password is mandatory.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// Required when the user has multiple tenant memberships.
    /// Wire name <c>company_id</c> (T0.5 SSoT). Isolation after login uses JWT <c>tenant_id</c> only.
    /// </summary>
    [JsonPropertyName(TenancyKeyNames.CompanyId)]
    public int? CompanyId { get; init; }
}
