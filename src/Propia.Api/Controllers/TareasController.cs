using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Tareas;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

[ApiController]
[Route("api/tareas")]
[Authorize]
public class TareasController : ControllerBase
{
    private readonly ITareasService _svc;
    public TareasController(ITareasService svc) => _svc = svc;

    // --- Estados ---
    [HttpGet("estados")]
    public async Task<IActionResult> ListarEstados(CancellationToken ct) => Ok(await _svc.ListarEstadosAsync(ct));

    [HttpPost("estados")]
    public async Task<IActionResult> CrearEstado([FromBody] CrearEstadoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearEstadoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("estados/{id:guid}")]
    public async Task<IActionResult> ActualizarEstado(Guid id, [FromBody] ActualizarEstadoRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarEstadoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("estados/{id:guid}")]
    public async Task<IActionResult> EliminarEstado(Guid id, CancellationToken ct)
    {
        try { return await _svc.EliminarEstadoAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Etiquetas ---
    [HttpGet("etiquetas")]
    public async Task<IActionResult> ListarEtiquetas(CancellationToken ct) => Ok(await _svc.ListarEtiquetasAsync(ct));

    [HttpPost("etiquetas")]
    public async Task<IActionResult> CrearEtiqueta([FromBody] CrearEtiquetaRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearEtiquetaAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("etiquetas/{id:guid}")]
    public async Task<IActionResult> ActualizarEtiqueta(Guid id, [FromBody] ActualizarEtiquetaRequest req, CancellationToken ct)
        => await _svc.ActualizarEtiquetaAsync(id, req, ct) ? NoContent() : NotFound();

    [HttpDelete("etiquetas/{id:guid}")]
    public async Task<IActionResult> EliminarEtiqueta(Guid id, CancellationToken ct)
        => await _svc.EliminarEtiquetaAsync(id, ct) ? NoContent() : NotFound();

    // --- Tareas ---
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] Guid? estadoId, [FromQuery] PrioridadTarea? prioridad,
        [FromQuery] Guid? asignadoPersonaId, [FromQuery] Guid? padreId,
        [FromQuery] bool? soloRaiz, [FromQuery] string? q, CancellationToken ct)
        => Ok(await _svc.ListarTareasAsync(estadoId, prioridad, asignadoPersonaId, padreId, soloRaiz, q, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetTareaAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearTareaRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearTareaAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] ActualizarTareaRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarTareaAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}/estado")]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] CambiarEstadoRequest req, CancellationToken ct)
    {
        try { return await _svc.CambiarEstadoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Comentarios / Etiquetas / Colaboradores ---
    [HttpPost("{id:guid}/comentarios")]
    public async Task<IActionResult> AgregarComentario(Guid id, [FromBody] CrearComentarioRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarComentarioAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/etiquetas")]
    public async Task<IActionResult> AsignarEtiqueta(Guid id, [FromBody] AsignarEtiquetaRequest req, CancellationToken ct)
        => await _svc.AsignarEtiquetaAsync(id, req, ct) ? NoContent() : NotFound();

    [HttpDelete("{id:guid}/etiquetas/{etiquetaId:guid}")]
    public async Task<IActionResult> RemoverEtiqueta(Guid id, Guid etiquetaId, CancellationToken ct)
        => await _svc.RemoverEtiquetaAsync(id, etiquetaId, ct) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/colaboradores")]
    public async Task<IActionResult> AgregarColaborador(Guid id, [FromBody] AgregarColaboradorRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarColaboradorAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("{id:guid}/colaboradores/{colaboradorId:guid}")]
    public async Task<IActionResult> RemoverColaborador(Guid id, Guid colaboradorId, CancellationToken ct)
        => await _svc.RemoverColaboradorAsync(id, colaboradorId, ct) ? NoContent() : NotFound();

    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen(CancellationToken ct) => Ok(await _svc.GetResumenAsync(ct));

    // --- Dependencias (Fase 2) ---

    [HttpGet("{id:guid}/dependencias")]
    public async Task<IActionResult> ListarDependencias(Guid id, CancellationToken ct)
        => Ok(await _svc.ListarDependenciasAsync(id, ct));

    [HttpPost("{id:guid}/dependencias")]
    public async Task<IActionResult> AgregarDependencia(Guid id, [FromBody] AgregarDependenciaRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarDependenciaAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("{id:guid}/dependencias/{dependenciaId:guid}")]
    public async Task<IActionResult> RemoverDependencia(Guid id, Guid dependenciaId, CancellationToken ct)
        => await _svc.RemoverDependenciaAsync(id, dependenciaId, ct) ? NoContent() : NotFound();

    // --- Bulk actions (Fase 2) ---

    [HttpPost("bulk/estado")]
    public async Task<IActionResult> BulkCambiarEstado([FromBody] BulkCambiarEstadoRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.BulkCambiarEstadoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("bulk/prioridad")]
    public async Task<IActionResult> BulkCambiarPrioridad([FromBody] BulkCambiarPrioridadRequest req, CancellationToken ct)
        => Ok(await _svc.BulkCambiarPrioridadAsync(req, ct));

    [HttpPost("bulk/asignado")]
    public async Task<IActionResult> BulkAsignarPersona([FromBody] BulkAsignarPersonaRequest req, CancellationToken ct)
        => Ok(await _svc.BulkAsignarPersonaAsync(req, ct));

    // ----- Tableros de trabajo (2.10) -----
    [HttpGet("tableros")]
    public async Task<IActionResult> ListarTableros(CancellationToken ct) => Ok(await _svc.ListarTablerosAsync(ct));

    [HttpGet("tableros/{id:guid}")]
    public async Task<IActionResult> GetTablero(Guid id, CancellationToken ct)
    {
        var t = await _svc.GetTableroAsync(id, ct);
        return t is null ? NotFound() : Ok(t);
    }

    [HttpPost("tableros")]
    public async Task<IActionResult> CrearTablero([FromBody] GuardarTableroRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearTableroAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("tableros/{id:guid}")]
    public async Task<IActionResult> ActualizarTablero(Guid id, [FromBody] GuardarTableroRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarTableroAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("tableros/{id:guid}")]
    public async Task<IActionResult> EliminarTablero(Guid id, CancellationToken ct)
        => await _svc.EliminarTableroAsync(id, ct) ? NoContent() : NotFound();
}
