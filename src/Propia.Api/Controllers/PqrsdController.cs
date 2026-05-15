using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Pqrsd;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>Endpoints del modulo 2.9 PQRSD y Convivencia (spec v1.0).</summary>
[ApiController]
[Route("api/pqrsd")]
[Authorize]
public class PqrsdController : ControllerBase
{
    private readonly IPqrsdService _svc;
    public PqrsdController(IPqrsdService svc) => _svc = svc;

    // --- Catalogo ---
    [HttpGet("categorias")]
    public async Task<IActionResult> ListarCategorias(CancellationToken ct) => Ok(await _svc.ListarCategoriasAsync(ct));

    [HttpPost("categorias")]
    public async Task<IActionResult> CrearCategoria([FromBody] CrearCategoriaRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearCategoriaAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("categorias/{id:guid}")]
    public async Task<IActionResult> ActualizarCategoria(Guid id, [FromBody] ActualizarCategoriaRequest req, CancellationToken ct)
        => await _svc.ActualizarCategoriaAsync(id, req, ct) ? NoContent() : NotFound();

    [HttpDelete("categorias/{id:guid}")]
    public async Task<IActionResult> EliminarCategoria(Guid id, CancellationToken ct)
        => await _svc.EliminarCategoriaAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("categorias/restablecer")]
    public async Task<IActionResult> RestablecerCategorias(CancellationToken ct)
        => Ok(new { restablecidas = await _svc.RestablecerCategoriasBaseAsync(ct) });

    [HttpGet("plazos")]
    public async Task<IActionResult> ListarPlazos(CancellationToken ct) => Ok(await _svc.ListarPlazosAsync(ct));

    [HttpPut("plazos/{tipo}")]
    public async Task<IActionResult> ActualizarPlazo(TipoPqrsd tipo, [FromBody] ActualizarPlazoRequest req, CancellationToken ct)
        => await _svc.ActualizarPlazoAsync(tipo, req, ct) ? NoContent() : NotFound();

    // --- Bandeja + ficha ---
    [HttpGet("bandeja")]
    public async Task<IActionResult> Bandeja(
        [FromQuery] EstadoPqrsd? estado, [FromQuery] TipoPqrsd? tipo,
        [FromQuery] Guid? categoriaId, [FromQuery] string? q, CancellationToken ct)
        => Ok(await _svc.GetBandejaAsync(estado, tipo, categoriaId, q, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetExpediente(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetExpedienteAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    // --- Radicacion ---
    [HttpPost]
    public async Task<IActionResult> Radicar([FromBody] RadicarPqrsdRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.RadicarAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Vista del residente ---
    [HttpGet("mis-pqrsd")]
    public async Task<IActionResult> MisPqrsd(CancellationToken ct) => Ok(await _svc.ListarMisPqrsdAsync(ct));

    // --- Ciclo de gestion ---
    [HttpPut("{id:guid}/tomar")]
    public async Task<IActionResult> Tomar(Guid id, [FromBody] TomarExpedienteRequest req, CancellationToken ct)
        => await _svc.TomarExpedienteAsync(id, req, ct) ? NoContent() : NotFound();

    [HttpPut("{id:guid}/responder")]
    public async Task<IActionResult> Responder(Guid id, [FromBody] ResponderExpedienteRequest req, CancellationToken ct)
    {
        try { return await _svc.ResponderAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}/inconformidad")]
    public async Task<IActionResult> Inconformidad(Guid id, [FromBody] ManifestarInconformidadRequest req, CancellationToken ct)
    {
        try { return await _svc.ManifestarInconformidadAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}/cerrar")]
    public async Task<IActionResult> Cerrar(Guid id, [FromBody] CerrarDefinitivoRequest req, CancellationToken ct)
    {
        try { return await _svc.CerrarDefinitivoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Tutela ---
    [HttpPut("{id:guid}/tutela")]
    public async Task<IActionResult> ActivarTutela(Guid id, [FromBody] ActivarTutelaRequest req, CancellationToken ct)
    {
        try { return await _svc.ActivarTutelaAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Comite ---
    [HttpPost("{id:guid}/comite")]
    public async Task<IActionResult> EscalarAComite(Guid id, [FromBody] EscalarAComiteRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.EscalarAComiteAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("comite/{sesionId:guid}/registrar")]
    public async Task<IActionResult> RegistrarSesionComite(Guid sesionId, [FromBody] RegistrarSesionComiteRequest req, CancellationToken ct)
    {
        try { return await _svc.RegistrarSesionComiteAsync(sesionId, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
