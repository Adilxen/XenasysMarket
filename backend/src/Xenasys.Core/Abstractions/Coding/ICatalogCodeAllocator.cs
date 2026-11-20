namespace Xenasys.Core.Abstractions.Coding;

// ==========================================================================================
// WHAT: Allocates per-company Category / SubCategory codes (CAT01, SUB01).
// WHY: M2.7 — stable SKU segment codes via PlatformSequences (not free text).
// EXAMPLE: AllocateCategoryAsync(5) → "CAT01".
// ==========================================================================================
public interface ICatalogCodeAllocator
{
    Task<string> AllocateCategoryAsync(int companyId, CancellationToken cancellationToken = default);

    Task<string> AllocateSubCategoryAsync(int companyId, CancellationToken cancellationToken = default);
}
