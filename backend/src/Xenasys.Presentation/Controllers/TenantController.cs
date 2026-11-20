using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions;
using Xenasys.Core.Contracts.Tenants;
using Xenasys.Infrastructure.Security;
using Xenasys.Persistence.Data;

namespace Xenasys.Presentation.Controllers;

// ==========================================================================================
// WHAT: Current-tenant company profile API (Settings → Company).
// WHY: BRD — Tenant Admin read/edit own company; isolation from JWT tenant_id only.
// EXAMPLE: GET/PUT /api/tenant/me with company.read / company.update.
// ==========================================================================================
[ApiController]
[Route("api/tenant")]
[Authorize]
public sealed class TenantController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public TenantController(ApplicationDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    [HttpGet("me")]
    [RequirePermission("company.read")]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantDto>> GetCurrentTenant(CancellationToken cancellationToken)
    {
        if (_tenantProvider.TenantId is not int tenantId || tenantId <= 0)
        {
            return NotFound();
        }

        var tenant = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new TenantDto
            {
                Id = t.Id,
                Name = t.Name,
                TenantCode = t.TenantCode,
                IsActive = t.IsActive,
                ContactEmail = t.ContactEmail,
                ContactPhone = t.ContactPhone,
                Address = t.Address,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return tenant is null ? NotFound() : Ok(tenant);
    }

    [HttpPut("me")]
    [RequirePermission("company.update")]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TenantDto>> UpdateCurrentTenant(
        [FromBody] UpdateTenantProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (_tenantProvider.TenantId is not int tenantId || tenantId <= 0)
        {
            return NotFound();
        }

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return NotFound();
        }

        if (!tenant.IsActive)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["root"] = ["This company is inactive and cannot be edited."],
                },
            });
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
        tenant.ContactEmail = string.IsNullOrWhiteSpace(request.ContactEmail)
            ? null
            : request.ContactEmail.Trim();
        tenant.ContactPhone = string.IsNullOrWhiteSpace(request.ContactPhone)
            ? null
            : request.ContactPhone.Trim();
        tenant.Address = string.IsNullOrWhiteSpace(request.Address)
            ? null
            : request.Address.Trim();

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new TenantDto
        {
            Id = tenant.Id,
            Name = tenant.Name,
            TenantCode = tenant.TenantCode,
            IsActive = tenant.IsActive,
            ContactEmail = tenant.ContactEmail,
            ContactPhone = tenant.ContactPhone,
            Address = tenant.Address,
        });
    }
}
