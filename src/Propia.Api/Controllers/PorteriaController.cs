using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Porteria;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>
/// Endpoints REST del modulo 2.12 Porteria y Control de Acceso (spec v1.0 MVP).
/// </summary>
[ApiController]
[Route("api/porteria")]
[Authorize]
public class PorteriaController : ControllerBase
{
    private readonly IPorteriaService _svc;

    public PorteriaController(IPorteriaService svc) => _svc = svc;

    // ----- Turno -----

    [HttpGet("turnos/activo")]
    public async Task<IActionResult> GetTurnoActivo([FromQuery] Guid guardaPersonaId, [FromQuery] string puntoAcceso, CancellationToken ct)
    {
        var t = await _svc.GetTurnoActivoAsync(guardaPersonaId, puntoAcceso, ct);
        return t is null ? NotFound() : Ok(t);
    }

    [HttpGet("turnos")]
    public async Task<IActionResult> ListarTurnos([FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, CancellationToken ct)
        => Ok(await _svc.ListarTurnosAsync(desde, hasta, ct));

    [HttpPost("turnos/abrir")]
    public async Task<IActionResult> AbrirTurno([FromBody] AbrirTurnoRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.AbrirTurnoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("turnos/{id:guid}/cerrar")]
    public async Task<IActionResult> CerrarTurno(Guid id, [FromBody] CerrarTurnoRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.CerrarTurnoAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("turnos/{id:guid}/resumen")]
    public async Task<IActionResult> ResumenTurno(Guid id, CancellationToken ct)
        => Ok(await _svc.GetResumenTurnoAsync(id, ct));

    // ----- Visitantes frecuentes -----

    [HttpGet("visitantes-frecuentes")]
    public async Task<IActionResult> BuscarVF([FromQuery] string? q, CancellationToken ct)
        => Ok(await _svc.BuscarVisitantesFrecuentesAsync(q, ct));

    // ----- Autorizaciones -----

    [HttpGet("autorizaciones")]
    public async Task<IActionResult> ListarAutorizaciones([FromQuery] EstadoAutorizacion? estado, [FromQuery] Guid? unidadId, CancellationToken ct)
        => Ok(await _svc.ListarAutorizacionesAsync(estado, unidadId, ct));

    [HttpGet("autorizaciones/{id:guid}")]
    public async Task<IActionResult> GetAutorizacion(Guid id, CancellationToken ct)
    {
        var a = await _svc.GetAutorizacionAsync(id, ct);
        return a is null ? NotFound() : Ok(a);
    }

    [HttpPost("autorizaciones")]
    public async Task<IActionResult> CrearAutorizacion([FromBody] CrearAutorizacionRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.CrearAutorizacionAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("autorizaciones/{id:guid}")]
    public async Task<IActionResult> RevocarAutorizacion(Guid id, CancellationToken ct)
        => await _svc.RevocarAutorizacionAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("autorizaciones/validar")]
    public async Task<IActionResult> ValidarCodigo([FromBody] ValidarCodigoRequest req, CancellationToken ct)
        => Ok(await _svc.ValidarCodigoAsync(req, ct));

    // ----- Registro visitas -----

