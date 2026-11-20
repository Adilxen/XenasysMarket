namespace Xenasys.Core.Abstractions.Auth;

// ==========================================================================================
// WHAT: Port for creating signed JWTs bound to user, tenant, and role.
// WHY: AuthService issues tokens via this abstraction; claim names stay tenant_id SSoT.
// EXAMPLE: CreateToken(userId: 1, tenantId: 5, role: "TENANT_ADMIN", userName: "alice").
// ==========================================================================================
public interface IJwtTokenService
{
    string CreateToken(int userId, int tenantId, string role, string userName);
}
