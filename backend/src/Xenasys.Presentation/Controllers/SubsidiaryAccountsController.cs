using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions;
using Xenasys.Core.Abstractions.Coding;
using Xenasys.Core.Abstractions.Pagination;
using Xenasys.Core.Contracts.Accounts;
using Xenasys.Core.Entities;
using Xenasys.Core.Querying;
using Xenasys.Infrastructure.Security;
using Xenasys.Persistence.Data;
using Xenasys.Persistence.Querying;

namespace Xenasys.Presentation.Controllers;

// ==========================================================================================
// WHAT: Tenant Subsidiary Account CRUD — keyset list; AccountNo auto; block system delete.
// WHY: BRD M1 — SA View all companies; TA own company; JWT isolation for non-SA writes.
// EXAMPLE: GET /api/coa/subsidiaries → Super Admin sees every company leaf row
// ==========================================================================================
[ApiController]
[Route("api/coa/subsidiaries")]
[Authorize]
public sealed class SubsidiaryAccountsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly ISubsidiaryAccountNoAllocator _accountNoAllocator;

    public SubsidiaryAccountsController(
        ApplicationDbContext db,
        ITenantProvider tenantProvider,
        ISubsidiaryAccountNoAllocator accountNoAllocator)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _accountNoAllocator = accountNoAllocator;
    }

    [HttpGet]
    [RequirePermission("coa.subsidiaries.read")]
    [ProducesResponseType(typeof(KeysetPageResult<SubsidiaryAccountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<KeysetPageResult<SubsidiaryAccountDto>>> List(
        [FromQuery] int? afterId,
        [FromQuery] int limit = 50,
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? shape = null,
        [FromQuery] int? companyId = null,
        [FromQuery] string? companyName = null,
        [FromQuery] string? subsidiaryAccountCode = null,
        [FromQuery] string? subsidiaryAccountTitle = null,
        [FromQuery] string? accountNo = null,
        [FromQuery] string? controlAccountTitle = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveListCompanyId(companyId, out var companyFilter, out var forbid))
        {
            return forbid!;
        }

        var request = new KeysetPageRequest { AfterId = afterId, Limit = limit };
        var take = request.GetSafeLimit();
        var dataShape = DataShapeParser.Parse(shape, DataShape.List);

        IQueryable<SubsidiaryAccount> query = _db.SubsidiaryAccounts
            .AsNoTracking()
            .WhereActive(includeInactive)
            .WhereContains(companyName, s => s.Company != null ? s.Company.Name : null)
            .WhereContains(subsidiaryAccountCode, s => s.SubsidiaryAccountCode)
            .WhereContains(subsidiaryAccountTitle, s => s.SubsidiaryAccountTitle)
            .WhereContains(accountNo, s => s.AccountNo)
            .WhereContains(controlAccountTitle, s => s.ControlAccount != null ? s.ControlAccount.Title : null);

        if (companyFilter is int scopedCompanyId)
        {
            query = query.Where(s => s.CompanyId == scopedCompanyId);
        }

        // Total before keyset cursor — footer Total must match filtered ledger size.
        var totalCount = await query.LongCountAsync(cancellationToken);

        if (request.AfterId is int cursor)
        {
            // Id-only seek requires Id ordering (global IDENTITY). Company column still shown for SA.
            query = query.Where(s => s.Id > cursor);
        }

        var rows = await query
            .OrderBy(s => s.Id)
            .Take(take + 1)
            .Select(SubsidiaryAccountProjections.ForShape(dataShape))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > take;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return Ok(new KeysetPageResult<SubsidiaryAccountDto>
        {
            Items = rows,
            NextCursor = rows.Count > 0 ? rows[^1].Id : null,
            HasMore = hasMore,
            TotalCount = totalCount,
        });
    }

    [HttpGet("{id:int}")]
    [RequirePermission("coa.subsidiaries.read")]
    [ProducesResponseType(typeof(SubsidiaryAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubsidiaryAccountDto>> GetById(
        int id,
        [FromQuery] string? shape = null,
        CancellationToken cancellationToken = default)
    {
        var dataShape = DataShapeParser.Parse(shape, DataShape.Detail);

        var row = await _db.SubsidiaryAccounts
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(SubsidiaryAccountProjections.ForShape(dataShape))
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    [RequirePermission("coa.subsidiaries.create")]
    [ProducesResponseType(typeof(SubsidiaryAccountDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SubsidiaryAccountDto>> Create(
        [FromBody] CreateSubsidiaryAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveWriteCompanyId(out var companyId, out var errorResult))
        {
            return errorResult!;
        }

        var fieldErrors = ValidateFields(request.ControlAccountId, request.SubsidiaryAccountTitle);
        if (fieldErrors.Count > 0)
        {
            return UnprocessableEntity(new { errors = fieldErrors });
        }

        var title = request.SubsidiaryAccountTitle.Trim();

        if (!await _db.ControlAccounts.AnyAsync(
                c => c.Id == request.ControlAccountId
                    && c.CompanyId == companyId
                    && c.IsActive,
                cancellationToken))
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["controlAccountId"] = ["Control Account is required for this company."],
                },
            });
        }

        SubsidiaryAccountCodes codes;
        try
        {
            codes = await _accountNoAllocator.AllocateManualAsync(companyId, cancellationToken);
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

        var entity = new SubsidiaryAccount
        {
            CompanyId = companyId,
            ControlAccountId = request.ControlAccountId,
            SubsidiaryAccountCode = codes.AccountCode,
            SubsidiaryAccountTitle = title,
            AccountNo = codes.AccountNo,
            IsActive = true,
            IsSystemGenerated = false,
        };

        _db.SubsidiaryAccounts.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = await ProjectAsync(entity.Id, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, dto);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("coa.subsidiaries.update")]
    [ProducesResponseType(typeof(SubsidiaryAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SubsidiaryAccountDto>> Update(
        int id,
        [FromBody] UpdateSubsidiaryAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.SubsidiaryAccounts.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var fieldErrors = ValidateFields(request.ControlAccountId, request.SubsidiaryAccountTitle);
        if (fieldErrors.Count > 0)
        {
            return UnprocessableEntity(new { errors = fieldErrors });
        }

        // Account No + Account Code are system formats — never updated after create.
        if (entity.IsSystemGenerated && request.ControlAccountId != entity.ControlAccountId)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["controlAccountId"] =
                    [
                        "System-generated subsidiary accounts cannot change Control Account.",
                    ],
                },
            });
        }

        var title = request.SubsidiaryAccountTitle.Trim();

        if (!await _db.ControlAccounts.AnyAsync(
                c => c.Id == request.ControlAccountId
                    && c.CompanyId == entity.CompanyId
                    && c.IsActive,
                cancellationToken))
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["controlAccountId"] = ["Control Account is required for this company."],
                },
            });
        }

        if (!entity.IsSystemGenerated)
        {
            entity.ControlAccountId = request.ControlAccountId;
        }

        entity.SubsidiaryAccountTitle = title;
        if (request.IsActive is bool active && !entity.IsSystemGenerated)
        {
            entity.IsActive = active;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await ProjectAsync(id, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("coa.subsidiaries.delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.SubsidiaryAccounts.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (entity.IsSystemGenerated)
        {
            return Conflict(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["isSystemGenerated"] = ["System-generated subsidiary accounts cannot be deleted."],
                },
            });
        }

        entity.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<SubsidiaryAccountDto> ProjectAsync(int id, CancellationToken cancellationToken) =>
        await _db.SubsidiaryAccounts
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(SubsidiaryAccountProjections.Detail)
            .FirstAsync(cancellationToken);

    /// <summary>
    /// BRD matrix: Super Admin View all companies; Tenant Admin own company only.
    /// Optional query companyId narrows SA view; JWT tenant_id must not hide other companies.
    /// </summary>
    private bool TryResolveListCompanyId(
        int? requestedCompanyId,
        out int? companyFilter,
        out ActionResult? forbid)
    {
        companyFilter = null;
        forbid = null;

        if (_tenantProvider.IsSuperAdmin)
        {
            if (requestedCompanyId is int explicitId and > 0)
            {
                companyFilter = explicitId;
            }

            return true;
        }

        if (_tenantProvider.TenantId is int companyId && companyId > 0)
        {
            // Tenants ignore client companyId — JWT only (BRD §2).
            companyFilter = companyId;
            return true;
        }

        forbid = Forbid();
        return false;
    }

    private bool TryResolveWriteCompanyId(out int companyId, out ActionResult? errorResult)
    {
        companyId = 0;
        errorResult = null;

        if (_tenantProvider.TenantId is int tid && tid > 0)
        {
            companyId = tid;
            return true;
        }

        errorResult = UnprocessableEntity(new
        {
            errors = new Dictionary<string, string[]>
            {
                ["companyId"] = ["Select a company context before creating a subsidiary account."],
            },
        });
        return false;
    }

    private static Dictionary<string, string[]> ValidateFields(
        int controlAccountId,
        string? title)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (controlAccountId <= 0)
        {
            errors["controlAccountId"] = ["Control Account is required."];
        }

        var t = (title ?? string.Empty).Trim();
        if (t.Length == 0)
        {
            errors["subsidiaryAccountTitle"] = ["Subsidiary Account Title is required."];
        }
        else if (t.Length > 150)
        {
            errors["subsidiaryAccountTitle"] =
                ["Subsidiary Account Title must be at most 150 characters."];
        }

        return errors;
    }
}
