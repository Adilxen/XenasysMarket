using System.Text.Json.Serialization;

namespace Xenasys.Core.Contracts.Auth;

// ==========================================================================================
// WHAT: Successful login payload — JWT plus bound user/tenant/role (T0.3).
// WHY: SPA needs token + tenant context; Token embeds tenant_id for subsequent isolation.
// EXAMPLE: { Token: "eyJ...", UserId: 1, TenantId: 5, Role: "TENANT_STAFF", accessToken alias }.
// ==========================================================================================
public sealed class LoginResponseDto
{
    /// <summary>Signed JWT containing user_id, tenant_id, role.</summary>
    public required string Token { get; init; }

    /// <summary>Alias for API spec / existing SPA (<c>accessToken</c>).</summary>
    [JsonPropertyName("accessToken")]
    public string AccessToken => Token;

    [JsonPropertyName("userName")]
    public required string Username { get; init; }

    public required int UserId { get; init; }

    /// <summary>Bound company id from JWT context (0 = platform / unbound Super Admin).</summary>
    public required int TenantId { get; init; }

    public required string CompanyName { get; init; }

    public required string Role { get; init; }

    /// <summary>Role.DefaultRoute landing path after login (normalized; fallback /dashboard).</summary>
    public string DefaultRoute { get; init; } = "/dashboard";
}
