using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Xenasys.Core.Validation;

namespace Xenasys.Core.Contracts.Coa;

// ==========================================================================================
// WHAT: Master Account CRUD wire DTOs for Chart of Accounts (platform-shared roots).
// WHY: BRD §4.7.1 — Code/Title/Prefix/Active; tenants GET; Super Admin writes.
// EXAMPLE: POST /api/coa/masters { code:"1000", title:"Assets", prefix:"AST" }
// ==========================================================================================

public sealed class MasterAccountDto
{
    public int Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Prefix { get; init; } = string.Empty;

    public bool IsActive { get; init; }
}

public sealed class CreateMasterAccountRequest
{
    [Required(ErrorMessage = "Master Account Code is required.")]
    [MaxLength(20)]
    [RegularExpression(@"^[0-9]{4,}$", ErrorMessage = "Master Account Code must be numeric and at least 4 digits.")]
    [NoHtmlOrScript]
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [Required(ErrorMessage = "Master Account Title is required.")]
    [MaxLength(100)]
    [NoHtmlOrScript]
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [Required(ErrorMessage = "Prefix is required.")]
    [MaxLength(5)]
    [RegularExpression(@"^[A-Za-z0-9]+$", ErrorMessage = "Prefix must be alphanumeric.")]
    [NoHtmlOrScript]
    [JsonPropertyName("prefix")]
    public string Prefix { get; init; } = string.Empty;
}

public sealed class UpdateMasterAccountRequest
{
    [Required(ErrorMessage = "Master Account Code is required.")]
    [MaxLength(20)]
    [RegularExpression(@"^[0-9]{4,}$", ErrorMessage = "Master Account Code must be numeric and at least 4 digits.")]
    [NoHtmlOrScript]
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [Required(ErrorMessage = "Master Account Title is required.")]
    [MaxLength(100)]
    [NoHtmlOrScript]
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [Required(ErrorMessage = "Prefix is required.")]
    [MaxLength(5)]
    [RegularExpression(@"^[A-Za-z0-9]+$", ErrorMessage = "Prefix must be alphanumeric.")]
    [NoHtmlOrScript]
    [JsonPropertyName("prefix")]
    public string Prefix { get; init; } = string.Empty;

    [JsonPropertyName("isActive")]
    public bool? IsActive { get; init; }
}
