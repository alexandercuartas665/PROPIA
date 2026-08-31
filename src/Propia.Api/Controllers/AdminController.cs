using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.SuperAdmin;

namespace Propia.Api.Controllers;

/// <summary>
/// Endpoints de la Consola de A&D GROUP (modulo 0.1 Super Admin Console).
/// Ruta: /admin/* - separados del API cliente. Auth via JWT con claim
/// is_super_admin=true (policy "SuperAdmin"). Excepto /admin/login que es anonymous.
///
/// ALCANCE MVP (paso 8 de la hoja de ruta):
///   - Login con email + password (SIN MFA TOTP - Fase 2)
///   - SIN IP allowlist - Fase 2
///   - CRUD de Organizaciones y Tenants (con justificacion en cambios de estado)
///   - Gestion de equipo A&D GROUP (con regla del ultimo SuperAdmin)
///   - Log inmutable de cada accion (trigger Postgres ya activo)
///   - Lectura de logs
///
/// FUERA DE ALCANCE MVP (proximas iteraciones):
///   - Impersonacion (modo lectura)
///   - Dashboard de salud con metricas
///   - Re-autenticacion para acciones criticas
///   - Sesiones max 4h enforced via refresh tokens
/// </summary>
[ApiController]
[Route("admin")]
public class AdminController : ControllerBase
{
    public const string SuperAdminPolicy = "SuperAdmin";

    private readonly ISuperAdminAuthService _auth;
    private readonly ISuperAdminService _svc;

    public AdminController(ISuperAdminAuthService auth, ISuperAdminService svc)
    {
        _auth = auth;
        _svc = svc;
    }

