using System.Linq.Expressions;

namespace Xenasys.Persistence.Querying;

// ==========================================================================================
// WHAT: Shared header-filter Contains for list IQueryables (T1.52).
// WHY: DataTable funnel text must hit SQL — not client DOM hide on the current page.
// EXAMPLE: tenants.WhereContains(name, t => t.Name)
// ==========================================================================================
public static class QueryFilterExtensions
{
    /// <summary>Trim + lower; null when empty (omit filter). Caps at 80 chars.</summary>
    public static string? NormalizeContainsTerm(string? term)
    {
        var t = (term ?? string.Empty).Trim();
        if (t.Length == 0)
        {
            return null;
        }

        if (t.Length > 80)
        {
            t = t[..80];
        }

        return t.ToLowerInvariant();
    }

    /// <summary>
    /// Case-insensitive Contains on a string property (nullable → coalesce to empty).
    /// EF-translatable: <c>Coalesce(prop, "").ToLower().Contains(pattern)</c>.
    /// </summary>
    public static IQueryable<T> WhereContains<T>(
        this IQueryable<T> query,
        string? term,
        Expression<Func<T, string?>> selector)
    {
        var pattern = NormalizeContainsTerm(term);
        if (pattern is null)
        {
            return query;
        }

        var param = selector.Parameters[0];
        var prop = selector.Body;

        var coalesce = Expression.Coalesce(prop, Expression.Constant(string.Empty));
        var toLower = Expression.Call(
            coalesce,
            typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);
        var contains = Expression.Call(
            toLower,
            typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!,
            Expression.Constant(pattern));

        var lambda = Expression.Lambda<Func<T, bool>>(contains, param);
        return query.Where(lambda);
    }
}
