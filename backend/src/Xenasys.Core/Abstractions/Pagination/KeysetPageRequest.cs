namespace Xenasys.Core.Abstractions.Pagination;

/// <summary>
/// Keyset (cursor) pagination — avoids OFFSET scans on large ledger indexes.
/// </summary>
public sealed class KeysetPageRequest
{
    /// <summary>Exclusive lower bound cursor (last seen Id). Null = first page.</summary>
    public int? AfterId { get; init; }

    /// <summary>Page size. Capped server-side.</summary>
    public int Limit { get; init; } = 50;

    public int GetSafeLimit(int max = 100) =>
        Limit <= 0 ? 50 : Math.Min(Limit, max);
}
