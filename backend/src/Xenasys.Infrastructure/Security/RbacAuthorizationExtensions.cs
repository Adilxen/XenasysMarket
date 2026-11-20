using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xenasys.Core.Entities;
using Xenasys.Core.Security;

namespace Xenasys.Infrastructure.Security;

// ==========================================================================================
// WHAT: Registers ASP.NET Core authorization policies for SUPER_ADMIN / TENANT_ADMIN / STAFF.
// WHY: Controllers need named RBAC policies aligned to seeded role codes, not ad-hoc role strings.
// EXAMPLE: [Authorize(Policy = PolicyTenantAdmin)] allows TENANT_ADMIN and SUPER_ADMIN roles.
// ==========================================================================================
public static class RbacAuthorizationExtensions
{
    public const string PolicySuperAdmin = "RBAC_SUPER_ADMIN";
    public const string PolicyTenantAdmin = "RBAC_TENANT_ADMIN";
    public const string PolicyTenantStaff = "RBAC_TENANT_STAFF";
    public const string PolicyTenantAdminOrAbove = "RBAC_TENANT_ADMIN_OR_ABOVE";

    public static IServiceCollection AddXenasysRbacAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        var builder = services.AddAuthorizationBuilder()
            .AddPolicy(PolicySuperAdmin, p =>
                p.RequireRole(SeedConstants.RoleSuperAdmin))
            .AddPolicy(PolicyTenantAdmin, p =>
                p.RequireRole(SeedConstants.RoleTenantAdmin, SeedConstants.RoleSuperAdmin))
            .AddPolicy(PolicyTenantStaff, p =>
                p.RequireRole(
                    SeedConstants.RoleTenantStaff,
                    SeedConstants.RoleTenantAdmin,
                    SeedConstants.RoleSuperAdmin))
            .AddPolicy(PolicyTenantAdminOrAbove, p =>
                p.RequireRole(SeedConstants.RoleTenantAdmin, SeedConstants.RoleSuperAdmin));

        foreach (var (code, _, _, _) in PermissionCatalog.All)
        {
            var permissionCode = code;
            builder.AddPolicy(
                RequirePermissionAttribute.PolicyPrefix + permissionCode,
                p => p.Requirements.Add(new PermissionRequirement(permissionCode)));
        }

        return services;
    }
}
