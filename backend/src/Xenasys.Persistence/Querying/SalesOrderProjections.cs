using System.Linq.Expressions;
using Xenasys.Core.Contracts.Sales;
using Xenasys.Core.Entities;
using Xenasys.Core.Querying;

namespace Xenasys.Persistence.Querying;

// ==========================================================================================
// WHAT: SalesOrder list/detail EF projections.
// WHY: shape=list omits lines; shape=detail includes line snapshots.
// EXAMPLE: Select(SalesOrderProjections.ForShape(DataShape.Detail))
// ==========================================================================================
public static class SalesOrderProjections
{
    public static Expression<Func<SalesOrder, SalesOrderDto>> ForShape(DataShape shape) =>
        shape == DataShape.Detail ? Detail : List;

    public static Expression<Func<SalesOrder, SalesOrderDto>> List { get; } = o => new SalesOrderDto
    {
        Id = o.Id,
        CompanyId = o.CompanyId,
        CompanyName = o.Company != null ? o.Company.Name : null,
        OrderCode = o.OrderCode,
        OrderDate = o.OrderDate,
        CustomerName = o.CustomerName,
        GrossAmount = o.GrossAmount,
        Status = o.Status,
        IsActive = o.IsActive,
        LineCount = o.Lines.Count,
    };

    public static Expression<Func<SalesOrder, SalesOrderDto>> Detail { get; } = o => new SalesOrderDto
    {
        Id = o.Id,
        CompanyId = o.CompanyId,
        CompanyName = o.Company != null ? o.Company.Name : null,
        OrderCode = o.OrderCode,
        OrderDate = o.OrderDate,
        CustomerName = o.CustomerName,
        Notes = o.Notes,
        GrossAmount = o.GrossAmount,
        Status = o.Status,
        IsActive = o.IsActive,
        LineCount = o.Lines.Count,
        Lines = o.Lines
            .OrderBy(l => l.Id)
            .Select(l => new SalesOrderLineDto
            {
                Id = l.Id,
                ProductId = l.ProductId,
                ProductCode = l.ProductCode,
                ProductName = l.ProductName,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                LineTotal = l.LineTotal,
            })
            .ToList(),
    };
}
