using Xenasys.Core.Contracts.Auth;

namespace Xenasys.Core.Abstractions.Auth;

// ==========================================================================================
// WHAT: Authentication use-case port — login + anonymous company list for the SPA dropdown.
// WHY: Presentation depends on this contract so BCrypt/JWT/membership rules stay in Infrastructure.
// EXAMPLE: LoginAsync → LoginSucceeded | LoginCompanySelectionRequired | 401/422 outcomes.
// ==========================================================================================
public interface IAuthService
{
    Task<LoginOutcome> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LoginTenantOptionDto>> ListLoginTenantsAsync(
        CancellationToken cancellationToken = default);
}

public abstract record LoginOutcome;

public sealed record LoginSucceeded(LoginResponseDto Response) : LoginOutcome;

// ==========================================================================================
// WHAT: Credentials verified; client must re-post login with company_id before JWT is issued.
// WHY: Multi-company users must choose a tenant — JWT tenant_id cannot be ambiguous.
// EXAMPLE: User in Acme + Beta without company_id → RequiresCompanySelection + Companies list.
// ==========================================================================================
public sealed record LoginCompanySelectionRequired(LoginCompanyChallengeDto Challenge) : LoginOutcome;

public sealed record LoginValidationFailed(
    IReadOnlyDictionary<string, string[]> Errors) : LoginOutcome;

// ==========================================================================================
// WHAT: Opaque auth failure for unknown user, inactive, frozen tenant, or bad password.
// WHY: Map to HTTP 401 without leaking which check failed (credential enumeration resistance).
// EXAMPLE: Wrong password or inactive user → LoginUnauthorized → INVALID_CREDENTIALS.
// ==========================================================================================
public sealed record LoginUnauthorized : LoginOutcome;
