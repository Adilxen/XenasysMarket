using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions;
using Xenasys.Core.Abstractions.Auth;
using Xenasys.Core.Contracts.Auth;
using Xenasys.Core.Entities;
using Xenasys.Core.Security;
using Xenasys.Persistence.Data;

namespace Xenasys.Presentation.Controllers;

// ==========================================================================================
// WHAT: M0 auth HTTP API — company list, login, self-service profile (T1.32).
// WHY: SPA entry + header Profile update (name/password); role/company JWT-bound read-only.
// EXAMPLE: POST /api/auth/login → JWT; GET/PUT /api/auth/me → profile chip + update.
// ==========================================================================================
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantProvider _tenantProvider;

    public AuthController(
        IAuthService authService,
        ApplicationDbContext db,
        ICurrentUser currentUser,
        ITenantProvider tenantProvider)
    {
        _authService = authService;
        _db = db;
        _currentUser = currentUser;
        _tenantProvider = tenantProvider;
    }

    /// <summary>Anonymous company dropdown — returns display names + INT ids.</summary>
    [HttpGet("companies")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<LoginTenantOptionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LoginTenantOptionDto>>> ListCompanies(
        CancellationToken cancellationToken)
    {
        var companies = await _authService.ListLoginTenantsAsync(cancellationToken);
        return Ok(companies);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LoginCompanyChallengeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(BuildModelStateErrors());
        }

        var outcome = await _authService.LoginAsync(request, cancellationToken);

        return outcome switch
        {
            LoginSucceeded succeeded => Ok(succeeded.Response),
            LoginCompanySelectionRequired challenge => Ok(challenge.Challenge),
            LoginValidationFailed failed => UnprocessableEntity(new { errors = failed.Errors }),
            LoginUnauthorized => Unauthorized(new
            {
                success = false,
                error = new
                {
                    code = "INVALID_CREDENTIALS",
                    message = "Invalid user name or password.",
                },
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>Current session profile for header + Profile drawer (role/company read-only).</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(CurrentUserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserProfileDto>> GetProfile(CancellationToken cancellationToken)
    {
        var profile = await BuildProfileAsync(cancellationToken);
        return profile is null ? Unauthorized() : Ok(profile);
    }

    /// <summary>Self-service update — User Name + optional Password only (roles locked).</summary>
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType(typeof(CurrentUserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CurrentUserProfileDto>> UpdateProfile(
        [FromBody] UpdateCurrentUserProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not int userId || userId <= 0)
        {
            return Unauthorized();
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        var userName = (request.UserName ?? string.Empty).Trim();
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(userName) || userName.Length > 50)
        {
            errors["userName"] = ["User Name is mandatory (max 50)."];
        }

        if (!string.IsNullOrWhiteSpace(request.Password) && request.Password.Length < 8)
        {
            errors["password"] = ["Password must be at least 8 characters."];
        }

        if (errors.Count == 0
            && !string.Equals(user.UserName, userName, StringComparison.OrdinalIgnoreCase)
            && await _db.Users.AnyAsync(u => u.UserName == userName && u.Id != userId, cancellationToken))
        {
            errors["userName"] = ["User Name is already taken."];
        }

        if (errors.Count > 0)
        {
            return UnprocessableEntity(new { errors });
        }

        user.UserName = userName;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            // T0.12 TEMP: store plaintext — same as Users seed / create until BCrypt restore.
            user.PasswordHash = request.Password;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var profile = await BuildProfileAsync(cancellationToken);
        return profile is null ? Unauthorized() : Ok(profile);
    }

    private async Task<CurrentUserProfileDto?> BuildProfileAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not int userId || userId <= 0)
        {
            return null;
        }

        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => new { u.Id, u.UserName, u.Email })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        var companyId = _tenantProvider.TenantId ?? 0;
        string companyName = "Platform";
        string roleCode = _currentUser.Role ?? string.Empty;
        string roleName = roleCode;
        var defaultRoute = DefaultRouteNormalizer.Fallback;

        if (companyId > 0)
        {
            var membership = await _db.UserTenantMemberships.AsNoTracking()
                .Where(m => m.UserId == userId && m.CompanyId == companyId && m.IsActive)
                .Select(m => new
                {
                    CompanyName = m.Company!.Name,
                    RoleCode = m.Role!.RoleCode,
                    RoleName = m.Role.Name,
                    DefaultRoute = m.Role.DefaultRoute,
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (membership is not null)
            {
                companyName = membership.CompanyName;
                roleCode = membership.RoleCode;
                roleName = membership.RoleName;
                defaultRoute = DefaultRouteNormalizer.Normalize(membership.DefaultRoute);
            }
            else
            {
                var company = await _db.Tenants.AsNoTracking()
                    .Where(t => t.Id == companyId)
                    .Select(t => t.Name)
                    .FirstOrDefaultAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(company))
                {
                    companyName = company;
                }
            }
        }
        else if (string.Equals(roleCode, SeedConstants.RoleSuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            companyName = "Platform";
            roleName = "Super Admin";
            var platformRoute = await _db.Roles.AsNoTracking()
                .Where(r => r.RoleCode == SeedConstants.RoleSuperAdmin && r.CompanyId == null)
                .Select(r => r.DefaultRoute)
                .FirstOrDefaultAsync(cancellationToken);
            defaultRoute = DefaultRouteNormalizer.Normalize(platformRoute);
        }

        return new CurrentUserProfileDto
        {
            UserId = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            Role = roleCode,
            RoleName = roleName,
            CompanyId = companyId,
            CompanyName = companyName,
            DefaultRoute = defaultRoute,
        };
    }

    private object BuildModelStateErrors()
    {
        var errors = ModelState
            .Where(kvp => kvp.Value is { Errors.Count: > 0 })
            .ToDictionary(
                kvp => ToCamelClientKey(kvp.Key),
                kvp => kvp.Value!.Errors
                    .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage)
                        ? "Invalid value."
                        : e.ErrorMessage)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        return new { errors };
    }

    private static string ToCamelClientKey(string key) =>
        key switch
        {
            nameof(LoginRequestDto.Username) or "Username" => "userName",
            nameof(LoginRequestDto.Password) or "Password" => "password",
            nameof(LoginRequestDto.CompanyId) or "CompanyId" => TenancyKeyNames.CompanyId,
            _ => key.Length > 0
                ? char.ToLowerInvariant(key[0]) + key[1..]
                : key,
        };
}
