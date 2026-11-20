namespace Xenasys.Core.Contracts.Dashboard;

// ==========================================================================================
// WHAT: Production Dashboard wire contracts (single /api/dashboard for SA + tenants).
// WHY: Server totals + activity filter + low-stock queue — UI must not load 2k empty tenants.
// EXAMPLE: GET /api/dashboard?companyName=TGL&includeEmpty=false
// ==========================================================================================

public sealed class DashboardDto
{
    public bool IsSuperAdmin { get; init; }

    public int? ScopedCompanyId { get; init; }

    public string? ScopedCompanyName { get; init; }

    public DashboardTotalsDto Totals { get; init; } = new();

    public IReadOnlyList<DashboardInventoryRowDto> Inventory { get; init; } =
        Array.Empty<DashboardInventoryRowDto>();

    public int? InventoryNextCursor { get; init; }

    public bool InventoryHasMore { get; init; }

    public long InventoryTotalCount { get; init; }

    public IReadOnlyList<DashboardLowStockItemDto> LowStockItems { get; init; } =
        Array.Empty<DashboardLowStockItemDto>();

    public IReadOnlyList<DashboardCompanyOptionDto> CompanyOptions { get; init; } =
        Array.Empty<DashboardCompanyOptionDto>();

    public bool SalesModuleLive { get; init; }

    public string? SalesMessage { get; init; }
}

public sealed class DashboardTotalsDto
{
    public int CompanyCount { get; init; }

    public int ActiveProductCount { get; init; }

    public long TotalQuantityOnHand { get; init; }

    public int LowStockProductCount { get; init; }

    public int ActiveVendorCount { get; init; }

    public int PendingLowStockAlerts { get; init; }
}

public sealed class DashboardInventoryRowDto
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

public sealed class DashboardLowStockItemDto
{
    public int ProductId { get; init; }

    public int CompanyId { get; init; }

    public string CompanyName { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public string ProductCode { get; init; } = string.Empty;

    public int QuantityOnHand { get; init; }

    public int MinimumStockLevel { get; init; }
}

public sealed class DashboardCompanyOptionDto
{
    public int CompanyId { get; init; }

    public string CompanyName { get; init; } = string.Empty;

    public string TenantCode { get; init; } = string.Empty;
}
