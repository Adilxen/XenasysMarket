using System.Linq.Expressions;
using Xenasys.Core.Contracts.Forms;
using Xenasys.Core.Entities;
using Xenasys.Core.Querying;

namespace Xenasys.Persistence.Querying;

// ==========================================================================================
// WHAT: Form projections — Id, Name, Path, IsActive.
// WHY: Keep SELECT lists tiny for Settings listing.
// EXAMPLE: shape=list → Id, Name, Path, IsActive.
// ==========================================================================================
public static class FormProjections
{
    public static Expression<Func<AppForm, FormDto>> ForShape(DataShape shape) =>
        shape == DataShape.Detail ? Detail : List;

    public static Expression<Func<AppForm, FormDto>> List { get; } = f => new FormDto
    {
        Id = f.Id,
        Name = f.Name,
        Path = f.Path,
        IsActive = f.IsActive,
    };

    public static Expression<Func<AppForm, FormDto>> Detail { get; } = f => new FormDto
    {
        Id = f.Id,
        Name = f.Name,
        Path = f.Path,
        IsActive = f.IsActive,
    };
}
