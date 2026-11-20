namespace Xenasys.Core.Entities;

// ==========================================================================================
// WHAT: Platform-scoped monotonic counters for auto-generated codes (tenant, future SKUs).
// WHY: Atomic LastValue++ under UPDLOCK avoids UNIQUE races without scanning Tenants.
// EXAMPLE: SequenceKey="TENANT_CODE", LastValue=42 → next code uses 43.
// ==========================================================================================
public sealed class PlatformSequence
{
    /// <summary>Stable key — e.g. TENANT_CODE.</summary>
    public string SequenceKey { get; set; } = string.Empty;

    public long LastValue { get; set; }
}
