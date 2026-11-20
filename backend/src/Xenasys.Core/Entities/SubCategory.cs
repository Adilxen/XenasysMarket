using Xenasys.Core.Entities.Common;

namespace Xenasys.Core.Entities;

// ==========================================================================================
// WHAT: Tenant-scoped sub-category under an active Category (BRD §4.3).
// WHY: Unique NameNormalized within parent Category; optional image; Code for SKU segments.
// EXAMPLE: CategoryId=3, Name="Phones", Code="SUB01".
// ==========================================================================================
public class SubCategory : AuditableEntity, ITenantScoped, IActivatable
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public int CategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Trim + lower — unique with CategoryId for case-insensitive name.</summary>
    public string NameNormalized { get; set; } = string.Empty;

    /// <summary>Auto code e.g. SUB01 — stable SKU segment.</summary>
    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Optional relative image path — JPG/JPEG/PNG, max 2MB.</summary>
    public string? ImagePath { get; set; }

    public bool IsActive { get; set; } = true;

    public Tenant? Company { get; set; }

    public Category? Category { get; set; }
}
