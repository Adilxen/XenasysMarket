using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Xenasys.Core.Validation;

namespace Xenasys.Core.Contracts.Tenants;

// ==========================================================================================
// WHAT: Tenant/company CRUD wire DTOs for Settings → Company (listing + drawer).
// WHY: TenantCode is server-allocated (prefix+seq); clients never invent codes.
// EXAMPLE: POST /api/tenants { name, contactEmail, … } → TenantCode "ACM007" in response.
// ==========================================================================================

public sealed class TenantDto
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string TenantCode { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public string? ContactEmail { get; init; }

    public string? ContactPhone { get; init; }

    public string? Address { get; init; }
}

public sealed class CreateTenantRequest
{
    [Required(ErrorMessage = "Company name is required.")]
    [MaxLength(200)]
    [NoHtmlOrScript]
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [EmailAddress(ErrorMessage = "Contact email is invalid.")]
    [MaxLength(256)]
    [NoHtmlOrScript]
    [JsonPropertyName("contactEmail")]
    public string? ContactEmail { get; init; }

    [MaxLength(30)]
    [NoHtmlOrScript]
    [JsonPropertyName("contactPhone")]
    public string? ContactPhone { get; init; }

    [MaxLength(500)]
    [NoHtmlOrScript]
    [JsonPropertyName("address")]
    public string? Address { get; init; }
}

public sealed class UpdateTenantRequest
{
    [Required(ErrorMessage = "Company name is required.")]
    [MaxLength(200)]
    [NoHtmlOrScript]
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [EmailAddress(ErrorMessage = "Contact email is invalid.")]
    [MaxLength(256)]
    [NoHtmlOrScript]
    [JsonPropertyName("contactEmail")]
    public string? ContactEmail { get; init; }

    [MaxLength(30)]
    [NoHtmlOrScript]
    [JsonPropertyName("contactPhone")]
    public string? ContactPhone { get; init; }

    [MaxLength(500)]
    [NoHtmlOrScript]
    [JsonPropertyName("address")]
    public string? Address { get; init; }

    [JsonPropertyName("isActive")]
    public bool? IsActive { get; init; }
}

/// <summary>Legacy alias used by PUT /api/tenant/me.</summary>
public sealed class UpdateTenantProfileRequest
{
    [Required(ErrorMessage = "Company name is required.")]
    [MaxLength(200)]
    [NoHtmlOrScript]
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [EmailAddress(ErrorMessage = "Contact email is invalid.")]
    [MaxLength(256)]
    [NoHtmlOrScript]
    [JsonPropertyName("contactEmail")]
    public string? ContactEmail { get; init; }

    [MaxLength(30)]
    [NoHtmlOrScript]
    [JsonPropertyName("contactPhone")]
    public string? ContactPhone { get; init; }

    [MaxLength(500)]
    [NoHtmlOrScript]
    [JsonPropertyName("address")]
    public string? Address { get; init; }
}
