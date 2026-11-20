namespace Xenasys.Core.Abstractions.Pagination;

public sealed class KeysetPageResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>Pass as AfterId on the next request.</summary>
    public int? NextCursor { get; init; }

    public bool HasMore { get; init; }

    /// <summary>Total rows matching filters (excludes keyset cursor). Null when not computed.</summary>
    public long? TotalCount { get; init; }
}
