using Xenasys.Core.Abstractions;

namespace Xenasys.Infrastructure.Tenancy;

// ==========================================================================================
// WHAT: Scoped ambient tenant context (TenantId + IsSuperAdmin) for the current HTTP request.
// WHY: EF filters and services need a single in-process SSoT populated from JWT tenant_id.
// EXAMPLE: Middleware SetTenant(5) → HasTenant true; Super Admin SetTenant(0, true) → unbound platform.
// ==========================================================================================
public sealed class TenantProvider : ITenantProvider
{
    public int? TenantId { get; private set; }

    public bool HasTenant => TenantId.HasValue && TenantId.Value > 0;

    public bool IsSuperAdmin { get; private set; }

    public void SetTenant(int tenantId, bool isSuperAdmin = false)
    {
        TenantId = tenantId;
        IsSuperAdmin = isSuperAdmin;
    }
}
