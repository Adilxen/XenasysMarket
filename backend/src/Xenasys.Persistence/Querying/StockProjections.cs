using System.Linq.Expressions;
using Xenasys.Core.Contracts.Stock;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Querying;

// ==========================================================================================
// WHAT: Stock list projection from Products (qty + min + IsLowStock).
// WHY: M5 listing reuses Product rows; QuantityOnHand is denormalized balance.
// EXAMPLE: IsLowStock = QuantityOnHand <= MinimumStockLevel
// ==========================================================================================
public static class StockProjections
{
    public static Expression<Func<Product, StockItemDto>> Item { get; } = p => new StockItemDto
    {
        ProductId = p.Id,
        CompanyId = p.CompanyId,
        CompanyName = p.Company != null ? p.Company.Name : null,
        ProductName = p.ProductName,
        ProductCode = p.ProductCode,
        UnitOfMeasurement = p.UnitOfMeasurement,
        QuantityOnHand = p.QuantityOnHand,
        MinimumStockLevel = p.MinimumStockLevel,
        IsLowStock = p.QuantityOnHand <= p.MinimumStockLevel,
        IsActive = p.IsActive,
    };
}
