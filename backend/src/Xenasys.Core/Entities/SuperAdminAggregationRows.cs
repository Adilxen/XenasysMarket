namespace Xenasys.Core.Entities;

// ==========================================================================================
// WHAT: Keyless row mapped to vw_super_admin_inventory_summary (M7.1).
// WHY: Cross-tenant rollup without OFFSET; view keeps <1s GroupBy shape.
// EXAMPLE: CompanyId=2053, ProductCount=6, LowStockProductCount=1
// ==========================================================================================
public sealed class SuperAdminInventorySummaryRow
{
    public int CompanyId { get; set; }

    public string CompanyName { get; set; } = string.Empty;

    public string TenantCode { get; set; } = string.Empty;

    public int ProductCount { get; set; }

    public int ActiveProductCount { get; set; }

    public long TotalQuantityOnHand { get; set; }

    public int LowStockProductCount { get; set; }

    public int ActiveVendorCount { get; set; }
}

// ==========================================================================================
// WHAT: Keyless row for vw_super_admin_sales_by_tenant (M7.2 → T8.1 live).
// WHY: Aggregates Posted active SalesOrders per tenant; Status=Live.
// ==========================================================================================
public sealed class SuperAdminSalesByTenantRow
{
    public int CompanyId { get; set; }

    public string CompanyName { get; set; } = string.Empty;

    public string TenantCode { get; set; } = string.Empty;

    public decimal GrossSales { get; set; }

    public int OrderCount { get; set; }

    public string Status { get; set; } = "Live";
}
