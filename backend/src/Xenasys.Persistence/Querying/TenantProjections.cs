using System.Linq.Expressions;
using Xenasys.Core.Contracts.Tenants;
using Xenasys.Core.Entities;
using Xenasys.Core.Querying;

namespace Xenasys.Persistence.Querying;

// ==========================================================================================
// WHAT: Tenant read projections — List (4 cols) vs Detail (business cols).
// WHY: Calling code picks shape; SQL SELECT list stays tiny for 1CR-scale listings.
// EXAMPLE: shape=list → Id, Name, TenantCode, IsActive only.
// ==========================================================================================
public static class TenantProjections
{
    public static Expression<Func<Tenant, TenantDto>> ForShape(DataShape shape) =>
        shape == DataShape.Detail ? Detail : List;

    /// <summary>Grid columns only — covers Company listing (email shown) without address/phone.</summary>
    public static Expression<Func<Tenant, TenantDto>> List { get; } = t => new TenantDto
    {
        Id = t.Id,
        Name = t.Name,
        TenantCode = t.TenantCode,
        IsActive = t.IsActive,
        ContactEmail = t.ContactEmail,
    };

    /// <summary>Drawer / profile — all business fields, still excludes audit stamps.</summary>
    public static Expression<Func<Tenant, TenantDto>> Detail { get; } = t => new TenantDto
    {
        Id = t.Id,
        Name = t.Name,
        TenantCode = t.TenantCode,
        IsActive = t.IsActive,
        ContactEmail = t.ContactEmail,
        ContactPhone = t.ContactPhone,
        Address = t.Address,
    };
}
