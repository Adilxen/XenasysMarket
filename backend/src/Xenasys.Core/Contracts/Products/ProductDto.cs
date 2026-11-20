using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Xenasys.Core.Validation;

namespace Xenasys.Core.Contracts.Products;

// ==========================================================================================
// WHAT: Product CRUD wire DTOs — catalog FKs required; SKU allocated server-side on create.
// WHY: BRD §4.5 / M3 — Category+Sub+Brand+Unit; Retail >= Invoice; Inventory subsidiary TX.
// EXAMPLE: POST { productName, categoryId, subCategoryId, brandId, unitId, prices… }
// ==========================================================================================

public sealed class ProductDto
{
    public int Id { get; init; }

    public int CompanyId { get; init; }

    public string? CompanyName { get; init; }

    public int CategoryId { get; init; }

    public string? CategoryName { get; init; }

    public int SubCategoryId { get; init; }

    public string? SubCategoryName { get; init; }

    public int BrandId { get; init; }

    public string? BrandName { get; init; }

    public int UnitId { get; init; }

    public string? UnitCode { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string? ProductDescription { get; init; }

    public string Packaging { get; init; } = string.Empty;

    public string UnitOfMeasurement { get; init; } = string.Empty;

    public string ProductCode { get; init; } = string.Empty;

    public string? Barcode { get; init; }

    public int MinimumStockLevel { get; init; }

    public int QuantityOnHand { get; init; }

    public decimal InvoicePrice { get; init; }

    public decimal RetailPrice { get; init; }

    public string ImagePath { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public int? SubsidiaryAccountId { get; init; }
}

public sealed class CreateProductRequest
{
    [JsonPropertyName("companyId")]
    public int? CompanyId { get; init; }

    [Required(ErrorMessage = "Product Name is required.")]
    [MaxLength(150)]
    [NoHtmlOrScript]
    [JsonPropertyName("productName")]
    public string ProductName { get; init; } = string.Empty;

    [MaxLength(1000)]
    [NoHtmlOrScript]
    [JsonPropertyName("productDescription")]
    public string? ProductDescription { get; init; }

    [Required(ErrorMessage = "Packaging is required.")]
    [MaxLength(50)]
    [NoHtmlOrScript]
    [JsonPropertyName("packaging")]
    public string Packaging { get; init; } = string.Empty;

    [Required(ErrorMessage = "Category is required.")]
    [JsonPropertyName("categoryId")]
    public int CategoryId { get; init; }

    [Required(ErrorMessage = "Sub Category is required.")]
    [JsonPropertyName("subCategoryId")]
    public int SubCategoryId { get; init; }

    [Required(ErrorMessage = "Brand is required.")]
    [JsonPropertyName("brandId")]
    public int BrandId { get; init; }

    [Required(ErrorMessage = "Unit is required.")]
    [JsonPropertyName("unitId")]
    public int UnitId { get; init; }

    [JsonPropertyName("invoicePrice")]
    public decimal InvoicePrice { get; init; }

    [JsonPropertyName("retailPrice")]
    public decimal RetailPrice { get; init; }

    [JsonPropertyName("minimumStockLevel")]
    public int MinimumStockLevel { get; init; }

    [MaxLength(500)]
    [NoHtmlOrScript]
    [JsonPropertyName("barcode")]
    public string? Barcode { get; init; }

    [MaxLength(500)]
    [JsonPropertyName("imagePath")]
    public string? ImagePath { get; init; }
}

public sealed class UpdateProductRequest
{
    [Required(ErrorMessage = "Product Name is required.")]
    [MaxLength(150)]
    [NoHtmlOrScript]
    [JsonPropertyName("productName")]
    public string ProductName { get; init; } = string.Empty;

    [MaxLength(1000)]
    [NoHtmlOrScript]
    [JsonPropertyName("productDescription")]
    public string? ProductDescription { get; init; }

    [Required(ErrorMessage = "Packaging is required.")]
    [MaxLength(50)]
    [NoHtmlOrScript]
    [JsonPropertyName("packaging")]
    public string Packaging { get; init; } = string.Empty;

    [Required(ErrorMessage = "Category is required.")]
    [JsonPropertyName("categoryId")]
    public int CategoryId { get; init; }

    [Required(ErrorMessage = "Sub Category is required.")]
    [JsonPropertyName("subCategoryId")]
    public int SubCategoryId { get; init; }

    [Required(ErrorMessage = "Brand is required.")]
    [JsonPropertyName("brandId")]
    public int BrandId { get; init; }

    [Required(ErrorMessage = "Unit is required.")]
    [JsonPropertyName("unitId")]
    public int UnitId { get; init; }

    [JsonPropertyName("invoicePrice")]
    public decimal InvoicePrice { get; init; }

    [JsonPropertyName("retailPrice")]
    public decimal RetailPrice { get; init; }

    [JsonPropertyName("minimumStockLevel")]
    public int MinimumStockLevel { get; init; }

    [MaxLength(500)]
    [NoHtmlOrScript]
    [JsonPropertyName("barcode")]
    public string? Barcode { get; init; }

    [MaxLength(500)]
    [JsonPropertyName("imagePath")]
    public string? ImagePath { get; init; }

    [JsonPropertyName("isActive")]
    public bool? IsActive { get; init; }
}

public sealed class SetProductActiveRequest
{
    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
}
