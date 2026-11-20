using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions;
using Xenasys.Core.Abstractions.Coding;
using Xenasys.Core.Abstractions.Pagination;
using Xenasys.Core.Contracts.Vendors;
using Xenasys.Core.Entities;
using Xenasys.Core.Querying;
using Xenasys.Core.Time;
using Xenasys.Infrastructure.Security;
using Xenasys.Persistence.Data;
using Xenasys.Persistence.Querying;
using Xenasys.Persistence.Seeding;

namespace Xenasys.Presentation.Controllers;

// ==========================================================================================
// WHAT: Tenant Vendor CRUD — keyset list; AP subsidiary TX on create (IsSystemGenerated).
// WHY: BRD §4.6 — vendor soft-delete keeps AP ledger; NTN unique per company.
// EXAMPLE: POST /api/vendors → SubsidiaryAccount title "Vendor: Acme Supplies" under AP.
// ==========================================================================================
[ApiController]
[Route("api/vendors")]
[Authorize]
public sealed class VendorsController : ControllerBase
{
    // Documents NTN mask: 1234567-8 (optional field; validated when provided).
    private static readonly Regex NtnRegex = new(@"^[0-9]{7}-[0-9]{1}$", RegexOptions.Compiled);

    // Pakistan mobile: +92 followed by exactly 10 digits — e.g. +923368288338.
    private static readonly Regex PhoneRegex = new(@"^\+92[0-9]{10}$", RegexOptions.Compiled);

    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUser _currentUser;
    private readonly ISubsidiaryAccountNoAllocator _accountNoAllocator;