    // -------------------- Auth --------------------

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] SuperAdminLoginRequest req, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _auth.LoginAsync(req, ip, ct);
        if (result is null) return Unauthorized(new { error = "credenciales_invalidas" });
        return Ok(result);
    }

    /// <summary>Segundo paso del login cuando el user tiene MFA: valida ticket + codigo TOTP.</summary>
    [HttpPost("mfa/verify-login")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyMfaLogin([FromBody] VerifyMfaLoginRequest req, CancellationToken ct)
    {
        var result = await _auth.VerifyMfaLoginAsync(req, Ip(), ct);
        if (result is null) return Unauthorized(new { error = "mfa_invalido" });
        return Ok(result);
    }

    /// <summary>
    /// Inicia el enrollment de MFA para el usuario autenticado. Devuelve secret + URI otpauth://
    /// para que el cliente lo muestre como QR. NO marca como configurado hasta VerifyEnrollMfa.
    /// </summary>
    [HttpPost("mfa/enroll")]
    [Authorize(Policy = SuperAdminPolicy)]
    public async Task<IActionResult> EnrollMfa(CancellationToken ct)
    {
        var (id, _) = Actor();
        if (id == Guid.Empty) return Unauthorized();
        var result = await _auth.EnrollMfaAsync(id, ct);
        return Ok(result);
    }

    /// <summary>Verifica el codigo TOTP del primer escaneo y marca MfaConfigurado = true.</summary>
    [HttpPost("mfa/verify-enroll")]
    [Authorize(Policy = SuperAdminPolicy)]
    public async Task<IActionResult> VerifyEnrollMfa([FromBody] VerifyMfaEnrollRequest req, CancellationToken ct)
    {
        var (id, _) = Actor();
        if (id == Guid.Empty) return Unauthorized();
        var ok = await _auth.VerifyEnrollMfaAsync(id, req, Ip(), ct);
        if (!ok) return BadRequest(new { error = "codigo_invalido" });
        return Ok(new { mfaConfigurado = true });
    }

    // -------------------- Organizaciones --------------------

    // Ruta absoluta bajo /api para NO colisionar con la pagina Blazor @page "/admin/organizaciones"
    // (host unificado: el GET del controller y la pagina compartian ruta -> AmbiguousMatch/500).
    [HttpGet("~/api/admin/organizaciones")]
    [Authorize(Policy = SuperAdminPolicy)]
    public async Task<IActionResult> ListOrganizaciones(CancellationToken ct)
        => Ok(await _svc.ListOrganizacionesAsync(ct));

    [HttpPost("~/api/admin/organizaciones")]
    [Authorize(Policy = SuperAdminPolicy)]
    public async Task<IActionResult> CrearOrganizacion([FromBody] CrearOrganizacionRequest req, CancellationToken ct)
    {
        var (id, email) = Actor();
        try
        {
            var org = await _svc.CrearOrganizacionAsync(req, id, email, Ip(), ct);
            return CreatedAtAction(nameof(ListOrganizaciones), new { }, org);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.GetBaseException().Message }); }
    }

    [HttpPut("~/api/admin/organizaciones/{orgId:guid}/estado")]
    [Authorize(Policy = SuperAdminPolicy)]
    public async Task<IActionResult> CambiarEstadoOrganizacion(Guid orgId, [FromBody] CambiarEstadoOrganizacionRequest req, CancellationToken ct)
    {
        var (id, email) = Actor();
        try
        {
            var org = await _svc.CambiarEstadoOrganizacionAsync(orgId, req, id, email, Ip(), ct);
            return org is null ? NotFound() : Ok(org);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.GetBaseException().Message }); }
    }

    [HttpPost("~/api/admin/organizaciones/{orgId:guid}/admin")]
    [Authorize(Policy = SuperAdminPolicy)]
    public async Task<IActionResult> CrearAdminOrganizacion(Guid orgId, [FromBody] CrearAdminOrganizacionRequest req, CancellationToken ct)
    {
        var (id, email) = Actor();
        try
        {
            var admin = await _svc.CrearAdminOrganizacionAsync(orgId, req, id, email, Ip(), ct);
            return Ok(admin);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.GetBaseException().Message }); }
    }

    // -------------------- Tenants --------------------

    [HttpGet("tenants")]
    [Authorize(Policy = SuperAdminPolicy)]
    public async Task<IActionResult> ListTenants(CancellationToken ct)
        => Ok(await _svc.ListTenantsAsync(ct));

    [HttpPost("tenants")]
    [Authorize(Policy = SuperAdminPolicy)]
    public async Task<IActionResult> CrearTenant([FromBody] CrearTenantRequest req, CancellationToken ct)
    {
        var (id, email) = Actor();
        var t = await _svc.CrearTenantAsync(req, id, email, Ip(), ct);
        return CreatedAtAction(nameof(ListTenants), new { }, t);
    }

    [HttpPut("tenants/{tenantId:guid}/estado")]
    [Authorize(Policy = SuperAdminPolicy)]
    public async Task<IActionResult> CambiarEstadoTenant(Guid tenantId, [FromBody] CambiarEstadoTenantRequest req, CancellationToken ct)
    {
        var (id, email) = Actor();
        try
        {
            var t = await _svc.CambiarEstadoTenantAsync(tenantId, req, id, email, Ip(), ct);
            if (t is null) return NotFound();
            return Ok(t);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // -------------------- Equipo A&D GROUP --------------------

    // Ruta absoluta bajo /api para NO colisionar con la pagina Blazor @page "/admin/equipo".
    [HttpGet("~/api/admin/equipo")]
    [Authorize(Policy = SuperAdminPolicy)]
    public async Task<IActionResult> ListEquipo(CancellationToken ct)
        => Ok(await _svc.ListEquipoAsync(ct));

    [HttpPost("equipo")]
    [Authorize(Policy = SuperAdminPolicy)]
    public async Task<IActionResult> CrearMiembro([FromBody] CrearSuperAdminUsuarioRequest req, CancellationToken ct)
    {
        var (id, email) = Actor();
        try
        {
            var u = await _svc.CrearMiembroEquipoAsync(req, id, email, Ip(), ct);
            return CreatedAtAction(nameof(ListEquipo), new { }, u);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPut("equipo/{usuarioId:guid}/desactivar")]
    [Authorize(Policy = SuperAdminPolicy)]
    public async Task<IActionResult> DesactivarMiembro(Guid usuarioId, CancellationToken ct)
    {
        var (id, email) = Actor();
        try
        {
            var ok = await _svc.DesactivarMiembroEquipoAsync(usuarioId, id, email, Ip(), ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            // Regla del ultimo SuperAdmin
            return BadRequest(new { error = ex.Message });
        }
    }

    // -------------------- Logs --------------------

    [HttpGet("logs")]
    [Authorize(Policy = SuperAdminPolicy)]
    public async Task<IActionResult> ListLogs([FromQuery] int take = 100, CancellationToken ct = default)
        => Ok(await _svc.ListLogsAsync(Math.Clamp(take, 1, 500), ct));

    // -------------------- Helpers --------------------

    private (Guid Id, string Email) Actor()
    {
        var id = Guid.TryParse(User.FindFirstValue("user_id"), out var g) ? g : Guid.Empty;
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email") ?? "unknown";
        return (id, email);
    }

    private string? Ip() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
