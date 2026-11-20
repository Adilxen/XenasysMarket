using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions;
using Xenasys.Core.Abstractions.Coding;
using Xenasys.Core.Abstractions.Pagination;
using Xenasys.Core.Contracts.Products;
using Xenasys.Core.Entities;
using Xenasys.Core.Querying;
using Xenasys.Core.Time;
using Xenasys.Infrastructure.Security;
using Xenasys.Persistence.Coding;
using Xenasys.Persistence.Data;
using Xenasys.Persistence.Querying;
using Xenasys.Persistence.Seeding;
using Xenasys.Presentation.Storage;

namespace Xenasys.Presentation.Controllers;

// ==========================================================================================
// WHAT: Tenant Product CRUD — keyset list; Inventory subsidiary TX; mandatory product image.
// WHY: BRD §4.5 / M3 — SKU + catalog FKs; JPG/PNG ≤2MB ImagePath (T3.8).
// EXAMPLE: POST multipart /api/products + image → SubsidiaryAccount + /uploads/{co}/products/…
// ==========================================================================================
[ApiController]
[Route("api/products")]
[Authorize]
public sealed class ProductsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUser _currentUser;
    private readonly IProductCodeAllocator _productCodeAllocator;
    private readonly ISubsidiaryAccountNoAllocator _accountNoAllocator;
    private readonly CatalogImageStorage _images;

    public ProductsController(
        ApplicationDbContext db,
        ITenantProvider tenantProvider,
        ICurrentUser currentUser,
        IProductCodeAllocator productCodeAllocator,
        ISubsidiaryAccountNoAllocator accountNoAllocator,
        CatalogImageStorage images)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
        _productCodeAllocator = productCodeAllocator;
        _accountNoAllocator = accountNoAllocator;
        _images = images;
    }

    [HttpGet]
    [RequirePermission("products.read")]
    [ProducesResponseType(typeof(KeysetPageResult<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<KeysetPageResult<ProductDto>>> List(
        [FromQuery] int? afterId,
        [FromQuery] int limit = 50,
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? shape = null,
        [FromQuery] string? productName = null,
        [FromQuery] string? productCode = null,
        [FromQuery] string? companyName = null,
        [FromQuery] string? categoryName = null,
        [FromQuery] string? brandName = null,
        [FromQuery] string? packaging = null,
        CancellationToken cancellationToken = default)
    {
        var request = new KeysetPageRequest { AfterId = afterId, Limit = limit };
        var take = request.GetSafeLimit();
        var dataShape = DataShapeParser.Parse(shape, DataShape.List);

        // SA bypasses EF tenant filter (ApplicationDbContext); tenants stay CompanyId-scoped.
        var query = _db.Products
            .AsNoTracking()
            .WhereActive(includeInactive)
            .WhereContains(productName, p => p.ProductName)
            .WhereContains(productCode, p => p.ProductCode)
            .WhereContains(companyName, p => p.Company != null ? p.Company.Name : null)
            .WhereContains(categoryName, p => p.Category != null ? p.Category.Name : null)
            .WhereContains(brandName, p => p.Brand != null ? p.Brand.Name : null)
            .WhereContains(packaging, p => p.Packaging);

        // Total before keyset cursor — footer Total must match filtered row count.
        var totalCount = await query.LongCountAsync(cancellationToken);

        if (request.AfterId is int cursor)
        {
            query = query.Where(p => p.Id > cursor);
        }

        var rows = await query
            .OrderBy(p => p.Id)
            .Take(take + 1)
            .Select(ProductProjections.ForShape(dataShape))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > take;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return Ok(new KeysetPageResult<ProductDto>
        {
            Items = rows,
            NextCursor = rows.Count > 0 ? rows[^1].Id : null,
            HasMore = hasMore,
            TotalCount = totalCount,
        });
    }

    [HttpGet("{id:int}")]
    [RequirePermission("products.read")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById(
        int id,
        [FromQuery] string? shape = null,
        CancellationToken cancellationToken = default)
    {
        var dataShape = DataShapeParser.Parse(shape, DataShape.Detail);

        var product = await _db.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(ProductProjections.ForShape(dataShape))
            .FirstOrDefaultAsync(cancellationToken);

        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    [RequirePermission("products.create")]
    [RequestSizeLimit(CatalogImageStorage.MaxBytes + 256_000)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ProductDto>> Create(
        [FromForm] int? companyId,
        [FromForm] string productName,
        [FromForm] string? productDescription,
        [FromForm] string packaging,
        [FromForm] int categoryId,
        [FromForm] int subCategoryId,
        [FromForm] int brandId,
        [FromForm] int unitId,
        [FromForm] decimal invoicePrice,
        [FromForm] decimal retailPrice,
        [FromForm] int minimumStockLevel,
        [FromForm] string? barcode,
        IFormFile? image,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveWriteCompanyId(companyId, out var resolvedCompanyId, out var errorResult))
        {
            return errorResult!;
        }

        var fieldErrors = ValidateFields(
            productName,
            productDescription,
            packaging,
            invoicePrice,
            retailPrice,
            minimumStockLevel,
            barcode);
        var imageError = CatalogImageStorage.Validate(image, required: true);
        if (imageError is not null)
        {
            fieldErrors["image"] = [imageError];
        }

        if (fieldErrors.Count > 0)
        {
            return UnprocessableEntity(new { errors = fieldErrors });
        }

        var catalogErrors = await ValidateCatalogFksAsync(
            resolvedCompanyId,
            categoryId,
            subCategoryId,
            brandId,
            unitId,
            cancellationToken);
        if (catalogErrors.Count > 0)
        {
            return UnprocessableEntity(new { errors = catalogErrors });
        }

        var unit = await _db.Units
            .AsNoTracking()
            .FirstAsync(u => u.Id == unitId && u.IsActive, cancellationToken);

        var invControl = await FindActiveInvControlAsync(resolvedCompanyId, cancellationToken);
        if (invControl is null)
        {
            var actorId = _currentUser.UserId is int uid and > 0 ? uid : 1;
            await TenantCoaBootstrap.EnsureCompanyBaselineAsync(
                _db,
                resolvedCompanyId,
                actorId,
                AuditClock.Now,
                cancellationToken: cancellationToken);
            invControl = await FindActiveInvControlAsync(resolvedCompanyId, cancellationToken);
        }

        if (invControl is null)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["controlAccount"] =
                    [
                        "Inventory control account is missing for this company. Complete COA setup first.",
                    ],
                },
            });
        }

        var actorForSku = _currentUser.UserId is int skuActor and > 0 ? skuActor : 1;
        string productCode;
        SubsidiaryAccountCodes codes;
        try
        {
            productCode = await _productCodeAllocator.AllocateAsync(
                resolvedCompanyId,
                actorForSku,
                cancellationToken);
            codes = await _accountNoAllocator.AllocateProductAsync(resolvedCompanyId, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["productCode"] = [ex.Message],
                },
            });
        }

        var name = productName.Trim();
        var subsidiaryTitle = BuildSubsidiaryTitle(name);
        var (resolvedBarcode, barcodeError) = await ProductBarcodeRules.ResolveAsync(
            _db,
            resolvedCompanyId,
            barcode,
            productCode,
            excludeProductId: null,
            cancellationToken);
        if (barcodeError is not null)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["barcode"] = [barcodeError],
                },
            });
        }

        var imagePath = await _images.SaveAsync(
            image!,
            resolvedCompanyId,
            "products",
            cancellationToken);

        var strategy = _db.Database.CreateExecutionStrategy();
        ProductDto? createdDto = null;
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var subsidiary = new SubsidiaryAccount
                {
                    CompanyId = resolvedCompanyId,
                    ControlAccountId = invControl.Id,
                    SubsidiaryAccountCode = codes.AccountCode,
                    SubsidiaryAccountTitle = subsidiaryTitle,
                    AccountNo = codes.AccountNo,
                    ProductCode = productCode,
                    IsActive = true,
                    IsSystemGenerated = true,
                    SourceEntityType = "Product",
                };

                _db.SubsidiaryAccounts.Add(subsidiary);
                await _db.SaveChangesAsync(cancellationToken);

                var product = new Product
                {
                    CompanyId = resolvedCompanyId,
                    CategoryId = categoryId,
                    SubCategoryId = subCategoryId,
                    BrandId = brandId,
                    UnitId = unitId,
                    ProductName = name,
                    ProductDescription = NullIfWhite(productDescription),
                    Packaging = packaging.Trim(),
                    UnitOfMeasurement = unit.Code,
                    ProductCode = productCode,
                    Barcode = resolvedBarcode,
                    MinimumStockLevel = minimumStockLevel,
                    QuantityOnHand = 0,
                    InvoicePrice = invoicePrice,
                    RetailPrice = retailPrice,
                    ImagePath = imagePath,
                    IsActive = true,
                    SubsidiaryAccountId = subsidiary.Id,
                };

                _db.Products.Add(product);
                await _db.SaveChangesAsync(cancellationToken);

                subsidiary.SourceEntityId = product.Id;
                await _db.SaveChangesAsync(cancellationToken);

                await tx.CommitAsync(cancellationToken);

                createdDto = await ProjectAsync(product.Id, cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });

        return CreatedAtAction(nameof(GetById), new { id = createdDto!.Id }, createdDto);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("products.update")]
    [RequestSizeLimit(CatalogImageStorage.MaxBytes + 256_000)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ProductDto>> Update(
        int id,
        [FromForm] string productName,
        [FromForm] string? productDescription,
        [FromForm] string packaging,
        [FromForm] int categoryId,
        [FromForm] int subCategoryId,
        [FromForm] int brandId,
        [FromForm] int unitId,
        [FromForm] decimal invoicePrice,
        [FromForm] decimal retailPrice,
        [FromForm] int minimumStockLevel,
        [FromForm] string? barcode,
        [FromForm] bool? isActive,
        IFormFile? image,
        CancellationToken cancellationToken = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        if (!CanMutateCompany(product.CompanyId))
        {
            return Forbid();
        }

        var fieldErrors = ValidateFields(
            productName,
            productDescription,
            packaging,
            invoicePrice,
            retailPrice,
            minimumStockLevel,
            barcode);

        var needsImage = string.IsNullOrWhiteSpace(product.ImagePath)
            || product.ImagePath.Trim() is "/" or "\\";
        var imageError = CatalogImageStorage.Validate(image, required: needsImage);
        if (imageError is not null)
        {
            fieldErrors["image"] = [imageError];
        }

        if (fieldErrors.Count > 0)
        {
            return UnprocessableEntity(new { errors = fieldErrors });
        }

        var catalogErrors = await ValidateCatalogFksAsync(
            product.CompanyId,
            categoryId,
            subCategoryId,
            brandId,
            unitId,
            cancellationToken);
        if (catalogErrors.Count > 0)
        {
            return UnprocessableEntity(new { errors = catalogErrors });
        }

        var unit = await _db.Units
            .AsNoTracking()
            .FirstAsync(u => u.Id == unitId && u.IsActive, cancellationToken);

        var name = productName.Trim();
        var (resolvedBarcode, barcodeError) = await ProductBarcodeRules.ResolveAsync(
            _db,
            product.CompanyId,
            barcode,
            product.ProductCode,
            excludeProductId: id,
            cancellationToken);
        if (barcodeError is not null)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["barcode"] = [barcodeError],
                },
            });
        }

        product.CategoryId = categoryId;
        product.SubCategoryId = subCategoryId;
        product.BrandId = brandId;
        product.UnitId = unitId;
        product.ProductName = name;
        product.ProductDescription = NullIfWhite(productDescription);
        product.Packaging = packaging.Trim();
        product.UnitOfMeasurement = unit.Code;
        product.Barcode = resolvedBarcode;
        product.MinimumStockLevel = minimumStockLevel;
        product.InvoicePrice = invoicePrice;
        product.RetailPrice = retailPrice;

        if (isActive is bool active)
        {
            product.IsActive = active;
        }

        if (image is { Length: > 0 })
        {
            product.ImagePath = await _images.SaveAsync(
                image,
                product.CompanyId,
                "products",
                cancellationToken);
        }

        if (product.SubsidiaryAccountId is int subId)
        {
            var subsidiary = await _db.SubsidiaryAccounts
                .FirstOrDefaultAsync(s => s.Id == subId, cancellationToken);
            if (subsidiary is not null)
            {
                subsidiary.SubsidiaryAccountTitle = BuildSubsidiaryTitle(name);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await ProjectAsync(id, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("products.delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        if (!CanMutateCompany(product.CompanyId))
        {
            return Forbid();
        }

        product.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:int}/active")]
    [RequirePermission("products.update")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> SetActive(
        int id,
        [FromBody] SetProductActiveRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        if (!CanMutateCompany(product.CompanyId))
        {
            return Forbid();
        }

        product.IsActive = request.IsActive;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await ProjectAsync(id, cancellationToken));
    }

    private async Task<ProductDto> ProjectAsync(int id, CancellationToken cancellationToken) =>
        await _db.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(ProductProjections.Detail)
            .FirstAsync(cancellationToken);

    private async Task<ControlAccount?> FindActiveInvControlAsync(
        int companyId,
        CancellationToken cancellationToken) =>
        await _db.ControlAccounts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.CompanyId == companyId
                    && c.ControlKind == ControlAccountKinds.Inventory
                    && c.IsActive,
                cancellationToken);

    private async Task<Dictionary<string, string[]>> ValidateCatalogFksAsync(
        int companyId,
        int categoryId,
        int subCategoryId,
        int brandId,
        int unitId,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (categoryId <= 0)
        {
            errors["categoryId"] = ["Category is required."];
        }

        if (subCategoryId <= 0)
        {
            errors["subCategoryId"] = ["Sub Category is required."];
        }

        if (brandId <= 0)
        {
            errors["brandId"] = ["Brand is required."];
        }

        if (unitId <= 0)
        {
            errors["unitId"] = ["Unit is required."];
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        var categoryOk = await _db.Categories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(
                c => c.Id == categoryId && c.CompanyId == companyId && c.IsActive,
                cancellationToken);
        if (!categoryOk)
        {
            errors["categoryId"] = ["Select an active Category that belongs to this company."];
        }

        var sub = await _db.SubCategories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.Id == subCategoryId && s.CompanyId == companyId && s.IsActive,
                cancellationToken);
        if (sub is null)
        {
            errors["subCategoryId"] = ["Select an active Sub Category that belongs to this company."];
        }
        else if (sub.CategoryId != categoryId)
        {
            errors["subCategoryId"] = ["Sub Category must belong to the selected Category."];
        }

        var brandOk = await _db.Brands
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(
                b => b.Id == brandId && b.CompanyId == companyId && b.IsActive,
                cancellationToken);
        if (!brandOk)
        {
            errors["brandId"] = ["Select an active Brand that belongs to this company."];
        }

        var unitOk = await _db.Units
            .AsNoTracking()
            .AnyAsync(u => u.Id == unitId && u.IsActive, cancellationToken);
        if (!unitOk)
        {
            errors["unitId"] = ["Select an active Unit of Measurement."];
        }

        return errors;
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
                    ["companyId"] = ["Select a company before creating a product."],
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
                ["companyId"] = ["Select a company context before creating a product."],
            },
        });
        return false;
    }

    private bool CanMutateCompany(int companyId) =>
        _tenantProvider.IsSuperAdmin
        || (_tenantProvider.TenantId is int tid && tid == companyId);

    private static Dictionary<string, string[]> ValidateFields(
        string? productName,
        string? productDescription,
        string? packaging,
        decimal invoicePrice,
        decimal retailPrice,
        int minimumStockLevel,
        string? barcode)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        var name = (productName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            errors["productName"] = ["Product Name is required."];
        }
        else if (name.Length > 150)
        {
            errors["productName"] = ["Product Name must be at most 150 characters."];
        }

        var pack = (packaging ?? string.Empty).Trim();
        if (pack.Length == 0)
        {
            errors["packaging"] = ["Packaging is required."];
        }
        else if (pack.Length > 50)
        {
            errors["packaging"] = ["Packaging must be at most 50 characters."];
        }

        if (invoicePrice <= 0)
        {
            errors["invoicePrice"] = ["Invoice Price must be greater than 0."];
        }

        if (retailPrice <= 0)
        {
            errors["retailPrice"] = ["Retail Price must be greater than 0."];
        }
        else if (retailPrice < invoicePrice)
        {
            errors["retailPrice"] = ["Retail Price must be greater than or equal to Invoice Price."];
        }

        if (minimumStockLevel < 0)
        {
            errors["minimumStockLevel"] = ["Minimum Stock Level must be at least 0."];
        }

        var description = productDescription ?? string.Empty;
        if (description.Length > 1000)
        {
            errors["productDescription"] = ["Product Description must be at most 1000 characters."];
        }

        var code = barcode ?? string.Empty;
        if (code.Length > 500)
        {
            errors["barcode"] = ["Barcode must be at most 500 characters."];
        }
        else if (code.Length > 0
            && !System.Text.RegularExpressions.Regex.IsMatch(code, @"^[A-Za-z0-9,-]*$"))
        {
            errors["barcode"] =
            [
                "Barcode may only contain letters, numbers, commas, and hyphens.",
            ];
        }

        return errors;
    }

    /// <summary>INV subsidiary title = Product Name only (SKU lives in ProductCode / Product.ProductCode).</summary>
    private static string BuildSubsidiaryTitle(string productName)
    {
        var title = productName.Trim();
        return title.Length > 150 ? title[..150] : title;
    }

    private static string? NullIfWhite(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
