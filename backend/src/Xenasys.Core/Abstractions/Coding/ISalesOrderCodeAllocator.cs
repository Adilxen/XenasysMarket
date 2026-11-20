namespace Xenasys.Core.Abstractions.Coding;

// ==========================================================================================
// WHAT: Per-company Sales Order code allocator.
// WHY: Stable SO###### business keys without client-supplied codes.
// EXAMPLE: AllocateAsync(2053) → "SO000042".
// ==========================================================================================
public interface ISalesOrderCodeAllocator
{
    Task<string> AllocateAsync(int companyId, CancellationToken cancellationToken = default);
}
