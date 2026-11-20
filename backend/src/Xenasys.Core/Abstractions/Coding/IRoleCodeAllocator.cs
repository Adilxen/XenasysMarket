namespace Xenasys.Core.Abstractions.Coding;

// ==========================================================================================
// WHAT: Allocates the next unique CHAR(6) Role Code for custom operational roles.
// WHY: Clients never invent codes — Persistence owns the atomic sequence (per company).
// EXAMPLE: await AllocateAsync(companyId, "Inventory Manager") → "IN0012".
// ==========================================================================================
public interface IRoleCodeAllocator
{
    Task<string> AllocateAsync(
        int companyId,
        string roleName,
        CancellationToken cancellationToken = default);
}
