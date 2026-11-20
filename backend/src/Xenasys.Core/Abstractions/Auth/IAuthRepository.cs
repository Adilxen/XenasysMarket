using Xenasys.Core.Entities;

namespace Xenasys.Core.Abstractions.Auth;

// ==========================================================================================
// WHAT: Auth read-model queries — user+memberships graph and active tenants for login.
// WHY: Keeps persistence details out of AuthService while supporting single-roundtrip login loads.
// EXAMPLE: GetByUserNameWithMembershipsAsync("jdoe") → User with Memberships→Company/Role.
// ==========================================================================================
public interface IAuthRepository
{
    /// <summary>
    /// Loads user by exact username with Memberships → Company and Memberships → Role.
    /// </summary>
    Task<User?> GetByUserNameWithMembershipsAsync(
        string userName,
        CancellationToken cancellationToken = default);

    Task<Tenant?> GetActiveTenantByIdAsync(
        int tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Active tenants for anonymous login company dropdown.</summary>
    Task<IReadOnlyList<Tenant>> ListActiveTenantsForLoginAsync(
        CancellationToken cancellationToken = default);
}
