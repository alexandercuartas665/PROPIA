using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Asambleas;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

[ApiController]
[Route("api/asambleas")]
[Authorize]
public class AsambleaController : ControllerBase
{
    private readonly IAsambleaService _svc;
    public AsambleaController(IAsambleaService svc) => _svc = svc;

    // Bandeja + ficha
    [HttpGet]
    public async Task<IActionResult> Bandeja([FromQuery] EstadoSesion? estado, [FromQuery] TipoSesion? tipo, CancellationToken ct)
        => Ok(await _svc.GetBandejaAsync(estado, tipo, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSesion(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetSesionAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    // Creacion + edicion
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearSesionRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearSesionAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("puntos/{id:guid}")]
    public async Task<IActionResult> ActualizarPunto(Guid id, [FromBody] ActualizarPuntoRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarPuntoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/documentos")]
    public async Task<IActionResult> AgregarDocumento(Guid id, [FromBody] AgregarDocumentoRequest req, CancellationToken ct)
        => Created("", await _svc.AgregarDocumentoAsync(id, req, ct));

    [HttpDelete("documentos/{id:guid}")]
    public async Task<IActionResult> EliminarDocumento(Guid id, CancellationToken ct)
    {
        try { return await _svc.EliminarDocumentoAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Citacion
    [HttpPut("{id:guid}/citar")]
    public async Task<IActionResult> Citar(Guid id, [FromBody] EnviarCitacionRequest req, CancellationToken ct)
    {
        try { return await _svc.EnviarCitacionAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Poderes
    [HttpPost("{id:guid}/poderes")]
    public async Task<IActionResult> OtorgarPoder(Guid id, [FromBody] OtorgarPoderRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.OtorgarPoderAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("poderes/{id:guid}/decidir")]
    public async Task<IActionResult> DecidirPoder(Guid id, [FromBody] DecidirPoderRequest req, CancellationToken ct)
    {
        try { return await _svc.DecidirPoderAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Sala
    [HttpPut("{id:guid}/abrir-sala")]
    public async Task<IActionResult> AbrirSala(Guid id, CancellationToken ct)
    {
        try { return await _svc.AbrirSalaAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}/check-in")]
    public async Task<IActionResult> CheckIn(Guid id, [FromBody] CheckInParticipanteRequest req, CancellationToken ct)
    {
        try { return await _svc.CheckInParticipanteAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Votaciones
    [HttpPost("{id:guid}/votaciones")]
    public async Task<IActionResult> AbrirVotacion(Guid id, [FromBody] AbrirVotacionRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AbrirVotacionAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("votaciones/{id:guid}/votar")]
    public async Task<IActionResult> Votar(Guid id, [FromBody] EmitirVotoRequest req, CancellationToken ct)
    {
        try { return await _svc.EmitirVotoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("votaciones/{id:guid}/cerrar")]
    public async Task<IActionResult> CerrarVotacion(Guid id, CancellationToken ct)
    {
        try { return Ok(await _svc.CerrarVotacionAsync(id, new CerrarVotacionRequest(), ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Cierre
    [HttpPut("{id:guid}/cerrar")]
    public async Task<IActionResult> CerrarSesion(Guid id, [FromBody] CerrarSesionRequest req, CancellationToken ct)
    {
        try { return await _svc.CerrarSesionAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Acta
    [HttpPost("{id:guid}/acta")]
    public async Task<IActionResult> GenerarActa(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GenerarActaAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPut("actas/{id:guid}/firmar")]
    public async Task<IActionResult> FirmarActa(Guid id, [FromBody] FirmarActaRequest req, CancellationToken ct)
    {
        try { return await _svc.FirmarActaAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("actas/{id:guid}/publicar")]
    public async Task<IActionResult> PublicarActa(Guid id, CancellationToken ct)
    {
        try { return await _svc.PublicarActaAsync(id, new PublicarActaRequest(), ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Config
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig(CancellationToken ct) => Ok(await _svc.GetConfigAsync(ct));

    [HttpPut("config")]
    public async Task<IActionResult> ActualizarConfig([FromBody] AsambleaConfigDto req, CancellationToken ct)
        => await _svc.ActualizarConfigAsync(req, ct) ? NoContent() : NotFound();
}
