using Xenasys.Core.Abstractions;
using Xenasys.Core.Abstractions.Auth;
using Xenasys.Core.Abstractions.Validation;
using Xenasys.Core.Contracts.Auth;
using Xenasys.Core.Entities;
using Xenasys.Core.Security;
using Xenasys.Infrastructure.Tenancy;

namespace Xenasys.Infrastructure.Auth;

// ==========================================================================================
// WHAT: Login orchestration — validate input, BCrypt-verify, resolve membership, issue JWT.
// WHY: Centralizes multi-company challenge and keeps password check before membership disclosure.
// EXAMPLE: Valid password + one company → LoginSucceeded; two companies without company_id → challenge.
// ==========================================================================================
public sealed class AuthService : IAuthService
{
    private readonly IAuthRepository _authRepository;
    private readonly IPasswordVerifier _passwordVerifier;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IActiveForeignKeyValidator _activeForeignKeyValidator;

    public AuthService(
        IAuthRepository authRepository,
        IPasswordVerifier passwordVerifier,
        IJwtTokenService jwtTokenService,
        IActiveForeignKeyValidator activeForeignKeyValidator)
    {
        _authRepository = authRepository;
        _passwordVerifier = passwordVerifier;
        _jwtTokenService = jwtTokenService;
        _activeForeignKeyValidator = activeForeignKeyValidator;
    }

    // ==========================================================================================
    // WHAT: Lists active tenants for the anonymous login company dropdown.
    // WHY: SPA needs display names + ids to submit company_id on multi-company login.
    // EXAMPLE: Returns [{ Id: 1, Name: "Acme", TenantCode: "ACME01" }, ...].
    // ==========================================================================================
    public async Task<IReadOnlyList<LoginTenantOptionDto>> ListLoginTenantsAsync(
        CancellationToken cancellationToken = default)
    {
        var tenants = await _authRepository.ListActiveTenantsForLoginAsync(cancellationToken);
        return tenants
            .Select(t => new LoginTenantOptionDto
            {
                Id = t.Id,
                Name = t.Name,
                TenantCode = t.TenantCode,
            })
            .ToList();
    }

    // ==========================================================================================
    // WHAT: Authenticates username/password, binds company, returns token or multi-company challenge.
    // WHY: BCrypt must succeed before revealing memberships; JWT tenant_id is isolation after login.
    // EXAMPLE: Multi-member user omits company_id → LoginCompanySelectionRequired; with company_id → JWT.
    // ==========================================================================================
    public async Task<LoginOutcome> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var username = (request.Username ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(username))
        {
            errors["userName"] = ["User Name is mandatory."];
        }
        else if (username.Length > 50)
        {
            errors["userName"] = ["User Name must be at most 50 characters."];
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors["password"] = ["Password is mandatory."];
        }
        else if (request.Password.Length < 8)
        {
            errors["password"] = ["Password must be at least 8 characters."];
        }

        if (errors.Count > 0)
        {
            return new LoginValidationFailed(errors);
        }

        var user = await _authRepository.GetByUserNameWithMembershipsAsync(
            username,
            cancellationToken);

        if (user is null || !user.IsActive)
        {
            return new LoginUnauthorized();
        }

        // Always verify credentials before revealing membership graph.
        if (!_passwordVerifier.Verify(request.Password, user.PasswordHash))
        {
            return new LoginUnauthorized();
        }

        if (request.CompanyId is > 0
            && !await _activeForeignKeyValidator.IsActiveTenantAsync(
                request.CompanyId.Value,
                cancellationToken))
        {
            return new LoginValidationFailed(new Dictionary<string, string[]>
            {
                [TenancyKeyNames.CompanyId] =
                [
                    "Selected company was not found or is inactive.",
                ],
            });
        }

        var membershipResolution = await ResolveMembershipAsync(
            user,
            request.CompanyId,
            cancellationToken);

        if (membershipResolution.Challenge is not null)
        {
            return new LoginCompanySelectionRequired(membershipResolution.Challenge);
        }

        if (membershipResolution.Errors is not null)
        {
            return new LoginValidationFailed(membershipResolution.Errors);
        }

        if (membershipResolution.Unauthorized)
        {
            return new LoginUnauthorized();
        }

        var binding = membershipResolution.Binding!;
        var token = _jwtTokenService.CreateToken(
            user.Id,
            binding.TenantId,
            binding.RoleCode,
            user.UserName);

