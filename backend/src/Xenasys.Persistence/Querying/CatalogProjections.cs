using System.Linq.Expressions;
using Xenasys.Core.Contracts.Catalog;
using Xenasys.Core.Entities;
using Xenasys.Core.Querying;

namespace Xenasys.Persistence.Querying;

// ==========================================================================================
// WHAT: Catalog read projections — List (grid) vs Detail (drawer).
// WHY: List keeps payload lean for keyset throughput; Detail includes description/image.
// EXAMPLE: shape=list → Name, Code, CompanyName, IsActive (Category).
// ==========================================================================================
public static class CategoryProjections
{
    public static Expression<Func<Category, CategoryDto>> ForShape(DataShape shape) =>
        shape == DataShape.Detail ? Detail : List;

    public static Expression<Func<Category, CategoryDto>> List { get; } = c => new CategoryDto
    {
        Id = c.Id,
        CompanyId = c.CompanyId,
        CompanyName = c.Company != null ? c.Company.Name : null,
        Name = c.Name,
        Code = c.Code,
        ImagePath = c.ImagePath,
        IsActive = c.IsActive,
    };

    public static Expression<Func<Category, CategoryDto>> Detail { get; } = c => new CategoryDto
    {
        Id = c.Id,
        CompanyId = c.CompanyId,
        CompanyName = c.Company != null ? c.Company.Name : null,
        Name = c.Name,
        Code = c.Code,
        Description = c.Description,
        ImagePath = c.ImagePath,
        IsActive = c.IsActive,
    };
}

public static class SubCategoryProjections
{
    public static Expression<Func<SubCategory, SubCategoryDto>> ForShape(DataShape shape) =>
        shape == DataShape.Detail ? Detail : List;

    public static Expression<Func<SubCategory, SubCategoryDto>> List { get; } = s => new SubCategoryDto
    {
        Id = s.Id,
        CompanyId = s.CompanyId,
        CompanyName = s.Company != null ? s.Company.Name : null,
        CategoryId = s.CategoryId,
        CategoryName = s.Category != null ? s.Category.Name : null,
        Name = s.Name,
        Code = s.Code,
        ImagePath = s.ImagePath,
        IsActive = s.IsActive,
    };

    public static Expression<Func<SubCategory, SubCategoryDto>> Detail { get; } = s => new SubCategoryDto
    {
        Id = s.Id,
        CompanyId = s.CompanyId,
        CompanyName = s.Company != null ? s.Company.Name : null,
        CategoryId = s.CategoryId,
        CategoryName = s.Category != null ? s.Category.Name : null,
        Name = s.Name,
        Code = s.Code,
        Description = s.Description,
        ImagePath = s.ImagePath,
        IsActive = s.IsActive,
    };
}

public static class BrandProjections
{
    public static Expression<Func<Brand, BrandDto>> ForShape(DataShape shape) =>
        shape == DataShape.Detail ? Detail : List;

    public static Expression<Func<Brand, BrandDto>> List { get; } = b => new BrandDto
    {
        Id = b.Id,
        CompanyId = b.CompanyId,
        CompanyName = b.Company != null ? b.Company.Name : null,
        Name = b.Name,
        IsActive = b.IsActive,
    };

    public static Expression<Func<Brand, BrandDto>> Detail { get; } = b => new BrandDto
    {
        Id = b.Id,
        CompanyId = b.CompanyId,
        CompanyName = b.Company != null ? b.Company.Name : null,
        Name = b.Name,
        Description = b.Description,
        IsActive = b.IsActive,
    };
}
