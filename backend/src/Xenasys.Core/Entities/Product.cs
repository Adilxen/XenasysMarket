using Xenasys.Core.Entities.Common;

namespace Xenasys.Core.Entities;

/// <summary>
/// BRD §4.5 — Product listing with product_code (SKU), pricing, catalog FKs, stock thresholds.
/// </summary>
public class Product : AuditableEntity, ITenantScoped, IActivatable
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public int CategoryId { get; set; }

    public int SubCategoryId { get; set; }

    public int BrandId { get; set; }

    public int UnitId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string? ProductDescription { get; set; }

    public string Packaging { get; set; } = string.Empty;

    /// <summary>Denormalized Unit.Code snapshot for list grids.</summary>
    public string UnitOfMeasurement { get; set; } = string.Empty;

    /// <summary>CHAR(16) SKU: {TenantCode 6}{UserNn 2}{seq:D8} — T0.4 SSoT.</summary>
    public string ProductCode { get; set; } = string.Empty;

    public string? Barcode { get; set; }

    public int MinimumStockLevel { get; set; }

    /// <summary>Denormalized on-hand balance; adjusted via StockMovements (M5).</summary>
    public int QuantityOnHand { get; set; }

    public decimal InvoicePrice { get; set; }

    public decimal RetailPrice { get; set; }

    public string ImagePath { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>Linked inventory subsidiary created on product save.</summary>
    public int? SubsidiaryAccountId { get; set; }

    public Tenant? Company { get; set; }

    public Category? Category { get; set; }

    public SubCategory? SubCategory { get; set; }

    public Brand? Brand { get; set; }

    public Unit? Unit { get; set; }

    public SubsidiaryAccount? SubsidiaryAccount { get; set; }
}
