using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions;
using Xenasys.Core.Abstractions.Pagination;
using Xenasys.Core.Contracts.Coa;
using Xenasys.Core.Entities;
using Xenasys.Core.Querying;
using Xenasys.Infrastructure.Security;
using Xenasys.Persistence.Data;
using Xenasys.Persistence.Querying;

namespace Xenasys.Presentation.Controllers;

// ==========================================================================================
// WHAT: Tenant Control Account CRUD — keyset list; Code auto; INV/AP unique per company.
// WHY: BRD §3.2 — TA Full Own Tenant; Masters remain platform-shared; Task 1.8 keyset.
// EXAMPLE: GET /api/coa/controls?afterId&limit → KeysetPageResult
// ==========================================================================================
[ApiController]
[Route("api/coa/controls")]
[Authorize]
public sealed class ControlAccountsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public ControlAccountsController(ApplicationDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [RequirePermission("coa.controls.read")]
    [ProducesResponseType(typeof(KeysetPageResult<ControlAccountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<KeysetPageResult<ControlAccountDto>>> List(
        [FromQuery] int? afterId,
        [FromQuery] int limit = 50,
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? shape = null,
        [FromQuery] int? companyId = null,
        [FromQuery] string? companyName = null,
        [FromQuery] string? code = null,
        [FromQuery] string? title = null,
        [FromQuery] string? controlKind = null,
        [FromQuery] string? masterAccountTitle = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveListCompanyId(companyId, out var companyFilter, out var forbid))
        {
            return forbid!;
        }

        var request = new KeysetPageRequest { AfterId = afterId, Limit = limit };
        var take = request.GetSafeLimit();
        var dataShape = DataShapeParser.Parse(shape, DataShape.List);

        IQueryable<ControlAccount> query = _db.ControlAccounts
            .AsNoTracking()
            .WhereActive(includeInactive)
            .WhereContains(companyName, c => c.Company != null ? c.Company.Name : null)
            .WhereContains(code, c => c.Code)
            .WhereContains(title, c => c.Title)
            .WhereContains(controlKind, c => c.ControlKind)
            .WhereContains(masterAccountTitle, c => c.MasterAccount != null ? c.MasterAccount.Title : null);

        if (companyFilter is int scopedCompanyId)
        {
            query = query.Where(c => c.CompanyId == scopedCompanyId);
        }

        var totalCount = await query.LongCountAsync(cancellationToken);

        if (request.AfterId is int cursor)
        {
            query = query.Where(c => c.Id > cursor);
        }

        var rows = await query
            .OrderBy(c => c.Id)
            .Take(take + 1)
            .Select(ControlAccountProjections.ForShape(dataShape))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > take;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return Ok(new KeysetPageResult<ControlAccountDto>
        {
            Items = rows,
            NextCursor = rows.Count > 0 ? rows[^1].Id : null,
            HasMore = hasMore,
            TotalCount = totalCount,
        });
    }

    [HttpGet("{id:int}")]
    [RequirePermission("coa.controls.read")]
    [ProducesResponseType(typeof(ControlAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ControlAccountDto>> GetById(
        int id,
        [FromQuery] string? shape = null,
        CancellationToken cancellationToken = default)
    {
        var dataShape = DataShapeParser.Parse(shape, DataShape.Detail);

        var row = await _db.ControlAccounts
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(ControlAccountProjections.ForShape(dataShape))
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    [RequirePermission("coa.controls.create")]
    [ProducesResponseType(typeof(ControlAccountDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ControlAccountDto>> Create(
        [FromBody] CreateControlAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveWriteCompanyId(request.CompanyId, out var companyId, out var errorResult))
        {
            return errorResult!;
        }

        var fieldErrors = ValidateFields(request.Title, request.ControlKind, request.MasterAccountId);
        if (fieldErrors.Count > 0)
        {
            return UnprocessableEntity(new { errors = fieldErrors });
        }

        var kind = NormalizeKind(request.ControlKind);
        var title = request.Title.Trim();

        var master = await _db.MasterAccounts
            .FirstOrDefaultAsync(m => m.Id == request.MasterAccountId && m.IsActive, cancellationToken);
        if (master is null)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["masterAccountId"] = ["Master Account is required."],
                },
            });
        }

        if (IsAutomationKind(kind)
            && await _db.ControlAccounts.IgnoreQueryFilters().AnyAsync(
                c => c.CompanyId == companyId && c.ControlKind == kind,
                cancellationToken))
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["controlKind"] =
                    [
                        $"A {kind} control account already exists for this company (unique automation node).",
                    ],
                },
            });
        }

        var code = await AllocateCodeAsync(companyId, master.Prefix, kind, cancellationToken);
        if (code is null)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["code"] = ["Unable to allocate a unique Control Account Code."],
                },
            });
        }

        var entity = new ControlAccount
        {
            CompanyId = companyId,
            MasterAccountId = master.Id,
            Code = code,
            Title = title,
            ControlKind = kind,
            IsActive = true,
        };

        _db.ControlAccounts.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = await ProjectAsync(entity.Id, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, dto);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("coa.controls.update")]
    [ProducesResponseType(typeof(ControlAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ControlAccountDto>> Update(
        int id,
        [FromBody] UpdateControlAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.ControlAccounts.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (!CanMutateCompany(entity.CompanyId))
        {
            return Forbid();
        }

        var fieldErrors = ValidateFields(request.Title, request.ControlKind, request.MasterAccountId);
        if (fieldErrors.Count > 0)
        {
            return UnprocessableEntity(new { errors = fieldErrors });
        }

        var kind = NormalizeKind(request.ControlKind);
        var title = request.Title.Trim();

        var master = await _db.MasterAccounts
            .FirstOrDefaultAsync(m => m.Id == request.MasterAccountId && m.IsActive, cancellationToken);
        if (master is null)
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["masterAccountId"] = ["Master Account is required."],
                },
            });
        }

        if (!string.Equals(entity.ControlKind, kind, StringComparison.Ordinal)
            && IsAutomationKind(kind)
            && await _db.ControlAccounts.IgnoreQueryFilters().AnyAsync(
                c => c.CompanyId == entity.CompanyId && c.ControlKind == kind && c.Id != id,
                cancellationToken))
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["controlKind"] =
                    [
                        $"A {kind} control account already exists for this company (unique automation node).",
                    ],
                },
            });
        }

        if (IsAutomationKind(entity.ControlKind)
            && !string.Equals(entity.ControlKind, kind, StringComparison.Ordinal))
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["controlKind"] = ["Automation control kinds (INVENTORY / ACCOUNTS_PAYABLE) cannot change kind."],
                },
            });
        }

        if (request.IsActive is false && IsAutomationKind(entity.ControlKind))
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["isActive"] = ["Inventory and Accounts Payable control accounts cannot be deactivated."],
                },
            });
        }

        entity.MasterAccountId = master.Id;
        entity.Title = title;
        entity.ControlKind = kind;
        if (request.IsActive is bool active)
        {
            entity.IsActive = active;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await ProjectAsync(id, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("coa.controls.delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.ControlAccounts.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (!CanMutateCompany(entity.CompanyId))
        {
            return Forbid();
        }

        if (IsAutomationKind(entity.ControlKind))
        {
            return Conflict(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["controlKind"] = ["Inventory and Accounts Payable control accounts cannot be deleted."],
                },
            });
        }

        entity.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<ControlAccountDto> ProjectAsync(int id, CancellationToken cancellationToken) =>
        await _db.ControlAccounts
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(ControlAccountProjections.Detail)
            .FirstAsync(cancellationToken);

    private async Task<string?> AllocateCodeAsync(
        int companyId,
        string masterPrefix,
        string kind,
        CancellationToken cancellationToken)
    {
        var prefix = (masterPrefix ?? string.Empty).Trim().ToUpperInvariant();
        if (prefix.Length == 0)
        {
            prefix = "CTL";
        }

        string candidate = kind switch
        {
            ControlAccountKinds.Inventory => $"{prefix}-CTL-INV",
            ControlAccountKinds.AccountsPayable => $"{prefix}-CTL-AP",
            _ => string.Empty,
        };

        if (candidate.Length > 0)
        {
            if (await _db.ControlAccounts.IgnoreQueryFilters().AnyAsync(
                    c => c.CompanyId == companyId && c.Code == candidate,
                    cancellationToken))
            {
                return null;
            }

            return candidate.Length <= 40 ? candidate : candidate[..40];
        }

        for (var i = 1; i <= 9999; i++)
        {
            candidate = $"{prefix}-CTL-{i:D4}";
            if (candidate.Length > 40)
            {
                candidate = candidate[..40];
            }

            if (!await _db.ControlAccounts.IgnoreQueryFilters().AnyAsync(
                    c => c.CompanyId == companyId && c.Code == candidate,
                    cancellationToken))
            {
                return candidate;
            }
        }

        return null;
    }

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
            companyFilter = companyId;
            return true;
        }

        forbid = Forbid();
        return false;
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
                    ["companyId"] = ["Select a company before creating a control account."],
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
                ["companyId"] = ["Select a company context before creating a control account."],
            },
        });
        return false;
    }

    private bool CanMutateCompany(int companyId) =>
        _tenantProvider.IsSuperAdmin
        || (_tenantProvider.TenantId is int tid && tid == companyId);

    private static bool IsAutomationKind(string kind) =>
        kind is ControlAccountKinds.Inventory or ControlAccountKinds.AccountsPayable;

    private static string NormalizeKind(string? raw)
    {
        var k = (raw ?? string.Empty).Trim().ToUpperInvariant().Replace(' ', '_');
        return k switch
        {
            "INVENTORY" => ControlAccountKinds.Inventory,
            "ACCOUNTS_PAYABLE" or "ACCOUNTSPAYABLE" or "AP" => ControlAccountKinds.AccountsPayable,
            "OTHER" => ControlAccountKinds.Other,
            _ => k,
        };
    }

    private static Dictionary<string, string[]> ValidateFields(string? title, string? controlKind, int masterAccountId)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (masterAccountId <= 0)
        {
            errors["masterAccountId"] = ["Master Account is required."];
        }

        var t = (title ?? string.Empty).Trim();
        if (t.Length == 0)
        {
            errors["title"] = ["Control Account Title is required."];
        }
        else if (t.Length > 100)
        {
            errors["title"] = ["Control Account Title must be at most 100 characters."];
        }

        var kind = NormalizeKind(controlKind);
        if (kind.Length == 0)
        {
            errors["controlKind"] = ["Control Kind is required."];
        }
        else if (kind is not (
            ControlAccountKinds.Inventory
            or ControlAccountKinds.AccountsPayable
            or ControlAccountKinds.Other))
        {
            errors["controlKind"] = ["Control Kind must be INVENTORY, ACCOUNTS_PAYABLE, or OTHER."];
        }

        return errors;
    }
}
