using System.Linq.Expressions;
using Xenasys.Core.Contracts.Vendors;
using Xenasys.Core.Entities;
using Xenasys.Core.Querying;

namespace Xenasys.Persistence.Querying;

// ==========================================================================================
// WHAT: Vendor read projections — List (grid) vs Detail (drawer).
// WHY: List omits address/representative fields for keyset throughput.
// EXAMPLE: shape=list → VendorName, CompanyName, NtnNo, VendorPhone, Email, IsActive.
// ==========================================================================================
public static class VendorProjections
{
    public static Expression<Func<Vendor, VendorDto>> ForShape(DataShape shape) =>
        shape == DataShape.Detail ? Detail : List;

    public static Expression<Func<Vendor, VendorDto>> List { get; } = v => new VendorDto
    {
        Id = v.Id,
        CompanyId = v.CompanyId,
        CompanyName = v.Company != null ? v.Company.Name : null,
        NtnNo = v.NtnNo ?? string.Empty,
        VendorName = v.VendorName,
        VendorPhone = v.VendorPhone,
        Email = v.Email,
        IsActive = v.IsActive,
    };

    public static Expression<Func<Vendor, VendorDto>> Detail { get; } = v => new VendorDto
    {
        Id = v.Id,
        CompanyId = v.CompanyId,
        CompanyName = v.Company != null ? v.Company.Name : null,
        NtnNo = v.NtnNo ?? string.Empty,
        VendorName = v.VendorName,
        Address = v.Address,
        VendorPhone = v.VendorPhone,
        Email = v.Email,
        RepresentativeName = v.RepresentativeName,
        RepresentativeNo = v.RepresentativeNo,
        IsActive = v.IsActive,
        SubsidiaryAccountId = v.SubsidiaryAccountId,
    };
}
