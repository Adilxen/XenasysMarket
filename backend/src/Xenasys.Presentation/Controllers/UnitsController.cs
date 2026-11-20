using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Contracts.Catalog;
using Xenasys.Core.Querying;
using Xenasys.Infrastructure.Security;
using Xenasys.Persistence.Data;

namespace Xenasys.Presentation.Controllers;

// ==========================================================================================
// WHAT: Platform Units read API (system UoM — not tenant-scoped).
// WHY: M2.6 — seed Pcs/Kg/Ltr/Box/Pack; Product dropdowns consume this list.
// EXAMPLE: GET /api/units → [{ code: "Pcs", name: "Pieces", … }].
// ==========================================================================================
[ApiController]
[Route("api/units")]
[Authorize]
public sealed class UnitsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public UnitsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [RequirePermission("categories.read")]
    [ProducesResponseType(typeof(IReadOnlyList<UnitDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UnitDto>>> List(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Units.AsNoTracking().WhereActive(includeInactive);

        var rows = await query
            .OrderBy(u => u.Id)
            .Select(u => new UnitDto
            {
                Id = u.Id,
                Code = u.Code,
                Name = u.Name,
                IsActive = u.IsActive,
            })
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }
}
