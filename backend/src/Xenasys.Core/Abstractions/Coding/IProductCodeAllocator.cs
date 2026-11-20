namespace Xenasys.Core.Abstractions.Coding;

// ==========================================================================================
// WHAT: Allocates immutable CHAR(16) product_code per company.
// WHY: BRD SKU identity — TenantCode(6) + UserNn(2) + seq(D8); never updated after create.
// EXAMPLE: AllocateAsync(12, userId:7) → "ACM0070700000042"
// ==========================================================================================
public interface IProductCodeAllocator
{
    Task<string> AllocateAsync(
        int companyId,
        int userId,
        CancellationToken cancellationToken = default);
}
