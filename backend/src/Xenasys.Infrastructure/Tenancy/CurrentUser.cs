using System.Security.Claims;
using Xenasys.Core.Abstractions;

namespace Xenasys.Infrastructure.Tenancy;

// ==========================================================================================
// WHAT: Reads authenticated user_id and role from the current ClaimsPrincipal.
// WHY: App services need identity without parsing JWT claims ad hoc or trusting body fields.
// EXAMPLE: Claims user_id=42 + role TENANT_STAFF → UserId 42, Role TENANT_STAFF when authenticated.
// ==========================================================================================
public sealed class CurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal? _principal;

    public CurrentUser(ClaimsPrincipal? principal)
    {
        _principal = principal;
    }

    public int? UserId
    {
        get
        {
            var raw = _principal?.FindFirstValue(TenancyKeyNames.JwtUserId)
                      ?? _principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Role =>
        _principal?.FindFirstValue(ClaimTypes.Role)
        ?? _principal?.FindFirstValue(TenancyKeyNames.JwtRole);

    public string? UserName =>
        _principal?.FindFirstValue(ClaimTypes.Name)
        ?? _principal?.Identity?.Name;

    public bool IsAuthenticated => _principal?.Identity?.IsAuthenticated == true;
}
