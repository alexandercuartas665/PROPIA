using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Propia.Application.Auth;
using Propia.Application.SuperAdmin;
using Propia.Infrastructure.SuperAdmin;

namespace Propia.Api.Controllers;

/// <summary>
/// Endpoints de autenticacion y selector de tenant.
/// Convencion de rutas estilo OIDC (/connect/...) para facilitar migrar a OpenIddict
/// en el futuro sin romper clientes.
/// </summary>
[ApiController]
[Route("connect")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly ISuperAdminAuthService _superAdminAuth;

    public AuthController(IAuthService auth, ISuperAdminAuthService superAdminAuth)
    {
        _auth = auth;
        _superAdminAuth = superAdminAuth;
    }

    /// <summary>Login con email + password. Devuelve JWT con tenant activo si hay uno solo, o null si hay varios.</summary>
    [HttpPost("token")]
    [AllowAnonymous]
    public async Task<IActionResult> Token([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(request, ct, Ip(), UserAgent());
        if (result is null) return Unauthorized(new { error = "credenciales_invalidas" });
        return Ok(result);
    }

    /// <summary>
    /// SOLO DEVELOPMENT: emite el JWT del admin demo (admin@demo.propia) usando las credenciales seed
    /// que ya conoce el servidor, para que las sesiones del equipo entren sin escribir credenciales.
    /// GATE fail-closed: fuera de Development el endpoint devuelve 404 (nunca emite token). No expone
    /// ninguna clave al cliente; la pagina /dev/login solo consume el JWT resultante.
    /// </summary>
    [HttpPost("dev-login")]
    [AllowAnonymous]
    public async Task<IActionResult> DevLogin([FromServices] IWebHostEnvironment env, CancellationToken ct)
    {
        if (!env.IsDevelopment()) return NotFound();
        var result = await _auth.LoginAsync(
            new LoginRequest(DemoSeeder.DemoAdminEmail, DemoSeeder.DemoUserPassword), ct, Ip(), UserAgent());
        if (result is null) return NotFound(new { error = "demo_no_seedeado" });
        return Ok(result);
    }

    /// <summary>
    /// Login UNIFICADO: un solo punto de entrada para todos los usuarios. Primero intenta la cuenta
    /// de plataforma (A&D GROUP, tabla super_admin_usuarios) y, si no coincide, la cuenta de
    /// copropiedad (ApplicationUser). El rol determina que ve el usuario. Si es SuperAdmin con MFA,
    /// devuelve RequiresMfa=true + MfaTicket para el segundo paso (/connect/login/mfa).
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> UnifiedLogin([FromBody] LoginRequest request, CancellationToken ct)
    {
        // 1) Cuenta de plataforma (SuperAdmin). Devuelve null si el email no existe en esa tabla
        //    o la clave no coincide -> entonces se intenta como cliente.
        var sa = await _superAdminAuth.LoginAsync(new SuperAdminLoginRequest(request.Email, request.Password), Ip(), ct);
        if (sa is not null)
        {
            return Ok(new UnifiedLoginResponse(
                Kind: "superadmin",
                RequiresMfa: sa.RequiresMfa,
                AccessToken: sa.AccessToken,
                ExpiresAt: sa.ExpiresAt,
                Email: sa.Email,
                MfaTicket: sa.MfaTicket,
                ActiveTenantId: null,
                AvailableTenants: Array.Empty<TenantInfo>()));
        }

        // 2) Cuenta de copropiedad (cliente).
        var cl = await _auth.LoginAsync(request, ct, Ip(), UserAgent());
        if (cl is not null)
        {
            return Ok(new UnifiedLoginResponse(
                Kind: "client",
                RequiresMfa: false,
                AccessToken: cl.AccessToken,
                ExpiresAt: cl.ExpiresAt,
                Email: cl.Email,
                MfaTicket: null,
                ActiveTenantId: cl.ActiveTenantId,
                AvailableTenants: cl.AvailableTenants,
                DebeCambiarClave: cl.DebeCambiarClave));
        }

        return Unauthorized(new { error = "credenciales_invalidas" });
    }

    /// <summary>Segundo paso del login unificado para SuperAdmin con MFA: valida ticket + codigo TOTP.</summary>
    [HttpPost("login/mfa")]
    [AllowAnonymous]
    public async Task<IActionResult> UnifiedLoginMfa([FromBody] VerifyMfaLoginRequest request, CancellationToken ct)
    {
        var sa = await _superAdminAuth.VerifyMfaLoginAsync(request, Ip(), ct);
        if (sa is null || sa.AccessToken is null) return Unauthorized(new { error = "mfa_invalido" });
        return Ok(new UnifiedLoginResponse(
            Kind: "superadmin",
            RequiresMfa: false,
            AccessToken: sa.AccessToken,
            ExpiresAt: sa.ExpiresAt,
            Email: sa.Email,
            MfaTicket: null,
            ActiveTenantId: null,
            AvailableTenants: Array.Empty<TenantInfo>()));
    }

    /// <summary>
    /// Sliding refresh: recibe el JWT actual (posiblemente recien expirado, dentro de Jwt:RefreshSlidingHours)
    /// en el header Authorization Bearer y devuelve uno nuevo con la misma identidad y tenant activo.
    /// Esto evita que el usuario tenga que volver a loguearse cuando vuelve al tab horas despues.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var auth = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(auth)) return Unauthorized(new { error = "token_ausente" });
        var raw = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? auth.Substring(7).Trim()
            : auth.Trim();

        var result = await _auth.RefreshAsync(raw, ct);
        if (result is null) return Unauthorized(new { error = "token_invalido_o_vencido" });
        return Ok(result);
    }

    private string? Ip() => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? UserAgent() => Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;

    /// <summary>Info del usuario autenticado + lista de copropiedades accesibles.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        Guid? activeTenant = null;
        var t = User.FindFirstValue("tenant_id");
        if (Guid.TryParse(t, out var parsed)) activeTenant = parsed;

        Guid? activeRol = null;
        var r = User.FindFirstValue("rol_id");
        if (Guid.TryParse(r, out var parsedRol)) activeRol = parsedRol;

        var me = await _auth.GetMeAsync(userId.Value, activeTenant, ct, activeRol);
        if (me is null) return NotFound();
        return Ok(me);
    }

    /// <summary>
    /// Reemite un JWT fijando el rol ACTIVO de la sesion (2.5 v2.0 multi-rol). El usuario debe tener
    /// asignado ese rol en la copropiedad activa. El tenant activo se toma del JWT actual.
    /// </summary>
    [HttpPost("switch-rol")]
    [Authorize]
    public async Task<IActionResult> SwitchRol([FromBody] SwitchRolRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var t = User.FindFirstValue("tenant_id");
        if (!Guid.TryParse(t, out var tenantId)) return BadRequest(new { error = "sin_tenant_activo" });

        var result = await _auth.SwitchRolAsync(userId.Value, tenantId, request.RolId, ct);
        if (result is null) return Forbid();
        return Ok(result);
    }

    /// <summary>Reemite un JWT con un tenant_id distinto. El usuario debe tener acceso al tenant pedido.</summary>
    [HttpPost("switch-tenant")]
    [Authorize]
    public async Task<IActionResult> SwitchTenant([FromBody] SwitchTenantRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _auth.SwitchTenantAsync(userId.Value, request.TenantId, ct);
        if (result is null) return Forbid();
        return Ok(result);
    }

    /// <summary>Cambia la clave del usuario autenticado validando la actual (Mi Perfil).</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] Propia.Application.MiPerfil.CambiarPasswordRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var (ok, error) = await _auth.ChangePasswordAsync(userId.Value, request.CurrentPassword, request.NewPassword, ct);
        return ok ? NoContent() : BadRequest(new { error });
    }

    /// <summary>
    /// "Olvide mi contrasena" (anonimo, rate limit "auth"). Siempre 202: no revela si el correo tiene cuenta.
    /// </summary>
    [HttpPost("recuperar-clave")]
    [AllowAnonymous]
    public async Task<IActionResult> RecuperarClave([FromBody] SolicitarRecuperacionRequest request, CancellationToken ct)
    {
        await _auth.SolicitarRecuperacionAsync(request?.Email ?? "", request?.BaseUrl, ct);
        return Accepted(new { ok = true });
    }

    /// <summary>Fija la nueva clave con el token del enlace del correo (anonimo).</summary>
    [HttpPost("restablecer-clave")]
    [AllowAnonymous]
    public async Task<IActionResult> RestablecerClave([FromBody] RestablecerConTokenRequest request, CancellationToken ct)
    {
        var (ok, error) = await _auth.RestablecerConTokenAsync(request, ct);
        return ok ? NoContent() : BadRequest(new { error });
    }

    /// <summary>Cambio obligatorio tras una clave temporal puesta por el administrador (no pide la actual).</summary>
    [HttpPost("cambiar-clave-obligatoria")]
    [Authorize]
    public async Task<IActionResult> CambiarClaveObligatoria([FromBody] CambiarClaveObligatoriaRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        // Solo con un JWT emitido tras la clave temporal (claim pwd_chg); el servicio revalida la marca.
        if (User.FindFirstValue(ClaveTemporal.JwtClaim) != "1") return Forbid();
        var (ok, error) = await _auth.CambiarClaveObligatoriaAsync(userId.Value, request, ct);
        return ok ? NoContent() : BadRequest(new { error });
    }

    private Guid? GetUserId()
    {
        var raw = User.FindFirstValue("user_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
