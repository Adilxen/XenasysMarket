using System.Linq.Expressions;
using Xenasys.Core.Contracts.Products;
using Xenasys.Core.Entities;
using Xenasys.Core.Querying;

namespace Xenasys.Persistence.Querying;

// ==========================================================================================
// WHAT: Product read projections — List (grid) vs Detail (full drawer).
// WHY: List omits description/image; includes catalog names for DataTable columns.
// EXAMPLE: shape=list → Code, Name, Brand, prices, stock, CompanyName.
// ==========================================================================================
public static class ProductProjections
{
    public static Expression<Func<Product, ProductDto>> ForShape(DataShape shape) =>
        shape == DataShape.Detail ? Detail : List;

    public static Expression<Func<Product, ProductDto>> List { get; } = p => new ProductDto
    {
        Id = p.Id,
        CompanyId = p.CompanyId,
        CompanyName = p.Company != null ? p.Company.Name : null,
        CategoryId = p.CategoryId,
        CategoryName = p.Category != null ? p.Category.Name : null,
        SubCategoryId = p.SubCategoryId,
        SubCategoryName = p.SubCategory != null ? p.SubCategory.Name : null,
        BrandId = p.BrandId,
        BrandName = p.Brand != null ? p.Brand.Name : null,
        UnitId = p.UnitId,
        UnitCode = p.Unit != null ? p.Unit.Code : p.UnitOfMeasurement,
        ProductName = p.ProductName,
        Packaging = p.Packaging,
        UnitOfMeasurement = p.UnitOfMeasurement,
        ProductCode = p.ProductCode,
        MinimumStockLevel = p.MinimumStockLevel,
        QuantityOnHand = p.QuantityOnHand,
        InvoicePrice = p.InvoicePrice,
        RetailPrice = p.RetailPrice,
        IsActive = p.IsActive,
        SubsidiaryAccountId = p.SubsidiaryAccountId,
    };

    public static Expression<Func<Product, ProductDto>> Detail { get; } = p => new ProductDto
    {
        Id = p.Id,
        CompanyId = p.CompanyId,
        CompanyName = p.Company != null ? p.Company.Name : null,
        CategoryId = p.CategoryId,
        CategoryName = p.Category != null ? p.Category.Name : null,
        SubCategoryId = p.SubCategoryId,
        SubCategoryName = p.SubCategory != null ? p.SubCategory.Name : null,
        BrandId = p.BrandId,
        BrandName = p.Brand != null ? p.Brand.Name : null,
        UnitId = p.UnitId,
        UnitCode = p.Unit != null ? p.Unit.Code : p.UnitOfMeasurement,
        ProductName = p.ProductName,
        ProductDescription = p.ProductDescription,
        Packaging = p.Packaging,
        UnitOfMeasurement = p.UnitOfMeasurement,
        ProductCode = p.ProductCode,
        Barcode = p.Barcode,
        MinimumStockLevel = p.MinimumStockLevel,
        QuantityOnHand = p.QuantityOnHand,
        InvoicePrice = p.InvoicePrice,
        RetailPrice = p.RetailPrice,
        ImagePath = p.ImagePath,
        IsActive = p.IsActive,
        SubsidiaryAccountId = p.SubsidiaryAccountId,
    };
}
