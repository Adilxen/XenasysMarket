namespace Xenasys.Core.Security;

// ==========================================================================================
// WHAT: Normalizes Role.DefaultRoute for post-login navigation.
// WHY: Blank / invalid paths must fall back to /dashboard (always allowed shell route).
// EXAMPLE: Normalize(null) → "/dashboard"; Normalize("settings/users") → "/dashboard".
// ==========================================================================================
public static class DefaultRouteNormalizer
{
    public const string Fallback = "/dashboard";

    public static string Normalize(string? route)
    {
        var r = (route ?? string.Empty).Trim();
        if (r.Length == 0 || r[0] != '/' || r.StartsWith("//", StringComparison.Ordinal))
        {
            return Fallback;
        }

        if (r.Length > 200)
        {
            r = r[..200];
        }

        // Strip query/hash — landing is a path only.
        var q = r.IndexOfAny(['?', '#']);
        if (q >= 0)
        {
            r = r[..q];
        }

        if (r.Length > 1 && r.EndsWith('/'))
        {
            r = r.TrimEnd('/');
        }

        return string.IsNullOrWhiteSpace(r) ? Fallback : r;
    }
}
