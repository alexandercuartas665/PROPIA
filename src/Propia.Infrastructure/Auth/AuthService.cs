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

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct, string? ip = null, string? userAgent = null)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            await RegistrarIngresoAsync(request.Email, null, null, null, false, "usuario_no_existe", ip, userAgent, ct);
            return null;
        }

        // S-04: no permitir login de cuentas con email sin confirmar (cierra el pre-hijacking:
        // el atacante pre-registra el correo de la victima pero nunca confirma; su cuenta no entra).
        if (!user.EmailConfirmed)
        {
            await RegistrarIngresoAsync(request.Email, user.Id, user.PersonaId, null, false, "email_no_confirmado", ip, userAgent, ct);
            return null;
        }

        // S-03: lockout efectivo. Si la cuenta esta bloqueada por intentos fallidos, no se evalua la clave.
        if (await _userManager.IsLockedOutAsync(user))
        {
            await RegistrarIngresoAsync(request.Email, user.Id, user.PersonaId, null, false, "cuenta_bloqueada", ip, userAgent, ct);
            return null;
        }

        var ok = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!ok)
        {
            // Incrementa AccessFailedCount y bloquea al alcanzar el maximo (config en DependencyInjection).
            await _userManager.AccessFailedAsync(user);
            await RegistrarIngresoAsync(request.Email, user.Id, user.PersonaId, null, false, "clave_incorrecta", ip, userAgent, ct);
            return null;
        }
        // Login correcto: se limpia el contador de fallos.
        await _userManager.ResetAccessFailedCountAsync(user);

        var tenants = await LoadAvailableTenantsAsync(user.Id, ct);
        // Si solo hay una copropiedad vinculada, se selecciona auto. Caso comun para residentes.
        Guid? activeTenant = tenants.Count == 1 ? tenants[0].TenantId : null;

        var (token, expires) = _tokenService.IssueAccessToken(user, activeTenant);

        // Marcamos ultimo acceso del UsuarioTenant si hay tenant activo
        if (activeTenant.HasValue)
        {
            await UpdateUltimoAccesoAsync(user.PersonaId, activeTenant.Value, ct);
        }

        // Registro de ingreso exitoso. Si hay varias copropiedades y ninguna activa aun, se toma la primera
        // solo para agrupar el evento por copropiedad en el Super Admin.
        var tenantParaRegistro = activeTenant ?? (tenants.Count > 0 ? tenants[0].TenantId : (Guid?)null);
        await RegistrarIngresoAsync(request.Email, user.Id, user.PersonaId, tenantParaRegistro, true, null, ip, userAgent, ct);

        return new LoginResponse(token, expires, user.Id, user.Email!, activeTenant, tenants);
    }

    // Escribe un evento en el registro de ingresos (auditoria global, sin RLS). No debe tumbar el login
    // si algo falla, por eso captura y descarta cualquier excepcion. La copropiedad determina la organizacion.
    private async Task RegistrarIngresoAsync(string email, Guid? usuarioId, Guid? personaId, Guid? tenantId,
        bool exito, string? motivo, string? ip, string? userAgent, CancellationToken ct)
    {
        try
        {
            // Si no viene una copropiedad resuelta pero el usuario existe (ej. clave incorrecta), se toma su
            // copropiedad (la unica, o la primera) para que el evento agrupe bajo la organizacion en el Super Admin.
            if (tenantId is null && usuarioId is Guid uid)
            {
                var tenants = await LoadAvailableTenantsAsync(uid, ct);
                if (tenants.Count > 0) tenantId = tenants[0].TenantId;
            }
            Guid? orgId = null;
            if (tenantId is Guid tid)
                orgId = await _db.Tenants.AsNoTracking().Where(t => t.Id == tid).Select(t => t.OrganizacionId).FirstOrDefaultAsync(ct);

            _db.LoginAuditEvents.Add(new Propia.Domain.Entities.LoginAuditEvent
            {
                Email = (email ?? string.Empty).Trim().ToLowerInvariant(),
                UsuarioId = usuarioId,
                PersonaId = personaId,
                TenantId = tenantId,
                OrganizacionId = orgId,
                Exito = exito,
                Motivo = motivo,
                Ip = string.IsNullOrWhiteSpace(ip) ? null : ip.Trim(),
                UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent.Trim()[..Math.Min(userAgent.Trim().Length, 400)],
                TipoCuenta = "client",
                CreatedAt = DateTimeOffset.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }
        catch { /* la auditoria nunca bloquea el login */ }
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

        // S-11: revocacion. El token solo puede refrescarse si su sello coincide con el actual del
        // usuario (cambia al resetear clave/rol o al revocar) y el usuario no esta bloqueado ni sin
        // confirmar. Tokens emitidos antes de este cambio no traen sstamp -> se fuerza re-login.
        var stampToken = principal.FindFirstValue("sstamp");
        if (string.IsNullOrEmpty(stampToken) || !string.Equals(stampToken, user.SecurityStamp, StringComparison.Ordinal))
            return null;
        if (!user.EmailConfirmed) return null;
        if (await _userManager.IsLockedOutAsync(user)) return null;

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
