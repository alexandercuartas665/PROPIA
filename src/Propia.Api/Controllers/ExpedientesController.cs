using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Documentos;

namespace Propia.Api.Controllers;

/// <summary>
/// Modulo 2.15 Documentos - vista "Expedientes" (TRD). Configuracion documental + expedientes.
/// Requiere JWT del tenant. El "Archivo central" sigue en DocumentosController.
/// </summary>
[ApiController]
[Route("api/expedientes")]
[Authorize]
public class ExpedientesController : ControllerBase
{
    private readonly IExpedientesService _svc;

    public ExpedientesController(IExpedientesService svc) => _svc = svc;

    // ----- Configuracion documental (TRD) -----
    [HttpGet("trd")]
    public async Task<IActionResult> Trd(CancellationToken ct) => Ok(await _svc.ListarTrdAsync(ct));

    [HttpPost("trd/series")]
    public async Task<IActionResult> CrearSerie([FromBody] CrearSerieRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.CrearSerieAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("trd/series/{id:guid}")]
    public async Task<IActionResult> EliminarSerie(Guid id, CancellationToken ct)
        => await _svc.EliminarSerieAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("trd/subseries")]
    public async Task<IActionResult> CrearSubserie([FromBody] CrearSubserieRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.CrearSubserieAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("trd/subseries/{id:guid}")]
    public async Task<IActionResult> EliminarSubserie(Guid id, CancellationToken ct)
        => await _svc.EliminarSubserieAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("trd/tipologias")]
    public async Task<IActionResult> CrearTipologiaCfg([FromBody] CrearTipologiaCfgRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.CrearTipologiaCfgAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("trd/tipologias/{id:guid}")]
    public async Task<IActionResult> EliminarTipologiaCfg(Guid id, CancellationToken ct)
        => await _svc.EliminarTipologiaCfgAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("trd/campos")]
    public async Task<IActionResult> CrearCampoCfg([FromBody] CrearCampoCfgRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.CrearCampoCfgAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("trd/campos/{id:guid}")]
    public async Task<IActionResult> EliminarCampoCfg(Guid id, CancellationToken ct)
        => await _svc.EliminarCampoCfgAsync(id, ct) ? NoContent() : NotFound();

    // ----- Expedientes -----
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct) => Ok(await _svc.ListarExpedientesAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var e = await _svc.GetExpedienteAsync(id, ct);
        return e is null ? NotFound() : Ok(e);
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearExpedienteRequest req, CancellationToken ct)
    {
        try
        {
            var e = await _svc.CrearExpedienteAsync(req, ct);
            return CreatedAtAction(nameof(Get), new { id = e.Id }, e);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/tipologias")]
    public async Task<IActionResult> AgregarTipologia(Guid id, [FromBody] AgregarTipologiaExpRequest req, CancellationToken ct)
    {
        try { return await _svc.AgregarTipologiaAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("tipologias/{tipologiaId:guid}")]
    public async Task<IActionResult> EliminarTipologia(Guid tipologiaId, CancellationToken ct)
        => await _svc.EliminarTipologiaAsync(tipologiaId, ct) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/campos")]
    public async Task<IActionResult> AgregarCampo(Guid id, [FromBody] AgregarCampoExpRequest req, CancellationToken ct)
    {
        try { return await _svc.AgregarCampoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("campos/{campoId:guid}")]
    public async Task<IActionResult> EliminarCampo(Guid campoId, CancellationToken ct)
        => await _svc.EliminarCampoAsync(campoId, ct) ? NoContent() : NotFound();

    [HttpPost("tipologias/{tipologiaId:guid}/cargar")]
    public async Task<IActionResult> Cargar(Guid tipologiaId, [FromBody] CargarTipologiaRequest req, CancellationToken ct)
    {
        try { return await _svc.CargarTipologiaAsync(tipologiaId, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (FormatException) { return BadRequest(new { error = "Contenido base64 invalido." }); }
    }

    [HttpDelete("tipologias/{tipologiaId:guid}/archivo")]
    public async Task<IActionResult> QuitarArchivo(Guid tipologiaId, CancellationToken ct)
        => await _svc.QuitarArchivoAsync(tipologiaId, ct) ? NoContent() : NotFound();

    [HttpGet("tipologias/{tipologiaId:guid}/descargar")]
    public async Task<IActionResult> Descargar(Guid tipologiaId, CancellationToken ct)
    {
        var dto = await _svc.DescargarTipologiaAsync(tipologiaId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }
}
