using Xenasys.Core.Entities.Common;

namespace Xenasys.Core.Entities;

// ==========================================================================================
// WHAT: Tenant-scoped product category master (BRD §4.2).
// WHY: Unique NameNormalized per company; Code feeds future SKU segments; image required.
// EXAMPLE: CompanyId=5, Name="Electronics", Code="CAT01", ImagePath="/uploads/5/categories/…".
// ==========================================================================================
public class Category : AuditableEntity, ITenantScoped, IActivatable
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Trim + lower — unique with CompanyId for case-insensitive name.</summary>
    public string NameNormalized { get; set; } = string.Empty;

    /// <summary>Auto code e.g. CAT01 — stable SKU segment.</summary>
    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Relative path under wwwroot — JPG/JPEG/PNG, max 2MB (required).</summary>
    public string ImagePath { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public Tenant? Company { get; set; }

    public ICollection<SubCategory> SubCategories { get; set; } = new List<SubCategory>();
}
