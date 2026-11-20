using System.Linq.Expressions;
using Xenasys.Core.Contracts.Actions;
using Xenasys.Core.Entities;
using Xenasys.Core.Querying;

namespace Xenasys.Persistence.Querying;

// ==========================================================================================
// WHAT: Action projections — Id, Name, IsActive (list == detail for this module).
// WHY: Keep SELECT lists tiny; claim gates come later.
// EXAMPLE: shape=list → Id, Name, IsActive.
// ==========================================================================================
public static class ActionProjections
{
    public static Expression<Func<ActionItem, ActionDto>> ForShape(DataShape shape) =>
        shape == DataShape.Detail ? Detail : List;

    public static Expression<Func<ActionItem, ActionDto>> List { get; } = a => new ActionDto
    {
        Id = a.Id,
        Name = a.Name,
        IsActive = a.IsActive,
    };

    public static Expression<Func<ActionItem, ActionDto>> Detail { get; } = a => new ActionDto
    {
        Id = a.Id,
        Name = a.Name,
        IsActive = a.IsActive,
    };
}
