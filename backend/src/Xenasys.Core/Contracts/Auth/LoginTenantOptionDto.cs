namespace Xenasys.Core.Contracts.Auth;

// ==========================================================================================
// WHAT: Public login dropdown row — company display name + id + tenant code.
// WHY: Anonymous GET /api/auth/companies needs ids the SPA submits as company_id.
// EXAMPLE: { Id: 3, Name: "Acme Corp", TenantCode: "ACME01" }.
// ==========================================================================================
public sealed class LoginTenantOptionDto
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string TenantCode { get; init; } = string.Empty;
}
