namespace Xenasys.Core.Abstractions;

// ==========================================================================================
// WHAT: Single Source of Truth for tenancy key names (company_id inbound vs tenant_id JWT).
// WHY: Prevents alias drift — inbound HTTP/JSON uses company_id; JWT isolation uses tenant_id only.
// EXAMPLE: Login body { "company_id": 5 } → JWT claim tenant_id=5 after successful bind.
// ==========================================================================================
public static class TenancyKeyNames
{
    /// <summary>Sole inbound parameter / JSON / query / route key.</summary>
    public const string CompanyId = "company_id";

    /// <summary>Sole JWT claim for tenant binding (never accept aliases).</summary>
    public const string JwtTenantId = "tenant_id";

    /// <summary>Sole JWT claim for role.</summary>
    public const string JwtRole = "role";

    /// <summary>Sole JWT claim for user id.</summary>
    public const string JwtUserId = "user_id";
}
