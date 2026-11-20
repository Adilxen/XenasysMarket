using System.Linq.Expressions;
using Xenasys.Core.Contracts.Coa;
using Xenasys.Core.Entities;
using Xenasys.Core.Querying;

namespace Xenasys.Persistence.Querying;

// ==========================================================================================
// WHAT: ControlAccount read projections — List (grid) vs Detail (drawer).
// WHY: Include Company + Master labels; exclude audit stamps.
// EXAMPLE: shape=list → CompanyName, Code, Title, ControlKind, MasterAccountTitle.
// ==========================================================================================
public static class ControlAccountProjections
{
    public static Expression<Func<ControlAccount, ControlAccountDto>> ForShape(DataShape shape) =>
        shape == DataShape.Detail ? Detail : List;

    public static Expression<Func<ControlAccount, ControlAccountDto>> List { get; } = c => new ControlAccountDto
    {
        Id = c.Id,
        CompanyId = c.CompanyId,
        CompanyName = c.Company != null ? c.Company.Name : null,
        CompanyCode = c.Company != null ? c.Company.TenantCode : null,
        MasterAccountId = c.MasterAccountId,
        MasterAccountCode = c.MasterAccount != null ? c.MasterAccount.Code : null,
        MasterAccountTitle = c.MasterAccount != null ? c.MasterAccount.Title : null,
        Code = c.Code,
        Title = c.Title,
        ControlKind = c.ControlKind,
        IsActive = c.IsActive,
    };

    public static Expression<Func<ControlAccount, ControlAccountDto>> Detail { get; } = c => new ControlAccountDto
    {
        Id = c.Id,
        CompanyId = c.CompanyId,
        CompanyName = c.Company != null ? c.Company.Name : null,
        CompanyCode = c.Company != null ? c.Company.TenantCode : null,
        MasterAccountId = c.MasterAccountId,
        MasterAccountCode = c.MasterAccount != null ? c.MasterAccount.Code : null,
        MasterAccountTitle = c.MasterAccount != null ? c.MasterAccount.Title : null,
        Code = c.Code,
        Title = c.Title,
        ControlKind = c.ControlKind,
        IsActive = c.IsActive,
    };
}
