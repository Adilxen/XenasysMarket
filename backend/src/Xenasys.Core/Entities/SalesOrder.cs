using Xenasys.Core.Entities.Common;

namespace Xenasys.Core.Entities;

// ==========================================================================================
// WHAT: Phase-1 Sales Order header (tenant sales ledger).
// WHY: BRD Trigger 4 — real GrossSales / OrderCount for vw_super_admin_sales_by_tenant.
// EXAMPLE: OrderCode=SO000042, Status=Posted, GrossAmount=sum(lines).
// ==========================================================================================
public class SalesOrder : AuditableEntity, ITenantScoped, IActivatable
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    /// <summary>Auto code SO + 6-digit seq per company.</summary>
    public string OrderCode { get; set; } = string.Empty;

    public DateTime OrderDate { get; set; }

    public string? CustomerName { get; set; }

    public string? Notes { get; set; }

    public decimal GrossAmount { get; set; }

    /// <summary>Posted | Cancelled</summary>
    public string Status { get; set; } = SalesOrderStatuses.Posted;

    public bool IsActive { get; set; } = true;

    public Tenant? Company { get; set; }

    public ICollection<SalesOrderLine> Lines { get; set; } = new List<SalesOrderLine>();
}

public static class SalesOrderStatuses
{
    public const string Posted = "Posted";
    public const string Cancelled = "Cancelled";
}
