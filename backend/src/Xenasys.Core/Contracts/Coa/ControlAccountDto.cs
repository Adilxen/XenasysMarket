using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Xenasys.Core.Validation;

namespace Xenasys.Core.Contracts.Coa;

// ==========================================================================================
// WHAT: Control Account CRUD wire DTOs (tenant-scoped under platform Master roots).
// WHY: BRD §3.2 — TA Full Own Tenant; SA View-all; INV/AP unique per company.
// EXAMPLE: POST /api/coa/controls { masterAccountId, title, controlKind:"OTHER" }
// ==========================================================================================

public sealed class ControlAccountDto
{
    public int Id { get; init; }

    public int CompanyId { get; init; }

    public string? CompanyName { get; init; }

    public string? CompanyCode { get; init; }

    public int MasterAccountId { get; init; }

    public string? MasterAccountCode { get; init; }

    public string? MasterAccountTitle { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string ControlKind { get; init; } = string.Empty;

    public bool IsActive { get; init; }
}

public sealed class CreateControlAccountRequest
{
    /// <summary>Super Admin may set target company; tenants ignore (JWT only).</summary>
    [JsonPropertyName("companyId")]
    public int? CompanyId { get; init; }

    [Required(ErrorMessage = "Master Account is required.")]
    [JsonPropertyName("masterAccountId")]
    public int MasterAccountId { get; init; }

    [Required(ErrorMessage = "Control Account Title is required.")]
    [MaxLength(100)]
    [NoHtmlOrScript]
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [Required(ErrorMessage = "Control Kind is required.")]
    [MaxLength(40)]
    [NoHtmlOrScript]
    [JsonPropertyName("controlKind")]
    public string ControlKind { get; init; } = string.Empty;
}

public sealed class UpdateControlAccountRequest
{
    [Required(ErrorMessage = "Master Account is required.")]
    [JsonPropertyName("masterAccountId")]
    public int MasterAccountId { get; init; }

    [Required(ErrorMessage = "Control Account Title is required.")]
    [MaxLength(100)]
    [NoHtmlOrScript]
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [Required(ErrorMessage = "Control Kind is required.")]
    [MaxLength(40)]
    [NoHtmlOrScript]
    [JsonPropertyName("controlKind")]
    public string ControlKind { get; init; } = string.Empty;

    [JsonPropertyName("isActive")]
    public bool? IsActive { get; init; }
}
