using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions;
using Xenasys.Core.Abstractions.Coding;
using Xenasys.Core.Abstractions.Pagination;
using Xenasys.Core.Contracts.Sales;
using Xenasys.Core.Entities;
using Xenasys.Core.Querying;
using Xenasys.Core.Time;
using Xenasys.Infrastructure.Security;
using Xenasys.Persistence.Data;
using Xenasys.Persistence.Querying;

namespace Xenasys.Presentation.Controllers;

// ==========================================================================================
// WHAT: Phase-1 Sales Orders CRUD — post deducts stock; soft-cancel restores QOH.
// WHY: BRD Trigger 4 volumes + M5 stock integrity in one TX (T8.1).
// EXAMPLE: POST /api/sales-orders { lines:[{ productId:1006, quantity:2 }] }
// ==========================================================================================
[ApiController]
[Route("api/sales-orders")]
[Authorize]
public sealed class SalesOrdersController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly ISalesOrderCodeAllocator _codeAllocator;

    public SalesOrdersController(
        ApplicationDbContext db,
        ITenantProvider tenantProvider,
        ISalesOrderCodeAllocator codeAllocator)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _codeAllocator = codeAllocator;
    }

    [HttpGet]
    [RequirePermission("sales.read")]
    [ProducesResponseType(typeof(KeysetPageResult<SalesOrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<KeysetPageResult<SalesOrderDto>>> List(
        [FromQuery] int? afterId,
        [FromQuery] int limit = 50,
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? shape = null,
        [FromQuery] string? orderCode = null,
        [FromQuery] string? customerName = null,
        [FromQuery] string? companyName = null,
        [FromQuery] int? companyId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new KeysetPageRequest { AfterId = afterId, Limit = limit };
        var take = request.GetSafeLimit();
        var dataShape = DataShapeParser.Parse(shape, DataShape.List);

        var query = _db.SalesOrders
            .AsNoTracking()
            .WhereActive(includeInactive)
            .WhereContains(orderCode, o => o.OrderCode)
            .WhereContains(customerName, o => o.CustomerName)
            .WhereContains(companyName, o => o.Company != null ? o.Company.Name : null);

        if (companyId is int scopedCompany and > 0)
        {
            query = query.Where(o => o.CompanyId == scopedCompany);
        }

        var totalCount = await query.LongCountAsync(cancellationToken);

        if (request.AfterId is int cursor)
        {
            query = query.Where(o => o.Id > cursor);
        }

        var rows = await query
            .OrderBy(o => o.Id)
            .Take(take + 1)
            .Select(SalesOrderProjections.ForShape(dataShape))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > take;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return Ok(new KeysetPageResult<SalesOrderDto>
        {
            Items = rows,
            NextCursor = rows.Count > 0 ? rows[^1].Id : null,
            HasMore = hasMore,
            TotalCount = totalCount,
        });
    }

    [HttpGet("{id:int}")]
    [RequirePermission("sales.read")]
    [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SalesOrderDto>> GetById(
        int id,
        [FromQuery] string? shape = null,
        CancellationToken cancellationToken = default)
    {
        var dataShape = DataShapeParser.Parse(shape, DataShape.Detail);
        var row = await _db.SalesOrders
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(SalesOrderProjections.ForShape(dataShape))
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    [RequirePermission("sales.create")]
    [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SalesOrderDto>> Create(
        [FromBody] CreateSalesOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveWriteCompanyId(request.CompanyId, out var companyId, out var errorResult))
        {
            return errorResult!;
        }

        var fieldErrors = ValidateHeader(request.CustomerName, request.Notes);
        if (request.Lines is null || request.Lines.Count == 0)
        {
            fieldErrors["lines"] = ["At least one line is required."];
        }

        if (fieldErrors.Count > 0)
        {
            return UnprocessableEntity(new { errors = fieldErrors });
        }

        var normalizedLines = new List<(int ProductId, int Quantity, decimal? UnitPrice)>();
        for (var i = 0; i < request.Lines.Count; i++)
        {
            var line = request.Lines[i];
            if (line.ProductId <= 0)
            {
                fieldErrors[$"lines[{i}].productId"] = ["Product is required."];
                continue;
            }

            if (line.Quantity <= 0)
            {
                fieldErrors[$"lines[{i}].quantity"] = ["Quantity must be greater than 0."];
                continue;
            }

            if (line.UnitPrice is decimal up && up < 0)
            {
                fieldErrors[$"lines[{i}].unitPrice"] = ["Unit Price cannot be negative."];
                continue;
            }

            normalizedLines.Add((line.ProductId, line.Quantity, line.UnitPrice));
        }

        if (fieldErrors.Count > 0)
        {
            return UnprocessableEntity(new { errors = fieldErrors });
        }

        // Merge duplicate product ids so stock deduct is once per product.
                var merged = normalizedLines
            .GroupBy(l => l.ProductId)
            .Select(g => (
                ProductId: g.Key,
                Quantity: g.Sum(x => x.Quantity),
                UnitPrice: g.Select(x => x.UnitPrice).LastOrDefault(p => p is not null)))
            .ToList();

        SalesOrderDto? dto = null;
        var strategy = _db.Database.CreateExecutionStrategy();
        ActionResult? fail = null;

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var productIds = merged.Select(m => m.ProductId).ToList();
                var products = await _db.Products
                    .Where(p => productIds.Contains(p.Id) && p.CompanyId == companyId && p.IsActive)
                    .ToDictionaryAsync(p => p.Id, cancellationToken);

                if (products.Count != productIds.Count)
                {
                    fail = UnprocessableEntity(new
                    {
                        errors = new Dictionary<string, string[]>
                        {
                            ["lines"] = ["One or more products are missing or inactive for this company."],
                        },
                    });
                    await tx.RollbackAsync(cancellationToken);
                    return;
                }

                foreach (var line in merged)
                {
                    var product = products[line.ProductId];
                    if (product.QuantityOnHand < line.Quantity)
                    {
                        fail = UnprocessableEntity(new
                        {
                            errors = new Dictionary<string, string[]>
                            {
                                ["lines"] =
                                [
                                    $"Insufficient stock for {product.ProductName} (available {product.QuantityOnHand}).",
                                ],
                            },
                        });
                        await tx.RollbackAsync(cancellationToken);
                        return;
                    }
                }

                var orderCode = await _codeAllocator.AllocateAsync(companyId, cancellationToken);
                var now = AuditClock.Now;
                var order = new SalesOrder
                {
                    CompanyId = companyId,
                    OrderCode = orderCode,
                    OrderDate = now,
                    CustomerName = NullIfWhite(request.CustomerName),
                    Notes = NullIfWhite(request.Notes),
                    Status = SalesOrderStatuses.Posted,
                    IsActive = true,
                };

                decimal gross = 0;
                foreach (var line in merged)
                {
                    var product = products[line.ProductId];
                    var unitPrice = line.UnitPrice ?? product.RetailPrice;
                    var lineTotal = decimal.Round(unitPrice * line.Quantity, 2, MidpointRounding.AwayFromZero);
                    gross += lineTotal;

                    order.Lines.Add(new SalesOrderLine
                    {
                        CompanyId = companyId,
                        ProductId = product.Id,
                        ProductCode = product.ProductCode,
                        ProductName = product.ProductName,
                        Quantity = line.Quantity,
                        UnitPrice = unitPrice,
                        LineTotal = lineTotal,
                    });

                    var before = product.QuantityOnHand;
                    var after = before - line.Quantity;
                    product.QuantityOnHand = after;
                    _db.StockMovements.Add(new StockMovement
                    {
                        CompanyId = companyId,
                        ProductId = product.Id,
                        QuantityDelta = -line.Quantity,
                        QuantityAfter = after,
                        Reason = $"Sale {orderCode}",
                        OccurredAt = now,
                    });
                    await EnqueueOrClearLowStockAsync(product, cancellationToken);
                }

                order.GrossAmount = gross;
                _db.SalesOrders.Add(order);
                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                dto = await ProjectAsync(order.Id, cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });

        if (fail is not null)
        {
            return fail;
        }

        return CreatedAtAction(nameof(GetById), new { id = dto!.Id }, dto);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("sales.update")]
    [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SalesOrderDto>> Update(
        int id,
        [FromBody] UpdateSalesOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.SalesOrders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (!CanMutateCompany(entity.CompanyId))
        {
            return Forbid();
        }

        if (!entity.IsActive || entity.Status != SalesOrderStatuses.Posted)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["status"] = ["Only active posted orders can be updated."],
                },
            });
        }

        var fieldErrors = ValidateHeader(request.CustomerName, request.Notes);
        if (fieldErrors.Count > 0)
        {
            return UnprocessableEntity(new { errors = fieldErrors });
        }

        entity.CustomerName = NullIfWhite(request.CustomerName);
        entity.Notes = NullIfWhite(request.Notes);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await ProjectAsync(id, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("sales.delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.SalesOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (!CanMutateCompany(entity.CompanyId))
        {
            return Forbid();
        }

        if (!entity.IsActive || entity.Status != SalesOrderStatuses.Posted)
        {
            return NoContent();
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        ActionResult? fail = null;

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var productIds = entity.Lines.Select(l => l.ProductId).Distinct().ToList();
                var products = await _db.Products
                    .Where(p => productIds.Contains(p.Id) && p.CompanyId == entity.CompanyId)
                    .ToDictionaryAsync(p => p.Id, cancellationToken);

                var now = AuditClock.Now;
                foreach (var line in entity.Lines)
                {
                    if (!products.TryGetValue(line.ProductId, out var product))
                    {
                        fail = UnprocessableEntity(new
                        {
                            errors = new Dictionary<string, string[]>
                            {
                                ["lines"] = [$"Product {line.ProductId} is missing; cannot restore stock."],
                            },
                        });
                        await tx.RollbackAsync(cancellationToken);
                        return;
                    }

                    var after = product.QuantityOnHand + line.Quantity;
                    product.QuantityOnHand = after;
                    _db.StockMovements.Add(new StockMovement
                    {
                        CompanyId = entity.CompanyId,
                        ProductId = product.Id,
                        QuantityDelta = line.Quantity,
                        QuantityAfter = after,
                        Reason = $"Cancel sale {entity.OrderCode}",
                        OccurredAt = now,
                    });
                    await EnqueueOrClearLowStockAsync(product, cancellationToken);
                }

                entity.IsActive = false;
                entity.Status = SalesOrderStatuses.Cancelled;
                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });

        return fail ?? NoContent();
    }

    private async Task<SalesOrderDto> ProjectAsync(int id, CancellationToken cancellationToken) =>
        await _db.SalesOrders
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(SalesOrderProjections.Detail)
            .FirstAsync(cancellationToken);

    private async Task EnqueueOrClearLowStockAsync(Product product, CancellationToken cancellationToken)
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

        var payload = JsonSerializer.Serialize(new
        {
            productId = product.Id,
            productName = product.ProductName,
            productCode = product.ProductCode,
            quantityOnHand = product.QuantityOnHand,
            minimumStockLevel = product.MinimumStockLevel,
            companyId = product.CompanyId,
        });

        if (pending.Count > 0)
        {
            pending[0].PayloadJson = payload;
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
            PayloadJson = payload,
            Status = NotificationOutboxStatuses.Pending,
        });
    }

    private bool TryResolveWriteCompanyId(
        int? requestedCompanyId,
        out int companyId,
        out ActionResult? errorResult)
    {
        companyId = 0;
        errorResult = null;

        if (_tenantProvider.IsSuperAdmin)
        {
            if (requestedCompanyId is int explicitId and > 0)
            {
                companyId = explicitId;
                return true;
            }

            if (_tenantProvider.TenantId is int jwtCompany and > 0)
            {
                companyId = jwtCompany;
                return true;
            }

            errorResult = UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["companyId"] = ["Select a company before creating a sales order."],
                },
            });
            return false;
        }

        if (_tenantProvider.TenantId is int tid && tid > 0)
        {
            companyId = tid;
            return true;
        }

        errorResult = UnprocessableEntity(new
        {
            errors = new Dictionary<string, string[]>
            {
                ["companyId"] = ["Select a company context before creating a sales order."],
            },
        });
        return false;
    }

    private bool CanMutateCompany(int companyId) =>
        _tenantProvider.IsSuperAdmin
        || (_tenantProvider.TenantId is int tid && tid == companyId);

    private static Dictionary<string, string[]> ValidateHeader(string? customerName, string? notes)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        var c = (customerName ?? string.Empty).Trim();
        if (c.Length > 150)
        {
            errors["customerName"] = ["Customer Name must be at most 150 characters."];
        }

        var n = (notes ?? string.Empty).Trim();
        if (n.Length > 500)
        {
            errors["notes"] = ["Notes must be at most 500 characters."];
        }

        return errors;
    }

    private static string? NullIfWhite(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