    [HttpPost("turnos/{turnoId:guid}/visitas")]
    public async Task<IActionResult> RegistrarVisita(Guid turnoId, [FromBody] RegistrarVisitaRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.RegistrarVisitaAsync(turnoId, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("visitas")]
    public async Task<IActionResult> ListarVisitas([FromQuery] Guid? turnoId, [FromQuery] DateOnly? fecha, CancellationToken ct)
        => Ok(await _svc.ListarVisitasAsync(turnoId, fecha, ct));

    // ----- Vehiculos -----

    [HttpGet("vehiculos")]
    public async Task<IActionResult> ListarVehiculos([FromQuery] Guid? unidadId, CancellationToken ct)
        => Ok(await _svc.ListarVehiculosAutorizadosAsync(unidadId, ct));

    [HttpPost("vehiculos")]
    public async Task<IActionResult> CrearVehiculo([FromBody] CrearVehiculoRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.CrearVehiculoAutorizadoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("vehiculos/{id:guid}")]
    public async Task<IActionResult> ActualizarVehiculo(Guid id, [FromBody] ActualizarVehiculoRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarVehiculoAutorizadoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("vehiculos/{id:guid}")]
    public async Task<IActionResult> DesactivarVehiculo(Guid id, CancellationToken ct)
        => await _svc.DesactivarVehiculoAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("turnos/{turnoId:guid}/vehiculos")]
    public async Task<IActionResult> RegistrarVehiculo(Guid turnoId, [FromBody] RegistrarVehiculoRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.RegistrarVehiculoAsync(turnoId, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("vehiculos/registros")]
    public async Task<IActionResult> ListarRegistrosVehiculo([FromQuery] Guid? turnoId, [FromQuery] DateOnly? fecha, CancellationToken ct)
        => Ok(await _svc.ListarRegistrosVehiculoAsync(turnoId, fecha, ct));

    // ----- Correspondencia -----

    [HttpGet("correspondencia")]
    public async Task<IActionResult> ListarCorrespondencia([FromQuery] EstadoCorrespondencia? estado, [FromQuery] Guid? unidadId, CancellationToken ct)
        => Ok(await _svc.ListarCorrespondenciaAsync(estado, unidadId, ct));

    [HttpPost("turnos/{turnoId:guid}/correspondencia")]
    public async Task<IActionResult> RegistrarCorrespondencia(Guid turnoId, [FromBody] RegistrarCorrespondenciaRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.RegistrarCorrespondenciaAsync(turnoId, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("correspondencia/{id:guid}/entregar")]
    public async Task<IActionResult> EntregarCorrespondencia(Guid id, [FromBody] EntregarCorrespondenciaRequest req, CancellationToken ct)
    {
        try { return await _svc.EntregarCorrespondenciaAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("correspondencia/{id:guid}/devolver")]
    public async Task<IActionResult> DevolverCorrespondencia(Guid id, [FromBody] DevolverCorrespondenciaRequest req, CancellationToken ct)
    {
        try { return await _svc.DevolverCorrespondenciaAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ----- Novedades -----

    [HttpPost("turnos/{turnoId:guid}/novedades")]
    public async Task<IActionResult> CrearNovedad(Guid turnoId, [FromBody] CrearNovedadRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.CrearNovedadAsync(turnoId, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("novedades")]
    public async Task<IActionResult> ListarNovedades([FromQuery] Guid? turnoId, [FromQuery] DateOnly? fecha, CancellationToken ct)
        => Ok(await _svc.ListarNovedadesAsync(turnoId, fecha, ct));

    // ----- Panel + configuracion -----

    [HttpGet("panel")]
    public async Task<IActionResult> Panel(CancellationToken ct) => Ok(await _svc.GetPanelMonitoreoAsync(ct));

    [HttpGet("configuracion")]
    public async Task<IActionResult> GetConfig(CancellationToken ct) => Ok(await _svc.GetConfiguracionAsync(ct));

    [HttpPut("configuracion")]
    public async Task<IActionResult> SetConfig([FromBody] ActualizarConfiguracionRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.ActualizarConfiguracionAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ----- Campos personalizados -----

    [HttpGet("campos")]
    public async Task<IActionResult> ListarCampos(CancellationToken ct) => Ok(await _svc.ListarCamposAsync(ct));

    [HttpPost("campos")]
    public async Task<IActionResult> CrearCampo([FromBody] GuardarPorteriaCampoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearCampoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("campos/{id:guid}")]
    public async Task<IActionResult> ActualizarCampo(Guid id, [FromBody] GuardarPorteriaCampoRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarCampoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("campos/{id:guid}")]
    public async Task<IActionResult> EliminarCampo(Guid id, CancellationToken ct)
        => await _svc.EliminarCampoAsync(id, ct) ? NoContent() : NotFound();
}
