using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xenasys.Core.Abstractions;
using Xenasys.Core.Abstractions.Auth;
using Xenasys.Infrastructure.Auth;
using Xenasys.Infrastructure.Security;
using Xenasys.Infrastructure.Tenancy;

namespace Xenasys.Infrastructure;

// ==========================================================================================
// WHAT: Wires Infrastructure DI (tenant, auth, BCrypt, JWT, RBAC) and tenancy middleware hooks.
// WHY: Keeps auth/tenancy registrations in one place so Host can compose the isolation pipeline.
// EXAMPLE: AddInfrastructure() + UseTenantResolution() + UseTenantClaimGuard() after authentication.
// ==========================================================================================
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddScoped<ICurrentUser>(sp =>
        {
            var http = sp.GetRequiredService<IHttpContextAccessor>();
            return new CurrentUser(http.HttpContext?.User);
        });

        services.AddSingleton<IPasswordVerifier, BcryptPasswordVerifier>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddXenasysRbacAuthorization();

        return services;
    }

    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TenantResolutionMiddleware>();
    }

    public static IApplicationBuilder UseTenantClaimGuard(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TenantClaimGuardMiddleware>();
    }
}
