namespace Xenasys.Core.Contracts.SuperAdmin;

// ==========================================================================================
// WHAT: Super Admin cross-tenant aggregation wire DTOs (M7 / BRD Trigger 4).
// WHY: Inventory rollup live; sales stub until Sales module ships (no fake heavy scans).
// EXAMPLE: GET /api/super/inventory-summary?companyId=2053
// ==========================================================================================

public sealed class SuperAdminInventorySummaryDto
{
    public int CompanyId { get; init; }

    public string CompanyName { get; init; } = string.Empty;

    public string TenantCode { get; init; } = string.Empty;

    public int ProductCount { get; init; }

    public int ActiveProductCount { get; init; }

    public long TotalQuantityOnHand { get; init; }

    public int LowStockProductCount { get; init; }

    public int ActiveVendorCount { get; init; }
}

public sealed class SuperAdminSalesByTenantDto
{
    public int CompanyId { get; init; }

    public string CompanyName { get; init; } = string.Empty;

    public string TenantCode { get; init; } = string.Empty;

    public decimal GrossSales { get; init; }

    public int OrderCount { get; init; }

    /// <summary>Live when Phase-1 Sales Orders module is shipped (T8.1).</summary>
    public string Status { get; init; } = "Live";
}

public sealed class SuperAdminDashboardDto
{
    public IReadOnlyList<SuperAdminInventorySummaryDto> Inventory { get; init; } =
        Array.Empty<SuperAdminInventorySummaryDto>();

    public IReadOnlyList<SuperAdminSalesByTenantDto> Sales { get; init; } =
        Array.Empty<SuperAdminSalesByTenantDto>();

    public bool SalesModuleLive { get; init; }

    public string? SalesMessage { get; init; }

    public int? ScopedCompanyId { get; init; }
}
