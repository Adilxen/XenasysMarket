using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions.Auth;
using Xenasys.Core.Entities;
using Xenasys.Persistence.Data;

namespace Xenasys.Persistence.Repositories;

// ==========================================================================================
// WHAT: EF-backed IAuthRepository for login user/tenant reads.
// WHY: Keeps auth queries in Persistence so Infrastructure AuthService stays DB-agnostic.
// EXAMPLE: GetByUserNameWithMembershipsAsync("admin") → User + memberships + company + role.
// ==========================================================================================
public sealed class AuthRepository : IAuthRepository
{
    private readonly ApplicationDbContext _db;

    public AuthRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    // ==========================================================================================
    // WHAT: Loads a user by UserName including memberships, company, and role in one query.
    // WHY: Login needs role/tenant context without N+1 roundtrips.
    // EXAMPLE: Include Memberships → ThenInclude Company/Role, FirstOrDefault by UserName.
    // ==========================================================================================
    public async Task<User?> GetByUserNameWithMembershipsAsync(
        string userName,
        CancellationToken cancellationToken = default)
    {
        // Single roundtrip: user + memberships + tenant + role
        return await _db.Users
            .AsNoTracking()
            .Include(u => u.Memberships)
                .ThenInclude(m => m.Company)
            .Include(u => u.Memberships)
                .ThenInclude(m => m.Role)
            .FirstOrDefaultAsync(
                u => u.UserName == userName,
                cancellationToken);
    }

    public Task<Tenant?> GetActiveTenantByIdAsync(
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        return _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.Id == tenantId && t.IsActive,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Tenant>> ListActiveTenantsForLoginAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.Tenants
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }
}