        return new LoginSucceeded(new LoginResponseDto
        {
            Token = token,
            Username = user.UserName,
            UserId = user.Id,
            TenantId = binding.TenantId,
            CompanyName = binding.CompanyName,
            Role = binding.RoleCode,
            DefaultRoute = binding.DefaultRoute,
        });
    }

    private async Task<MembershipResolution> ResolveMembershipAsync(
        User user,
        int? companyId,
        CancellationToken cancellationToken)
    {
        var active = user.Memberships
            .Where(m => m.IsActive)
            .ToList();

        if (active.Count == 0)
        {
            return MembershipResolution.Fail(new Dictionary<string, string[]>
            {
                ["userName"] = ["User has no active company membership."],
            });
        }

        var isSuperAdmin = user.IsSuperAdmin
            || active.Any(m =>
                m.CompanyId is null
                && string.Equals(
                    m.Role?.RoleCode,
                    TenantResolutionMiddleware.SuperAdminRole,
                    StringComparison.OrdinalIgnoreCase));

        var tenantMemberships = active
            .Where(m => m.CompanyId is > 0)
            .Where(m => m.Company is null || m.Company.IsActive)
            .ToList();

        if (companyId is > 0)
        {
            var exact = active.FirstOrDefault(m => m.CompanyId == companyId);
            if (exact is not null)
            {
                if (exact.Company is { IsActive: false })
                {
                    return MembershipResolution.Deny();
                }

                return MembershipResolution.Ok(FromMembership(exact));
            }

            if (isSuperAdmin)
            {
                var tenant = await _authRepository.GetActiveTenantByIdAsync(
                    companyId.Value,
                    cancellationToken);

                if (tenant is null)
                {
                    return MembershipResolution.Fail(new Dictionary<string, string[]>
                    {
                        ["company_id"] =
                        [
                            "Selected company was not found or is inactive.",
                        ],
                    });
                }

                return MembershipResolution.Ok(new TenantBinding(
                    TenantId: tenant.Id,
                    CompanyName: tenant.Name,
                    RoleCode: TenantResolutionMiddleware.SuperAdminRole,
                    DefaultRoute: DefaultRouteNormalizer.Normalize(
                        active.FirstOrDefault(m => m.CompanyId is null)?.Role?.DefaultRoute)));
            }

            return MembershipResolution.Fail(new Dictionary<string, string[]>
            {
                ["company_id"] = ["You are not a member of the selected company."],
            });
        }

        // No company_id — multi-tenant users get a challenge list for the SPA dropdown.
        if (tenantMemberships.Count > 1)
        {
            var companies = tenantMemberships
                .Select(m => new LoginCompanyOptionDto
                {
                    TenantId = m.CompanyId!.Value,
                    TenantCode = m.Company?.TenantCode ?? string.Empty,
                    CompanyName = m.Company?.Name ?? string.Empty,
                    Role = m.Role?.RoleCode ?? string.Empty,
                })
                .OrderBy(c => c.CompanyName)
                .ToList();

            return MembershipResolution.NeedSelection(new LoginCompanyChallengeDto
            {
                Username = user.UserName,
                UserId = user.Id,
                Companies = companies,
            });
        }

        if (tenantMemberships.Count == 1)
        {
            return MembershipResolution.Ok(FromMembership(tenantMemberships[0]));
        }

        if (isSuperAdmin)
        {
            var platform = active.First(m => m.CompanyId is null);
            return MembershipResolution.Ok(new TenantBinding(
                TenantId: 0,
                CompanyName: string.Empty,
                RoleCode: platform.Role?.RoleCode ?? TenantResolutionMiddleware.SuperAdminRole,
                DefaultRoute: DefaultRouteNormalizer.Normalize(platform.Role?.DefaultRoute)));
        }

        return MembershipResolution.Fail(new Dictionary<string, string[]>
        {
            ["company_id"] = ["Company selection is mandatory."],
        });
    }

    private static TenantBinding FromMembership(UserTenantMembership membership)
    {
        var roleCode = membership.Role?.RoleCode
            ?? throw new InvalidOperationException("Membership is missing Role navigation.");

        return new TenantBinding(
            TenantId: membership.CompanyId ?? 0,
            CompanyName: membership.Company?.Name ?? string.Empty,
            RoleCode: roleCode,
            DefaultRoute: DefaultRouteNormalizer.Normalize(membership.Role?.DefaultRoute));
    }

    private sealed record TenantBinding(
        int TenantId,
        string CompanyName,
        string RoleCode,
        string DefaultRoute);

    private sealed record MembershipResolution(
        TenantBinding? Binding,
        LoginCompanyChallengeDto? Challenge,
        IReadOnlyDictionary<string, string[]>? Errors,
        bool Unauthorized)
    {
        public static MembershipResolution Ok(TenantBinding binding) =>
            new(binding, null, null, false);

        public static MembershipResolution NeedSelection(LoginCompanyChallengeDto challenge) =>
            new(null, challenge, null, false);

        public static MembershipResolution Fail(IReadOnlyDictionary<string, string[]> errors) =>
            new(null, null, errors, false);

        public static MembershipResolution Deny() =>
            new(null, null, null, true);
    }
}
