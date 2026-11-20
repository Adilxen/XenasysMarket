namespace Xenasys.Core.Contracts.Sales;

// ==========================================================================================
// WHAT: Sales Order wire DTOs (Phase-1 ledger).
// WHY: Header + lines for list/detail; create posts stock; update is header-only.
// EXAMPLE: POST /api/sales-orders { lines:[{ productId, quantity, unitPrice? }] }
// ==========================================================================================

public sealed class SalesOrderLineDto
{
    public int Id { get; init; }

    public int ProductId { get; init; }

    public string ProductCode { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public decimal UnitPrice { get; init; }

    public decimal LineTotal { get; init; }
}

public sealed class SalesOrderDto
{
    public int Id { get; init; }

    public int CompanyId { get; init; }

    public string? CompanyName { get; init; }

    public string OrderCode { get; init; } = string.Empty;

    public DateTime OrderDate { get; init; }

    public string? CustomerName { get; init; }

    public string? Notes { get; init; }

    public decimal GrossAmount { get; init; }

    public string Status { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public int LineCount { get; init; }

    public IReadOnlyList<SalesOrderLineDto>? Lines { get; init; }
}

public sealed class CreateSalesOrderLineRequest
{
    public int ProductId { get; init; }

    public int Quantity { get; init; }

    /// <summary>Optional; defaults to Product.RetailPrice when omitted.</summary>
    public decimal? UnitPrice { get; init; }
}

public sealed class CreateSalesOrderRequest
{
    /// <summary>Inbound wire key for Super Admin company scope.</summary>
    public int? CompanyId { get; init; }

    public string? CustomerName { get; init; }

    public string? Notes { get; init; }

    public IReadOnlyList<CreateSalesOrderLineRequest> Lines { get; init; } =
        Array.Empty<CreateSalesOrderLineRequest>();
}

public sealed class UpdateSalesOrderRequest
{
    public string? CustomerName { get; init; }

    public string? Notes { get; init; }
}
