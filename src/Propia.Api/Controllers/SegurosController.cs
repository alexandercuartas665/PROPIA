using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Seguros;

namespace Propia.Api.Controllers;

/// <summary>Modulo Seguros (Ola 4): polizas, campos dinamicos y reclamaciones.</summary>
[ApiController]
[Route("api/seguros")]
[Authorize]
public class SegurosController : ControllerBase
{
    private readonly ISegurosService _svc;
    public SegurosController(ISegurosService svc) => _svc = svc;

    // ---- Polizas ----
    [HttpGet("polizas")] public async Task<IActionResult> List(CancellationToken ct) => Ok(await _svc.ListPolizasAsync(ct));

    [HttpGet("polizas/{id:guid}")]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken ct)
        => await _svc.ObtenerPolizaAsync(id, ct) is { } p ? Ok(p) : NotFound();

    [HttpPost("polizas")]
    public async Task<IActionResult> Crear([FromBody] CrearPolizaRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearPolizaAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("polizas/{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] ActualizarPolizaRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarPolizaAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("polizas/{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
        => await _svc.EliminarPolizaAsync(id, ct) ? NoContent() : NotFound();

    // ---- Campos personalizados (EAV) ----
    [HttpGet("campos")] public async Task<IActionResult> ListCampos(CancellationToken ct) => Ok(await _svc.ListCamposAsync(ct));

    [HttpPost("campos")]
    public async Task<IActionResult> CrearCampo([FromBody] CrearPolizaCampoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearCampoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("campos/{campoId:guid}")]
    public async Task<IActionResult> ActualizarCampo(Guid campoId, [FromBody] ActualizarPolizaCampoRequest req, CancellationToken ct)
        => await _svc.ActualizarCampoAsync(campoId, req, ct) ? NoContent() : NotFound();

    [HttpDelete("campos/{campoId:guid}")]
    public async Task<IActionResult> EliminarCampo(Guid campoId, CancellationToken ct)
        => await _svc.EliminarCampoAsync(campoId, ct) ? NoContent() : NotFound();

    [HttpPut("polizas/{id:guid}/campo-valor/{campoId:guid}")]
    public async Task<IActionResult> GuardarCampoValor(Guid id, Guid campoId, [FromBody] GuardarPolizaCampoValorRequest req, CancellationToken ct)
        => await _svc.GuardarCampoValorAsync(id, campoId, req, ct) ? NoContent() : NotFound();

    // ---- Reclamaciones (Ola 5) ----
    [HttpGet("polizas/{id:guid}/reclamaciones")]
    public async Task<IActionResult> ListReclamaciones(Guid id, CancellationToken ct) => Ok(await _svc.ListReclamacionesAsync(id, ct));

    [HttpPost("polizas/{id:guid}/reclamaciones")]
    public async Task<IActionResult> CrearReclamacion(Guid id, [FromBody] CrearReclamacionRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearReclamacionAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("reclamaciones/{reclamacionId:guid}/cerrar")]
    public async Task<IActionResult> CerrarReclamacion(Guid reclamacionId, [FromBody] CerrarReclamacionRequest req, CancellationToken ct)
        => await _svc.CerrarReclamacionAsync(reclamacionId, req, ct) ? NoContent() : NotFound();
}
