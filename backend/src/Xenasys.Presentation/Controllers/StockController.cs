using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions;
using Xenasys.Core.Abstractions.Pagination;
using Xenasys.Core.Contracts.Stock;
using Xenasys.Core.Entities;
using Xenasys.Core.Querying;
using Xenasys.Core.Time;
using Xenasys.Infrastructure.Security;
using Xenasys.Persistence.Data;
using Xenasys.Persistence.Querying;

namespace Xenasys.Presentation.Controllers;

// ==========================================================================================
// WHAT: Stock balances, adjustments, low-stock alerts, movement history (M5).
// WHY: BRD Stock Management — Update Stock; enqueue LOW_STOCK outbox when qty <= min.
// EXAMPLE: PUT /api/stock/1006 { quantityOnHand: 3 } → movement + Pending outbox if low.
// ==========================================================================================
[ApiController]
[Route("api/stock")]
[Authorize]
public sealed class StockController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUser _currentUser;

    public StockController(
        ApplicationDbContext db,
        ITenantProvider tenantProvider,
        ICurrentUser currentUser)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    [HttpGet]
    [RequirePermission("stock.read")]
    [ProducesResponseType(typeof(KeysetPageResult<StockItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<KeysetPageResult<StockItemDto>>> List(
        [FromQuery] int? afterId = null,
        [FromQuery] int limit = 50,
        [FromQuery] bool includeInactive = false,
        [FromQuery] bool? lowStockOnly = null,
        [FromQuery] string? productName = null,
        [FromQuery] string? productCode = null,
        [FromQuery] string? companyName = null,
        [FromQuery] int? companyId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new KeysetPageRequest { AfterId = afterId, Limit = limit };
        var take = request.GetSafeLimit();

        var query = _db.Products.AsNoTracking().WhereActive(includeInactive);

        if (companyId is int scoped and > 0)
        {
            query = query.Where(p => p.CompanyId == scoped);
        }

        query = query
            .WhereContains(productName, p => p.ProductName)
            .WhereContains(productCode, p => p.ProductCode)
            .WhereContains(companyName, p => p.Company != null ? p.Company.Name : null);

        if (lowStockOnly == true)
        {
            query = query.Where(p => p.QuantityOnHand <= p.MinimumStockLevel);
        }

        var totalCount = await query.LongCountAsync(cancellationToken);

        if (request.AfterId is int cursor)
        {
            query = query.Where(p => p.Id > cursor);
        }

        var rows = await query
            .OrderBy(p => p.Id)
            .Take(take + 1)
            .Select(StockProjections.Item)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > take;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return Ok(new KeysetPageResult<StockItemDto>
        {
            Items = rows,
            NextCursor = rows.Count > 0 ? rows[^1].ProductId : null,
            HasMore = hasMore,
            TotalCount = totalCount,
        });
    }

    [HttpGet("alerts")]
    [RequirePermission("stock.read")]
    [ProducesResponseType(typeof(KeysetPageResult<StockAlertDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<KeysetPageResult<StockAlertDto>>> ListAlerts(
        [FromQuery] int? afterId = null,
        [FromQuery] int limit = 50,
        [FromQuery] string? status = null,
        [FromQuery] int? companyId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new KeysetPageRequest { AfterId = afterId, Limit = limit };
        var take = request.GetSafeLimit();

        var query = _db.NotificationOutbox.AsNoTracking()
            .Where(o => o.EventType == NotificationEventTypes.LowStock);

        if (companyId is int scoped and > 0)
        {
            query = query.Where(o => o.CompanyId == scoped);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim();
            query = query.Where(o => o.Status == st);
        }
        else
        {
            query = query.Where(o => o.Status == NotificationOutboxStatuses.Pending);
        }

        var totalCount = await query.LongCountAsync(cancellationToken);

        if (request.AfterId is int cursor)
        {
            query = query.Where(o => o.Id > cursor);
        }

        var rows = await query
            .OrderBy(o => o.Id)
            .Take(take + 1)
            .Select(o => new StockAlertDto
            {
                OutboxId = o.Id,
                ProductId = o.ProductId ?? 0,
                CompanyId = o.CompanyId,
                CompanyName = o.Company != null ? o.Company.Name : null,
                ProductName = o.Product != null ? o.Product.ProductName : string.Empty,
                ProductCode = o.Product != null ? o.Product.ProductCode : string.Empty,
                QuantityOnHand = o.Product != null ? o.Product.QuantityOnHand : 0,
                MinimumStockLevel = o.Product != null ? o.Product.MinimumStockLevel : 0,
                Status = o.Status,
                InsertDate = o.InsertDate,
            })
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > take;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return Ok(new KeysetPageResult<StockAlertDto>
        {
            Items = rows,
            NextCursor = rows.Count > 0 ? rows[^1].OutboxId : null,
            HasMore = hasMore,
            TotalCount = totalCount,
        });
    }

    [HttpPost("alerts/{outboxId:int}/ack")]
    [RequirePermission("stock.read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcknowledgeAlert(
        int outboxId,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.NotificationOutbox
            .FirstOrDefaultAsync(
                o => o.Id == outboxId && o.EventType == NotificationEventTypes.LowStock,
                cancellationToken);
        if (row is null)
        {
            return NotFound();
        }

        if (!CanMutateCompany(row.CompanyId))
        {
            return Forbid();
        }

        if (row.Status == NotificationOutboxStatuses.Pending)
        {
            row.Status = NotificationOutboxStatuses.Processed;
            row.ProcessedAt = AuditClock.Now;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpGet("{productId:int}")]
    [RequirePermission("stock.read")]
    [ProducesResponseType(typeof(StockItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockItemDto>> Get(
        int productId,
        CancellationToken cancellationToken = default)
    {
        var item = await _db.Products.AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(StockProjections.Item)
            .FirstOrDefaultAsync(cancellationToken);

        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{productId:int}/movements")]
    [RequirePermission("stock.read")]
    [ProducesResponseType(typeof(KeysetPageResult<StockMovementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<KeysetPageResult<StockMovementDto>>> ListMovements(
        int productId,
        [FromQuery] int? afterId = null,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var exists = await _db.Products.AsNoTracking()
            .AnyAsync(p => p.Id == productId, cancellationToken);
        if (!exists)
        {
            return NotFound();
        }

        var request = new KeysetPageRequest { AfterId = afterId, Limit = limit };
        var take = request.GetSafeLimit();

        var query = _db.StockMovements.AsNoTracking()
            .Where(m => m.ProductId == productId);

        var totalCount = await query.LongCountAsync(cancellationToken);

        if (request.AfterId is int cursor)
        {
            query = query.Where(m => m.Id > cursor);
        }

        var rows = await query
            .OrderByDescending(m => m.Id)
            .Take(take + 1)
            .Select(m => new StockMovementDto
            {
                Id = m.Id,
                ProductId = m.ProductId,
                QuantityDelta = m.QuantityDelta,
                QuantityAfter = m.QuantityAfter,
                Reason = m.Reason,
                OccurredAt = m.OccurredAt,
                InsertUserId = m.InsertUserId,
            })
            .ToListAsync(cancellationToken);

        // Descending page — NextCursor = last (oldest) Id for "load more older".
        var hasMore = rows.Count > take;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return Ok(new KeysetPageResult<StockMovementDto>
        {
            Items = rows,
            NextCursor = rows.Count > 0 ? rows[^1].Id : null,
            HasMore = hasMore,
            TotalCount = totalCount,
        });
    }

    [HttpPut("{productId:int}")]
    [RequirePermission("stock.update")]
    [ProducesResponseType(typeof(StockItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<StockItemDto>> Update(
        int productId,
        [FromBody] UpdateStockRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.QuantityOnHand is null && request.QuantityDelta is null)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["quantityOnHand"] = ["Quantity On Hand or Quantity Delta is required."],
                },
            });
        }

        if (request.MinimumStockLevel is int min && min < 0)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["minimumStockLevel"] = ["Minimum Stock Level must be >= 0."],
                },
            });
        }

        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        if (!CanMutateCompany(product.CompanyId))
        {
            return Forbid();
        }

        var before = product.QuantityOnHand;
        int after;
        if (request.QuantityOnHand is int absolute)
        {
            after = absolute;
        }
        else
        {
            after = before + request.QuantityDelta!.Value;
        }

        if (after < 0)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["quantityOnHand"] = ["Quantity On Hand cannot be negative."],
                },
            });
        }

        var delta = after - before;
        if (delta == 0 && request.MinimumStockLevel is null)
        {
            var unchanged = await _db.Products.AsNoTracking()
                .Where(p => p.Id == productId)
                .Select(StockProjections.Item)
                .FirstAsync(cancellationToken);
            return Ok(unchanged);
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (reason is { Length: > 250 })
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["reason"] = ["Reason must be at most 250 characters."],
                },
            });
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        StockItemDto? result = null;
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                if (request.MinimumStockLevel is int newMin)
                {
                    product.MinimumStockLevel = newMin;
                }

                if (delta != 0)
                {
                    product.QuantityOnHand = after;
                    _db.StockMovements.Add(new StockMovement
                    {
                        CompanyId = product.CompanyId,
                        ProductId = product.Id,
                        QuantityDelta = delta,
                        QuantityAfter = after,
                        Reason = reason,
                        OccurredAt = AuditClock.Now,
                    });
                }

                await EnqueueOrClearLowStockAsync(product, cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                result = await _db.Products.AsNoTracking()
                    .Where(p => p.Id == productId)
                    .Select(StockProjections.Item)
                    .FirstAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });

        return Ok(result!);
    }

    private async Task EnqueueOrClearLowStockAsync(
        Product product,
        CancellationToken cancellationToken)
    {
        var isLow = product.QuantityOnHand <= product.MinimumStockLevel;

        var pending = await _db.NotificationOutbox
            .Where(o =>
                o.CompanyId == product.CompanyId
                && o.ProductId == product.Id
                && o.EventType == NotificationEventTypes.LowStock
                && o.Status == NotificationOutboxStatuses.Pending)
            .ToListAsync(cancellationToken);

        if (!isLow)
        {
            foreach (var row in pending)
            {
                row.Status = NotificationOutboxStatuses.Processed;
                row.ProcessedAt = AuditClock.Now;
            }

            return;
        }

        if (pending.Count > 0)
        {
            // Refresh payload on existing Pending row (no spam duplicates).
            var row = pending[0];
            row.PayloadJson = BuildLowStockPayload(product);
            foreach (var extra in pending.Skip(1))
            {
                extra.Status = NotificationOutboxStatuses.Processed;
                extra.ProcessedAt = AuditClock.Now;
            }

            return;
        }

        _db.NotificationOutbox.Add(new NotificationOutbox
        {
            CompanyId = product.CompanyId,
            ProductId = product.Id,
            EventType = NotificationEventTypes.LowStock,
            PayloadJson = BuildLowStockPayload(product),
            Status = NotificationOutboxStatuses.Pending,
        });
    }

    private static string BuildLowStockPayload(Product product) =>
        JsonSerializer.Serialize(new
        {
            productId = product.Id,
            productName = product.ProductName,
            productCode = product.ProductCode,
            quantityOnHand = product.QuantityOnHand,
            minimumStockLevel = product.MinimumStockLevel,
            companyId = product.CompanyId,
        });

    private bool CanMutateCompany(int companyId) =>
        _tenantProvider.IsSuperAdmin
        || (_tenantProvider.TenantId is int tid && tid == companyId);
}
