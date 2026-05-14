using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Auth;

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

    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Login con email + password. Devuelve JWT con tenant activo si hay uno solo, o null si hay varios.</summary>
    [HttpPost("token")]
    [AllowAnonymous]
    public async Task<IActionResult> Token([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(request, ct);
        if (result is null) return Unauthorized(new { error = "credenciales_invalidas" });
        return Ok(result);
    }

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
