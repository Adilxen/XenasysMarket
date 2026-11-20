using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Xenasys.Core.Validation;

namespace Xenasys.Core.Contracts.Stock;

// ==========================================================================================
// WHAT: Stock list / update / alert / movement wire DTOs for M5.
// WHY: BRD Stock Management — Update Stock + low-stock alerts; qty never below 0.
// EXAMPLE: PUT /api/stock/{productId} { quantityOnHand: 12, reason: "Receipt" }
// ==========================================================================================

public sealed class StockItemDto
{
    public int ProductId { get; init; }

    public int CompanyId { get; init; }

    public string? CompanyName { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string ProductCode { get; init; } = string.Empty;

    public string UnitOfMeasurement { get; init; } = string.Empty;

    public int QuantityOnHand { get; init; }

    public int MinimumStockLevel { get; init; }

    public bool IsLowStock { get; init; }

    public bool IsActive { get; init; }
}

public sealed class UpdateStockRequest
{
    /// <summary>Absolute quantity after adjustment (preferred).</summary>
    [JsonPropertyName("quantityOnHand")]
    public int? QuantityOnHand { get; init; }

    /// <summary>Signed delta when absolute qty is omitted.</summary>
    [JsonPropertyName("quantityDelta")]
    public int? QuantityDelta { get; init; }

    [MaxLength(250)]
    [NoHtmlOrScript]
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    /// <summary>Optional — update product threshold in same TX.</summary>
    [JsonPropertyName("minimumStockLevel")]
    public int? MinimumStockLevel { get; init; }
}

public sealed class StockMovementDto
{
    public int Id { get; init; }

    public int ProductId { get; init; }

    public int QuantityDelta { get; init; }

    public int QuantityAfter { get; init; }

    public string? Reason { get; init; }

    public DateTime OccurredAt { get; init; }

    public int InsertUserId { get; init; }
}

public sealed class StockAlertDto
{
    public int? OutboxId { get; init; }

    public int ProductId { get; init; }

    public int CompanyId { get; init; }

    public string? CompanyName { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string ProductCode { get; init; } = string.Empty;

    public int QuantityOnHand { get; init; }

    public int MinimumStockLevel { get; init; }

    public string Status { get; init; } = string.Empty;

    public DateTime InsertDate { get; init; }
}
