using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions;
using Xenasys.Core.Abstractions.Coding;
using Xenasys.Core.Contracts.Permissions;
using Xenasys.Core.Contracts.Roles;
using Xenasys.Core.Entities;
using Xenasys.Core.Querying;
using Xenasys.Core.Security;
using Xenasys.Infrastructure.Security;
using Xenasys.Persistence.Data;
using Xenasys.Persistence.Querying;
using Xenasys.Persistence.Seeding;

namespace Xenasys.Presentation.Controllers;

// ==========================================================================================
// WHAT: Roles CRUD — company-scoped Name/RoleCode/Description/IsActive (BRD §3.1).
// WHY: T1.28 — restore CompanyId; system roles locked via RoleCode.
// EXAMPLE: POST /api/roles { name, description } → RoleCode "IN0012" for JWT company.
// ==========================================================================================
[ApiController]
[Route("api/roles")]
[Authorize]
public sealed class RolesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IRoleCodeAllocator _roleCodeAllocator;

    public RolesController(
        ApplicationDbContext db,
        ITenantProvider tenantProvider,
        IRoleCodeAllocator roleCodeAllocator)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _roleCodeAllocator = roleCodeAllocator;
    }

    [HttpGet]
    [RequirePermission("roles.read")]
    [ProducesResponseType(typeof(IReadOnlyList<RoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> List(
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? shape = null,
        [FromQuery] string? roleCode = null,
        [FromQuery] string? name = null,
        [FromQuery] string? description = null,
        [FromQuery] string? companyName = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveCompanyScope(out var companyId, out var superAll))
        {
            return Forbid();
        }

        var dataShape = DataShapeParser.Parse(shape, DataShape.List);
        IQueryable<Role> query = _db.Roles.AsNoTracking().WhereActive(includeInactive);

        if (superAll)
        {
            // Platform view: all companies' roles + platform system roles.
        }
        else
        {
            // Company customs + shared TENANT_ADMIN (no SUPER_ADMIN / TENANT_STAFF).
            query = query.Where(r =>
                r.CompanyId == companyId
                || (r.CompanyId == null && r.RoleCode == SeedConstants.RoleTenantAdmin));
        }

        query = query
            .WhereContains(roleCode, r => r.RoleCode)
            .WhereContains(name, r => r.Name)
            .WhereContains(description, r => r.Description);

        var companyPattern = QueryFilterExtensions.NormalizeContainsTerm(companyName);
        if (companyPattern is not null)
        {
            query = query.Where(r =>
                r.Company != null && r.Company.Name.ToLower().Contains(companyPattern));
        }

        var roles = await query
            .OrderBy(r => r.CompanyId == null ? 0 : 1)
            .ThenBy(r => r.Company != null ? r.Company.Name : string.Empty)
            .ThenBy(r => r.Name)
            .Select(RoleProjections.ForShape(dataShape))
            .ToListAsync(cancellationToken);

        return Ok(roles);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("roles.read")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoleDto>> GetById(
        int id,
        [FromQuery] string? shape = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveCompanyScope(out var companyId, out var superAll))
        {
            return Forbid();
        }

        var dataShape = DataShapeParser.Parse(shape, DataShape.Detail);
        var roleQuery = _db.Roles.AsNoTracking().Where(r => r.Id == id);
        if (!superAll)
        {
            // Inline — EF cannot translate IsPlatformTenantSystemRole.
            roleQuery = roleQuery.Where(r =>
                r.CompanyId == companyId
                || (r.CompanyId == null && r.RoleCode == SeedConstants.RoleTenantAdmin));
        }

        var role = await roleQuery
            .Select(RoleProjections.ForShape(dataShape))
            .FirstOrDefaultAsync(cancellationToken);

        return role is null ? NotFound() : Ok(role);
    }

    [HttpPost]
    [RequirePermission("roles.create")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RoleDto>> Create(
        [FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryResolveCreateCompanyId(request.CompanyId, out var companyId, out var companyError))
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["company_id"] = [companyError],
                },
            });
        }

        var companyExists = await _db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == companyId && t.IsActive, cancellationToken);
        if (!companyExists)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["company_id"] = ["Company does not exist or is inactive."],
                },
            });
        }

        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var name = (request.Name ?? string.Empty).Trim();
        var description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        var defaultRoute = DefaultRouteNormalizer.Normalize(request.DefaultRoute);

        if (string.IsNullOrWhiteSpace(name) || name.Length < 2 || name.Length > 100)
        {
            errors["name"] = ["Role name is required (2–100 characters)."];
        }

        if (description is { Length: > 500 })
        {
            errors["description"] = ["Description max length is 500."];
        }

        if (ContainsDangerousMarkup(name) || (description is not null && ContainsDangerousMarkup(description)))
        {
            errors["name"] = ["HTML tags and script content are not allowed."];
        }

        if (errors.Count == 0
            && await _db.Roles.AsNoTracking()
                .AnyAsync(r => r.CompanyId == companyId && r.Name == name, cancellationToken))
        {
            errors["name"] = ["A role with this name already exists in this company."];
        }

        if (errors.Count > 0)
        {
            return UnprocessableEntity(new { errors });
        }

        string code;
        try
        {
            code = await _roleCodeAllocator.AllocateAsync(companyId, name, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["code"] = [ex.Message],
                },
            });
        }

        var role = new Role
        {
            CompanyId = companyId,
            RoleCode = code,
            Name = name,
            Description = description,
            DefaultRoute = defaultRoute,
            IsActive = true,
        };

        _db.Roles.Add(role);
        await _db.SaveChangesAsync(cancellationToken);

        // M6.3 — custom ops roles start with staff operational template (editable via Form×Action).
        await FormActionRbacSeed.ApplyStaffTemplateAsync(_db, role.Id, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = role.Id },
            await ToDtoAsync(role, cancellationToken));
    }

    [HttpPut("{id:int}")]
    [RequirePermission("roles.update")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RoleDto>> Update(
        int id,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var role = await FindCompanyRoleAsync(id, cancellationToken);
        if (role is null)
        {
            return NotFound();
        }

        var defaultRoute = DefaultRouteNormalizer.Normalize(request.DefaultRoute);

        // System roles: DefaultRoute only (name / Form×Action stay locked).
        if (SeedConstants.IsSystemRoleCode(role.RoleCode))
        {
            role.DefaultRoute = defaultRoute;
            await _db.SaveChangesAsync(cancellationToken);
            return Ok(await ToDtoAsync(role, cancellationToken));
        }

        var name = (request.Name ?? string.Empty).Trim();
        var description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();

        if (string.IsNullOrWhiteSpace(name) || name.Length < 2 || name.Length > 100)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["name"] = ["Role name is required (2–100 characters)."],
                },
            });
        }

        if (ContainsDangerousMarkup(name) || (description is not null && ContainsDangerousMarkup(description)))
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["name"] = ["HTML tags and script content are not allowed."],
                },
            });
        }

        var companyId = role.CompanyId!.Value;
        if (await _db.Roles.AsNoTracking().AnyAsync(
                r => r.CompanyId == companyId && r.Name == name && r.Id != id,
                cancellationToken))
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["name"] = ["A role with this name already exists in this company."],
                },
            });
        }

        role.Name = name;
        role.Description = description;
        role.DefaultRoute = defaultRoute;
        role.IsActive = request.IsActive;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await ToDtoAsync(role, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("roles.delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var role = await FindCompanyRoleAsync(id, cancellationToken);
        if (role is null)
        {
            return NotFound();
        }

        if (SeedConstants.IsSystemRoleCode(role.RoleCode))
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["roleId"] = ["System roles cannot be deleted."],
                },
            });
        }

        if (!role.IsActive)
        {
            return NoContent();
        }

        role.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:int}/form-actions")]
    [RequirePermission("roles.read")]
    [ProducesResponseType(typeof(RoleFormActionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoleFormActionsDto>> GetFormActions(
        int id,
        CancellationToken cancellationToken)
    {
        if (!TryResolveCompanyScope(out var companyId, out var superAll))
        {
            return Forbid();
        }

        var roleQuery = _db.Roles.AsNoTracking().Where(r => r.Id == id);
        if (!superAll)
        {
            roleQuery = roleQuery.Where(r =>
                r.CompanyId == companyId
                || (r.CompanyId == null && r.RoleCode == SeedConstants.RoleTenantAdmin));
        }

        if (!await roleQuery.AnyAsync(cancellationToken))
        {
            return NotFound();
        }

        var forms = await BuildRoleFormActionsDto(id, cancellationToken);
        return Ok(new RoleFormActionsDto { RoleId = id, Forms = forms });
    }

    [HttpPut("{id:int}/form-actions")]
    [RequirePermission("roles.update")]
    [ProducesResponseType(typeof(RoleFormActionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RoleFormActionsDto>> SyncFormActions(
        int id,
        [FromBody] SyncRoleFormActionsRequest request,
        CancellationToken cancellationToken)
    {
        var role = await FindCompanyRoleAsync(id, cancellationToken);
        if (role is null)
        {
            return NotFound();
        }

        if (SeedConstants.IsSystemRoleCode(role.RoleCode))
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["roleId"] = ["System role Form×Action maps are locked."],
                },
            });
        }

        var items = request.Items ?? [];
        var desired = new HashSet<(int FormId, int ActionId)>();
        foreach (var item in items)
        {
            foreach (var actionId in item.ActionIds ?? [])
            {
                if (item.FormId > 0 && actionId > 0)
                {
                    desired.Add((item.FormId, actionId));
                }
            }
        }

        // Only allow pairs present in active FormActions catalog.
        if (desired.Count > 0)
        {
            var catalog = await (
                    from fa in _db.FormActions.AsNoTracking()
                    join f in _db.Forms.AsNoTracking() on fa.FormId equals f.Id
                    where fa.IsActive && f.IsActive
                    select new { fa.FormId, fa.ActionId, f.Path })
                .ToListAsync(cancellationToken);

            if (!_tenantProvider.IsSuperAdmin)
            {
                catalog = catalog
                    .Where(c => !FormActionRbacSeed.PlatformAdminOnlyFormPaths.Contains(c.Path))
                    .ToList();
            }

            var allowed = catalog.Select(c => (c.FormId, c.ActionId)).ToHashSet();
            desired.RemoveWhere(p => !allowed.Contains(p));
        }

        await FormActionRbacSeed.SyncRoleFormActionsAsync(_db, role.Id, desired, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var forms = await BuildRoleFormActionsDto(id, cancellationToken);
        return Ok(new RoleFormActionsDto { RoleId = id, Forms = forms });
    }

    private async Task<IReadOnlyList<FormWithActionsDto>> BuildRoleFormActionsDto(
        int roleId,
        CancellationToken cancellationToken)
    {
        var rows = await (
                from rfa in _db.RoleFormActions.AsNoTracking()
                join f in _db.Forms.AsNoTracking() on rfa.FormId equals f.Id
                join a in _db.Actions.AsNoTracking() on rfa.ActionId equals a.Id
                where rfa.RoleId == roleId && rfa.IsActive && f.IsActive && a.IsActive
                select new { f.Id, f.Path, f.Name, ActionId = a.Id, ActionName = a.Name })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => new { r.Id, r.Path, r.Name })
            .OrderBy(g => g.Key.Name)
            .Select(g => new FormWithActionsDto
            {
                Id = g.Key.Id,
                Path = g.Key.Path,
                Name = g.Key.Name,
                Actions = g
                    .Select(x => new FormActionRefDto { Id = x.ActionId, Name = x.ActionName })
                    .DistinctBy(a => a.Id)
                    .OrderBy(a => a.Name)
                    .ToList(),
            })
            .ToList();
    }

    private bool TryResolveCompanyScope(out int? companyId, out bool superAll)
    {
        companyId = _tenantProvider.TenantId;
        // BRD §3.1 — Super Admin always sees all companies' roles (even when login-bound).
        superAll = _tenantProvider.IsSuperAdmin;
        if (superAll)
        {
            return true;
        }

        return companyId is > 0;
    }

    /// <summary>
    /// <summary>
    /// Create target company (T1.60): SA body company_id = provisioning; TA = JWT only.
    /// </summary>
    private bool TryResolveCreateCompanyId(
        int? requestedCompanyId,
        out int companyId,
        out string error)
    {
        companyId = 0;
        error = "Company context is required to create a role.";

        if (_tenantProvider.IsSuperAdmin)
        {
            if (requestedCompanyId is int rid && rid > 0)
            {
                companyId = rid;
                return true;
            }

            if (_tenantProvider.TenantId is int jwtCompany && jwtCompany > 0)
            {
                companyId = jwtCompany;
                return true;
            }

            error = "Select a company to create the role.";
            return false;
        }

        if (_tenantProvider.TenantId is int tenantCompany && tenantCompany > 0)
        {
            companyId = tenantCompany;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Load a role for mutation. Super Admin: any role; Tenant: own company customs only.
    /// </summary>
    private async Task<Role?> FindCompanyRoleAsync(int id, CancellationToken cancellationToken)
    {
        if (_tenantProvider.IsSuperAdmin)
        {
            return await _db.Roles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        }

        if (_tenantProvider.TenantId is not int companyId || companyId <= 0)
        {
            return null;
        }

        return await _db.Roles.FirstOrDefaultAsync(
            r => r.Id == id && r.CompanyId == companyId,
            cancellationToken);
    }

    private async Task<RoleDto> ToDtoAsync(Role role, CancellationToken cancellationToken)
    {
        string? companyName = null;
        if (role.CompanyId is int cid)
        {
            companyName = await _db.Tenants.AsNoTracking()
                .Where(t => t.Id == cid)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new RoleDto
        {
            Id = role.Id,
            CompanyId = role.CompanyId,
            CompanyName = companyName,
            RoleCode = role.RoleCode,
            Name = role.Name,
            Description = role.Description,
            DefaultRoute = DefaultRouteNormalizer.Normalize(role.DefaultRoute),
            IsActive = role.IsActive,
            IsSystem = SeedConstants.IsSystemRoleCode(role.RoleCode),
        };
    }

    private static bool ContainsDangerousMarkup(string value) =>
        value.Contains('<') || value.Contains('>') ||
        value.Contains("javascript:", StringComparison.OrdinalIgnoreCase);
}
