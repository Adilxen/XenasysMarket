namespace Xenasys.Core.Querying;

// ==========================================================================================
// WHAT: Named projection shapes for hot-path reads (list vs detail).
// WHY: Call sites declare intent — EF selects only required columns (SP-style column list).
// EXAMPLE: GET /api/tenants?shape=list → Id,Name,TenantCode,IsActive (not Address/audit).
// ==========================================================================================
public enum DataShape
{
    /// <summary>Grid / dropdown / search — minimal columns.</summary>
    List = 0,

    /// <summary>Drawer / edit / full card — all business columns (still no audit).</summary>
    Detail = 1,
}

public static class DataShapeParser
{
    public static DataShape Parse(string? raw, DataShape fallback = DataShape.List)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "list" or "grid" or "minimal" => DataShape.List,
            "detail" or "full" or "all" => DataShape.Detail,
            _ => fallback,
        };
    }
}
