using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions;
using Xenasys.Core.Abstractions.Auth;
using Xenasys.Core.Contracts.Forms;
using Xenasys.Core.Contracts.Permissions;
using Xenasys.Core.Entities;
using Xenasys.Core.Security;
using Xenasys.Infrastructure.Security;
using Xenasys.Persistence.Data;

namespace Xenasys.Presentation.Controllers;

// ==========================================================================================
// WHAT: Current-user Form×Action grants + company Role×Form×Action audit (T1.36 clean SSoT).
// WHY: newSalon-style RBAC — no Permissions/RolePermissions DB catalog.
// EXAMPLE: GET /api/permissions/me → forms[{path, actions[]}] + derived permissionCodes[].
// ==========================================================================================
[ApiController]
[Route("api/permissions")]
[Authorize]
public sealed class PermissionsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionGate _permissionGate;

    public PermissionsController(
        ApplicationDbContext db,
        ITenantProvider tenantProvider,
        ICurrentUser currentUser,
        IPermissionGate permissionGate)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
        _permissionGate = permissionGate;
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserPermissionsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentUserPermissionsDto>> GetMine(CancellationToken cancellationToken)
    {
        var codes = await _permissionGate.ListCodesAsync(cancellationToken);
        var forms = await _permissionGate.ListFormsAsync(cancellationToken);
        return Ok(new CurrentUserPermissionsDto
        {
            Role = _currentUser.Role ?? string.Empty,
            CompanyId = _tenantProvider.TenantId,
            Forms = forms,
            PermissionCodes = codes,
        });
    }

    /// <summary>
    /// Company-scoped Form→Action→Roles audit (newSalon GetFormActionRoleAudit shape).
    /// </summary>
    [HttpGet("form-action-audit")]
    [RequirePermission("roles.read")]
    [ProducesResponseType(typeof(FormActionAuditMatrixDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FormActionAuditMatrixDto>> GetFormActionAudit(
        [FromQuery] string? formSearch = null,
        [FromQuery] int? roleId = null,
        CancellationToken cancellationToken = default)
    {
        // Platform-only surface — Super Admin; Tenant Admin uses Roles drawer for own grants.
        if (!_tenantProvider.IsSuperAdmin)
        {
            return Forbid();
        }

        // Super Admin audit spans all tenant roles (ignore JWT company bind).
        int? scopedCompanyId = null;

        if (roleId is <= 0)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["roleId"] = ["RoleId must be greater than 0 when provided."],
                },
            });
        }

        if (roleId is int rid)
        {
            var roleOk = await _db.Roles.AsNoTracking().AnyAsync(
                r => r.Id == rid
                     && r.IsActive
                     && r.CompanyId != null
                     && (scopedCompanyId == null || r.CompanyId == scopedCompanyId),
                cancellationToken);
            if (!roleOk)
            {
                return UnprocessableEntity(new
                {
                    errors = new Dictionary<string, string[]>
                    {
                        ["roleId"] = ["Role does not exist or is inactive for this scope."],
                    },
                });
            }
        }

        var search = string.IsNullOrWhiteSpace(formSearch) ? null : formSearch.Trim();

        var formsQuery = _db.Forms.AsNoTracking().Where(f => f.IsActive);
        if (search is not null)
        {
            formsQuery = formsQuery.Where(f =>
                f.Name.Contains(search) || f.Path.Contains(search));
        }

        // Only forms that have at least one FormAction catalog link.
        formsQuery = formsQuery.Where(f =>
            _db.FormActions.Any(fa => fa.FormId == f.Id && fa.IsActive));

        if (roleId is int filterRoleId)
        {
            formsQuery = formsQuery.Where(f =>
                _db.RoleFormActions.Any(rfa =>
                    rfa.FormId == f.Id
                    && rfa.RoleId == filterRoleId
                    && rfa.IsActive));
        }

        var formRows = await formsQuery
            .OrderBy(f => f.Name)
            .Select(f => new { f.Id, f.Name, f.Path })
            .ToListAsync(cancellationToken);

        if (formRows.Count == 0)
        {
            return Ok(new FormActionAuditMatrixDto { Forms = [] });
        }

        var formIds = formRows.Select(f => f.Id).ToList();

        var catalogPairs = await (
                from fa in _db.FormActions.AsNoTracking()
                join a in _db.Actions.AsNoTracking() on fa.ActionId equals a.Id
                where formIds.Contains(fa.FormId) && fa.IsActive && a.IsActive
                select new { fa.FormId, a.Id, a.Name })
            .ToListAsync(cancellationToken);

        var companyRolesQuery = _db.Roles.AsNoTracking()
            .Where(r => r.CompanyId != null && r.IsActive)
            .Where(r => r.RoleCode != SeedConstants.RoleSuperAdmin);

        if (scopedCompanyId is int onlyCompany)
        {
            companyRolesQuery = companyRolesQuery.Where(r => r.CompanyId == onlyCompany);
        }

        var companyRoles = await companyRolesQuery
            .Select(r => new { r.Id, r.Name, r.RoleCode })
            .ToListAsync(cancellationToken);
        var companyRoleIds = companyRoles.Select(r => r.Id).ToList();
        var roleNameById = companyRoles.ToDictionary(r => r.Id, r => (r.Name, r.RoleCode));

        var grantQuery = _db.RoleFormActions.AsNoTracking()
            .Where(rfa => formIds.Contains(rfa.FormId)
                          && companyRoleIds.Contains(rfa.RoleId)
                          && rfa.IsActive);
        if (roleId is int onlyRole)
        {
            grantQuery = grantQuery.Where(rfa => rfa.RoleId == onlyRole);
        }

        var grants = await grantQuery
            .Select(rfa => new { rfa.FormId, rfa.ActionId, rfa.RoleId })
            .ToListAsync(cancellationToken);

        var grantsByFormAction = grants
            .GroupBy(g => (g.FormId, g.ActionId))
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.RoleId).Distinct().ToList());

        var forms = formRows.Select(f =>
        {
            var actionsForForm = catalogPairs
                .Where(p => p.FormId == f.Id)
                .OrderBy(p => p.Name)
                .ToList();

            var actionDtos = actionsForForm.Select(a =>
            {
                grantsByFormAction.TryGetValue((f.Id, a.Id), out var roleIds);
                roleIds ??= [];
                var roles = roleIds
                    .Where(id => roleNameById.ContainsKey(id))
                    .Select(id =>
                    {
                        var (name, code) = roleNameById[id];
                        return new FormActionAuditRoleRefDto
                        {
                            RoleId = id,
                            RoleName = name,
                            RoleCode = code,
                        };
                    })
                    .OrderBy(r => r.RoleName)
                    .ToList();

                return new FormActionAuditActionDto
                {
                    ActionId = a.Id,
                    ActionName = a.Name,
                    Roles = roles,
                };
            }).ToList();

            var assigned = actionDtos.Count(a => a.Roles.Count > 0);

            return new FormActionAuditFormDto
            {
                FormId = f.Id,
                FormName = f.Name,
                FormPath = f.Path,
                TotalActionCount = actionDtos.Count,
                AssignedActionCount = assigned,
                Actions = actionDtos,
            };
        }).ToList();

        return Ok(new FormActionAuditMatrixDto { Forms = forms });
    }
}
