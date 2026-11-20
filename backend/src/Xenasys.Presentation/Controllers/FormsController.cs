using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions;
using Xenasys.Core.Contracts.Forms;
using Xenasys.Core.Contracts.Permissions;
using Xenasys.Core.Entities;
using Xenasys.Core.Querying;
using Xenasys.Core.Time;
using Xenasys.Infrastructure.Security;
using Xenasys.Persistence.Data;
using Xenasys.Persistence.Querying;
using Xenasys.Persistence.Seeding;

namespace Xenasys.Presentation.Controllers;

// ==========================================================================================
// WHAT: Forms CRUD — Name, Path, IsActive (navigation catalog for permission-management).
// WHY: Access via forms.read / forms.manage (role permission codes — not TenantAdmin-only).
// EXAMPLE: POST /api/forms { name: "Roles", path: "/settings/roles" }.
// ==========================================================================================
[ApiController]
[Route("api/forms")]
[Authorize]
public sealed class FormsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public FormsController(ApplicationDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [RequirePermission("forms.read")]
    [ProducesResponseType(typeof(IReadOnlyList<FormDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FormDto>>> List(
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? shape = null,
        [FromQuery] string? name = null,
        [FromQuery] string? path = null,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantProvider.IsSuperAdmin)
        {
            return Forbid();
        }

        var dataShape = DataShapeParser.Parse(shape, DataShape.List);
        var rows = await _db.Forms.AsNoTracking()
            .WhereActive(includeInactive)
            .WhereContains(name, f => f.Name)
            .WhereContains(path, f => f.Path)
            .OrderBy(f => f.Name)
            .Select(FormProjections.ForShape(dataShape))
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    /// <summary>Active Forms with their FormActions catalog (for Roles Form×Action matrix).</summary>
    [HttpGet("action-catalog")]
    [RequirePermission("roles.read")]
    [ProducesResponseType(typeof(IReadOnlyList<FormWithActionsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FormWithActionsDto>>> ActionCatalog(
        CancellationToken cancellationToken)
    {
        var rows = await (
                from fa in _db.FormActions.AsNoTracking()
                join f in _db.Forms.AsNoTracking() on fa.FormId equals f.Id
                join a in _db.Actions.AsNoTracking() on fa.ActionId equals a.Id
                where fa.IsActive && f.IsActive && a.IsActive
                select new { f.Id, f.Path, f.Name, ActionId = a.Id, ActionName = a.Name })
            .ToListAsync(cancellationToken);

        if (!_tenantProvider.IsSuperAdmin)
        {
            rows = rows
                .Where(r => !FormActionRbacSeed.PlatformAdminOnlyFormPaths.Contains(r.Path))
                .ToList();
        }

        IReadOnlyList<FormWithActionsDto> result = rows
            .GroupBy(r => new { r.Id, r.Path, r.Name })
            .OrderBy(g => g.Key.Name)
            .Select(g => new FormWithActionsDto
            {
                Id = g.Key.Id,
                Path = g.Key.Path,
                Name = g.Key.Name,
                Actions = g
                    .Select(x => new FormActionRefDto { Id = x.ActionId, Name = x.ActionName })
                    .DistinctBy(a => a.Id)
                    .OrderBy(a => a.Name)
                    .ToList(),
            })
            .ToList();

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("forms.read")]
    [ProducesResponseType(typeof(FormDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FormDto>> GetById(
        int id,
        [FromQuery] string? shape = null,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantProvider.IsSuperAdmin)
        {
            return Forbid();
        }

        var dataShape = DataShapeParser.Parse(shape, DataShape.Detail);
        var row = await _db.Forms.AsNoTracking()
            .Where(f => f.Id == id)
            .Select(FormProjections.ForShape(dataShape))
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    [RequirePermission("forms.manage")]
    [ProducesResponseType(typeof(FormDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<FormDto>> Create(
        [FromBody] CreateFormRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantProvider.IsSuperAdmin)
        {
            return Forbid();
        }

        var name = (request.Name ?? string.Empty).Trim();
        var path = NormalizePath(request.Path);
        var errors = Validate(name, path);

        if (errors.Count == 0
            && await _db.Forms.AsNoTracking()
                .AnyAsync(f => f.IsActive && f.Path == path, cancellationToken))
        {
            errors["path"] = ["An active form with this path already exists."];
        }

        if (errors.Count > 0)
        {
            return UnprocessableEntity(new { errors });
        }

        var entity = new AppForm
        {
            Name = name,
            Path = path,
            IsActive = true,
        };

        _db.Forms.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = entity.Id },
            ToDto(entity));
    }

    [HttpPut("{id:int}")]
    [RequirePermission("forms.manage")]
    [ProducesResponseType(typeof(FormDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<FormDto>> Update(
        int id,
        [FromBody] UpdateFormRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantProvider.IsSuperAdmin)
        {
            return Forbid();
        }

        var entity = await _db.Forms.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var name = (request.Name ?? string.Empty).Trim();
        var path = NormalizePath(request.Path);
        var errors = Validate(name, path);

        if (errors.Count == 0
            && await _db.Forms.AsNoTracking()
                .AnyAsync(f => f.Id != id && f.IsActive && f.Path == path, cancellationToken))
        {
            errors["path"] = ["An active form with this path already exists."];
        }

        if (errors.Count > 0)
        {
            return UnprocessableEntity(new { errors });
        }

        entity.Name = name;
        entity.Path = path;
        entity.IsActive = request.IsActive;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("forms.manage")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (!_tenantProvider.IsSuperAdmin)
        {
            return Forbid();
        }

        var entity = await _db.Forms.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
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

    /// <summary>Lists all Actions with isAssigned for this Form (FormActions catalog).</summary>
    [HttpGet("{id:int}/actions")]
    [RequirePermission("forms.read")]
    [ProducesResponseType(typeof(IReadOnlyList<FormActionAssignmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<FormActionAssignmentDto>>> ListActions(
        int id,
        CancellationToken cancellationToken)
    {
        if (!_tenantProvider.IsSuperAdmin)
        {
            return Forbid();
        }

        if (!await _db.Forms.AsNoTracking().AnyAsync(f => f.Id == id, cancellationToken))
        {
            return NotFound();
        }

        var assigned = await _db.FormActions.AsNoTracking()
            .Where(fa => fa.FormId == id && fa.IsActive)
            .Select(fa => fa.ActionId)
            .ToListAsync(cancellationToken);
        var assignedSet = assigned.ToHashSet();

        var rows = (await _db.Actions.AsNoTracking()
                .Where(a => a.IsActive)
                .OrderBy(a => a.Name)
                .Select(a => new { a.Id, a.Name })
                .ToListAsync(cancellationToken))
            .Select(a => new FormActionAssignmentDto
            {
                ActionId = a.Id,
                ActionName = a.Name,
                IsAssigned = assignedSet.Contains(a.Id),
            })
            .ToList();

        return Ok(rows);
    }

    /// <summary>Sync FormActions catalog for a form (replace active set).</summary>
    [HttpPut("{id:int}/actions")]
    [RequirePermission("forms.manage")]
    [ProducesResponseType(typeof(IReadOnlyList<FormActionAssignmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<FormActionAssignmentDto>>> SyncActions(
        int id,
        [FromBody] SyncFormActionsRequest request,
        CancellationToken cancellationToken)
    {
        var form = await _db.Forms.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (form is null)
        {
            return NotFound();
        }

        var desiredIds = (request.ActionIds ?? [])
            .Where(x => x > 0)
            .Distinct()
            .ToHashSet();

        var validIds = await _db.Actions.AsNoTracking()
            .Where(a => a.IsActive && desiredIds.Contains(a.Id))
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);
        var validSet = validIds.ToHashSet();

        var existing = await _db.FormActions
            .Where(fa => fa.FormId == id)
            .ToListAsync(cancellationToken);
        var now = AuditClock.Now;

        foreach (var row in existing)
        {
            if (validSet.Contains(row.ActionId))
            {
                if (!row.IsActive)
                {
                    row.IsActive = true;
                    row.UpdateDate = now;
                }

                validSet.Remove(row.ActionId);
            }
            else if (row.IsActive)
            {
                row.IsActive = false;
                row.UpdateDate = now;
            }
        }

        foreach (var actionId in validSet)
        {
            _db.FormActions.Add(new FormAction
            {
                FormId = id,
                ActionId = actionId,
                IsActive = true,
                InsertDate = now,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await ListActions(id, cancellationToken);
    }

    private static FormDto ToDto(AppForm f) =>
        new() { Id = f.Id, Name = f.Name, Path = f.Path, IsActive = f.IsActive };

    private static string NormalizePath(string? raw)
    {
        var path = (raw ?? string.Empty).Trim();
        if (path.Length == 0)
        {
            return path;
        }

        path = path.Replace('\\', '/');
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        if (path.Length > 1 && path.EndsWith('/'))
        {
            path = path.TrimEnd('/');
        }

        return path;
    }

    private static Dictionary<string, string[]> Validate(string name, string path)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(name) || name.Length < 2 || name.Length > 100)
        {
            errors["name"] = ["Form name is required (2–100 characters)."];
        }
        else if (ContainsDangerousMarkup(name))
        {
            errors["name"] = ["HTML tags and script content are not allowed."];
        }

        if (string.IsNullOrWhiteSpace(path) || path.Length > 200)
        {
            errors["path"] = ["Form path is required (max 200 characters)."];
        }
        else if (!path.StartsWith('/'))
        {
            errors["path"] = ["Form path must start with '/'."];
        }
        else if (ContainsDangerousMarkup(path) || path.Contains(' ', StringComparison.Ordinal))
        {
            errors["path"] = ["Form path cannot contain spaces or markup."];
        }

        return errors;
    }

    private static bool ContainsDangerousMarkup(string value) =>
        value.Contains('<') || value.Contains('>') ||
        value.Contains("javascript:", StringComparison.OrdinalIgnoreCase);
}
