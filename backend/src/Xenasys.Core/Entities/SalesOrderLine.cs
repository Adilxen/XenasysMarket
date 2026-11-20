using Xenasys.Core.Entities.Common;

namespace Xenasys.Core.Entities;

// ==========================================================================================
// WHAT: Sales Order line — product qty × unit price snapshot.
// WHY: Immutable after post (Phase-1); drives stock issue and GrossAmount.
// EXAMPLE: ProductId=1006, Qty=2, UnitPrice=150 → LineTotal=300.
// ==========================================================================================
public class SalesOrderLine : AuditableEntity, ITenantScoped
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public int SalesOrderId { get; set; }

    public int ProductId { get; set; }

    /// <summary>Denormalized SKU snapshot at sale time.</summary>
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>Denormalized product name snapshot at sale time.</summary>
    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }

    public SalesOrder? SalesOrder { get; set; }

    public Product? Product { get; set; }

    public Tenant? Company { get; set; }
}
