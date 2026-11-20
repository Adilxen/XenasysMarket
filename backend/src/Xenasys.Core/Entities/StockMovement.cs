using Xenasys.Core.Entities.Common;

namespace Xenasys.Core.Entities;

// ==========================================================================================
// WHAT: Immutable stock adjustment ledger row (in/out) per product.
// WHY: M5.3 — audit trail for QuantityOnHand changes; index by company/product/time.
// EXAMPLE: Delta=+50, QuantityAfter=50, Reason="Opening stock".
// ==========================================================================================
public class StockMovement : AuditableEntity, ITenantScoped
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public int ProductId { get; set; }

    /// <summary>Signed change applied to QuantityOnHand (negative = issue).</summary>
    public int QuantityDelta { get; set; }

    /// <summary>Balance after this movement.</summary>
    public int QuantityAfter { get; set; }

    public string? Reason { get; set; }

    public DateTime OccurredAt { get; set; }

    public Tenant? Company { get; set; }

    public Product? Product { get; set; }
}
