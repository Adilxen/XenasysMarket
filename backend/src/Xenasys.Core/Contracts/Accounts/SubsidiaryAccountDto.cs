using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Xenasys.Core.Validation;

namespace Xenasys.Core.Contracts.Accounts;

// ==========================================================================================
// WHAT: Subsidiary Account CRUD wire DTOs (tenant-scoped COA leaves).
// WHY: BRD §4.7.3 — AccountNo + AccountCode server-allocated (immutable); system rows cannot delete.
// EXAMPLE: POST /api/coa/subsidiaries { controlAccountId, title } → server AccountNo + AccountCode
// ==========================================================================================

public sealed class SubsidiaryAccountDto
{
    public int Id { get; init; }

    public int CompanyId { get; init; }

    public string? CompanyName { get; init; }

    public string? CompanyCode { get; init; }

    public int ControlAccountId { get; init; }

    public string? ControlAccountCode { get; init; }

    public string? ControlAccountTitle { get; init; }

    public string SubsidiaryAccountCode { get; init; } = string.Empty;

    public string SubsidiaryAccountTitle { get; init; } = string.Empty;

    public string AccountNo { get; init; } = string.Empty;

    public string? ProductCode { get; init; }

    /// <summary>Parent control kind — INVENTORY / ACCOUNTS_PAYABLE / OTHER (for source labels).</summary>
    public string? ControlKind { get; init; }

    public bool IsActive { get; init; }

    public bool IsSystemGenerated { get; init; }
}

public sealed class CreateSubsidiaryAccountRequest
{
    [Required(ErrorMessage = "Control Account is required.")]
    [JsonPropertyName("controlAccountId")]
    public int ControlAccountId { get; init; }

    [Required(ErrorMessage = "Subsidiary Account Title is required.")]
    [MaxLength(150)]
    [NoHtmlOrScript]
    [JsonPropertyName("subsidiaryAccountTitle")]
    public string SubsidiaryAccountTitle { get; init; } = string.Empty;
}

public sealed class UpdateSubsidiaryAccountRequest
{
    [Required(ErrorMessage = "Control Account is required.")]
    [JsonPropertyName("controlAccountId")]
    public int ControlAccountId { get; init; }

    [Required(ErrorMessage = "Subsidiary Account Title is required.")]
    [MaxLength(150)]
    [NoHtmlOrScript]
    [JsonPropertyName("subsidiaryAccountTitle")]
    public string SubsidiaryAccountTitle { get; init; } = string.Empty;

    [JsonPropertyName("isActive")]
    public bool? IsActive { get; init; }
}
