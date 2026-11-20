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
// WHAT: Tenant SubCategory CRUD — filter by categoryId; optional image; auto SUB code.
// WHY: BRD §4.3 — unique NameNormalized within parent Category; same-company cascade.
// EXAMPLE: GET /api/sub-categories?categoryId=3 → cascading dropdown rows.
// ==========================================================================================
[ApiController]
[Route("api/sub-categories")]
[Authorize]
public sealed class SubCategoriesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICatalogCodeAllocator _codeAllocator;
    private readonly CatalogImageStorage _images;

    public SubCategoriesController(
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
    [ProducesResponseType(typeof(KeysetPageResult<SubCategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<KeysetPageResult<SubCategoryDto>>> List(
        [FromQuery] int? afterId,
        [FromQuery] int limit = 50,
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? shape = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] string? name = null,
        [FromQuery] string? code = null,
        [FromQuery] string? companyName = null,
        [FromQuery] int? companyId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new KeysetPageRequest { AfterId = afterId, Limit = limit };
        var take = request.GetSafeLimit();
        var dataShape = DataShapeParser.Parse(shape, DataShape.List);

        var query = _db.SubCategories
            .AsNoTracking()
            .WhereActive(includeInactive)
            .WhereContains(name, s => s.Name)
            .WhereContains(code, s => s.Code)
            .WhereContains(companyName, s => s.Company != null ? s.Company.Name : null);

        if (companyId is int scopedCompany and > 0)
        {
            query = query.Where(s => s.CompanyId == scopedCompany);
        }

        if (categoryId is int filterCat and > 0)
        {
            query = query.Where(s => s.CategoryId == filterCat);
        }

        // Total before keyset cursor — footer Total must match filtered row count.
        var totalCount = await query.LongCountAsync(cancellationToken);

        if (request.AfterId is int after)
        {
            query = query.Where(s => s.Id > after);
        }

        var rows = await query
            .OrderBy(s => s.Id)
            .Take(take + 1)
            .Select(SubCategoryProjections.ForShape(dataShape))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > take;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return Ok(new KeysetPageResult<SubCategoryDto>
        {
            Items = rows,
            NextCursor = rows.Count > 0 ? rows[^1].Id : null,
            HasMore = hasMore,
            TotalCount = totalCount,
        });
    }

    [HttpGet("{id:int}")]
    [RequirePermission("categories.read")]
    [ProducesResponseType(typeof(SubCategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubCategoryDto>> GetById(
        int id,
        [FromQuery] string? shape = null,
        CancellationToken cancellationToken = default)
    {
        var dataShape = DataShapeParser.Parse(shape, DataShape.Detail);
        var row = await _db.SubCategories
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(SubCategoryProjections.ForShape(dataShape))
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    [RequirePermission("categories.create")]
    [RequestSizeLimit(CatalogImageStorage.MaxBytes + 64_000)]
    [ProducesResponseType(typeof(SubCategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SubCategoryDto>> Create(
        [FromForm] int? companyId,
        [FromForm] int categoryId,
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
        var imageError = CatalogImageStorage.Validate(image, required: false);
        if (imageError is not null)
        {
            fieldErrors["image"] = [imageError];
        }

        if (categoryId <= 0)
        {
            fieldErrors["categoryId"] = ["Category is required."];
        }

        if (fieldErrors.Count > 0)
        {
            return UnprocessableEntity(new { errors = fieldErrors });
        }

        var parent = await _db.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == categoryId && c.CompanyId == resolvedCompanyId && c.IsActive,
                cancellationToken);

        if (parent is null)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["categoryId"] = ["Select an active category from the same company."],
                },
            });
        }

        var trimmedName = name.Trim();
        var normalized = NormalizeName(trimmedName);

        if (await NameTakenAsync(categoryId, normalized, excludeId: null, cancellationToken))
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["name"] = ["A sub-category with this name already exists under the selected category."],
                },
            });
        }

        string code;
        try
        {
            code = await _codeAllocator.AllocateSubCategoryAsync(resolvedCompanyId, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]> { ["code"] = [ex.Message] },
            });
        }

        string? imagePath = null;
        if (image is { Length: > 0 })
        {
            imagePath = await _images.SaveAsync(
                image, resolvedCompanyId, "sub-categories", cancellationToken);
        }

        var entity = new SubCategory
        {
            CompanyId = resolvedCompanyId,
            CategoryId = categoryId,
            Name = trimmedName,
            NameNormalized = normalized,
            Code = code,
            Description = NullIfWhite(description),
            ImagePath = imagePath,
            IsActive = true,
        };

        _db.SubCategories.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = await ProjectAsync(entity.Id, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, dto);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("categories.update")]
    [RequestSizeLimit(CatalogImageStorage.MaxBytes + 64_000)]
    [ProducesResponseType(typeof(SubCategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SubCategoryDto>> Update(
        int id,
        [FromForm] int categoryId,
        [FromForm] string name,
        [FromForm] string? description,
        [FromForm] bool? isActive,
        IFormFile? image,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.SubCategories.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
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

        if (categoryId <= 0)
        {
            fieldErrors["categoryId"] = ["Category is required."];
        }

        if (fieldErrors.Count > 0)
        {
            return UnprocessableEntity(new { errors = fieldErrors });
        }

        var parent = await _db.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == categoryId && c.CompanyId == entity.CompanyId && c.IsActive,
                cancellationToken);

        if (parent is null)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["categoryId"] = ["Select an active category from the same company."],
                },
            });
        }

        var trimmedName = name.Trim();
        var normalized = NormalizeName(trimmedName);

        if (await NameTakenAsync(categoryId, normalized, id, cancellationToken))
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["name"] = ["A sub-category with this name already exists under the selected category."],
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

        entity.CategoryId = categoryId;
        entity.Name = trimmedName;
        entity.NameNormalized = normalized;
        entity.Description = NullIfWhite(description);

        if (image is { Length: > 0 })
        {
            entity.ImagePath = await _images.SaveAsync(
                image, entity.CompanyId, "sub-categories", cancellationToken);
        }

        if (isActive is bool active)
        {
            entity.IsActive = active;
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
        var entity = await _db.SubCategories.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
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
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<ActionResult?> TryBlockDeactivateAsync(int subCategoryId, CancellationToken ct)
    {
        var hasProducts = await _db.Products
            .AsNoTracking()
            .AnyAsync(p => p.SubCategoryId == subCategoryId && p.IsActive, ct);

        if (hasProducts)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["subCategory"] =
                    [
                        "Cannot deactivate this sub-category while active products are linked.",
                    ],
                },
            });
        }

        return null;
    }

    private async Task<SubCategoryDto> ProjectAsync(int id, CancellationToken cancellationToken) =>
        await _db.SubCategories
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(SubCategoryProjections.Detail)
            .FirstAsync(cancellationToken);

    private async Task<bool> NameTakenAsync(
        int categoryId,
        string nameNormalized,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        var query = _db.SubCategories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.CategoryId == categoryId && s.NameNormalized == nameNormalized);

        if (excludeId is int skip)
        {
            query = query.Where(s => s.Id != skip);
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
                    ["companyId"] = ["Select a company before creating a sub-category."],
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
                ["companyId"] = ["Select a company context before creating a sub-category."],
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
