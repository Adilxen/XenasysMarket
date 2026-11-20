using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions;
using Xenasys.Core.Abstractions.Auth;
using Xenasys.Core.Contracts.Search;
using Xenasys.Core.Entities;
using Xenasys.Infrastructure.Security;
using Xenasys.Persistence.Data;

namespace Xenasys.Presentation.Controllers;

// ==========================================================================================
// WHAT: Global Search Center API — permission-gated, TOP-N SQL hits (never full table dump).
// WHY: SPA stays fast under load; searches run SEQUENTIALLY on one DbContext (EF thread-safe).
// EXAMPLE: GET /api/search?q=Admin&limit=8 → users/roles/companies/products containing Admin
// ==========================================================================================
[ApiController]
[Route("api/search")]
[Authorize]
public sealed class SearchController : ControllerBase
{
    private const int MinQueryLength = 2;
    private const int DefaultLimit = 8;
    private const int MaxLimit = 15;

    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IPermissionGate _permissionGate;

    public SearchController(
        ApplicationDbContext db,
        ITenantProvider tenantProvider,
        IPermissionGate permissionGate)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _permissionGate = permissionGate;
    }

    [HttpGet]
    [ProducesResponseType(typeof(GlobalSearchResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<GlobalSearchResponseDto>> Search(
        [FromQuery] string? q,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var query = (q ?? string.Empty).Trim();
        if (query.Length < MinQueryLength)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["q"] = [$"Query must be at least {MinQueryLength} characters."],
                },
            });
        }

        if (query.Length > 80)
        {
            query = query[..80];
        }

        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var pattern = query.ToLowerInvariant();

        // One permission snapshot — avoids concurrent DbContext use inside HasAsync + queries.
        var codes = await _permissionGate.ListCodesAsync(cancellationToken);
        var canCompany = Has(codes, "company.read");
        var canUsers = Has(codes, "users.read");
        var canRoles = Has(codes, "roles.read");
        var canProducts = Has(codes, "products.read");

        var groups = new List<GlobalSearchGroupDto>(4);

        // IMPORTANT: sequential awaits — scoped DbContext is not safe for Task.WhenAll.
        if (canCompany)
        {
            AddIfAny(groups, await SearchCompaniesAsync(pattern, take, cancellationToken));
        }

        if (canUsers)
        {
            AddIfAny(groups, await SearchUsersAsync(pattern, take, cancellationToken));
        }

        if (canRoles)
        {
            AddIfAny(groups, await SearchRolesAsync(pattern, take, cancellationToken));
        }

        if (canProducts)
        {
            AddIfAny(groups, await SearchProductsAsync(pattern, take, cancellationToken));
        }

        return Ok(new GlobalSearchResponseDto
        {
            Query = query,
            Groups = groups,
        });
    }

    private static bool Has(IReadOnlyList<string> codes, string code) =>
        codes.Any(c => string.Equals(c, code, StringComparison.OrdinalIgnoreCase));

    private static void AddIfAny(List<GlobalSearchGroupDto> groups, GlobalSearchGroupDto? group)
    {
        if (group is { Hits.Count: > 0 })
        {
            groups.Add(group);
        }
    }

    private async Task<GlobalSearchGroupDto?> SearchCompaniesAsync(
        string pattern,
        int take,
        CancellationToken cancellationToken)
    {
        try
        {
            IQueryable<Tenant> query = _db.Tenants.AsNoTracking();

            if (!_tenantProvider.IsSuperAdmin)
            {
                if (_tenantProvider.TenantId is not int companyId || companyId <= 0)
                {
                    return null;
                }

                query = query.Where(t => t.Id == companyId);
            }

            var rows = await query
                .Where(t => t.IsActive)
                .Where(t =>
                    t.Name.ToLower().Contains(pattern)
                    || t.TenantCode.ToLower().Contains(pattern)
                    || (t.ContactEmail != null && t.ContactEmail.ToLower().Contains(pattern)))
                .OrderBy(t => t.Name)
                .Take(take)
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.TenantCode,
                })
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                return null;
            }

            return new GlobalSearchGroupDto
            {
                Key = "companies",
                Label = "Companies",
                Hits = rows.Select(t => new GlobalSearchHitDto
                {
                    Id = $"company:{t.Id}",
                    Kind = "company",
                    Label = t.Name,
                    Description = t.TenantCode,
                    Href = $"/settings/company?q={Uri.EscapeDataString(t.Name)}",
                }).ToList(),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task<GlobalSearchGroupDto?> SearchUsersAsync(
        string pattern,
        int take,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_tenantProvider.IsSuperAdmin
                && (_tenantProvider.TenantId is not int companyId || companyId <= 0))
            {
                return null;
            }

            IQueryable<UserTenantMembership> membershipQuery = _db.UserTenantMemberships.AsNoTracking()
                .Where(m => m.CompanyId != null)
                .Where(m => m.IsActive && m.User!.IsActive);

            if (!_tenantProvider.IsSuperAdmin
                && _tenantProvider.TenantId is int scopedCompanyId
                && scopedCompanyId > 0)
            {
                membershipQuery = membershipQuery.Where(m => m.CompanyId == scopedCompanyId);
            }

            var rows = await membershipQuery
                .Where(m =>
                    m.User!.UserName.ToLower().Contains(pattern)
                    || (m.User.Email != null && m.User.Email.ToLower().Contains(pattern))
                    || m.Role!.Name.ToLower().Contains(pattern)
                    || m.Role.RoleCode.ToLower().Contains(pattern))
                .OrderBy(m => m.User!.UserName)
                .Take(take)
                .Select(m => new
                {
                    MembershipId = m.Id,
                    UserName = m.User!.UserName,
                    Email = m.User.Email,
                    RoleName = m.Role!.Name,
                    CompanyName = m.Company!.Name,
                })
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                return null;
            }

            return new GlobalSearchGroupDto
            {
                Key = "users",
                Label = "Users",
                Hits = rows.Select(m => new GlobalSearchHitDto
                {
                    Id = $"user:{m.MembershipId}",
                    Kind = "user",
                    Label = m.UserName,
                    Description = string.IsNullOrEmpty(m.Email)
                        ? $"{m.RoleName} · {m.CompanyName}"
                        : $"{m.RoleName} · {m.CompanyName} · {m.Email}",
                    Href = $"/settings/users?q={Uri.EscapeDataString(m.UserName)}",
                }).ToList(),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task<GlobalSearchGroupDto?> SearchRolesAsync(
        string pattern,
        int take,
        CancellationToken cancellationToken)
    {
        try
        {
            IQueryable<Role> query = _db.Roles.AsNoTracking();

            if (_tenantProvider.IsSuperAdmin)
            {
                // Platform-wide roles.
            }
            else if (_tenantProvider.TenantId is int companyId && companyId > 0)
            {
                query = query.Where(r => r.CompanyId == companyId || r.CompanyId == null);
            }
            else
            {
                return null;
            }

            var rows = await query
                .Where(r => r.IsActive)
                .Where(r =>
                    r.Name.ToLower().Contains(pattern)
                    || r.RoleCode.ToLower().Contains(pattern)
                    || (r.Description != null && r.Description.ToLower().Contains(pattern)))
                .OrderBy(r => r.Name)
                .Take(take)
                .Select(r => new
                {
                    r.Id,
                    r.Name,
                    r.RoleCode,
                    IsSystem = r.RoleCode == SeedConstants.RoleSuperAdmin
                        || r.RoleCode == SeedConstants.RoleTenantAdmin
                        || r.RoleCode == SeedConstants.RoleTenantStaff,
                })
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                return null;
            }

            return new GlobalSearchGroupDto
            {
                Key = "roles",
                Label = "Roles",
                Hits = rows.Select(r => new GlobalSearchHitDto
                {
                    Id = $"role:{r.Id}",
                    Kind = "role",
                    Label = r.Name,
                    Description = $"{r.RoleCode}{(r.IsSystem ? " · System" : " · Custom")}",
                    Href = $"/settings/roles?q={Uri.EscapeDataString(r.RoleCode)}",
                }).ToList(),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task<GlobalSearchGroupDto?> SearchProductsAsync(
        string pattern,
        int take,
        CancellationToken cancellationToken)
    {
        try
        {
            var rows = await _db.Products.AsNoTracking()
                .Where(p => p.IsActive)
                .Where(p =>
                    p.ProductName.ToLower().Contains(pattern)
                    || p.ProductCode.ToLower().Contains(pattern)
                    || (p.Barcode != null && p.Barcode.ToLower().Contains(pattern)))
                .OrderBy(p => p.ProductName)
                .Take(take)
                .Select(p => new
                {
                    p.Id,
                    p.ProductName,
                    p.ProductCode,
                })
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                return null;
            }

            return new GlobalSearchGroupDto
            {
                Key = "products",
                Label = "Products",
                Hits = rows.Select(p => new GlobalSearchHitDto
                {
                    Id = $"product:{p.Id}",
                    Kind = "product",
                    Label = p.ProductName,
                    Description = p.ProductCode,
                    Href = $"/products?q={Uri.EscapeDataString(p.ProductName)}",
                }).ToList(),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }
}
