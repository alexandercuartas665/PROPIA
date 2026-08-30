using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Propia.Application.Auth;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Auth;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly PropiaDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly JwtSettings _jwt;
    private readonly Storage.IBlobStorage _blob;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        PropiaDbContext db,
        ITokenService tokenService,
        IOptions<JwtSettings> jwtOptions,
        Storage.IBlobStorage blob)
    {
        _userManager = userManager;
        _db = db;
        _tokenService = tokenService;
        _jwt = jwtOptions.Value;
        _blob = blob;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null) return null;

        var ok = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!ok) return null;

        var tenants = await LoadAvailableTenantsAsync(user.Id, ct);
        // Si solo hay una copropiedad vinculada, se selecciona auto. Caso comun para residentes.
        Guid? activeTenant = tenants.Count == 1 ? tenants[0].TenantId : null;

        var (token, expires) = _tokenService.IssueAccessToken(user, activeTenant);

        // Marcamos ultimo acceso del UsuarioTenant si hay tenant activo
        if (activeTenant.HasValue)
        {
            await UpdateUltimoAccesoAsync(user.PersonaId, activeTenant.Value, ct);
        }

        return new LoginResponse(token, expires, user.Id, user.Email!, activeTenant, tenants);
    }

    public async Task<MeResponse?> GetMeAsync(Guid userId, Guid? activeTenantId, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.Persona)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return null;

        var tenants = await LoadAvailableTenantsAsync(userId, ct);
        return new MeResponse(
            user.Id,
            user.Email!,
            user.PersonaId,
            user.Persona?.Nombres,
            user.Persona?.Apellidos,
            activeTenantId,
            tenants,
            _blob.ResolveUrl(user.Persona?.FotoUrl),
            _blob.ResolveUrl(user.Persona?.FirmaUrl));
    }

    public async Task<(bool Ok, string? Error)> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            return (false, "La nueva clave debe tener al menos 8 caracteres.");

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return (false, "Usuario no encontrado.");

        var res = await _userManager.ChangePasswordAsync(user, currentPassword ?? "", newPassword);
        if (res.Succeeded) return (true, null);

        var err = res.Errors.FirstOrDefault();
        var msg = (err?.Code) switch
        {
            "PasswordMismatch" => "La clave actual no es correcta.",
            "PasswordTooShort" => "La nueva clave es muy corta.",
            "PasswordRequiresNonAlphanumeric" => "La clave debe incluir un caracter especial.",
            "PasswordRequiresDigit" => "La clave debe incluir un numero.",
            "PasswordRequiresUpper" => "La clave debe incluir una mayuscula.",
            "PasswordRequiresLower" => "La clave debe incluir una minuscula.",
            _ => err?.Description ?? "No se pudo cambiar la clave."
        };
        return (false, msg);
    }

    public async Task<LoginResponse?> SwitchTenantAsync(Guid userId, Guid newTenantId, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return null;

        var tenants = await LoadAvailableTenantsAsync(userId, ct);
        var valid = tenants.Any(t => t.TenantId == newTenantId);
        if (!valid) return null;  // El usuario no tiene acceso a ese tenant - rechazar

        var (token, expires) = _tokenService.IssueAccessToken(user, newTenantId);
        await UpdateUltimoAccesoAsync(user.PersonaId, newTenantId, ct);

        return new LoginResponse(token, expires, user.Id, user.Email!, newTenantId, tenants);
    }

    public async Task<LoginResponse?> RefreshAsync(string rawJwt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawJwt)) return null;

        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));
        var validation = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            // Ventana sliding: aceptamos tokens recientemente expirados (clock skew amplio).
            ValidateLifetime = false,
            ClockSkew = TimeSpan.Zero
        };

        ClaimsPrincipal principal;
        SecurityToken validated;
        try
        {
            principal = handler.ValidateToken(rawJwt, validation, out validated);
        }
        catch { return null; }

        if (validated is not JwtSecurityToken jwt) return null;

        // Sliding window: rechazar tokens expirados hace mucho.
        var ahora = DateTimeOffset.UtcNow;
        var exp = new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero);
        if (ahora - exp > TimeSpan.FromHours(_jwt.RefreshSlidingHours)) return null;

        var sub = principal.FindFirstValue("user_id") ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(sub, out var userId)) return null;

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return null;

        Guid? activeTenant = null;
        var t = principal.FindFirstValue("tenant_id");
        if (Guid.TryParse(t, out var parsedTenant)) activeTenant = parsedTenant;

        var tenants = await LoadAvailableTenantsAsync(userId, ct);
        // Si el tenant del JWT viejo ya no esta disponible, reasignamos al primero o null.
        if (activeTenant.HasValue && !tenants.Any(x => x.TenantId == activeTenant.Value))
            activeTenant = tenants.Count == 1 ? tenants[0].TenantId : null;

        var (nuevoToken, expires) = _tokenService.IssueAccessToken(user, activeTenant);
        return new LoginResponse(nuevoToken, expires, user.Id, user.Email!, activeTenant, tenants);
    }

    // ---------- Helpers ----------

    /// <summary>
    /// Lista las copropiedades a las que tiene acceso la persona vinculada al usuario.
    /// Usa la funcion SQL SECURITY DEFINER `get_tenants_for_persona` para bypassar RLS
    /// (necesario porque /connect/token corre antes de tener tenant activo).
    /// La seguridad la garantiza el filtro por persona_id del usuario autenticado.
    /// </summary>
    private async Task<List<TenantInfo>> LoadAvailableTenantsAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || user.PersonaId is null) return new();

        var personaId = user.PersonaId.Value;
        var rows = new List<TenantInfo>();
        var conn = _db.Database.GetDbConnection();
        var openedHere = conn.State != System.Data.ConnectionState.Open;
        if (openedHere) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT tenant_id, nombre, rol, logo_url FROM get_tenants_for_persona(@p_persona_id)";
            var p = cmd.CreateParameter();
            p.ParameterName = "@p_persona_id";
            p.Value = personaId;
            cmd.Parameters.Add(p);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var logo = reader.IsDBNull(3) ? null : reader.GetString(3);
                rows.Add(new TenantInfo(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), _blob.ResolveUrl(logo)));
            }
        }
        finally
        {
            if (openedHere) await conn.CloseAsync();
        }

        // Adjunta el codigo corto de 6 chars (tabla global tenants, sin RLS) para mostrarlo en el
        // selector de copropiedad. get_tenants_for_persona no lo devuelve, se resuelve aparte.
        if (rows.Count > 0)
        {
            var ids = rows.Select(r => r.TenantId).ToList();
            var codigos = await _db.Tenants.AsNoTracking()
                .Where(t => ids.Contains(t.Id))
                .Select(t => new { t.Id, t.CodigoCorto })
                .ToDictionaryAsync(x => x.Id, x => x.CodigoCorto, ct);
            rows = rows.Select(r => codigos.TryGetValue(r.TenantId, out var c) ? r with { CodigoCorto = c } : r).ToList();
        }
        return rows;
    }

    private async Task UpdateUltimoAccesoAsync(Guid? personaId, Guid tenantId, CancellationToken ct)
    {
        if (personaId is null) return;
        // Actualizamos sin pasar por el HasQueryFilter (no hay tenant activo aun en el scope auth)
        var vinculo = await _db.UsuariosTenant.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.PersonaId == personaId && x.TenantId == tenantId, ct);
        if (vinculo is null) return;
        vinculo.UltimoAcceso = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
