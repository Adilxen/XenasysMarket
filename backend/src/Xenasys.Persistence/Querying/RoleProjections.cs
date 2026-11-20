using System.Linq.Expressions;
using Xenasys.Core.Contracts.Roles;
using Xenasys.Core.Entities;
using Xenasys.Core.Querying;

namespace Xenasys.Persistence.Querying;

// ==========================================================================================
// WHAT: Role projections — company-scoped module fields + computed IsSystem.
// WHY: List/detail stay small for Settings Roles grid.
// EXAMPLE: shape=list → Id, CompanyId, CompanyName, Name, RoleCode, …
// ==========================================================================================
public static class RoleProjections
{
    public static Expression<Func<Role, RoleDto>> ForShape(DataShape shape) =>
        shape == DataShape.Detail ? Detail : List;

    public static Expression<Func<Role, RoleDto>> List { get; } = r => new RoleDto
    {
        Id = r.Id,
        CompanyId = r.CompanyId,
        CompanyName = r.Company != null ? r.Company.Name : null,
        RoleCode = r.RoleCode,
        Name = r.Name,
        Description = r.Description,
        DefaultRoute = r.DefaultRoute,
        IsActive = r.IsActive,
        IsSystem = r.RoleCode == SeedConstants.RoleSuperAdmin
            || r.RoleCode == SeedConstants.RoleTenantAdmin
            || r.RoleCode == SeedConstants.RoleTenantStaff,
    };

    public static Expression<Func<Role, RoleDto>> Detail { get; } = r => new RoleDto
    {
        Id = r.Id,
        CompanyId = r.CompanyId,
        CompanyName = r.Company != null ? r.Company.Name : null,
        RoleCode = r.RoleCode,
        Name = r.Name,
        Description = r.Description,
        DefaultRoute = r.DefaultRoute,
        IsActive = r.IsActive,
        IsSystem = r.RoleCode == SeedConstants.RoleSuperAdmin
            || r.RoleCode == SeedConstants.RoleTenantAdmin
            || r.RoleCode == SeedConstants.RoleTenantStaff,
    };
}
