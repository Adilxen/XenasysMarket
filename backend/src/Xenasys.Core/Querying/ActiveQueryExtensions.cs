using System.Linq.Expressions;
using Xenasys.Core.Entities.Common;

namespace Xenasys.Core.Querying;

// ==========================================================================================
// WHAT: Default-active filter for IQueryable&lt;IActivatable&gt;.
// WHY: Production tables hit ~1CR rows — inactive must not ride list/search by default.
// EXAMPLE: products.AsNoTracking().WhereActive(includeInactive)…
// ==========================================================================================
public static class ActiveQueryExtensions
{
    public static IQueryable<T> WhereActive<T>(
        this IQueryable<T> query,
        bool includeInactive = false)
        where T : class, IActivatable
    {
        if (includeInactive)
        {
            return query;
        }

        return query.Where(e => e.IsActive);
    }
}
