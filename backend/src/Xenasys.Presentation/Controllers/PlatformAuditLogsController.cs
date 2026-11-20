using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions;
using Xenasys.Core.Contracts.PlatformAudit;
using Xenasys.Infrastructure.Security;
using Xenasys.Persistence.Data;

namespace Xenasys.Presentation.Controllers;

// ==========================================================================================
// WHAT: Super Admin central PlatformAuditLogs read API (T1.58 / BRD §3.1).
// WHY: One place to review SA Create/Update/Delete across tenants/users/roles/forms.
// EXAMPLE: GET /api/platform-audit-logs?take=50 → newest rows first.
// ==========================================================================================
[ApiController]
[Route("api/platform-audit-logs")]
[Authorize]
public sealed class PlatformAuditLogsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public PlatformAuditLogsController(ApplicationDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [RequirePermission("platform.read")]
    [ProducesResponseType(typeof(IReadOnlyList<PlatformAuditLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<PlatformAuditLogDto>>> List(
        [FromQuery] int take = 100,
        [FromQuery] string? entityType = null,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantProvider.IsSuperAdmin)
        {
            return Forbid();
        }

        var limit = Math.Clamp(take, 1, 500);
        var query = _db.PlatformAuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var term = entityType.Trim();
            query = query.Where(x => x.EntityType == term);
        }

        var rows = await query
            .OrderByDescending(x => x.InsertDate)
            .ThenByDescending(x => x.Id)
            .Take(limit)
            .Select(x => new PlatformAuditLogDto
            {
                Id = x.Id,
                ActorUserId = x.ActorUserId,
                ActorUserName = x.ActorUserName,
                Action = x.Action,
                EntityType = x.EntityType,
                EntityId = x.EntityId,
                CompanyId = x.CompanyId,
                Summary = x.Summary,
                InsertDate = x.InsertDate,
            })
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }
}
