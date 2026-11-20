using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions;
using Xenasys.Core.Contracts.Actions;
using Xenasys.Core.Entities;
using Xenasys.Core.Querying;
using Xenasys.Infrastructure.Security;
using Xenasys.Persistence.Data;
using Xenasys.Persistence.Querying;

namespace Xenasys.Presentation.Controllers;

// ==========================================================================================
// WHAT: Actions CRUD — Name + IsActive (global catalog).
// WHY: Platform catalog — Super Admin only (Tenant Admin uses Roles Form×Action only).
// EXAMPLE: POST /api/actions { name: "Export" }.
// ==========================================================================================
[ApiController]
[Route("api/actions")]
[Authorize]
public sealed class ActionsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public ActionsController(ApplicationDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    private ActionResult? ForbidUnlessSuperAdmin() =>
        _tenantProvider.IsSuperAdmin ? null : Forbid();

    [HttpGet]
    [RequirePermission("actions.read")]
    [ProducesResponseType(typeof(IReadOnlyList<ActionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ActionDto>>> List(
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? shape = null,
        [FromQuery] string? name = null,
        CancellationToken cancellationToken = default)
    {
        if (ForbidUnlessSuperAdmin() is { } denied)
        {
            return denied;
        }

        var dataShape = DataShapeParser.Parse(shape, DataShape.List);
        var rows = await _db.Actions.AsNoTracking()
            .WhereActive(includeInactive)
            .WhereContains(name, a => a.Name)
            .OrderBy(a => a.Name)
            .Select(ActionProjections.ForShape(dataShape))
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("actions.read")]
    [ProducesResponseType(typeof(ActionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActionDto>> GetById(
        int id,
        [FromQuery] string? shape = null,
        CancellationToken cancellationToken = default)
    {
        if (ForbidUnlessSuperAdmin() is { } denied)
        {
            return denied;
        }

        var dataShape = DataShapeParser.Parse(shape, DataShape.Detail);
        var row = await _db.Actions.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(ActionProjections.ForShape(dataShape))
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    [RequirePermission("actions.manage")]
    [ProducesResponseType(typeof(ActionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ActionDto>> Create(
        [FromBody] CreateActionRequest request,
        CancellationToken cancellationToken)
    {
        if (ForbidUnlessSuperAdmin() is { } denied)
        {
            return denied;
        }

        var name = (request.Name ?? string.Empty).Trim();
        var errors = ValidateName(name);

        if (errors.Count == 0
            && await _db.Actions.AsNoTracking()
                .AnyAsync(a => a.IsActive && a.Name == name, cancellationToken))
        {
            errors["name"] = ["An active action with this name already exists."];
        }

        if (errors.Count > 0)
        {
            return UnprocessableEntity(new { errors });
        }

        var entity = new ActionItem
        {
            Name = name,
            IsActive = true,
        };

        _db.Actions.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = entity.Id },
            new ActionDto { Id = entity.Id, Name = entity.Name, IsActive = entity.IsActive });
    }

    [HttpPut("{id:int}")]
    [RequirePermission("actions.manage")]
    [ProducesResponseType(typeof(ActionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ActionDto>> Update(
        int id,
        [FromBody] UpdateActionRequest request,
        CancellationToken cancellationToken)
    {
        if (ForbidUnlessSuperAdmin() is { } denied)
        {
            return denied;
        }

        var entity = await _db.Actions.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var name = (request.Name ?? string.Empty).Trim();
        var errors = ValidateName(name);

        if (errors.Count == 0
            && await _db.Actions.AsNoTracking()
                .AnyAsync(a => a.Id != id && a.IsActive && a.Name == name, cancellationToken))
        {
            errors["name"] = ["An active action with this name already exists."];
        }

        if (errors.Count > 0)
        {
            return UnprocessableEntity(new { errors });
        }

        entity.Name = name;
        entity.IsActive = request.IsActive;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new ActionDto { Id = entity.Id, Name = entity.Name, IsActive = entity.IsActive });
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("actions.manage")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (ForbidUnlessSuperAdmin() is { } denied)
        {
            return denied;
        }

        var entity = await _db.Actions.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (!entity.IsActive)
        {
            return NoContent();
        }

        entity.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static Dictionary<string, string[]> ValidateName(string name)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(name) || name.Length < 2 || name.Length > 50)
        {
            errors["name"] = ["Action name is required (2–50 characters)."];
        }
        else if (name.Contains('<') || name.Contains('>')
                 || name.Contains("javascript:", StringComparison.OrdinalIgnoreCase))
        {
            errors["name"] = ["HTML tags and script content are not allowed."];
        }

        return errors;
    }
}
