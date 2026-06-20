using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Reservas;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>
/// Endpoints REST del modulo 2.13 Reservas de Zonas Comunes (spec v1.0 MVP).
/// </summary>
[ApiController]
[Route("api/reservas")]
[Authorize]
public class ReservasController : ControllerBase
{
    private readonly IReservasService _svc;

    public ReservasController(IReservasService svc) => _svc = svc;

    // ----- Configuracion de zona -----

    [HttpGet("configs")]
    public async Task<IActionResult> ListarConfigs(CancellationToken ct) => Ok(await _svc.ListarConfigsAsync(ct));

    [HttpGet("configs/{zonaComunId:guid}")]
    public async Task<IActionResult> GetConfig(Guid zonaComunId, CancellationToken ct)
    {
        var c = await _svc.GetConfigAsync(zonaComunId, ct);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpPut("configs")]
    public async Task<IActionResult> GuardarConfig([FromBody] GuardarConfigRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.GuardarConfigAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ----- Galeria + disponibilidad -----

    [HttpGet("galeria")]
    public async Task<IActionResult> Galeria([FromQuery] bool soloVisibles = false, CancellationToken ct = default)
        => Ok(await _svc.ListarGaleriaAsync(soloVisibles, ct));

    [HttpGet("disponibilidad/{zonaComunId:guid}")]
    public async Task<IActionResult> Disponibilidad(Guid zonaComunId, [FromQuery] DateOnly desde, [FromQuery] DateOnly hasta, CancellationToken ct)
    {
        try { return Ok(await _svc.CalcularDisponibilidadAsync(zonaComunId, desde, hasta, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ----- Reservas -----

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        [FromQuery] Guid? zonaId,
        [FromQuery] Guid? personaId,
        [FromQuery] EstadoReserva? estado,
        CancellationToken ct)
        => Ok(await _svc.ListarReservasAsync(desde, hasta, zonaId, personaId, estado, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var r = await _svc.GetReservaAsync(id, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearReservaRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.CrearReservaAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/cancelar-residente")]
    public async Task<IActionResult> CancelarResidente(Guid id, [FromBody] CancelarReservaRequest req, CancellationToken ct)
    {
        try { return await _svc.CancelarComoResidenteAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/cancelar-admin")]
    public async Task<IActionResult> CancelarAdmin(Guid id, [FromBody] CancelarReservaRequest req, CancellationToken ct)
    {
        try { return await _svc.CancelarComoAdminAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/aprobar")]
    public async Task<IActionResult> Aprobar(Guid id, CancellationToken ct)
    {
        try { return await _svc.AprobarAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ----- Bloqueos -----

    [HttpGet("bloqueos")]
    public async Task<IActionResult> ListarBloqueos([FromQuery] Guid? zonaId, [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, CancellationToken ct)
        => Ok(await _svc.ListarBloqueosAsync(zonaId, desde, hasta, ct));

    [HttpPost("bloqueos")]
    public async Task<IActionResult> CrearBloqueo([FromBody] CrearBloqueoRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.CrearBloqueoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("bloqueos/{id:guid}")]
    public async Task<IActionResult> EliminarBloqueo(Guid id, CancellationToken ct)
        => await _svc.EliminarBloqueoAsync(id, ct) ? NoContent() : NotFound();

    // ----- Vista portero (consumida por 2.12) -----

    [HttpGet("dia/{fecha}")]
    public async Task<IActionResult> Dia(DateOnly fecha, CancellationToken ct)
        => Ok(await _svc.ListarReservasDelDiaAsync(fecha, ct));

    // ----- Resumen -----

    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen(CancellationToken ct) => Ok(await _svc.GetResumenAsync(ct));
}
