using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xenasys.Core.Abstractions;
using Xenasys.Core.Abstractions.Auth;

namespace Xenasys.Infrastructure.Security;

// ==========================================================================================
// WHAT: Issues signed JWTs with user_id, tenant_id, and role claims for authenticated sessions.
// WHY: After login, tenant isolation binds only to JWT tenant_id — not inbound company_id.
// EXAMPLE: CreateToken(42, 7, "TENANT_ADMIN", "jdoe") → JWT claims user_id=42, tenant_id=7, role.
// ==========================================================================================
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string CreateToken(int userId, int tenantId, string role, string userName)
    {
        var keyBytes = Encoding.UTF8.GetBytes(
            _configuration["Jwt:Key"]
            ?? "Xenasys_Dev_Only_Replace_With_256bit_Secret_Key!!");

        var signingKey = new SymmetricSecurityKey(keyBytes);
        var expiryMinutes = int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var m) ? m : 480;

        var claims = new List<Claim>
        {
            new(TenancyKeyNames.JwtUserId, userId.ToString()),
            new(TenancyKeyNames.JwtTenantId, tenantId.ToString()),
            new(TenancyKeyNames.JwtRole, role),
            new(ClaimTypes.Role, role),
            new(ClaimTypes.Name, userName),
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };

        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "Xenasys",
            audience: _configuration["Jwt:Audience"] ?? "Xenasys.Frontend",
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
