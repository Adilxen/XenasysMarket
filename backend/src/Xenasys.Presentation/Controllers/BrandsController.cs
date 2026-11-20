using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions;
using Xenasys.Core.Abstractions.Pagination;
using Xenasys.Core.Contracts.Catalog;
using Xenasys.Core.Entities;
using Xenasys.Core.Querying;
using Xenasys.Infrastructure.Security;
using Xenasys.Persistence.Data;
using Xenasys.Persistence.Querying;

namespace Xenasys.Presentation.Controllers;

// ==========================================================================================
// WHAT: Tenant Brand CRUD — keyset list; unique NameNormalized; soft-delete.
// WHY: BRD §4.4 — brand master for Product (M3) without image/code requirements.
// EXAMPLE: POST /api/brands { name, description } → BrandDto.
// ==========================================================================================
[ApiController]
[Route("api/brands")]
[Authorize]
public sealed class BrandsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public BrandsController(ApplicationDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [RequirePermission("brands.read")]
    [ProducesResponseType(typeof(KeysetPageResult<BrandDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<KeysetPageResult<BrandDto>>> List(
        [FromQuery] int? afterId,
        [FromQuery] int limit = 50,
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? shape = null,
        [FromQuery] string? name = null,
        [FromQuery] string? companyName = null,
        [FromQuery] int? companyId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new KeysetPageRequest { AfterId = afterId, Limit = limit };
        var take = request.GetSafeLimit();
        var dataShape = DataShapeParser.Parse(shape, DataShape.List);

        var query = _db.Brands
            .AsNoTracking()
            .WhereActive(includeInactive)
            .WhereContains(name, b => b.Name)
            .WhereContains(companyName, b => b.Company != null ? b.Company.Name : null);

        if (companyId is int scopedCompany and > 0)
        {
            query = query.Where(b => b.CompanyId == scopedCompany);
        }

        // Total before keyset cursor — footer Total must match filtered row count.
        var totalCount = await query.LongCountAsync(cancellationToken);

        if (request.AfterId is int cursor)
        {
            query = query.Where(b => b.Id > cursor);
        }

        var rows = await query
            .OrderBy(b => b.Id)
            .Take(take + 1)
            .Select(BrandProjections.ForShape(dataShape))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > take;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return Ok(new KeysetPageResult<BrandDto>
        {
            Items = rows,
            NextCursor = rows.Count > 0 ? rows[^1].Id : null,
            HasMore = hasMore,
            TotalCount = totalCount,
        });
    }

    [HttpGet("{id:int}")]
    [RequirePermission("brands.read")]
    [ProducesResponseType(typeof(BrandDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BrandDto>> GetById(
        int id,
        [FromQuery] string? shape = null,
        CancellationToken cancellationToken = default)
    {
        var dataShape = DataShapeParser.Parse(shape, DataShape.Detail);
        var row = await _db.Brands
            .AsNoTracking()
            .Where(b => b.Id == id)
            .Select(BrandProjections.ForShape(dataShape))
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    [RequirePermission("brands.create")]
    [ProducesResponseType(typeof(BrandDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BrandDto>> Create(
        [FromBody] CreateBrandRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveWriteCompanyId(request.CompanyId, out var companyId, out var errorResult))
        {
            return errorResult!;
        }

        var fieldErrors = ValidateFields(request.Name, request.Description);
        if (fieldErrors.Count > 0)
        {
            return UnprocessableEntity(new { errors = fieldErrors });
        }

        var trimmedName = request.Name.Trim();
        var normalized = NormalizeName(trimmedName);

        if (await NameTakenAsync(companyId, normalized, excludeId: null, cancellationToken))
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["name"] = ["A brand with this name already exists for this company."],
                },
            });
        }

        var entity = new Brand
        {
            CompanyId = companyId,
            Name = trimmedName,
            NameNormalized = normalized,
            Description = NullIfWhite(request.Description),
            IsActive = true,
        };

        _db.Brands.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = await ProjectAsync(entity.Id, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, dto);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("brands.update")]
    [ProducesResponseType(typeof(BrandDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BrandDto>> Update(
        int id,
        [FromBody] UpdateBrandRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Brands.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (!CanMutateCompany(entity.CompanyId))
        {
            return Forbid();
        }

        var fieldErrors = ValidateFields(request.Name, request.Description);
        if (fieldErrors.Count > 0)
        {
            return UnprocessableEntity(new { errors = fieldErrors });
        }

        var trimmedName = request.Name.Trim();
        var normalized = NormalizeName(trimmedName);

        if (await NameTakenAsync(entity.CompanyId, normalized, id, cancellationToken))
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["name"] = ["A brand with this name already exists for this company."],
                },
            });
        }

        entity.Name = trimmedName;
        entity.NameNormalized = normalized;
        entity.Description = NullIfWhite(request.Description);

        if (request.IsActive is bool active)
        {
            entity.IsActive = active;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await ProjectAsync(id, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("brands.delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.Brands.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (!CanMutateCompany(entity.CompanyId))
        {
            return Forbid();
        }

        entity.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<BrandDto> ProjectAsync(int id, CancellationToken cancellationToken) =>
        await _db.Brands
            .AsNoTracking()
            .Where(b => b.Id == id)
            .Select(BrandProjections.Detail)
            .FirstAsync(cancellationToken);

    private async Task<bool> NameTakenAsync(
        int companyId,
        string nameNormalized,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        var query = _db.Brands
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(b => b.CompanyId == companyId && b.NameNormalized == nameNormalized);

        if (excludeId is int skip)
        {
            query = query.Where(b => b.Id != skip);
        }

        return await query.AnyAsync(cancellationToken);
    }

    private bool TryResolveWriteCompanyId(
        int? requestedCompanyId,
        out int companyId,
        out ActionResult? errorResult)
    {
        companyId = 0;
        errorResult = null;

        if (_tenantProvider.IsSuperAdmin)
        {
            if (requestedCompanyId is int explicitId and > 0)
            {
                companyId = explicitId;
                return true;
            }

            if (_tenantProvider.TenantId is int jwtCompany and > 0)
            {
                companyId = jwtCompany;
                return true;
            }

            errorResult = UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["companyId"] = ["Select a company before creating a brand."],
                },
            });
            return false;
        }

        if (_tenantProvider.TenantId is int tid && tid > 0)
        {
            companyId = tid;
            return true;
        }

        errorResult = UnprocessableEntity(new
        {
            errors = new Dictionary<string, string[]>
            {
                ["companyId"] = ["Select a company context before creating a brand."],
            },
        });
        return false;
    }

    private bool CanMutateCompany(int companyId) =>
        _tenantProvider.IsSuperAdmin
        || (_tenantProvider.TenantId is int tid && tid == companyId);

    private static Dictionary<string, string[]> ValidateFields(string? name, string? description)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        var n = (name ?? string.Empty).Trim();
        if (n.Length == 0)
        {
            errors["name"] = ["Name is required."];
        }
        else if (n.Length > 100)
        {
            errors["name"] = ["Name must be at most 100 characters."];
        }

        var d = (description ?? string.Empty).Trim();
        if (d.Length > 500)
        {
            errors["description"] = ["Description must be at most 500 characters."];
        }

        return errors;
    }

    private static string NormalizeName(string name) => name.Trim().ToLowerInvariant();

    private static string? NullIfWhite(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
