using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Xenasys.Core.Abstractions;

namespace Xenasys.Infrastructure.Tenancy;

// ==========================================================================================
// WHAT: Maps JWT claim tenant_id (and role) into scoped ITenantProvider for the request.
// WHY: Isolation must come from the signed JWT only — never from client company_id (BRD §4.1 / T0.5).
// EXAMPLE: Claim tenant_id=7 + role TENANT_ADMIN → SetTenant(7); SUPER_ADMIN without tenant → SetTenant(0, true).
// ==========================================================================================
public sealed class TenantResolutionMiddleware
{
    public const string TenantIdClaim = TenancyKeyNames.JwtTenantId;
    public const string RoleClaim = TenancyKeyNames.JwtRole;
    public const string SuperAdminRole = "SUPER_ADMIN";

    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantProvider tenantProvider)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantRaw = context.User.FindFirstValue(TenantIdClaim);

            var role = context.User.FindFirstValue(ClaimTypes.Role)
                       ?? context.User.FindFirstValue(RoleClaim);

            var isSuperAdmin = string.Equals(role, SuperAdminRole, StringComparison.OrdinalIgnoreCase);

            if (int.TryParse(tenantRaw, out var tenantId) && tenantId > 0)
            {
                tenantProvider.SetTenant(tenantId, isSuperAdmin);
            }
            else if (isSuperAdmin)
            {
                // Super Admin may operate without a bound tenant; filters yield empty
                // until an explicit tenant selector is applied via IgnoreQueryFilters + Where.
                tenantProvider.SetTenant(0, isSuperAdmin: true);
            }
        }

        await _next(context);
    }
}
