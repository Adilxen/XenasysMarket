using System.Linq.Expressions;
using Xenasys.Core.Contracts.Coa;
using Xenasys.Core.Entities;
using Xenasys.Core.Querying;

namespace Xenasys.Persistence.Querying;

// ==========================================================================================
// WHAT: MasterAccount read projections — List vs Detail (same business columns today).
// WHY: Shape contract matches Company/Products; keeps SELECT list explicit for scale.
// EXAMPLE: shape=list → Id, Code, Title, Prefix, IsActive.
// ==========================================================================================
public static class MasterAccountProjections
{
    public static Expression<Func<MasterAccount, MasterAccountDto>> ForShape(DataShape shape) =>
        shape == DataShape.Detail ? Detail : List;

    public static Expression<Func<MasterAccount, MasterAccountDto>> List { get; } = m => new MasterAccountDto
    {
        Id = m.Id,
        Code = m.Code,
        Title = m.Title,
        Prefix = m.Prefix,
        IsActive = m.IsActive,
    };

    public static Expression<Func<MasterAccount, MasterAccountDto>> Detail { get; } = m => new MasterAccountDto
    {
        Id = m.Id,
        Code = m.Code,
        Title = m.Title,
        Prefix = m.Prefix,
        IsActive = m.IsActive,
    };
}
