using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using iM1os.Application.Authentication;
using iM1os.Domain.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace iM1os.Infrastructure.Security;

public sealed class JwtTokenService(IOptions<JwtOptions> options) : IJwtTokenService
{
    public AuthResponse CreateToken(ApplicationUser user, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions)
    {
        var jwtOptions = options.Value;
        var expires = DateTimeOffset.UtcNow.AddMinutes(jwtOptions.ExpirationMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("organization_id", user.OrganizationId.ToString()),
            new("display_name", user.DisplayName)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(jwtOptions.Issuer, jwtOptions.Audience, claims, expires: expires.UtcDateTime, signingCredentials: credentials);

        return new AuthResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            expires,
            new UserProfile(user.Id, user.OrganizationId, user.Email, user.DisplayName, roles, permissions));
    }
}
