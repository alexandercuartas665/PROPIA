using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Propia.Application.SuperAdmin;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Auth;
using Propia.Infrastructure.Persistence;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Propia.Infrastructure.SuperAdmin;

public class SuperAdminAuthService : ISuperAdminAuthService
{
    private readonly PropiaDbContext _db;
    private readonly IPasswordHasher<SuperAdminUsuario> _hasher;
    private readonly JwtSettings _jwt;

    public SuperAdminAuthService(PropiaDbContext db, IPasswordHasher<SuperAdminUsuario> hasher, IOptions<JwtSettings> jwt)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt.Value;
    }

    public async Task<SuperAdminLoginResponse?> LoginAsync(SuperAdminLoginRequest request, string? ip, CancellationToken ct)
    {
        var user = await _db.SuperAdminUsuarios
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.Activo, ct);
        if (user is null) return null;

        var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verify == PasswordVerificationResult.Failed) return null;

        // Re-hash si Identity sugiere actualizar (formato viejo)
        if (verify == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, request.Password);
        }

        user.UltimoAcceso = DateTimeOffset.UtcNow;
        user.UltimaIp = ip;
        await _db.SaveChangesAsync(ct);

        // Log de login
        _db.SuperAdminLogs.Add(new SuperAdminLog
        {
            ActorId = user.Id,
            ActorEmail = user.Email,
            Accion = "SUPER_ADMIN_LOGIN",
            Ip = ip
        });
        await _db.SaveChangesAsync(ct);

        var (token, expires) = IssueToken(user);
        return new SuperAdminLoginResponse(token, expires, user.Id, user.Email, user.Rol);
    }

    private (string token, DateTimeOffset expires) IssueToken(SuperAdminUsuario user)
    {
        var now = DateTimeOffset.UtcNow;
        // Sesion corta para super admins (4h max - spec del modulo 0.1)
        var expires = now.AddHours(4);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("user_id", user.Id.ToString()),
            new("is_super_admin", "true"),
            new("super_admin_rol", user.Rol.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
