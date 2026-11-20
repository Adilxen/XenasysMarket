namespace Xenasys.Core.Entities.Common;

// ==========================================================================================
// WHAT: Marker for soft-enable rows (IsActive bit).
// WHY: Shared WhereActive() extensions keep list/search defaulting to live rows only.
// EXAMPLE: query.WhereActive(includeInactive: false) → WHERE IsActive = 1
// ==========================================================================================
public interface IActivatable
{
    bool IsActive { get; }
}
