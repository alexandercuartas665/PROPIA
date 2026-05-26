using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Auth;
using Propia.Application.SuperAdmin;

namespace Propia.Api.Controllers;

/// <summary>
/// Endpoints de autenticacion y selector de tenant.
/// Convencion de rutas estilo OIDC (/connect/...) para facilitar migrar a OpenIddict
/// en el futuro sin romper clientes.
/// </summary>
[ApiController]
[Route("connect")]
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
        var result = await _auth.LoginAsync(request, ct);
        if (result is null) return Unauthorized(new { error = "credenciales_invalidas" });
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
        var cl = await _auth.LoginAsync(request, ct);
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
                AvailableTenants: cl.AvailableTenants));
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

    private string? Ip() => HttpContext.Connection.RemoteIpAddress?.ToString();

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

        var me = await _auth.GetMeAsync(userId.Value, activeTenant, ct);
        if (me is null) return NotFound();
        return Ok(me);
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

    private Guid? GetUserId()
    {
        var raw = User.FindFirstValue("user_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
