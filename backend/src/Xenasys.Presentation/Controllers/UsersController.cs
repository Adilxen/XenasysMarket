using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions;
using Xenasys.Core.Contracts.Users;
using Xenasys.Core.Entities;
using Xenasys.Core.Time;
using Xenasys.Infrastructure.Security;
using Xenasys.Persistence.Data;
using Xenasys.Persistence.Querying;

namespace Xenasys.Presentation.Controllers;

// ==========================================================================================
// WHAT: Tenant Users CRUD — list/create/view/edit + soft-deactivate + multi-role (T1.38).
// WHY: BRD Point 8 + newSalon UserRoles — JWT company membership; RoleIds[] → UserRoles.
// EXAMPLE: Super Admin GET /api/users → all companies' users; Tenant Admin → JWT company only.
// ==========================================================================================
[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public UsersController(ApplicationDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [RequirePermission("users.read")]
    [ProducesResponseType(typeof(IReadOnlyList<TenantUserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TenantUserDto>>> List(
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? userName = null,
        [FromQuery] string? email = null,
        [FromQuery] string? roleName = null,
        [FromQuery] string? companyName = null,
        CancellationToken cancellationToken = default)
    {
        // BRD §3.1 — Super Admin: global user visibility across all companies.
        if (_tenantProvider.IsSuperAdmin)
        {
            return Ok(await ListAllUsersAsync(
                includeInactive,
                userName,
                email,
                roleName,
                companyName,
                cancellationToken));
        }

        if (_tenantProvider.TenantId is not int companyId || companyId <= 0)
        {
            return Forbid();
        }

        var company = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == companyId)
            .Select(t => new { t.Id, t.Name, t.TenantCode })
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
        {
            return Forbid();
        }

        var companyPattern = QueryFilterExtensions.NormalizeContainsTerm(companyName);
        if (companyPattern is not null
            && !company.Name.ToLowerInvariant().Contains(companyPattern))
        {
            return Ok(Array.Empty<TenantUserDto>());
        }

        var membershipQuery = _db.UserTenantMemberships.AsNoTracking()
            .Where(m => m.CompanyId == companyId)
            .Where(m => includeInactive || (m.IsActive && m.User!.IsActive))
            .WhereContains(userName, m => m.User!.UserName)
            .WhereContains(email, m => m.User!.Email)
            .WhereContains(roleName, m => m.Role!.Name);

        var users = await membershipQuery
            .OrderBy(m => m.User!.UserName)
            .Select(m => new TenantUserDto
            {
                Id = m.User!.Id,
                UserName = m.User.UserName,
                Email = m.User.Email,
                IsActive = m.User.IsActive,
                MembershipId = m.Id,
                RoleId = m.RoleId,
                RoleCode = m.Role!.RoleCode,
                RoleName = m.Role.Name,
                MembershipIsActive = m.IsActive,
                CompanyId = company.Id,
                CompanyName = company.Name,
                TenantCode = company.TenantCode,
            })
            .ToListAsync(cancellationToken);

        await AttachCompanyRolesAsync(users, companyId, cancellationToken);
        await AttachUserCompaniesAsync(users, cancellationToken);
        return Ok(users);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("users.read")]
    [ProducesResponseType(typeof(TenantUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantUserDto>> GetById(int id, CancellationToken cancellationToken)
    {
        if (_tenantProvider.IsSuperAdmin)
        {
            var dto = await ProjectUserForSuperAdminAsync(id, cancellationToken);
            return dto is null ? NotFound() : Ok(dto);
        }

        if (_tenantProvider.TenantId is not int companyId || companyId <= 0)
        {
            return Forbid();
        }

        var membershipDto = await ProjectMembershipAsync(id, companyId, cancellationToken);
        return membershipDto is null ? NotFound() : Ok(membershipDto);
    }

    [HttpPost]
    [RequirePermission("users.manage")]
    [ProducesResponseType(typeof(TenantUserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TenantUserDto>> Create(
        [FromBody] CreateTenantUserRequest request,
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

        var companyOk = await _db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == companyId && t.IsActive, cancellationToken);
        if (!companyOk)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["company_id"] = ["Company does not exist or is inactive."],
                },
            });
        }

        var userName = (request.UserName ?? string.Empty).Trim();
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(userName) || userName.Length > 50)
        {
            errors["userName"] = ["User Name is mandatory (max 50)."];
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            errors["password"] = ["Password must be at least 8 characters."];
        }

        var roles = await ResolveAssignableRolesAsync(request.RoleIds, companyId, cancellationToken);
        if (roles.Count == 0)
        {
            errors["roleIds"] = ["Select at least one valid role for this company."];
        }
        else if (roles.Count != request.RoleIds.Distinct().Count())
        {
            errors["roleIds"] = ["One or more selected roles are invalid for this company."];
        }

        if (errors.Count > 0)
        {
            return UnprocessableEntity(new { errors });
        }

        var primaryRole = roles[0];

        var existing = await _db.Users.FirstOrDefaultAsync(
            u => u.UserName == userName,
            cancellationToken);

        User user;
        if (existing is null)
        {
            user = new User
            {
                UserName = userName,
                Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                PasswordHash = request.Password,
                UserNn = await AllocateUserNnAsync(cancellationToken),
                IsActive = true,
                IsSuperAdmin = false,
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            user = existing;
            if (user.IsSuperAdmin)
            {
                return UnprocessableEntity(new
                {
                    errors = new Dictionary<string, string[]>
                    {
                        ["userName"] = ["Super Admin accounts cannot be added as tenant users."],
                    },
                });
            }

            // Soft-deleted / inactive rows reuse: reactivate + refresh credentials.
            user.IsActive = true;
            user.PasswordHash = request.Password;
            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                user.Email = request.Email.Trim();
            }

            var existingMembership = await _db.UserTenantMemberships.FirstOrDefaultAsync(
                m => m.UserId == user.Id && m.CompanyId == companyId,
                cancellationToken);
            if (existingMembership is not null)
            {
                if (existingMembership.IsActive)
                {
                    return UnprocessableEntity(new
                    {
                        errors = new Dictionary<string, string[]>
                        {
                            ["userName"] = ["User already has a membership in this company."],
                        },
                    });
                }

                existingMembership.IsActive = true;
                existingMembership.RoleId = primaryRole.Id;
                await SyncCompanyUserRolesAsync(
                    user.Id,
                    companyId,
                    roles.Select(r => r.Id).ToList(),
                    cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);

                var reactivated = await ProjectMembershipAsync(user.Id, companyId, cancellationToken);
                return CreatedAtAction(nameof(GetById), new { id = user.Id }, reactivated);
            }
        }

        var membership = new UserTenantMembership
        {
            UserId = user.Id,
            CompanyId = companyId,
            RoleId = primaryRole.Id,
            IsActive = true,
        };
        _db.UserTenantMemberships.Add(membership);
        await SyncCompanyUserRolesAsync(
            user.Id,
            companyId,
            roles.Select(r => r.Id).ToList(),
            cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = await ProjectMembershipAsync(user.Id, companyId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, dto);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("users.manage")]
    [ProducesResponseType(typeof(TenantUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantUserDto>> Update(
        int id,
        [FromBody] UpdateTenantUserRequest request,
        CancellationToken cancellationToken)
    {
        // Super Admin may update profile without JWT company; membership edits need company_id.
        if (_tenantProvider.IsSuperAdmin
            && (request.CompanyId is null || request.CompanyId <= 0))
        {
            return await UpdateUserProfileOnlyAsync(id, request, cancellationToken);
        }

        if (!TryResolveMembershipCompanyId(request.CompanyId, out var companyId))
        {
            return Forbid();
        }

        var membership = await _db.UserTenantMemberships
            .Include(m => m.User)
            .FirstOrDefaultAsync(
                m => m.UserId == id && m.CompanyId == companyId,
                cancellationToken);

        if (membership?.User is null)
        {
            return NotFound();
        }

        if (membership.User.IsSuperAdmin)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["userId"] = ["Super Admin accounts cannot be edited here."],
                },
            });
        }

        var roleIds = request.RoleIds ?? [];
        var roles = await ResolveAssignableRolesAsync(roleIds, companyId, cancellationToken);
        if (roles.Count == 0 || roles.Count != roleIds.Distinct().Count())
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["roleIds"] = ["Select at least one valid role for this company."],
                },
            });
        }

        if (!string.IsNullOrWhiteSpace(request.Password) && request.Password.Length < 8)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["password"] = ["Password must be at least 8 characters."],
                },
            });
        }

        membership.User.Email = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : request.Email.Trim();
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            membership.User.PasswordHash = request.Password;
        }

        membership.RoleId = roles[0].Id;
        membership.IsActive = request.MembershipIsActive;
        await SyncCompanyUserRolesAsync(
            id,
            companyId,
            roles.Select(r => r.Id).ToList(),
            cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = _tenantProvider.IsSuperAdmin
            ? await ProjectUserForSuperAdminAsync(id, cancellationToken)
            : await ProjectMembershipAsync(id, companyId, cancellationToken);
        return Ok(dto);
    }

    /// <summary>Super Admin: add another company membership (T1.56).</summary>
    [HttpPost("{id:int}/memberships")]
    [RequirePermission("users.manage")]
    [ProducesResponseType(typeof(TenantUserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TenantUserDto>> AddMembership(
        int id,
        [FromBody] AddUserMembershipRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantProvider.IsSuperAdmin)
        {
            return Forbid();
        }

        var companyId = request.CompanyId;
        if (companyId <= 0)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["company_id"] = ["Select a company."],
                },
            });
        }

        var companyOk = await _db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == companyId && t.IsActive, cancellationToken);
        if (!companyOk)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["company_id"] = ["Company does not exist or is inactive."],
                },
            });
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        if (user.IsSuperAdmin)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["userId"] = ["Platform Super Admin cannot receive tenant memberships."],
                },
            });
        }

        var roles = await ResolveAssignableRolesAsync(request.RoleIds, companyId, cancellationToken);
        if (roles.Count == 0 || roles.Count != request.RoleIds.Distinct().Count())
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["roleIds"] = ["Select at least one valid role for this company."],
                },
            });
        }

        var existingMembership = await _db.UserTenantMemberships.FirstOrDefaultAsync(
            m => m.UserId == id && m.CompanyId == companyId,
            cancellationToken);

        if (existingMembership is not null)
        {
            if (existingMembership.IsActive)
            {
                return UnprocessableEntity(new
                {
                    errors = new Dictionary<string, string[]>
                    {
                        ["company_id"] = ["User already has an active membership in this company."],
                    },
                });
            }

            existingMembership.IsActive = true;
            existingMembership.RoleId = roles[0].Id;
        }
        else
        {
            _db.UserTenantMemberships.Add(new UserTenantMembership
            {
                UserId = id,
                CompanyId = companyId,
                RoleId = roles[0].Id,
                IsActive = true,
            });
        }

        user.IsActive = true;
        await SyncCompanyUserRolesAsync(
            id,
            companyId,
            roles.Select(r => r.Id).ToList(),
            cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = await ProjectUserForSuperAdminAsync(id, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, dto);
    }

    /// <summary>Update roles / active for one company membership (SA any company; TA JWT only).</summary>
    [HttpPut("{id:int}/memberships/{companyId:int}")]
    [RequirePermission("users.manage")]
    [ProducesResponseType(typeof(TenantUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantUserDto>> UpdateMembership(
        int id,
        int companyId,
        [FromBody] UpdateUserMembershipRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanManageMembershipCompany(companyId))
        {
            return Forbid();
        }

        var membership = await _db.UserTenantMemberships
            .Include(m => m.User)
            .FirstOrDefaultAsync(
                m => m.UserId == id && m.CompanyId == companyId,
                cancellationToken);

        if (membership?.User is null)
        {
            return NotFound();
        }

        if (membership.User.IsSuperAdmin)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["userId"] = ["Super Admin accounts cannot be edited here."],
                },
            });
        }

        var roles = await ResolveAssignableRolesAsync(request.RoleIds, companyId, cancellationToken);
        if (roles.Count == 0 || roles.Count != request.RoleIds.Distinct().Count())
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["roleIds"] = ["Select at least one valid role for this company."],
                },
            });
        }

        membership.RoleId = roles[0].Id;
        membership.IsActive = request.MembershipIsActive;
        await SyncCompanyUserRolesAsync(
            id,
            companyId,
            roles.Select(r => r.Id).ToList(),
            cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = _tenantProvider.IsSuperAdmin
            ? await ProjectUserForSuperAdminAsync(id, cancellationToken)
            : await ProjectMembershipAsync(id, companyId, cancellationToken);
        return Ok(dto!);
    }

    /// <summary>Soft-deactivate one company membership (path companyId — T1.56).</summary>
    [HttpDelete("{id:int}/memberships/{companyId:int}")]
    [RequirePermission("users.manage")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateMembershipByCompany(
        int id,
        int companyId,
        CancellationToken cancellationToken)
    {
        if (!CanManageMembershipCompany(companyId))
        {
            return Forbid();
        }

        return await SoftDeactivateMembershipAsync(id, companyId, cancellationToken);
    }

    /// <summary>Soft-deactivate membership in the JWT company (Users module delete — Tenant Admin).</summary>
    [HttpDelete("{id:int}/membership")]
    [RequirePermission("users.manage")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateMembership(int id, CancellationToken cancellationToken)
    {
        if (_tenantProvider.TenantId is not int companyId || companyId <= 0)
        {
            return Forbid();
        }

        return await SoftDeactivateMembershipAsync(id, companyId, cancellationToken);
    }

    private async Task<ActionResult<TenantUserDto>> UpdateUserProfileOnlyAsync(
        int id,
        UpdateTenantUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        if (user.IsSuperAdmin)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["userId"] = ["Super Admin accounts cannot be edited here."],
                },
            });
        }

        if (!string.IsNullOrWhiteSpace(request.Password) && request.Password.Length < 8)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["password"] = ["Password must be at least 8 characters."],
                },
            });
        }

        user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = request.Password;
        }

        await _db.SaveChangesAsync(cancellationToken);
        var dto = await ProjectUserForSuperAdminAsync(id, cancellationToken);
        return Ok(dto);
    }

    private async Task<IActionResult> SoftDeactivateMembershipAsync(
        int userId,
        int companyId,
        CancellationToken cancellationToken)
    {
        var membership = await _db.UserTenantMemberships
            .FirstOrDefaultAsync(
                m => m.UserId == userId && m.CompanyId == companyId,
                cancellationToken);

        if (membership is null)
        {
            return NotFound();
        }

        if (!membership.IsActive)
        {
            return NoContent();
        }

        var otherActive = await _db.UserTenantMemberships.AsNoTracking()
            .AnyAsync(
                m => m.UserId == userId
                     && m.CompanyId != null
                     && m.CompanyId != companyId
                     && m.IsActive,
                cancellationToken);

        // Super Admin: keep at least one active company so login still works (T1.56).
        // Tenant Admin may soft-remove the user from their only company.
        if (_tenantProvider.IsSuperAdmin && !otherActive)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["company_id"] =
                    [
                        "Cannot deactivate the last active company membership. Add another company first, or keep this one active.",
                    ],
                },
            });
        }

        membership.IsActive = false;

        var companyRoleRows = await _db.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userId && ur.IsActive
                         && ur.Role!.CompanyId == companyId)
            .ToListAsync(cancellationToken);
        var now = AuditClock.Now;
        foreach (var row in companyRoleRows)
        {
            row.IsActive = false;
            row.UpdateDate = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>SA: any company. TA: JWT company only.</summary>
    private bool CanManageMembershipCompany(int companyId)
    {
        if (companyId <= 0)
        {
            return false;
        }

        if (_tenantProvider.IsSuperAdmin)
        {
            return true;
        }

        return _tenantProvider.TenantId == companyId;
    }

    /// <summary>Resolve membership company for Update: SA uses body company_id; TA uses JWT.</summary>
    private bool TryResolveMembershipCompanyId(int? requestedCompanyId, out int companyId)
    {
        companyId = 0;
        if (_tenantProvider.IsSuperAdmin)
        {
            if (requestedCompanyId is int rid && rid > 0)
            {
                companyId = rid;
                return true;
            }

            return false;
        }

        if (_tenantProvider.TenantId is int tenantCompany && tenantCompany > 0)
        {
            companyId = tenantCompany;
            return true;
        }

        return false;
    }

    private async Task<IReadOnlyList<Role>> ResolveAssignableRolesAsync(
        int[]? roleIds,
        int companyId,
        CancellationToken cancellationToken)
    {
        if (roleIds is null || roleIds.Length == 0)
        {
            return [];
        }

        var distinct = roleIds.Where(id => id > 0).Distinct().ToList();
        if (distinct.Count == 0)
        {
            return [];
        }

        // Inline platform TENANT_ADMIN check — EF cannot translate IsPlatformTenantSystemRole.
        var found = await _db.Roles.AsNoTracking()
            .Where(r => distinct.Contains(r.Id)
                        && r.IsActive
                        && r.RoleCode != SeedConstants.RoleSuperAdmin
                        && (r.CompanyId == companyId
                            || (r.CompanyId == null
                                && r.RoleCode == SeedConstants.RoleTenantAdmin)))
            .ToListAsync(cancellationToken);

        // Preserve request order (primary = first).
        return distinct
            .Select(id => found.FirstOrDefault(r => r.Id == id))
            .Where(r => r is not null)
            .Cast<Role>()
            .ToList();
    }

    /// <summary>
    /// Create target company resolution (T1.60 / BRD §2 exception).
    /// Super Admin: body company_id = provisioning target (not isolation).
    /// Tenant Admin: JWT company always wins; client company_id ignored.
    /// Isolation for reads/writes remains JWT tenant_id → ITenantProvider.
    /// </summary>
    private bool TryResolveCreateCompanyId(
        int? requestedCompanyId,
        out int companyId,
        out string error)
    {
        companyId = 0;
        error = "Tenant context is required to create a user.";

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

            error = "Select a company to create the user.";
            return false;
        }

        if (_tenantProvider.TenantId is int tenantCompany && tenantCompany > 0)
        {
            companyId = tenantCompany;
            return true;
        }

        return false;
    }

    private async Task SyncCompanyUserRolesAsync(
        int userId,
        int companyId,
        IReadOnlyList<int> roleIds,
        CancellationToken cancellationToken)
    {
        var desired = roleIds.Distinct().ToHashSet();
        var now = AuditClock.Now;

        // Shared platform TENANT_ADMIN is one UserRole for all companies — never deactivate
        // it when syncing another company's role set (T1.56 multi-membership).
        var tenantAdminId = await _db.Roles.AsNoTracking()
            .Where(r => r.CompanyId == null && r.RoleCode == SeedConstants.RoleTenantAdmin)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (tenantAdminId > 0 && desired.Contains(tenantAdminId))
        {
            var taRow = await _db.UserRoles
                .FirstOrDefaultAsync(
                    ur => ur.UserId == userId && ur.RoleId == tenantAdminId,
                    cancellationToken);
            if (taRow is null)
            {
                _db.UserRoles.Add(new UserRole
                {
                    UserId = userId,
                    RoleId = tenantAdminId,
                    IsActive = true,
                    InsertDate = now,
                });
            }
            else if (!taRow.IsActive)
            {
                taRow.IsActive = true;
                taRow.UpdateDate = now;
            }

            desired.Remove(tenantAdminId);
        }

        var existing = await _db.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userId && ur.Role!.CompanyId == companyId)
            .ToListAsync(cancellationToken);

        foreach (var row in existing)
        {
            if (desired.Contains(row.RoleId))
            {
                if (!row.IsActive)
                {
                    row.IsActive = true;
                    row.UpdateDate = now;
                }

                desired.Remove(row.RoleId);
            }
            else if (row.IsActive)
            {
                row.IsActive = false;
                row.UpdateDate = now;
            }
        }

        foreach (var roleId in desired)
        {
            _db.UserRoles.Add(new UserRole
            {
                UserId = userId,
                RoleId = roleId,
                IsActive = true,
                InsertDate = now,
            });
        }
    }

    /// <summary>
    /// Super Admin: one row per user with primary membership + all company memberships.
    /// </summary>
    private async Task<List<TenantUserDto>> ListAllUsersAsync(
        bool includeInactive,
        string? userName,
        string? email,
        string? roleName,
        string? companyName,
        CancellationToken cancellationToken)
    {
        var membershipQuery = _db.UserTenantMemberships.AsNoTracking()
            .Where(m => m.CompanyId != null)
            .Where(m => includeInactive || (m.IsActive && m.User!.IsActive))
            .WhereContains(userName, m => m.User!.UserName)
            .WhereContains(email, m => m.User!.Email)
            .WhereContains(roleName, m => m.Role!.Name)
            .WhereContains(companyName, m => m.Company!.Name);

        var memberships = await membershipQuery
            .OrderBy(m => m.User!.UserName)
            .ThenBy(m => m.Company!.Name)
            .Select(m => new TenantUserDto
            {
                Id = m.User!.Id,
                UserName = m.User.UserName,
                Email = m.User.Email,
                IsActive = m.User.IsActive,
                MembershipId = m.Id,
                RoleId = m.RoleId,
                RoleCode = m.Role!.RoleCode,
                RoleName = m.Role.Name,
                MembershipIsActive = m.IsActive,
                CompanyId = m.CompanyId!.Value,
                CompanyName = m.Company!.Name,
                TenantCode = m.Company.TenantCode,
            })
            .ToListAsync(cancellationToken);

        // One list row per user; Companies[] carries every membership.
        var users = memberships
            .GroupBy(u => u.Id)
            .Select(g => g.First())
            .OrderBy(u => u.UserName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Platform Super Admin (CompanyId NULL membership only) — still list the user
        // unless companyName filter is set (platform row has no company).
        var companyPattern = QueryFilterExtensions.NormalizeContainsTerm(companyName);
        if (companyPattern is null)
        {
            var listedIds = users.Select(u => u.Id).ToHashSet();
            var platformUsers = await _db.Users.AsNoTracking()
                .Where(u => u.IsSuperAdmin)
                .Where(u => includeInactive || u.IsActive)
                .Where(u => !listedIds.Contains(u.Id))
                .WhereContains(userName, u => u.UserName)
                .WhereContains(email, u => u.Email)
                .OrderBy(u => u.UserName)
                .Select(u => new TenantUserDto
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    Email = u.Email,
                    IsActive = u.IsActive,
                    MembershipId = 0,
                    RoleId = 0,
                    RoleCode = SeedConstants.RoleSuperAdmin,
                    RoleName = "Super Admin",
                    MembershipIsActive = u.IsActive,
                    CompanyId = 0,
                    CompanyName = string.Empty,
                    TenantCode = null,
                })
                .ToListAsync(cancellationToken);

            var rolePattern = QueryFilterExtensions.NormalizeContainsTerm(roleName);
            if (rolePattern is not null)
            {
                platformUsers = platformUsers
                    .Where(u => u.RoleName.ToLowerInvariant().Contains(rolePattern))
                    .ToList();
            }

            users.AddRange(platformUsers);
            users = users
                .OrderBy(u => u.UserName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        await AttachCompanyRolesAsync(users, companyId: null, cancellationToken);
        await AttachUserCompaniesAsync(users, cancellationToken);
        return users;
    }

    private async Task<TenantUserDto?> ProjectUserForSuperAdminAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var companyId = await _db.UserTenantMemberships.AsNoTracking()
            .Where(m => m.UserId == userId && m.CompanyId != null)
            .OrderBy(m => m.Company!.Name)
            .Select(m => m.CompanyId!.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (companyId > 0)
        {
            return await ProjectMembershipAsync(userId, companyId, cancellationToken);
        }

        var platform = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.IsSuperAdmin)
            .Select(u => new TenantUserDto
            {
                Id = u.Id,
                UserName = u.UserName,
                Email = u.Email,
                IsActive = u.IsActive,
                MembershipId = 0,
                RoleId = 0,
                RoleCode = SeedConstants.RoleSuperAdmin,
                RoleName = "Super Admin",
                MembershipIsActive = u.IsActive,
                CompanyId = 0,
                CompanyName = string.Empty,
                TenantCode = null,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (platform is null)
        {
            return null;
        }

        await AttachCompanyRolesAsync([platform], companyId: null, cancellationToken);
        await AttachUserCompaniesAsync([platform], cancellationToken);
        return platform;
    }

    private async Task AttachCompanyRolesAsync(
        List<TenantUserDto> users,
        int? companyId,
        CancellationToken cancellationToken)
    {
        if (users.Count == 0)
        {
            return;
        }

        var userIds = users.Select(u => u.Id).ToList();
        var roleQuery =
            from ur in _db.UserRoles.AsNoTracking()
            join r in _db.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where userIds.Contains(ur.UserId)
                  && ur.IsActive
                  && r.IsActive
            select new { ur.UserId, Role = r };

        if (companyId is int scopedCompanyId)
        {
            roleQuery = roleQuery.Where(x =>
                x.Role.CompanyId == scopedCompanyId
                || (x.Role.CompanyId == null
                    && x.Role.RoleCode == SeedConstants.RoleTenantAdmin));
        }

        var roleRows = await roleQuery
            .OrderBy(x => x.Role.Name)
            .Select(x => new
            {
                x.UserId,
                Ref = new UserRoleRefDto
                {
                    RoleId = x.Role.Id,
                    RoleCode = x.Role.RoleCode,
                    RoleName = x.Role.Name,
                },
            })
            .ToListAsync(cancellationToken);

        var byUser = roleRows
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<UserRoleRefDto>)g.Select(x => x.Ref).ToList());

        foreach (var user in users)
        {
            if (byUser.TryGetValue(user.Id, out var roles) && roles.Count > 0)
            {
                // Drop shared platform TENANT_ADMIN unless this company's membership primary is TA.
                roles = roles
                    .Where(r =>
                        !string.Equals(r.RoleCode, SeedConstants.RoleTenantAdmin, StringComparison.OrdinalIgnoreCase)
                        || r.RoleId == user.RoleId)
                    .ToList();

                user.Roles = roles;
                // Prefer primary membership role when still assigned; else first UserRole.
                var primary = roles.FirstOrDefault(r => r.RoleId == user.RoleId) ?? roles[0];
                user.RoleId = primary.RoleId;
                user.RoleCode = primary.RoleCode;
                user.RoleName = primary.RoleName;
            }
            else
            {
                user.Roles =
                [
                    new UserRoleRefDto
                    {
                        RoleId = user.RoleId,
                        RoleCode = user.RoleCode,
                        RoleName = user.RoleName,
                    },
                ];
            }
        }
    }

    private async Task<TenantUserDto?> ProjectMembershipAsync(
        int userId,
        int companyId,
        CancellationToken cancellationToken)
    {
        var dto = await _db.UserTenantMemberships.AsNoTracking()
            .Where(m => m.UserId == userId && m.CompanyId == companyId)
            .Select(m => new TenantUserDto
            {
                Id = m.User!.Id,
                UserName = m.User.UserName,
                Email = m.User.Email,
                IsActive = m.User.IsActive,
                MembershipId = m.Id,
                RoleId = m.RoleId,
                RoleCode = m.Role!.RoleCode,
                RoleName = m.Role.Name,
                MembershipIsActive = m.IsActive,
                CompanyId = companyId,
                CompanyName = m.Company!.Name,
                TenantCode = m.Company.TenantCode,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (dto is null)
        {
            return null;
        }

        await AttachCompanyRolesAsync([dto], companyId, cancellationToken);
        await AttachUserCompaniesAsync([dto], cancellationToken);
        return dto;
    }

    /// <summary>Loads every company membership for the listed users (multi-company visibility).</summary>
    private async Task AttachUserCompaniesAsync(
        List<TenantUserDto> users,
        CancellationToken cancellationToken)
    {
        if (users.Count == 0)
        {
            return;
        }

        var userIds = users.Select(u => u.Id).ToList();
        var rows = await _db.UserTenantMemberships.AsNoTracking()
            .Where(m => userIds.Contains(m.UserId) && m.CompanyId != null)
            .OrderBy(m => m.Company!.Name)
            .Select(m => new
            {
                m.UserId,
                Ref = new UserCompanyRefDto
                {
                    CompanyId = m.CompanyId!.Value,
                    CompanyName = m.Company!.Name,
                    TenantCode = m.Company.TenantCode,
                    MembershipIsActive = m.IsActive,
                },
            })
            .ToListAsync(cancellationToken);

        var roleRows = await (
            from ur in _db.UserRoles.AsNoTracking()
            join r in _db.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where userIds.Contains(ur.UserId) && ur.IsActive && r.IsActive
            select new
            {
                ur.UserId,
                r.CompanyId,
                r.RoleCode,
                Ref = new UserRoleRefDto
                {
                    RoleId = r.Id,
                    RoleCode = r.RoleCode,
                    RoleName = r.Name,
                },
            }).ToListAsync(cancellationToken);

        var membershipRoleByUserCompany = await _db.UserTenantMemberships.AsNoTracking()
            .Where(m => userIds.Contains(m.UserId) && m.CompanyId != null && m.IsActive)
            .Select(m => new { m.UserId, CompanyId = m.CompanyId!.Value, m.RoleId })
            .ToListAsync(cancellationToken);
        var membershipRoleMap = membershipRoleByUserCompany
            .ToDictionary(x => (x.UserId, x.CompanyId), x => x.RoleId);

        var byUser = rows
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var list = g.Select(x =>
                    {
                        var companyId = x.Ref.CompanyId;
                        membershipRoleMap.TryGetValue((g.Key, companyId), out var membershipRoleId);
                        var rolesForCompany = roleRows
                            .Where(rr => rr.UserId == g.Key
                                         && (rr.CompanyId == companyId
                                             || (rr.CompanyId == null
                                                 && rr.RoleCode == SeedConstants.RoleTenantAdmin
                                                 && rr.Ref.RoleId == membershipRoleId)))
                            .Select(rr => rr.Ref)
                            .GroupBy(rr => rr.RoleId)
                            .Select(gg => gg.First())
                            .OrderBy(rr => rr.RoleName)
                            .ToList();
                        x.Ref.Roles = rolesForCompany;
                        return x.Ref;
                    }).ToList();
                    return (IReadOnlyList<UserCompanyRefDto>)list;
                });

        foreach (var user in users)
        {
            if (byUser.TryGetValue(user.Id, out var companies) && companies.Count > 0)
            {
                user.Companies = companies;
                if (user.CompanyId <= 0)
                {
                    var first = companies[0];
                    user.CompanyId = first.CompanyId;
                    user.CompanyName = first.CompanyName;
                    user.TenantCode = first.TenantCode;
                    user.MembershipIsActive = first.MembershipIsActive;
                }
            }
            else if (user.CompanyId > 0)
            {
                user.Companies =
                [
                    new UserCompanyRefDto
                    {
                        CompanyId = user.CompanyId,
                        CompanyName = user.CompanyName,
                        TenantCode = user.TenantCode,
                        MembershipIsActive = user.MembershipIsActive,
                        Roles = user.Roles,
                    },
                ];
            }
            else
            {
                user.Companies = [];
            }
        }
    }

    private async Task<string> AllocateUserNnAsync(CancellationToken cancellationToken)
    {
        var existing = await _db.Users.AsNoTracking()
            .Select(u => u.UserNn)
            .ToListAsync(cancellationToken);

        var used = existing
            .Where(nn => int.TryParse(nn, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            .Select(nn => int.Parse(nn, CultureInfo.InvariantCulture))
            .ToHashSet();

        for (var i = 2; i <= 99; i++)
        {
            if (!used.Contains(i))
            {
                return i.ToString("00", CultureInfo.InvariantCulture);
            }
        }

        throw new InvalidOperationException("UserNn space exhausted (00-99).");
    }
}
