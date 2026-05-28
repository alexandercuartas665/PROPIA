using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Onboarding;

namespace Propia.Api.Controllers;

/// <summary>
/// Endpoints del modulo 2.1 Onboarding y Activacion - wizard de 5 pasos.
/// Todos son [AllowAnonymous] - el usuario aun no tiene cuenta hasta paso 5.
/// </summary>
[ApiController]
[Route("api/onboarding")]
[AllowAnonymous]
public class OnboardingController : ControllerBase
{
    private readonly IOnboardingService _svc;
    public OnboardingController(IOnboardingService svc) => _svc = svc;

    [HttpPost("paso1/registrar")]
    public async Task<IActionResult> Paso1Registrar([FromBody] RegistroRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.Paso1RegistrarAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("paso1/confirmar-email")]
    public async Task<IActionResult> Paso1ConfirmarEmail([FromBody] ConfirmarEmailRequest req, CancellationToken ct)
    {
        var ok = await _svc.Paso1ConfirmarEmailAsync(req, ct);
        return ok ? Ok(new { confirmado = true }) : NotFound();
    }

    [HttpPost("paso2/clasificar")]
    public async Task<IActionResult> Paso2Clasificar([FromBody] ClasificacionRequest req, CancellationToken ct)
    {
        try
        {
            var r = await _svc.Paso2ClasificarAsync(req, ct);
            return r is null ? NotFound() : Ok(r);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("paso3/organizacion")]
    public async Task<IActionResult> Paso3Organizacion([FromBody] DatosOrganizacionRequest req, CancellationToken ct)
    {
        try
        {
            var r = await _svc.Paso3OrganizacionAsync(req, ct);
            return r is null ? NotFound() : Ok(r);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("paso4/copropiedad")]
    public async Task<IActionResult> Paso4Copropiedad([FromBody] DatosCopropiedadRequest req, CancellationToken ct)
    {
        try
        {
            var r = await _svc.Paso4CopropiedadAsync(req, ct);
            return r is null ? NotFound() : Ok(r);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("paso5/activar")]
    public async Task<IActionResult> Paso5Activar([FromBody] ActivacionRequest req, CancellationToken ct)
    {
        try
        {
            var r = await _svc.Paso5ActivarAsync(req, ct);
            return r is null ? NotFound() : Ok(r);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("status/{sessionId:guid}")]
    public async Task<IActionResult> Status(Guid sessionId, CancellationToken ct)
    {
        var s = await _svc.GetStatusAsync(sessionId, ct);
        return s is null ? NotFound() : Ok(s);
    }

    /// <summary>
    /// Estado del onboarding del usuario AUTENTICADO. Lo usa el frontend tras login para
    /// decidir si redirigir a /onboarding/continuar (Completado=false) o a /mi-copropiedad
    /// (Completado=true).
    /// </summary>
    [HttpGet("mi-sesion-pendiente")]
    [Authorize]
    public async Task<IActionResult> MiSesionPendiente(CancellationToken ct)
    {
        var userId = User.FindFirstValue("user_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized();
        return Ok(await _svc.GetMiSesionPendienteAsync(uid, ct));
    }
}
