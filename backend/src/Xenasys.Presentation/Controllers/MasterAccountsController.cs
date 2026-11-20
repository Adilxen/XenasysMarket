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
// WHAT: Platform Master Account CRUD — keyset list + shape projections.
// WHY: BRD Task 1.1 / 1.8 — tenants GET only; Super Admin create/update/soft-delete.
// EXAMPLE: GET /api/coa/masters?afterId&limit → KeysetPageResult
// ==========================================================================================
[ApiController]
[Route("api/coa/masters")]
[Authorize]
public sealed class MasterAccountsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public MasterAccountsController(ApplicationDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [RequirePermission("coa.masters.read")]
    [ProducesResponseType(typeof(KeysetPageResult<MasterAccountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<KeysetPageResult<MasterAccountDto>>> List(
        [FromQuery] int? afterId,
        [FromQuery] int limit = 50,
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? shape = null,
        [FromQuery] string? code = null,
        [FromQuery] string? title = null,
        [FromQuery] string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        var request = new KeysetPageRequest { AfterId = afterId, Limit = limit };
        var take = request.GetSafeLimit();
        var dataShape = DataShapeParser.Parse(shape, DataShape.List);

        var query = _db.MasterAccounts
            .AsNoTracking()
            .WhereActive(includeInactive)
            .WhereContains(code, m => m.Code)
            .WhereContains(title, m => m.Title)
            .WhereContains(prefix, m => m.Prefix);

        var totalCount = await query.LongCountAsync(cancellationToken);

        if (request.AfterId is int cursor)
        {
            query = query.Where(m => m.Id > cursor);
        }

        var rows = await query
            .OrderBy(m => m.Id)
            .Take(take + 1)
            .Select(MasterAccountProjections.ForShape(dataShape))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > take;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return Ok(new KeysetPageResult<MasterAccountDto>
        {
            Items = rows,
            NextCursor = rows.Count > 0 ? rows[^1].Id : null,
            HasMore = hasMore,
            TotalCount = totalCount,
        });
    }

    [HttpGet("{id:int}")]
    [RequirePermission("coa.masters.read")]
    [ProducesResponseType(typeof(MasterAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MasterAccountDto>> GetById(
        int id,
        [FromQuery] string? shape = null,
        CancellationToken cancellationToken = default)
    {
        var dataShape = DataShapeParser.Parse(shape, DataShape.Detail);

        var row = await _db.MasterAccounts
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(MasterAccountProjections.ForShape(dataShape))
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    [RequirePermission("coa.masters.create")]
    [ProducesResponseType(typeof(MasterAccountDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MasterAccountDto>> Create(
        [FromBody] CreateMasterAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantProvider.IsSuperAdmin)
        {
            return Forbid();
        }

        var errors = ValidateFields(request.Code, request.Title, request.Prefix);
        if (errors.Count > 0)
        {
            return UnprocessableEntity(new { errors });
        }

        var code = request.Code.Trim();
        var title = request.Title.Trim();
        var prefix = request.Prefix.Trim().ToUpperInvariant();

        if (await _db.MasterAccounts.AnyAsync(m => m.Code == code, cancellationToken))
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["code"] = ["Master Account Code must be unique."],
                },
            });
        }

        if (await _db.MasterAccounts.AnyAsync(m => m.Prefix == prefix, cancellationToken))
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["prefix"] = ["Prefix must be unique."],
                },
            });
        }

        var entity = new MasterAccount
        {
            Code = code,
            Title = title,
            Prefix = prefix,
            IsActive = true,
        };

        _db.MasterAccounts.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = await _db.MasterAccounts
            .AsNoTracking()
            .Where(m => m.Id == entity.Id)
            .Select(MasterAccountProjections.Detail)
            .FirstAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, dto);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("coa.masters.update")]
    [ProducesResponseType(typeof(MasterAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MasterAccountDto>> Update(
        int id,
        [FromBody] UpdateMasterAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantProvider.IsSuperAdmin)
        {
            return Forbid();
        }

        var entity = await _db.MasterAccounts.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var errors = ValidateFields(request.Code, request.Title, request.Prefix);
        if (errors.Count > 0)
        {
            return UnprocessableEntity(new { errors });
        }

        var code = request.Code.Trim();
        var title = request.Title.Trim();
        var prefix = request.Prefix.Trim().ToUpperInvariant();

        if (await _db.MasterAccounts.AnyAsync(m => m.Code == code && m.Id != id, cancellationToken))
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["code"] = ["Master Account Code must be unique."],
                },
            });
        }

        if (await _db.MasterAccounts.AnyAsync(m => m.Prefix == prefix && m.Id != id, cancellationToken))
        {
            return UnprocessableEntity(new
            {
                errors = new Dictionary<string, string[]>
                {
                    ["prefix"] = ["Prefix must be unique."],
                },
            });
        }

        entity.Code = code;
        entity.Title = title;
        entity.Prefix = prefix;
        if (request.IsActive is bool active)
        {
            entity.IsActive = active;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var dto = await _db.MasterAccounts
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(MasterAccountProjections.Detail)
            .FirstAsync(cancellationToken);

        return Ok(dto);
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("coa.masters.delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        if (!_tenantProvider.IsSuperAdmin)
        {
            return Forbid();
        }

        var entity = await _db.MasterAccounts.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        entity.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static Dictionary<string, string[]> ValidateFields(string? code, string? title, string? prefix)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        var c = (code ?? string.Empty).Trim();
        if (c.Length == 0)
        {
            errors["code"] = ["Master Account Code is required."];
        }
        else if (!c.All(char.IsDigit))
        {
            errors["code"] = ["Master Account Code must be numeric only."];
        }
        else if (c.Length < 4)
        {
            errors["code"] = ["Master Account Code must be at least 4 digits."];
        }
        else if (c.Length > 20)
        {
            errors["code"] = ["Master Account Code must be at most 20 characters."];
        }

        var t = (title ?? string.Empty).Trim();
        if (t.Length == 0)
        {
            errors["title"] = ["Master Account Title is required."];
        }
        else if (t.Length > 100)
        {
            errors["title"] = ["Master Account Title must be at most 100 characters."];
        }

        var p = (prefix ?? string.Empty).Trim();
        if (p.Length == 0)
        {
            errors["prefix"] = ["Prefix is required."];
        }
        else if (p.Length > 5)
        {
            errors["prefix"] = ["Prefix must be at most 5 characters."];
        }
        else if (!p.All(ch => char.IsLetterOrDigit(ch)))
        {
            errors["prefix"] = ["Prefix must be alphanumeric."];
        }

        return errors;
    }
}
