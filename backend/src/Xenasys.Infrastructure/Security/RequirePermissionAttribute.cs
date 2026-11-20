using Microsoft.AspNetCore.Authorization;

namespace Xenasys.Infrastructure.Security;

// ==========================================================================================
// WHAT: [RequirePermission("products.read")] → policy name PERM:products.read.
// WHY: Declarative matrix enforcement on controllers without ad-hoc if-checks.
// EXAMPLE: [RequirePermission("categories.read")] on CategoriesController.List.
// ==========================================================================================
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "PERM:";

    public RequirePermissionAttribute(string permissionCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);
        Policy = PolicyPrefix + permissionCode.Trim();
        PermissionCode = permissionCode.Trim();
    }

    public string PermissionCode { get; }
}