    public VendorsController(
        ApplicationDbContext db,
        ITenantProvider tenantProvider,
        ICurrentUser currentUser,
        ISubsidiaryAccountNoAllocator accountNoAllocator)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
        _accountNoAllocator = accountNoAllocator;
    }

    [HttpGet]
    [RequirePermission("vendors.read")]
    [ProducesResponseType(typeof(KeysetPageResult<VendorDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<KeysetPageResult<VendorDto>>> List(
        [FromQuery] int? afterId,
        [FromQuery] int limit = 50,
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? shape = null,
        [FromQuery] string? vendorName = null,
        [FromQuery] string? ntnNo = null,
        [FromQuery] string? vendorPhone = null,
        [FromQuery] string? email = null,
        [FromQuery] string? companyName = null,
        CancellationToken cancellationToken = default)
    {
        var request = new KeysetPageRequest { AfterId = afterId, Limit = limit };
        var take = request.GetSafeLimit();
        var dataShape = DataShapeParser.Parse(shape, DataShape.List);

        var query = _db.Vendors
            .AsNoTracking()
            .WhereActive(includeInactive)
            .WhereContains(vendorName, v => v.VendorName)
            .WhereContains(ntnNo, v => v.NtnNo)
            .WhereContains(vendorPhone, v => v.VendorPhone)
            .WhereContains(email, v => v.Email)
            .WhereContains(companyName, v => v.Company != null ? v.Company.Name : null);

        // Total before keyset cursor — footer Total must match filtered row count.
        var totalCount = await query.LongCountAsync(cancellationToken);

        if (request.AfterId is int cursor)
        {
            query = query.Where(v => v.Id > cursor);
        }

        var rows = await query
            .OrderBy(v => v.Id)
            .Take(take + 1)
            .Select(VendorProjections.ForShape(dataShape))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > take;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return Ok(new KeysetPageResult<VendorDto>
        {
            Items = rows,
            NextCursor = rows.Count > 0 ? rows[^1].Id : null,
            HasMore = hasMore,
            TotalCount = totalCount,
        });
    }

    [HttpGet("{id:int}")]
    [RequirePermission("vendors.read")]
    [ProducesResponseType(typeof(VendorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VendorDto>> GetById(
        int id,
        [FromQuery] string? shape = null,
        CancellationToken cancellationToken = default)
    {
        var dataShape = DataShapeParser.Parse(shape, DataShape.Detail);

        var vendor = await _db.Vendors
            .AsNoTracking()
            .Where(v => v.Id == id)
            .Select(VendorProjections.ForShape(dataShape))
            .FirstOrDefaultAsync(cancellationToken);

        return vendor is null ? NotFound() : Ok(vendor);
    }

    [HttpPost]
    [RequirePermission("vendors.create")]
    [ProducesResponseType(typeof(VendorDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<VendorDto>> Create(
        [FromBody] CreateVendorRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveWriteCompanyId(request.CompanyId, out var companyId, out var errorResult))
        {
            return errorResult!;
        }

        var fieldErrors = ValidateFields(
            request.VendorName,
            request.NtnNo,
            request.Address,
            request.VendorPhone,
            request.Email,
            request.RepresentativeName,
            request.RepresentativeNo);
        if (fieldErrors.Count > 0)
        {
            return UnprocessableEntity(new { errors = fieldErrors });
        }

        var vendorName = request.VendorName.Trim();
        var ntnNo = NormalizeNtn(request.NtnNo);

        if (ntnNo.Length > 0
            && await NtnTakenAsync(companyId, ntnNo, excludeId: null, cancellationToken))
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["ntnNo"] = ["NTN No is already registered for this company."],
                },
            });
        }

        var apControl = await FindActiveApControlAsync(companyId, cancellationToken);
        if (apControl is null)
        {
            // Auto-heal missing COA baseline so vendor create is not blocked on older tenants.
            var actorId = _currentUser.UserId is int uid and > 0 ? uid : 1;
            await TenantCoaBootstrap.EnsureCompanyBaselineAsync(
                _db,
                companyId,
                actorId,
                AuditClock.Now,
                cancellationToken: cancellationToken);
            apControl = await FindActiveApControlAsync(companyId, cancellationToken);
        }

        if (apControl is null)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["controlAccount"] =
                    [
                        "Accounts Payable control account is missing for this company. Complete COA setup first.",
                    ],
                },
            });
        }

        SubsidiaryAccountCodes codes;
        try
        {
            codes = await _accountNoAllocator.AllocateVendorAsync(companyId, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["accountNo"] = [ex.Message],
                },
            });
        }

        // Retry strategy requires the ambient transaction to run inside CreateExecutionStrategy.
        var strategy = _db.Database.CreateExecutionStrategy();
        VendorDto? createdDto = null;
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var subsidiaryTitle = $"Vendor: {vendorName}";
                var subsidiary = new SubsidiaryAccount
                {
                    CompanyId = companyId,
                    ControlAccountId = apControl.Id,
                    SubsidiaryAccountCode = codes.AccountCode,
                    SubsidiaryAccountTitle = subsidiaryTitle,
                    AccountNo = codes.AccountNo,
                    IsActive = true,
                    IsSystemGenerated = true,
                    SourceEntityType = "Vendor",
                };

                _db.SubsidiaryAccounts.Add(subsidiary);
                await _db.SaveChangesAsync(cancellationToken);

                var vendor = new Vendor
                {
                    CompanyId = companyId,
                    VendorName = vendorName,
                    NtnNo = ntnNo.Length > 0 ? ntnNo : null,
                    Address = EmptyIfWhite(request.Address),
                    VendorPhone = NormalizePkMobile(request.VendorPhone),
                    Email = NullIfWhite(request.Email),
                    RepresentativeName = EmptyIfWhite(request.RepresentativeName),
                    RepresentativeNo = NormalizePkMobile(request.RepresentativeNo),
                    IsActive = true,
                    SubsidiaryAccountId = subsidiary.Id,
                };

                _db.Vendors.Add(vendor);
                await _db.SaveChangesAsync(cancellationToken);

                subsidiary.SourceEntityId = vendor.Id;
                await _db.SaveChangesAsync(cancellationToken);

                await tx.CommitAsync(cancellationToken);

                createdDto = await ProjectAsync(vendor.Id, cancellationToken);
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
    [RequirePermission("vendors.update")]
    [ProducesResponseType(typeof(VendorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<VendorDto>> Update(
        int id,
        [FromBody] UpdateVendorRequest request,
        CancellationToken cancellationToken = default)
    {
        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (vendor is null)
        {
            return NotFound();
        }

        if (!CanMutateCompany(vendor.CompanyId))
        {
            return Forbid();
        }

        var fieldErrors = ValidateFields(
            request.VendorName,
            request.NtnNo,
            request.Address,
            request.VendorPhone,
            request.Email,
            request.RepresentativeName,
            request.RepresentativeNo);
        if (fieldErrors.Count > 0)
        {
            return UnprocessableEntity(new { errors = fieldErrors });
        }

        var vendorName = request.VendorName.Trim();
        var ntnNo = NormalizeNtn(request.NtnNo);

        if (ntnNo.Length > 0
            && await NtnTakenAsync(vendor.CompanyId, ntnNo, id, cancellationToken))
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["ntnNo"] = ["NTN No is already registered for this company."],
                },
            });
        }

        vendor.VendorName = vendorName;
        vendor.NtnNo = ntnNo.Length > 0 ? ntnNo : null;
        vendor.Address = EmptyIfWhite(request.Address);
        vendor.VendorPhone = NormalizePkMobile(request.VendorPhone);
        vendor.Email = NullIfWhite(request.Email);
        vendor.RepresentativeName = EmptyIfWhite(request.RepresentativeName);
        vendor.RepresentativeNo = NormalizePkMobile(request.RepresentativeNo);

        if (request.IsActive is bool active)
        {
            vendor.IsActive = active;
        }

        if (vendor.SubsidiaryAccountId is int subId)
        {
            var subsidiary = await _db.SubsidiaryAccounts
                .FirstOrDefaultAsync(s => s.Id == subId, cancellationToken);
            if (subsidiary is not null)
            {
                subsidiary.SubsidiaryAccountTitle = $"Vendor: {vendorName}";
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await ProjectAsync(id, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("vendors.delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (vendor is null)
        {
            return NotFound();
        }

        if (!CanMutateCompany(vendor.CompanyId))
        {
            return Forbid();
        }

        vendor.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<VendorDto> ProjectAsync(int id, CancellationToken cancellationToken) =>
        await _db.Vendors
            .AsNoTracking()
            .Where(v => v.Id == id)
            .Select(VendorProjections.Detail)
            .FirstAsync(cancellationToken);

    private async Task<bool> NtnTakenAsync(
        int companyId,
        string ntnNo,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        var query = _db.Vendors
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(v => v.CompanyId == companyId && v.NtnNo == ntnNo);

        if (excludeId is int skip)
        {
            query = query.Where(v => v.Id != skip);
        }

        return await query.AnyAsync(cancellationToken);
    }

    private async Task<ControlAccount?> FindActiveApControlAsync(
        int companyId,
        CancellationToken cancellationToken) =>
        await _db.ControlAccounts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.CompanyId == companyId
                    && c.ControlKind == ControlAccountKinds.AccountsPayable
                    && c.IsActive,
                cancellationToken);

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
                    ["companyId"] = ["Select a company before creating a vendor."],
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
                ["companyId"] = ["Select a company context before creating a vendor."],
            },
        });
        return false;
    }

    private bool CanMutateCompany(int companyId) =>
        _tenantProvider.IsSuperAdmin
        || (_tenantProvider.TenantId is int tid && tid == companyId);

    private static Dictionary<string, string[]> ValidateFields(
        string? vendorName,
        string? ntnNo,
        string? address,
        string? vendorPhone,
        string? email,
        string? representativeName,
        string? representativeNo)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        var name = (vendorName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            errors["vendorName"] = ["Vendor Name is required."];
        }
        else if (name.Length > 100)
        {
            errors["vendorName"] = ["Vendor Name must be at most 100 characters."];
        }

        var ntn = NormalizeNtn(ntnNo);
        if (ntn.Length > 0)
        {
            if (!NtnRegex.IsMatch(ntn))
            {
                errors["ntnNo"] = ["NTN No must match format 1234567-8."];
            }
        }

        var addr = (address ?? string.Empty).Trim();
        if (addr.Length > 250)
        {
            errors["address"] = ["Address must be at most 250 characters."];
        }

        var phone = NormalizePkMobile(vendorPhone);
        if (phone.Length == 0)
        {
            errors["vendorPhone"] = ["Phone No is required."];
        }
        else if (!PhoneRegex.IsMatch(phone))
        {
            errors["vendorPhone"] =
                ["Phone No must match format +923368288338 (+92 + 10 digits)."];
        }

        var mail = (email ?? string.Empty).Trim();
        if (mail.Length > 0)
        {
            if (mail.Length > 256)
            {
                errors["email"] = ["Email must be at most 256 characters."];
            }
            else if (!mail.Contains('@') || mail.IndexOf('@') != mail.LastIndexOf('@'))
            {
                errors["email"] = ["Enter a valid email address."];
            }
        }

        var repName = (representativeName ?? string.Empty).Trim();
        if (repName.Length > 100)
        {
            errors["representativeName"] = ["Representative Name must be at most 100 characters."];
        }

        var repNo = NormalizePkMobile(representativeNo);
        if (repNo.Length > 0 && !PhoneRegex.IsMatch(repNo))
        {
            errors["representativeNo"] =
                ["Representative No must match format +923368288338 (+92 + 10 digits)."];
        }

        return errors;
    }

    private static string EmptyIfWhite(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    /// <summary>Documents mask 1234567-8; 8 bare digits auto-hyphenated.</summary>
    private static string NormalizeNtn(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var compact = value.Trim().Replace(" ", string.Empty);
        var digits = Regex.Replace(compact, @"\D", string.Empty);
        if (digits.Length == 8)
        {
            return $"{digits[..7]}-{digits[7]}";
        }

        return compact;
    }

    /// <summary>Keep leading +; strip spaces/dashes so +92 336… → +92336…</summary>
    private static string NormalizePkMobile(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        // Drop spaces/hyphens only; keep leading +.
        var compact = Regex.Replace(value.Trim(), @"[\s\-]", string.Empty);
        if (compact.Length == 0)
        {
            return string.Empty;
        }

        if (!compact.StartsWith('+'))
        {
            compact = "+" + compact;
        }

        // Local 0XXXXXXXXXX → +92XXXXXXXXXX
        if (Regex.IsMatch(compact, @"^\+0[0-9]{10}$"))
        {
            compact = "+92" + compact[2..];
        }

        return compact;
    }

    private static string? NullIfWhite(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
