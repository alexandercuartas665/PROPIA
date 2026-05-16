using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Calendario;

namespace Propia.Api.Controllers;

/// <summary>
/// Modulo 1.2 Calendario Multi-Copropiedad. Spec v1.0 MVP.
/// Las rutas /api/calendario/ical/{token}.ics se exponen SIN auth porque el token
/// es un identificador opaco que reemplaza la sesion (RFC 5545 esperado por Apple/Google).
/// </summary>
[ApiController]
[Route("api/calendario")]
[Authorize]
public class CalendarioController : ControllerBase
{
    private readonly ICalendarioService _svc;
    public CalendarioController(ICalendarioService svc) => _svc = svc;

    // ----- Vista agenda -----

    [HttpPost("eventos/buscar")]
    public async Task<IActionResult> ListarEventos([FromBody] FiltroCalendarioDto filtro, CancellationToken ct)
        => Ok(await _svc.ListarEventosAsync(filtro, ct));

    [HttpGet("criticos")]
    public async Task<IActionResult> ListarCriticos(CancellationToken ct)
        => Ok(await _svc.ListarCriticosAsync(ct));

    // ----- Eventos internos -----

    [HttpGet("eventos-internos")]
    public async Task<IActionResult> ListarInternos(
        [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, CancellationToken ct)
        => Ok(await _svc.ListarEventosInternosAsync(desde, hasta, ct));

    [HttpGet("eventos-internos/{id:guid}")]
    public async Task<IActionResult> GetInterno(Guid id, CancellationToken ct)
    {
        var ev = await _svc.GetEventoInternoAsync(id, ct);
        return ev is null ? NotFound() : Ok(ev);
    }

    [HttpPost("eventos-internos")]
    public async Task<IActionResult> CrearInterno([FromBody] CrearEventoInternoRequest req, CancellationToken ct)
        => Ok(await _svc.CrearEventoInternoAsync(req, ct));

    [HttpPut("eventos-internos/{id:guid}")]
    public async Task<IActionResult> ActualizarInterno(
        Guid id, [FromBody] ActualizarEventoInternoRequest req, CancellationToken ct)
        => Ok(new { actualizado = await _svc.ActualizarEventoInternoAsync(id, req, ct) });

    [HttpDelete("eventos-internos/{id:guid}")]
    public async Task<IActionResult> EliminarInterno(Guid id, CancellationToken ct)
        => Ok(new { eliminado = await _svc.EliminarEventoInternoAsync(id, ct) });

    // ----- Config personal -----

    [HttpGet("config")]
    public async Task<IActionResult> GetConfig(CancellationToken ct)
        => Ok(await _svc.GetConfigAsync(ct));

    [HttpPut("config")]
    public async Task<IActionResult> ActualizarConfig(
        [FromBody] ActualizarConfigCalendarioRequest req, CancellationToken ct)
        => Ok(await _svc.ActualizarConfigAsync(req, ct));

    [HttpPost("ical/token")]
    public async Task<IActionResult> GenerarToken(CancellationToken ct)
        => Ok(new { token = await _svc.GenerarOReGenerarIcalTokenAsync(ct) });

    [HttpDelete("ical/token")]
    public async Task<IActionResult> RevocarToken(CancellationToken ct)
        => Ok(new { revocado = await _svc.RevocarIcalTokenAsync(ct) });

    // ----- Feed publico iCal -----

    [HttpGet("ical/{token:guid}.ics")]
    [AllowAnonymous]
    public async Task<IActionResult> Feed(Guid token, CancellationToken ct)
    {
        var ics = await _svc.GenerarIcsAsync(token, ct);
        if (ics is null) return NotFound();
        return Content(ics, "text/calendar; charset=utf-8");
    }

    // ----- Resumen -----

    [HttpGet("resumen")]
    public async Task<IActionResult> GetResumen(CancellationToken ct)
        => Ok(await _svc.GetResumenAsync(ct));
}
