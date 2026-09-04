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
    private const string MfaAudience = "propia-mfa-challenge";
    private const string Issuer = "propia-api";
    private const string AuthIssuer = "PROPIA Super Admin";
    private const int MaxAccessFailed = 5;          // S-03b
    private const int LockoutMinutes = 15;          // S-03b

    private readonly PropiaDbContext _db;
    private readonly IPasswordHasher<SuperAdminUsuario> _hasher;
    private readonly ITotpService _totp;
    private readonly JwtSettings _jwt;

    public SuperAdminAuthService(
        PropiaDbContext db,
        IPasswordHasher<SuperAdminUsuario> hasher,
        ITotpService totp,
        IOptions<JwtSettings> jwt)
    {
        _db = db;
        _hasher = hasher;
        _totp = totp;
        _jwt = jwt.Value;
    }

    // S-03b: helpers de lockout persistente (password + MFA).
    private static bool EstaBloqueado(SuperAdminUsuario user)
        => user.LockoutEnd is { } end && end > DateTimeOffset.UtcNow;

    private async Task RegistrarFalloAsync(SuperAdminUsuario user, string? ip, CancellationToken ct)
    {
        user.AccessFailedCount++;
        if (user.AccessFailedCount >= MaxAccessFailed)
        {
            user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(LockoutMinutes);
            user.AccessFailedCount = 0;  // reinicia el contador para el proximo ciclo tras el lockout
            await LogAsync(user, "SUPER_ADMIN_LOCKOUT", ip, ct);
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task ResetFallosAsync(SuperAdminUsuario user, CancellationToken ct)
    {
        if (user.AccessFailedCount != 0 || user.LockoutEnd is not null)
        {
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            await _db.SaveChangesAsync(ct);
        }
    }

    // ----------------------------------- LOGIN -----------------------------------

    public async Task<SuperAdminLoginResponse?> LoginAsync(SuperAdminLoginRequest request, string? ip, CancellationToken ct)
    {
        var user = await _db.SuperAdminUsuarios
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.Activo, ct);
        if (user is null) return null;

        // S-03b: cuenta bloqueada por intentos fallidos -> rechazar sin verificar la clave.
        if (EstaBloqueado(user)) { await LogAsync(user, "SUPER_ADMIN_LOGIN_BLOQUEADO", ip, ct); return null; }

        var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verify == PasswordVerificationResult.Failed)
        {
            await RegistrarFalloAsync(user, ip, ct);
            return null;
        }

        if (verify == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, request.Password);
        }
        await ResetFallosAsync(user, ct);

        // Si tiene MFA configurado: devolver MfaTicket en lugar de JWT
        if (user.MfaConfigurado && !string.IsNullOrEmpty(user.MfaSecret))
        {
            var (ticket, ticketExpires) = IssueMfaTicket(user);
            await LogAsync(user, "SUPER_ADMIN_LOGIN_MFA_CHALLENGED", ip, ct);
            return new SuperAdminLoginResponse(
                RequiresMfa: true,
                AccessToken: null, ExpiresAt: null, UserId: null, Email: null, Rol: null,
                MfaTicket: ticket, MfaTicketExpiresAt: ticketExpires);
        }

        // Sin MFA configurado: JWT directo (caso del founder en su primer login)
        return await FinalizarLoginAsync(user, ip, "SUPER_ADMIN_LOGIN", ct);
    }

    public async Task<SuperAdminLoginResponse?> VerifyMfaLoginAsync(VerifyMfaLoginRequest request, string? ip, CancellationToken ct)
    {
        var userId = ValidateMfaTicket(request.MfaTicket);
        if (userId is null) return null;

        var user = await _db.SuperAdminUsuarios.FirstOrDefaultAsync(u => u.Id == userId.Value && u.Activo, ct);
        if (user is null || !user.MfaConfigurado || string.IsNullOrEmpty(user.MfaSecret)) return null;

        // S-03b: mismo lockout aplica al segundo factor (evita fuerza bruta del TOTP dentro del ticket).
        if (EstaBloqueado(user)) { await LogAsync(user, "SUPER_ADMIN_LOGIN_BLOQUEADO", ip, ct); return null; }

        if (!_totp.VerifyCode(user.MfaSecret, request.Code))
        {
            await RegistrarFalloAsync(user, ip, ct);
            await LogAsync(user, "SUPER_ADMIN_LOGIN_MFA_FAILED", ip, ct);
            return null;
        }
        await ResetFallosAsync(user, ct);

        return await FinalizarLoginAsync(user, ip, "SUPER_ADMIN_LOGIN_MFA_OK", ct);
    }

    // ----------------------------------- ENROLL MFA -----------------------------------

    public async Task<MfaEnrollResponse> EnrollMfaAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.SuperAdminUsuarios.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) throw new InvalidOperationException("Usuario no encontrado.");

        // Generamos un secret NUEVO cada vez que se hace enroll - si el user reescanea, el viejo deja de servir.
        var secret = _totp.GenerateSecret();
        user.MfaSecret = secret;
        user.MfaConfigurado = false;  // se confirmara con VerifyEnrollMfaAsync
        await _db.SaveChangesAsync(ct);

        var otpAuthUri = _totp.BuildOtpAuthUri(secret, user.Email, AuthIssuer);
        return new MfaEnrollResponse(secret, otpAuthUri);
    }

    public async Task<bool> VerifyEnrollMfaAsync(Guid userId, VerifyMfaEnrollRequest request, string? ip, CancellationToken ct)
    {
        var user = await _db.SuperAdminUsuarios.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || string.IsNullOrEmpty(user.MfaSecret)) return false;

        if (!_totp.VerifyCode(user.MfaSecret, request.Code)) return false;

        user.MfaConfigurado = true;
        await _db.SaveChangesAsync(ct);
        await LogAsync(user, "MFA_ENROLLED", ip, ct);
        return true;
    }

    // ----------------------------------- Helpers -----------------------------------

    private async Task<SuperAdminLoginResponse> FinalizarLoginAsync(SuperAdminUsuario user, string? ip, string accion, CancellationToken ct)
    {
        user.UltimoAcceso = DateTimeOffset.UtcNow;
        user.UltimaIp = ip;
        await _db.SaveChangesAsync(ct);
        await LogAsync(user, accion, ip, ct);

        var (token, expires) = IssueAccessToken(user);
        return new SuperAdminLoginResponse(
            RequiresMfa: false,
            AccessToken: token, ExpiresAt: expires,
            UserId: user.Id, Email: user.Email, Rol: user.Rol,
            MfaTicket: null, MfaTicketExpiresAt: null);
    }

    private (string token, DateTimeOffset expires) IssueAccessToken(SuperAdminUsuario user)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddHours(4);  // spec 0.1: max 4h

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("user_id", user.Id.ToString()),
            new("is_super_admin", "true"),
            new("super_admin_rol", user.Rol.ToString())
        };

        return IssueJwt(claims, now, expires, _jwt.Audience);
    }

    private (string ticket, DateTimeOffset expires) IssueMfaTicket(SuperAdminUsuario user)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(5);  // ticket corto - solo para completar MFA

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("user_id", user.Id.ToString()),
            new("token_purpose", "mfa_challenge")
        };

        return IssueJwt(claims, now, expires, MfaAudience);
    }

    private Guid? ValidateMfaTicket(string ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket)) return null;
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));
            var validation = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _jwt.Issuer,
                ValidAudience = MfaAudience,  // distinta de la audience de un JWT normal
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(ticket, validation, out _);
            var idStr = principal.FindFirstValue("user_id");
            return Guid.TryParse(idStr, out var id) ? id : null;
        }
        catch
        {
            return null;
        }
    }

    private (string token, DateTimeOffset expires) IssueJwt(List<Claim> claims, DateTimeOffset notBefore, DateTimeOffset expires, string audience)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: audience,
            claims: claims,
            notBefore: notBefore.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    private async Task LogAsync(SuperAdminUsuario user, string accion, string? ip, CancellationToken ct)
    {
        _db.SuperAdminLogs.Add(new SuperAdminLog
        {
            ActorId = user.Id,
            ActorEmail = user.Email,
            Accion = accion,
            Ip = ip
        });
        await _db.SaveChangesAsync(ct);
    }
}
