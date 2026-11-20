using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions;
using Xenasys.Core.Abstractions.Coding;
using Xenasys.Core.Abstractions.Pagination;
using Xenasys.Core.Contracts.Catalog;
using Xenasys.Core.Entities;
using Xenasys.Core.Querying;
using Xenasys.Infrastructure.Security;
using Xenasys.Persistence.Data;
using Xenasys.Persistence.Querying;
using Xenasys.Presentation.Storage;

namespace Xenasys.Presentation.Controllers;

// ==========================================================================================
// WHAT: Tenant Category CRUD — keyset list; mandatory image; auto CAT code.
// WHY: BRD §4.2 — unique NameNormalized; soft-delete cascades to SubCategories when safe.
// EXAMPLE: POST multipart /api/categories → Code=CAT01, ImagePath=/uploads/…/categories/….
// ==========================================================================================
[ApiController]
[Route("api/categories")]
[Authorize]
public sealed class CategoriesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICatalogCodeAllocator _codeAllocator;
    private readonly CatalogImageStorage _images;

    public CategoriesController(
        ApplicationDbContext db,
        ITenantProvider tenantProvider,
        ICatalogCodeAllocator codeAllocator,
        CatalogImageStorage images)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _codeAllocator = codeAllocator;
        _images = images;
    }

    [HttpGet]
    [RequirePermission("categories.read")]
    [ProducesResponseType(typeof(KeysetPageResult<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<KeysetPageResult<CategoryDto>>> List(
        [FromQuery] int? afterId,
        [FromQuery] int limit = 50,
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? shape = null,
        [FromQuery] string? name = null,
        [FromQuery] string? code = null,
        [FromQuery] string? companyName = null,
        [FromQuery] int? companyId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new KeysetPageRequest { AfterId = afterId, Limit = limit };
        var take = request.GetSafeLimit();
        var dataShape = DataShapeParser.Parse(shape, DataShape.List);

        var query = _db.Categories
            .AsNoTracking()
            .WhereActive(includeInactive)
            .WhereContains(name, c => c.Name)
            .WhereContains(code, c => c.Code)
            .WhereContains(companyName, c => c.Company != null ? c.Company.Name : null);

        if (companyId is int scopedCompany and > 0)
        {
            // SA dropdowns must scope to JWT / selected company — unscoped SA sees all tenants.
            query = query.Where(c => c.CompanyId == scopedCompany);
        }

        // Total before keyset cursor — footer Total must match filtered row count.
        var totalCount = await query.LongCountAsync(cancellationToken);

        if (request.AfterId is int cursor)
        {
            query = query.Where(c => c.Id > cursor);
        }

        var rows = await query
            .OrderBy(c => c.Id)
            .Take(take + 1)
            .Select(CategoryProjections.ForShape(dataShape))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > take;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return Ok(new KeysetPageResult<CategoryDto>
        {
            Items = rows,
            NextCursor = rows.Count > 0 ? rows[^1].Id : null,
            HasMore = hasMore,
            TotalCount = totalCount,
        });
    }

    [HttpGet("{id:int}")]
    [RequirePermission("categories.read")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> GetById(
        int id,
        [FromQuery] string? shape = null,
        CancellationToken cancellationToken = default)
    {
        var dataShape = DataShapeParser.Parse(shape, DataShape.Detail);
        var row = await _db.Categories
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(CategoryProjections.ForShape(dataShape))
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    [RequirePermission("categories.create")]
    [RequestSizeLimit(CatalogImageStorage.MaxBytes + 64_000)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CategoryDto>> Create(
        [FromForm] int? companyId,
        [FromForm] string name,
        [FromForm] string? description,
        IFormFile? image,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveWriteCompanyId(companyId, out var resolvedCompanyId, out var errorResult))
        {
            return errorResult!;
        }

        var fieldErrors = ValidateNameDescription(name, description);
        var imageError = CatalogImageStorage.Validate(image, required: true);
        if (imageError is not null)
        {
            fieldErrors["image"] = [imageError];
        }

        if (fieldErrors.Count > 0)
        {
            return UnprocessableEntity(new { errors = fieldErrors });
        }

        var trimmedName = name.Trim();
        var normalized = NormalizeName(trimmedName);

        if (await NameTakenAsync(resolvedCompanyId, normalized, excludeId: null, cancellationToken))
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["name"] = ["A category with this name already exists for this company."],
                },
            });
        }

        string code;
        try
        {
            code = await _codeAllocator.AllocateCategoryAsync(resolvedCompanyId, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]> { ["code"] = [ex.Message] },
            });
        }

        var imagePath = await _images.SaveAsync(image!, resolvedCompanyId, "categories", cancellationToken);

        var entity = new Category
        {
            CompanyId = resolvedCompanyId,
            Name = trimmedName,
            NameNormalized = normalized,
            Code = code,
            Description = NullIfWhite(description),
            ImagePath = imagePath,
            IsActive = true,
        };

        _db.Categories.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = await ProjectAsync(entity.Id, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, dto);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("categories.update")]
    [RequestSizeLimit(CatalogImageStorage.MaxBytes + 64_000)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CategoryDto>> Update(
        int id,
        [FromForm] string name,
        [FromForm] string? description,
        [FromForm] bool? isActive,
        IFormFile? image,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (!CanMutateCompany(entity.CompanyId))
        {
            return Forbid();
        }

        var fieldErrors = ValidateNameDescription(name, description);
        var imageError = CatalogImageStorage.Validate(image, required: false);
        if (imageError is not null)
        {
            fieldErrors["image"] = [imageError];
        }

        if (fieldErrors.Count > 0)
        {
            return UnprocessableEntity(new { errors = fieldErrors });
        }

        var trimmedName = name.Trim();
        var normalized = NormalizeName(trimmedName);

        if (await NameTakenAsync(entity.CompanyId, normalized, id, cancellationToken))
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["name"] = ["A category with this name already exists for this company."],
                },
            });
        }

        if (isActive is false && entity.IsActive)
        {
            var block = await TryBlockDeactivateAsync(entity.Id, cancellationToken);
            if (block is not null)
            {
                return block;
            }
        }

        entity.Name = trimmedName;
        entity.NameNormalized = normalized;
        entity.Description = NullIfWhite(description);

        if (image is { Length: > 0 })
        {
            entity.ImagePath = await _images.SaveAsync(
                image, entity.CompanyId, "categories", cancellationToken);
        }

        if (isActive is bool active)
        {
            entity.IsActive = active;
            if (!active)
            {
                await SoftDeactivateChildrenAsync(entity.Id, cancellationToken);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await ProjectAsync(id, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("categories.delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (!CanMutateCompany(entity.CompanyId))
        {
            return Forbid();
        }

        var block = await TryBlockDeactivateAsync(entity.Id, cancellationToken);
        if (block is not null)
        {
            return block;
        }

        entity.IsActive = false;
        await SoftDeactivateChildrenAsync(entity.Id, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<ActionResult?> TryBlockDeactivateAsync(int categoryId, CancellationToken ct)
    {
        var hasProducts = await _db.Products
            .AsNoTracking()
            .AnyAsync(p => p.CategoryId == categoryId && p.IsActive, ct);

        if (hasProducts)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["category"] =
                    [
                        "Cannot deactivate this category while active products are linked. Move or deactivate products first.",
                    ],
                },
            });
        }

        return null;
    }

    private async Task SoftDeactivateChildrenAsync(int categoryId, CancellationToken ct)
    {
        var children = await _db.SubCategories
            .Where(s => s.CategoryId == categoryId && s.IsActive)
            .ToListAsync(ct);

        foreach (var child in children)
        {
            child.IsActive = false;
        }
    }

    private async Task<CategoryDto> ProjectAsync(int id, CancellationToken cancellationToken) =>
        await _db.Categories
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(CategoryProjections.Detail)
            .FirstAsync(cancellationToken);

    private async Task<bool> NameTakenAsync(
        int companyId,
        string nameNormalized,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        var query = _db.Categories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.CompanyId == companyId && c.NameNormalized == nameNormalized);

        if (excludeId is int skip)
        {
            query = query.Where(c => c.Id != skip);
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
                    ["companyId"] = ["Select a company before creating a category."],
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
                ["companyId"] = ["Select a company context before creating a category."],
            },
        });
        return false;
    }

    private bool CanMutateCompany(int companyId) =>
        _tenantProvider.IsSuperAdmin
        || (_tenantProvider.TenantId is int tid && tid == companyId);

    private static Dictionary<string, string[]> ValidateNameDescription(string? name, string? description)
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
