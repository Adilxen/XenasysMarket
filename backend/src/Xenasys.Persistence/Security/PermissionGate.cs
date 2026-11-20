using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions;
using Xenasys.Core.Abstractions.Auth;
using Xenasys.Core.Contracts.Permissions;
using Xenasys.Core.Entities;
using Xenasys.Core.Security;
using Xenasys.Persistence.Data;

namespace Xenasys.Persistence.Security;

// ==========================================================================================
// WHAT: Runtime permission check — codes from RoleFormActions ∪ all UserRoles (T1.38).
// WHY: newSalon GetUserPermissions unions every assigned role; Super Admin allow-all.
// EXAMPLE: HasAsync("company.read") when any assigned role grants /settings/company + Read.
// ==========================================================================================
public sealed class PermissionGate : IPermissionGate
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantProvider _tenantProvider;

    public PermissionGate(
        ApplicationDbContext db,
        ICurrentUser currentUser,
        ITenantProvider tenantProvider)
    {
        _db = db;
        _currentUser = currentUser;
        _tenantProvider = tenantProvider;
    }

    public async Task<bool> HasAsync(string permissionCode, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.Role))
        {
            return false;
        }

        if (string.Equals(_currentUser.Role, SeedConstants.RoleSuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var codes = await ListCodesAsync(cancellationToken);
        return codes.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<string>> ListCodesAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.Role))
        {
            return [];
        }

        if (string.Equals(_currentUser.Role, SeedConstants.RoleSuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return PermissionCatalog.All.Select(p => p.Code).ToList();
        }

        var roleIds = await ResolveAssignedRoleIdsAsync(cancellationToken);
        if (roleIds.Count == 0)
        {
            return [];
        }

        var union = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var roleId in roleIds)
        {
            foreach (var code in await ListDerivedCodesForRoleAsync(roleId, cancellationToken))
            {
                union.Add(code);
            }
        }

        return union.ToList();
    }

    public async Task<IReadOnlyList<FormWithActionsDto>> ListFormsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.Role))
        {
            return [];
        }

        if (string.Equals(_currentUser.Role, SeedConstants.RoleSuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return await BuildAllActiveFormsAsync(cancellationToken);
        }

        var roleIds = await ResolveAssignedRoleIdsAsync(cancellationToken);
        if (roleIds.Count == 0)
        {
            return [];
        }

        return await BuildRolesFormsUnionAsync(roleIds, cancellationToken);
    }

    public static async Task<IReadOnlyList<string>> ListDerivedCodesForRoleAsync(
        ApplicationDbContext db,
        int roleId,
        CancellationToken cancellationToken = default)
    {
        var rows = await (
                from rfa in db.RoleFormActions.AsNoTracking()
                join fa in db.FormActions.AsNoTracking()
                    on new { rfa.FormId, rfa.ActionId } equals new { fa.FormId, fa.ActionId }
                join f in db.Forms.AsNoTracking() on rfa.FormId equals f.Id
                join a in db.Actions.AsNoTracking() on rfa.ActionId equals a.Id
                where rfa.RoleId == roleId
                    && rfa.IsActive
                    && fa.IsActive
                    && f.IsActive
                    && a.IsActive
                select new { f.Path, a.Name })
            .ToListAsync(cancellationToken);

        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var code = FormPermissionBridge.ToPermissionCode(row.Path, row.Name);
            if (!string.IsNullOrWhiteSpace(code))
            {
                codes.Add(code);
            }

            // View/Read both map to read; also emit *.read when View granted.
            var norm = FormPermissionBridge.NormalizeAction(row.Name);
            if (norm == "read")
            {
                var module = FormPermissionBridge.ModuleForPath(row.Path);
                if (module is not null)
                {
                    codes.Add($"{module}.read");
                }
            }
        }

        return codes.ToList();
    }

    private Task<IReadOnlyList<string>> ListDerivedCodesForRoleAsync(
        int roleId,
        CancellationToken cancellationToken) =>
        ListDerivedCodesForRoleAsync(_db, roleId, cancellationToken);

    /// <summary>
    /// Roles for the JWT company only: membership primary RoleId + company-scoped UserRoles.
    /// Platform TENANT_ADMIN UserRole is shared across companies — do NOT union it into every
    /// tenant session (T1.56 / M6) or Inventory-only users inherit full TA grants.
    /// </summary>
    private async Task<IReadOnlyList<int>> ResolveAssignedRoleIdsAsync(
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not int userId || userId <= 0)
        {
            var fallback = await ResolveRoleAsync(cancellationToken);
            return fallback is null ? [] : [fallback.Id];
        }

        var tenantId = _tenantProvider.TenantId;
        if (tenantId is not int tid || tid <= 0)
        {
            var platformFallback = await ResolveRoleAsync(cancellationToken);
            return platformFallback is null ? [] : [platformFallback.Id];
        }

        var membershipRoleId = await _db.UserTenantMemberships.AsNoTracking()
            .Where(m => m.UserId == userId && m.CompanyId == tid && m.IsActive)
            .Select(m => (int?)m.RoleId)
            .FirstOrDefaultAsync(cancellationToken);

        var companyRoleIds = await (
                from ur in _db.UserRoles.AsNoTracking()
                join r in _db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                where ur.UserId == userId
                      && ur.IsActive
                      && r.IsActive
                      && r.CompanyId == tid
                select r.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        var set = companyRoleIds.ToHashSet();
        if (membershipRoleId is int mid && mid > 0)
        {
            set.Add(mid);
        }

        if (set.Count > 0)
        {
            return set.ToList();
        }

        var role = await ResolveRoleAsync(cancellationToken);
        return role is null ? [] : [role.Id];
    }

    private async Task<IReadOnlyList<FormWithActionsDto>> BuildRolesFormsUnionAsync(
        IReadOnlyList<int> roleIds,
        CancellationToken cancellationToken)
    {
        var rows = await (
                from rfa in _db.RoleFormActions.AsNoTracking()
                join fa in _db.FormActions.AsNoTracking()
                    on new { rfa.FormId, rfa.ActionId } equals new { fa.FormId, fa.ActionId }
                join f in _db.Forms.AsNoTracking() on rfa.FormId equals f.Id
                join a in _db.Actions.AsNoTracking() on rfa.ActionId equals a.Id
                where roleIds.Contains(rfa.RoleId)
                    && rfa.IsActive
                    && fa.IsActive
                    && f.IsActive
                    && a.IsActive
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

    private async Task<IReadOnlyList<FormWithActionsDto>> BuildAllActiveFormsAsync(
        CancellationToken cancellationToken)
    {
        var rows = await (
                from fa in _db.FormActions.AsNoTracking()
                join f in _db.Forms.AsNoTracking() on fa.FormId equals f.Id
                join a in _db.Actions.AsNoTracking() on fa.ActionId equals a.Id
                where fa.IsActive && f.IsActive && a.IsActive
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

    private async Task<Role?> ResolveRoleAsync(CancellationToken cancellationToken)
    {
        var roleCode = _currentUser.Role!;
        var tenantId = _tenantProvider.TenantId;

        return await _db.Roles
            .AsNoTracking()
            .Where(r => r.RoleCode == roleCode)
            .Where(r =>
                r.CompanyId == null
                || (tenantId != null && r.CompanyId == tenantId))
            .OrderByDescending(r => r.CompanyId.HasValue)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
