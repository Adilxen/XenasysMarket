using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions.Validation;
using Xenasys.Persistence.Data;

namespace Xenasys.Persistence.Validation;

// ==========================================================================================
// WHAT: EF-backed checks that dropdown / FK ids point at active rows.
// WHY: Rejects inactive or missing PKs before writes (login company, catalog FKs).
// EXAMPLE: companyId=1 active → true; inactive/missing → false / InvalidOperationException.
// ==========================================================================================
public sealed class ActiveForeignKeyValidator : IActiveForeignKeyValidator
{
    private readonly ApplicationDbContext _db;

    public ActiveForeignKeyValidator(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<bool> IsActiveTenantAsync(int companyId, CancellationToken cancellationToken = default)
    {
        if (companyId <= 0)
        {
            return Task.FromResult(false);
        }

        return _db.Tenants.AsNoTracking()
            .AnyAsync(
                t => t.Id == companyId && t.IsActive,
                cancellationToken);
    }

    public async Task EnsureActiveTenantAsync(int companyId, CancellationToken cancellationToken = default)
    {
        if (!await IsActiveTenantAsync(companyId, cancellationToken))
        {
            throw new InvalidOperationException(
                $"company_id {companyId} is missing or inactive.");
        }
    }
}
