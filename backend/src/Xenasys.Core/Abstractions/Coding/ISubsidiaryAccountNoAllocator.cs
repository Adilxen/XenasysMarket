namespace Xenasys.Core.Abstractions.Coding;

// ==========================================================================================
// WHAT: Allocates immutable AccountNo + AccountCode (manual / product / vendor).
// WHY: One coding context; BRD AccountNo ACC-yyyy-seq; Code Tenant-M/P/V; never updated.
// EXAMPLE: AllocateManualAsync(12) → No=ACC-2026-00042, Code=ACM007-M00042 (No unique platform-wide)
// ==========================================================================================
public sealed record SubsidiaryAccountCodes(string AccountNo, string AccountCode, long Sequence);

public interface ISubsidiaryAccountNoAllocator
{
    Task<SubsidiaryAccountCodes> AllocateManualAsync(
        int companyId,
        CancellationToken cancellationToken = default);

    Task<SubsidiaryAccountCodes> AllocateProductAsync(
        int companyId,
        CancellationToken cancellationToken = default);

    Task<SubsidiaryAccountCodes> AllocateVendorAsync(
        int companyId,
        CancellationToken cancellationToken = default);

    /// <summary>Globally unique BRD AccountNo only (Cash/Bank baselines).</summary>
    Task<string> AllocateAccountNoAsync(CancellationToken cancellationToken = default);
}
