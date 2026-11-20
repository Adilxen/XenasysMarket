using Microsoft.AspNetCore.Authorization;
using Xenasys.Core.Abstractions.Auth;

namespace Xenasys.Infrastructure.Security;

// ==========================================================================================
// WHAT: Authorization requirement keyed by PermissionCatalog code (e.g. products.read).
// WHY: Controllers enforce the BRD roles/permission matrix via IPermissionGate, not UI hide.
// EXAMPLE: new PermissionRequirement("products.read") succeeded when gate returns true.
// ==========================================================================================
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permissionCode)
    {
        PermissionCode = permissionCode;
    }

    public string PermissionCode { get; }
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionGate _permissionGate;

    public PermissionAuthorizationHandler(IPermissionGate permissionGate)
    {
        _permissionGate = permissionGate;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (await _permissionGate.HasAsync(requirement.PermissionCode))
        {
            context.Succeed(requirement);
        }
    }
}
