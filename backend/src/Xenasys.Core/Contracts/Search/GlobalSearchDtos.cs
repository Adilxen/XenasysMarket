namespace Xenasys.Core.Contracts.Search;

// ==========================================================================================
// WHAT: Global Search Center wire DTOs — capped hits per entity group.
// WHY: Heavy tables must never be fully loaded into the SPA; server returns TOP-N only.
// EXAMPLE: GET /api/search?q=acme&limit=8 → { groups: [{ key: "companies", hits: […] }] }
// ==========================================================================================

public sealed class GlobalSearchResponseDto
{
    public string Query { get; init; } = string.Empty;

    public IReadOnlyList<GlobalSearchGroupDto> Groups { get; init; } = [];
}

public sealed class GlobalSearchGroupDto
{
    public string Key { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public IReadOnlyList<GlobalSearchHitDto> Hits { get; init; } = [];
}

public sealed class GlobalSearchHitDto
{
    public string Id { get; init; } = string.Empty;

    public string Kind { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string Href { get; init; } = string.Empty;
}
