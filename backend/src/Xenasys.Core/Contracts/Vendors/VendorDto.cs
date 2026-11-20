using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Xenasys.Core.Validation;

namespace Xenasys.Core.Contracts.Vendors;

// ==========================================================================================
// WHAT: Vendor CRUD wire DTOs (tenant-scoped; AP subsidiary on create).
// WHY: BRD §4.6 — NTN mask, phone regex, SubsidiaryAccountId set server-side on POST.
// EXAMPLE: POST /api/vendors { vendorName, ntnNo } → optional contact fields allowed empty.
// ==========================================================================================

public sealed class VendorDto
{
    public int Id { get; init; }

    public int CompanyId { get; init; }

    public string? CompanyName { get; init; }

    public string VendorName { get; init; } = string.Empty;

    public string? NtnNo { get; init; }

    public string Address { get; init; } = string.Empty;

    public string VendorPhone { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string RepresentativeName { get; init; } = string.Empty;

    public string RepresentativeNo { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public int? SubsidiaryAccountId { get; init; }
}

public sealed class CreateVendorRequest
{
    /// <summary>Super Admin only — when JWT has no tenant context.</summary>
    [JsonPropertyName("companyId")]
    public int? CompanyId { get; init; }

    [Required(ErrorMessage = "Vendor Name is required.")]
    [MaxLength(100)]
    [NoHtmlOrScript]
    [JsonPropertyName("vendorName")]
    public string VendorName { get; init; } = string.Empty;

    [MaxLength(9)]
    [NoHtmlOrScript]
    [JsonPropertyName("ntnNo")]
    public string? NtnNo { get; init; }

    [MaxLength(250)]
    [NoHtmlOrScript]
    [JsonPropertyName("address")]
    public string? Address { get; init; }

    [Required(ErrorMessage = "Phone No is required.")]
    [MaxLength(15)]
    [NoHtmlOrScript]
    [JsonPropertyName("vendorPhone")]
    public string VendorPhone { get; init; } = string.Empty;

    [MaxLength(256)]
    [NoHtmlOrScript]
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [MaxLength(100)]
    [NoHtmlOrScript]
    [JsonPropertyName("representativeName")]
    public string? RepresentativeName { get; init; }

    [MaxLength(15)]
    [NoHtmlOrScript]
    [JsonPropertyName("representativeNo")]
    public string? RepresentativeNo { get; init; }
}

public sealed class UpdateVendorRequest
{
    [Required(ErrorMessage = "Vendor Name is required.")]
    [MaxLength(100)]
    [NoHtmlOrScript]
    [JsonPropertyName("vendorName")]
    public string VendorName { get; init; } = string.Empty;

    [MaxLength(9)]
    [NoHtmlOrScript]
    [JsonPropertyName("ntnNo")]
    public string? NtnNo { get; init; }

    [MaxLength(250)]
    [NoHtmlOrScript]
    [JsonPropertyName("address")]
    public string? Address { get; init; }

    [Required(ErrorMessage = "Phone No is required.")]
    [MaxLength(15)]
    [NoHtmlOrScript]
    [JsonPropertyName("vendorPhone")]
    public string VendorPhone { get; init; } = string.Empty;

    [MaxLength(256)]
    [NoHtmlOrScript]
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [MaxLength(100)]
    [NoHtmlOrScript]
    [JsonPropertyName("representativeName")]
    public string? RepresentativeName { get; init; }

    [MaxLength(15)]
    [NoHtmlOrScript]
    [JsonPropertyName("representativeNo")]
    public string? RepresentativeNo { get; init; }

    [JsonPropertyName("isActive")]
    public bool? IsActive { get; init; }
}
