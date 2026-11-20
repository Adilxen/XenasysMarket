namespace Xenasys.Core.Abstractions.Coding;

// ==========================================================================================
// WHAT: Allocates the next unique CHAR(6) TenantCode for company registration.
// WHY: Controllers must not invent codes — Persistence owns the atomic sequence.
// EXAMPLE: await AllocateAsync("Acme Foods") → "ACM007".
// ==========================================================================================
public interface ITenantCodeAllocator
{
    /// <summary>
    /// Atomically increments the platform TENANT_CODE sequence and returns a unique code.
    /// Retries on rare UNIQUE collisions (e.g. legacy seed codes).
    /// </summary>
    Task<string> AllocateAsync(string companyName, CancellationToken cancellationToken = default);
}
