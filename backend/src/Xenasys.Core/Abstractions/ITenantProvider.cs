namespace Xenasys.Core.Abstractions;

/// <summary>
/// Runtime tenant context resolved from JWT — never from client payload.
/// </summary>
public interface ITenantProvider
{
    int? TenantId { get; }

    bool HasTenant { get; }

    /// <summary>True when Super Admin may bypass tenant filters via IgnoreQueryFilters.</summary>
    bool IsSuperAdmin { get; }

    void SetTenant(int tenantId, bool isSuperAdmin = false);
}
