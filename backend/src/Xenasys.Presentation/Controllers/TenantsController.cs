using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions;
using Xenasys.Core.Abstractions.Coding;
using Xenasys.Core.Contracts.Tenants;
using Xenasys.Core.Entities;
using Xenasys.Core.Querying;
using Xenasys.Core.Time;
using Xenasys.Infrastructure.Security;
using Xenasys.Persistence.Data;
using Xenasys.Persistence.Querying;
using Xenasys.Persistence.Seeding;

namespace Xenasys.Presentation.Controllers;

// ==========================================================================================
// WHAT: Tenants (companies) CRUD — active-only list by default + shape-based projections.
// WHY: 1CR-scale safety — never pull inactive rows or unused columns on hot lists.
// EXAMPLE: GET /api/tenants?shape=list → 4 columns, IsActive=1 only
// ==========================================================================================
[ApiController]
[Route("api/tenants")]
[Authorize]
public sealed class TenantsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantCodeAllocator _tenantCodeAllocator;

    public TenantsController(
        ApplicationDbContext db,
        ITenantProvider tenantProvider,
        ICurrentUser currentUser,
        ITenantCodeAllocator tenantCodeAllocator)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
        _tenantCodeAllocator = tenantCodeAllocator;
    }

    /// <summary>
    /// Lists companies. Defaults: includeInactive=false, shape=list (minimal columns).
    /// </summary>
    [HttpGet]
    [RequirePermission("company.read")]
    [ProducesResponseType(typeof(IReadOnlyList<TenantDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TenantDto>>> List(
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? shape = null,
        [FromQuery] string? name = null,
        [FromQuery] string? tenantCode = null,
        CancellationToken cancellationToken = default)
    {
        var dataShape = DataShapeParser.Parse(shape, DataShape.List);

        IQueryable<Tenant> query = _db.Tenants
            .AsNoTracking()
            .WhereActive(includeInactive)
            .WhereContains(name, t => t.Name)
            .WhereContains(tenantCode, t => t.TenantCode);

        if (!_tenantProvider.IsSuperAdmin)
        {
            if (_tenantProvider.TenantId is not int companyId || companyId <= 0)
            {
                return Forbid();
            }

            query = query.Where(t => t.Id == companyId);
        }

        var rows = await query
            .OrderBy(t => t.Name)
            .Select(TenantProjections.ForShape(dataShape))
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("company.read")]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantDto>> GetById(
        int id,
        [FromQuery] string? shape = null,
        CancellationToken cancellationToken = default)
    {
        if (!CanAccessTenant(id))
        {
            return Forbid();
        }

        // Detail by default for single-row fetch; still projected (no SELECT *).
        var dataShape = DataShapeParser.Parse(shape, DataShape.Detail);

        var tenant = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(TenantProjections.ForShape(dataShape))
            .FirstOrDefaultAsync(cancellationToken);

        return tenant is null ? NotFound() : Ok(tenant);
    }

    [HttpPost]
    [RequirePermission("company.create")]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TenantDto>> Create(
        [FromBody] CreateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantProvider.IsSuperAdmin)
        {
            return Forbid();
        }

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["name"] = ["Company name is required (max 200)."],
                },
            });
        }

        string code;
        try
        {
            code = await _tenantCodeAllocator.AllocateAsync(name, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["tenantCode"] = [ex.Message],
                },
            });
        }

        var tenant = new Tenant
        {
            Name = name,
            TenantCode = code,
            ContactEmail = NullIfWhite(request.ContactEmail),
            ContactPhone = NullIfWhite(request.ContactPhone),
            Address = NullIfWhite(request.Address),
            IsActive = true,
        };

        _db.Tenants.Add(tenant);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            code = await _tenantCodeAllocator.AllocateAsync(name, cancellationToken);
            tenant = new Tenant
            {
                Name = name,
                TenantCode = code,
                ContactEmail = NullIfWhite(request.ContactEmail),
                ContactPhone = NullIfWhite(request.ContactPhone),
                Address = NullIfWhite(request.Address),
                IsActive = true,
            };
            _db.Tenants.Add(tenant);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var actorId = _currentUser.UserId ?? 0;
        var stampActor = actorId > 0 ? actorId : 1;
        var now = AuditClock.Now;
        await TenantRoleBootstrap.EnsurePlatformTenantRolesAsync(
            _db,
            stampActor,
            now,
            mapFormActions: true,
            cancellationToken);

        await TenantCoaBootstrap.EnsureCompanyBaselineAsync(
            _db,
            tenant.Id,
            stampActor,
            now,
            cancellationToken: cancellationToken);

        var dto = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenant.Id)
            .Select(TenantProjections.Detail)
            .FirstAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, dto);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("company.update")]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TenantDto>> Update(
        int id,
        [FromBody] UpdateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!CanAccessTenant(id))
        {
            return Forbid();
        }

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (tenant is null)
        {
            return NotFound();
        }

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["name"] = ["Company name is required (max 200)."],
                },
            });
        }

        tenant.Name = name;
        tenant.ContactEmail = NullIfWhite(request.ContactEmail);
        tenant.ContactPhone = NullIfWhite(request.ContactPhone);
        tenant.Address = NullIfWhite(request.Address);

        if (_tenantProvider.IsSuperAdmin && request.IsActive is bool active)
        {
            tenant.IsActive = active;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var dto = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(TenantProjections.Detail)
            .FirstAsync(cancellationToken);

        return Ok(dto);
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("company.delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        if (!CanAccessTenant(id))
        {
            return Forbid();
        }

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (tenant is null)
        {
            return NotFound();
        }

        tenant.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private bool CanAccessTenant(int id)
    {
        if (_tenantProvider.IsSuperAdmin)
        {
            return true;
        }

        return _tenantProvider.TenantId is int companyId && companyId == id;
    }

    private static string? NullIfWhite(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
