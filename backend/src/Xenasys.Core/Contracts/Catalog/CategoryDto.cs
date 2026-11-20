using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Xenasys.Core.Validation;

namespace Xenasys.Core.Contracts.Catalog;

// ==========================================================================================
// WHAT: Catalog master wire DTOs — Category, SubCategory, Brand, Unit (M2).
// WHY: BRD §4.2–4.4 — NameNormalized uniqueness; Category image mandatory.
// EXAMPLE: CategoryDto { Id, Name, Code, ImagePath, IsActive }.
// ==========================================================================================

public sealed class UnitDto
{
    public int Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public bool IsActive { get; init; }
}

public sealed class CategoryDto
{
    public int Id { get; init; }

    public int CompanyId { get; init; }

    public string? CompanyName { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string ImagePath { get; init; } = string.Empty;

    public bool IsActive { get; init; }
}

public sealed class CreateCategoryRequest
{
    [JsonPropertyName("companyId")]
    public int? CompanyId { get; init; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100)]
    [NoHtmlOrScript]
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [MaxLength(500)]
    [NoHtmlOrScript]
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

public sealed class UpdateCategoryRequest
{
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100)]
    [NoHtmlOrScript]
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [MaxLength(500)]
    [NoHtmlOrScript]
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("isActive")]
    public bool? IsActive { get; init; }
}

public sealed class SubCategoryDto
{
    public int Id { get; init; }

    public int CompanyId { get; init; }

    public string? CompanyName { get; init; }

    public int CategoryId { get; init; }

    public string? CategoryName { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? ImagePath { get; init; }

    public bool IsActive { get; init; }
}

public sealed class CreateSubCategoryRequest
{
    [JsonPropertyName("companyId")]
    public int? CompanyId { get; init; }

    [Required(ErrorMessage = "Category is required.")]
    [JsonPropertyName("categoryId")]
    public int CategoryId { get; init; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100)]
    [NoHtmlOrScript]
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [MaxLength(500)]
    [NoHtmlOrScript]
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

public sealed class UpdateSubCategoryRequest
{
    [Required(ErrorMessage = "Category is required.")]
    [JsonPropertyName("categoryId")]
    public int CategoryId { get; init; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100)]
    [NoHtmlOrScript]
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [MaxLength(500)]
    [NoHtmlOrScript]
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("isActive")]
    public bool? IsActive { get; init; }
}

public sealed class BrandDto
{
    public int Id { get; init; }

    public int CompanyId { get; init; }

    public string? CompanyName { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsActive { get; init; }
}

public sealed class CreateBrandRequest
{
    [JsonPropertyName("companyId")]
    public int? CompanyId { get; init; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100)]
    [NoHtmlOrScript]
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [MaxLength(500)]
    [NoHtmlOrScript]
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

public sealed class UpdateBrandRequest
{
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100)]
    [NoHtmlOrScript]
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [MaxLength(500)]
    [NoHtmlOrScript]
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("isActive")]
    public bool? IsActive { get; init; }
}
