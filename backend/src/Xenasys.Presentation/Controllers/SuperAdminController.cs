using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions;
using Xenasys.Core.Contracts.SuperAdmin;
using Xenasys.Core.Entities;
using Xenasys.Core.Time;
using Xenasys.Infrastructure.Security;
using Xenasys.Persistence.Data;

namespace Xenasys.Presentation.Controllers;

// ==========================================================================================
// WHAT: Super Admin cross-tenant aggregation APIs (inventory live + sales stub).
// WHY: BRD Trigger 4 / M7 — SA-only; optional companyId selector audited (7.3/7.4).
// EXAMPLE: GET /api/super/inventory-summary → per-tenant product/stock/vendor rollup.
// ==========================================================================================
[ApiController]
[Route("api/super")]
[Authorize]
public sealed class SuperAdminController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUser _currentUser;

    public SuperAdminController(
        ApplicationDbContext db,
        ITenantProvider tenantProvider,
        ICurrentUser currentUser)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    [HttpGet("inventory-summary")]
    [RequirePermission("platform.read")]
    [ProducesResponseType(typeof(IReadOnlyList<SuperAdminInventorySummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<SuperAdminInventorySummaryDto>>> InventorySummary(
        [FromQuery] int? companyId = null,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantProvider.IsSuperAdmin)
        {
            return Forbid();
        }

        await AuditTenantSelectorAsync("inventory-summary", companyId, cancellationToken);

        var query = _db.Set<SuperAdminInventorySummaryRow>().AsNoTracking();
        if (companyId is int scoped and > 0)
        {
            query = query.Where(r => r.CompanyId == scoped);
        }

        var rows = await query
            .OrderBy(r => r.CompanyName)
            .Select(r => new SuperAdminInventorySummaryDto
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

        return Ok(rows);
    }

    [HttpGet("sales-by-tenant")]
    [RequirePermission("platform.read")]
    [ProducesResponseType(typeof(IReadOnlyList<SuperAdminSalesByTenantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<SuperAdminSalesByTenantDto>>> SalesByTenant(
        [FromQuery] int? companyId = null,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantProvider.IsSuperAdmin)
        {
            return Forbid();
        }

        await AuditTenantSelectorAsync("sales-by-tenant", companyId, cancellationToken);

        // Stub view returns zero rows until Sales module exists — truthful empty, not fabricated.
        var query = _db.Set<SuperAdminSalesByTenantRow>().AsNoTracking();
        if (companyId is int scoped and > 0)
        {
            query = query.Where(r => r.CompanyId == scoped);
        }

        var rows = await query
            .OrderBy(r => r.CompanyName)
            .Select(r => new SuperAdminSalesByTenantDto
            {
                CompanyId = r.CompanyId,
                CompanyName = r.CompanyName,
                TenantCode = r.TenantCode,
                GrossSales = r.GrossSales,
                OrderCount = r.OrderCount,
                Status = r.Status,
            })
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    [HttpGet("dashboard")]
    [RequirePermission("platform.read")]
    [ProducesResponseType(typeof(SuperAdminDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SuperAdminDashboardDto>> Dashboard(
        [FromQuery] int? companyId = null,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantProvider.IsSuperAdmin)
        {
            return Forbid();
        }

        await AuditTenantSelectorAsync("dashboard", companyId, cancellationToken);

        var inventoryQuery = _db.Set<SuperAdminInventorySummaryRow>().AsNoTracking();
        if (companyId is int scoped and > 0)
        {
            inventoryQuery = inventoryQuery.Where(r => r.CompanyId == scoped);
        }

        var inventory = await inventoryQuery
            .OrderBy(r => r.CompanyName)
            .Select(r => new SuperAdminInventorySummaryDto
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

        var salesQuery = _db.Set<SuperAdminSalesByTenantRow>().AsNoTracking();
        if (companyId is int salesScoped and > 0)
        {
            salesQuery = salesQuery.Where(r => r.CompanyId == salesScoped);
        }

        var sales = await salesQuery
            .OrderBy(r => r.CompanyName)
            .Select(r => new SuperAdminSalesByTenantDto
            {
                CompanyId = r.CompanyId,
                CompanyName = r.CompanyName,
                TenantCode = r.TenantCode,
                GrossSales = r.GrossSales,
                OrderCount = r.OrderCount,
                Status = r.Status,
            })
            .ToListAsync(cancellationToken);

        return Ok(new SuperAdminDashboardDto
        {
            Inventory = inventory,
            Sales = sales,
            SalesModuleLive = true,
            SalesMessage = null,
            ScopedCompanyId = companyId is int c and > 0 ? c : null,
        });
    }

    /// <summary>M7.3 — explicit companyId selector on SA cross-tenant reads must leave an audit trail.</summary>
    private async Task AuditTenantSelectorAsync(
        string surface,
        int? companyId,
        CancellationToken cancellationToken)
    {
        if (companyId is null || companyId.Value <= 0)
        {
            return;
        }

        var scoped = companyId.Value;
        var actorId = _currentUser.UserId is int uid and > 0 ? uid : 0;
        var actorName = _currentUser.UserName ?? "superadmin";

        _db.PlatformAuditLogs.Add(new PlatformAuditLog
        {
            ActorUserId = actorId,
            ActorUserName = actorName,
            Action = "Read",
            EntityType = "SuperAdminAggregation",
            EntityId = scoped,
            CompanyId = scoped,
            Summary = $"SA {surface} scoped to company #{scoped}",
            PayloadJson = $"{{\"surface\":\"{surface}\",\"companyId\":{scoped}}}",
            InsertDate = AuditClock.Now,
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}
