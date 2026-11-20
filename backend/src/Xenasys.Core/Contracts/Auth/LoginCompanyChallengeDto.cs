using System.Text.Json.Serialization;

namespace Xenasys.Core.Contracts.Auth;

// ==========================================================================================
// WHAT: One company option inside a multi-tenant login challenge list (BRD §4.1).
// WHY: After BCrypt success, SPA needs tenant id/name/role so user can re-post company_id.
// EXAMPLE: { TenantId: 5, TenantCode: "ACME01", CompanyName: "Acme", Role: "TENANT_ADMIN" }.
// ==========================================================================================
public sealed class LoginCompanyOptionDto
{
    public int TenantId { get; init; }

    public string TenantCode { get; init; } = string.Empty;

    public string CompanyName { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;
}

// ==========================================================================================
// WHAT: Login challenge payload when credentials are OK but company selection is still required.
// WHY: Multi-company users must pick before a JWT with tenant_id is issued.
// EXAMPLE: RequiresCompanySelection=true + Companies=[Acme, Beta] — no Token yet.
// ==========================================================================================
public sealed class LoginCompanyChallengeDto
{
    public bool RequiresCompanySelection { get; init; } = true;

    [JsonPropertyName("userName")]
    public required string Username { get; init; }

    public required int UserId { get; init; }

    public required IReadOnlyList<LoginCompanyOptionDto> Companies { get; init; }
}
