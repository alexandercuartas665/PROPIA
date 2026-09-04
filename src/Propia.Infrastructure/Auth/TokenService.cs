using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Propia.Application.Auth;
using Propia.Domain.Entities;

namespace Propia.Infrastructure.Auth;

public class TokenService : ITokenService
{
    private readonly JwtSettings _settings;

    public TokenService(IOptions<JwtSettings> options)
    {
        _settings = options.Value;
        if (string.IsNullOrWhiteSpace(_settings.SigningKey) || _settings.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey ausente o demasiado corta (minimo 32 chars).");
        }
    }

    public (string Token, DateTimeOffset ExpiresAt) IssueAccessToken(ApplicationUser user, Guid? activeTenantId)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(_settings.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new("user_id", user.Id.ToString()),
            // S-11: sello de seguridad de Identity. El refresh lo compara contra el valor actual del
            // usuario; al cambiar clave/rol o revocar (UpdateSecurityStampAsync) los tokens viejos
            // dejan de poder refrescarse.
            new("sstamp", user.SecurityStamp ?? string.Empty)
        };

        if (user.PersonaId.HasValue)
            claims.Add(new Claim("persona_id", user.PersonaId.Value.ToString()));

        if (activeTenantId.HasValue)
            claims.Add(new Claim("tenant_id", activeTenantId.Value.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return (jwt, expires);
    }
}
