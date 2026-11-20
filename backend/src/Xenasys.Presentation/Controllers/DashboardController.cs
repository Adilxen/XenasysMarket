using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions;
using Xenasys.Core.Abstractions.Pagination;
using Xenasys.Core.Contracts.Dashboard;
using Xenasys.Core.Entities;
using Xenasys.Infrastructure.Security;
using Xenasys.Persistence.Data;
using Xenasys.Persistence.Querying;

namespace Xenasys.Presentation.Controllers;

// ==========================================================================================
// WHAT: Production Dashboard API — activity-scoped rollups, name filter, low-stock queue.
// WHY: SA must not dump 2k empty tenants; tenants see JWT company only; totals server-side.
// EXAMPLE: GET /api/dashboard?companyName=TGL&includeEmpty=false&limit=50
// ==========================================================================================
[ApiController]
[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController : ControllerBase
{
    private const int LowStockTake = 12;

    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public DashboardController(ApplicationDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [RequirePermission("dashboard.read")]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DashboardDto>> Get(
        [FromQuery] int? companyId = null,
        [FromQuery] string? companyName = null,
        [FromQuery] bool includeEmpty = false,
        [FromQuery] int? afterId = null,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var isSa = _tenantProvider.IsSuperAdmin;
        int? scopeId = null;
        string? scopeName = null;

        if (isSa)
        {
            if (companyId is int requested and > 0)
            {
                scopeId = requested;
            }
        }
        else if (_tenantProvider.TenantId is int tid and > 0)
        {
            scopeId = tid;
        }
        else
        {
            return Forbid();
        }

        var inventoryQuery = _db.Set<SuperAdminInventorySummaryRow>().AsNoTracking();

        if (scopeId is int scopedCompany)
        {
            inventoryQuery = inventoryQuery.Where(r => r.CompanyId == scopedCompany);
        }

        if (isSa && !string.IsNullOrWhiteSpace(companyName))
        {
            var name = companyName.Trim();
            inventoryQuery = inventoryQuery.WhereContains(name, r => r.CompanyName);
        }

        if (!includeEmpty)
        {
            // Production default: hide empty seed tenants (2053 → ~6 with catalog/vendors).
            inventoryQuery = inventoryQuery.Where(r =>
                r.ProductCount > 0 || r.ActiveVendorCount > 0);
        }

        if (scopeId is int sid)
        {
            scopeName = await inventoryQuery
                .Where(r => r.CompanyId == sid)
                .Select(r => r.CompanyName)
                .FirstOrDefaultAsync(cancellationToken)
                ?? await _db.Tenants.AsNoTracking()
                    .Where(t => t.Id == sid)
                    .Select(t => t.Name)
                    .FirstOrDefaultAsync(cancellationToken);
        }

        var totalsAgg = await inventoryQuery
            .GroupBy(_ => 1)
            .Select(g => new
            {
                CompanyCount = g.Count(),
                ActiveProductCount = g.Sum(x => x.ActiveProductCount),
                TotalQuantityOnHand = g.Sum(x => x.TotalQuantityOnHand),
                LowStockProductCount = g.Sum(x => x.LowStockProductCount),
                ActiveVendorCount = g.Sum(x => x.ActiveVendorCount),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var pendingAlertsQuery = _db.NotificationOutbox.AsNoTracking()
            .Where(o =>
                o.EventType == NotificationEventTypes.LowStock
                && o.Status == NotificationOutboxStatuses.Pending);
        if (scopeId is int alertScope)
        {
            pendingAlertsQuery = pendingAlertsQuery.Where(o => o.CompanyId == alertScope);
        }

        var pendingAlerts = await pendingAlertsQuery.LongCountAsync(cancellationToken);

        var totals = new DashboardTotalsDto
        {
            CompanyCount = totalsAgg?.CompanyCount ?? 0,
            ActiveProductCount = totalsAgg?.ActiveProductCount ?? 0,
            TotalQuantityOnHand = totalsAgg?.TotalQuantityOnHand ?? 0,
            LowStockProductCount = totalsAgg?.LowStockProductCount ?? 0,
            ActiveVendorCount = totalsAgg?.ActiveVendorCount ?? 0,
            PendingLowStockAlerts = (int)Math.Min(pendingAlerts, int.MaxValue),
        };

        var inventoryTotal = await inventoryQuery.LongCountAsync(cancellationToken);
        var page = new KeysetPageRequest { AfterId = afterId, Limit = limit };
        var take = page.GetSafeLimit();

        var pageQuery = inventoryQuery;
        if (page.AfterId is int cursor)
        {
            pageQuery = pageQuery.Where(r => r.CompanyId > cursor);
        }

        var inventoryRows = await pageQuery
            .OrderBy(r => r.CompanyId)
            .Take(take + 1)
            .Select(r => new DashboardInventoryRowDto
            {
                CompanyId = r.CompanyId,
                CompanyName = r.CompanyName,
                TenantCode = r.TenantCode,
                ProductCount = r.ProductCount,
                ActiveProductCount = r.ActiveProductCount,
                TotalQuantityOnHand = r.TotalQuantityOnHand,
                LowStockProductCount = r.LowStockProductCount,
                ActiveVendorCount = r.ActiveVendorCount,
            })
            .ToListAsync(cancellationToken);

        var hasMore = inventoryRows.Count > take;
        if (hasMore)
        {
            inventoryRows.RemoveAt(inventoryRows.Count - 1);
        }

        var lowStockQuery = _db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.QuantityOnHand <= p.MinimumStockLevel);
        if (scopeId is int lowScope)
        {
            lowStockQuery = lowStockQuery.Where(p => p.CompanyId == lowScope);
        }

        var lowStockItems = await lowStockQuery
            .OrderBy(p => p.QuantityOnHand)
            .ThenBy(p => p.ProductName)
            .Take(LowStockTake)
            .Select(p => new DashboardLowStockItemDto
            {
                ProductId = p.Id,
                CompanyId = p.CompanyId,
                CompanyName = p.Company != null ? p.Company.Name : string.Empty,
                ProductName = p.ProductName,
                ProductCode = p.ProductCode,
                QuantityOnHand = p.QuantityOnHand,
                MinimumStockLevel = p.MinimumStockLevel,
            })
            .ToListAsync(cancellationToken);

        IReadOnlyList<DashboardCompanyOptionDto> companyOptions =
            Array.Empty<DashboardCompanyOptionDto>();
        if (isSa)
        {
            var optionsQuery = _db.Set<SuperAdminInventorySummaryRow>().AsNoTracking();
            if (!includeEmpty)
            {
                optionsQuery = optionsQuery.Where(r =>
                    r.ProductCount > 0 || r.ActiveVendorCount > 0);
            }

            companyOptions = await optionsQuery
                .OrderBy(r => r.CompanyName)
                .Select(r => new DashboardCompanyOptionDto
                {
                    CompanyId = r.CompanyId,
                    CompanyName = r.CompanyName,
                    TenantCode = r.TenantCode,
                })
                .ToListAsync(cancellationToken);
        }

        return Ok(new DashboardDto
        {
            IsSuperAdmin = isSa,
            ScopedCompanyId = scopeId,
            ScopedCompanyName = scopeName,
            Totals = totals,
            Inventory = inventoryRows,
            InventoryNextCursor = inventoryRows.Count > 0 ? inventoryRows[^1].CompanyId : null,
            InventoryHasMore = hasMore,
            InventoryTotalCount = inventoryTotal,
            LowStockItems = lowStockItems,
            CompanyOptions = companyOptions,
            SalesModuleLive = true,
            SalesMessage = null,
        });
    }
}
