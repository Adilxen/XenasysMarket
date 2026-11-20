using System.Linq.Expressions;
using Xenasys.Core.Contracts.Accounts;
using Xenasys.Core.Entities;
using Xenasys.Core.Querying;

namespace Xenasys.Persistence.Querying;

// ==========================================================================================
// WHAT: SubsidiaryAccount read projections — List (grid) vs Detail (drawer).
// WHY: Include Control labels; exclude audit stamps; keyset-friendly slim SELECT.
// EXAMPLE: shape=list → AccountNo, Code, Title, ControlAccountTitle, IsSystemGenerated.
// ==========================================================================================
public static class SubsidiaryAccountProjections
{
    public static Expression<Func<SubsidiaryAccount, SubsidiaryAccountDto>> ForShape(DataShape shape) =>
        shape == DataShape.Detail ? Detail : List;

    public static Expression<Func<SubsidiaryAccount, SubsidiaryAccountDto>> List { get; } =
        s => new SubsidiaryAccountDto
        {
            Id = s.Id,
            CompanyId = s.CompanyId,
            CompanyName = s.Company != null ? s.Company.Name : null,
            CompanyCode = s.Company != null ? s.Company.TenantCode : null,
            ControlAccountId = s.ControlAccountId,
            ControlAccountCode = s.ControlAccount != null ? s.ControlAccount.Code : null,
            ControlAccountTitle = s.ControlAccount != null ? s.ControlAccount.Title : null,
            SubsidiaryAccountCode = s.SubsidiaryAccountCode,
            SubsidiaryAccountTitle = s.SubsidiaryAccountTitle,
            AccountNo = s.AccountNo,
            ProductCode = s.ProductCode,
            ControlKind = s.ControlAccount != null ? s.ControlAccount.ControlKind : null,
            IsActive = s.IsActive,
            IsSystemGenerated = s.IsSystemGenerated,
        };

    public static Expression<Func<SubsidiaryAccount, SubsidiaryAccountDto>> Detail { get; } =
        s => new SubsidiaryAccountDto
        {
            Id = s.Id,
            CompanyId = s.CompanyId,
            CompanyName = s.Company != null ? s.Company.Name : null,
            CompanyCode = s.Company != null ? s.Company.TenantCode : null,
            ControlAccountId = s.ControlAccountId,
            ControlAccountCode = s.ControlAccount != null ? s.ControlAccount.Code : null,
            ControlAccountTitle = s.ControlAccount != null ? s.ControlAccount.Title : null,
            SubsidiaryAccountCode = s.SubsidiaryAccountCode,
            SubsidiaryAccountTitle = s.SubsidiaryAccountTitle,
            AccountNo = s.AccountNo,
            ProductCode = s.ProductCode,
            ControlKind = s.ControlAccount != null ? s.ControlAccount.ControlKind : null,
            IsActive = s.IsActive,
            IsSystemGenerated = s.IsSystemGenerated,
        };
}