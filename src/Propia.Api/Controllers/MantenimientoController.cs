using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Api.Authorization;
using Propia.Application.Mantenimiento;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>
/// Endpoints REST del modulo 2.11 Mantenimiento y Activos.
/// Spec v1.0 MVP - panel de activos, planes preventivos, intervenciones, bitacora.
/// </summary>
[ApiController]
[Route("api/mantenimiento")]

[Authorize]
public class MantenimientoController : ControllerBase
{
    private readonly IMantenimientoService _svc;

    public MantenimientoController(IMantenimientoService svc) => _svc = svc;

    // ----- Panel y resumen -----

    [HttpGet("activos")]
    public async Task<IActionResult> ListarActivosPanel(
        [FromQuery] TipoActivoMantenimiento? activoTipo,
        [FromQuery] SemaforoMantenimiento? semaforo,
        [FromQuery] string? q,
        CancellationToken ct)
        => Ok(await _svc.ListarActivosPanelAsync(activoTipo, semaforo, q, ct));

    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen(CancellationToken ct)
        => Ok(await _svc.GetResumenAsync(ct));

    // ----- Planes preventivos -----

    [HttpGet("planes")]
    public async Task<IActionResult> ListarPlanes(
        [FromQuery] TipoActivoMantenimiento? activoTipo,
        [FromQuery] Guid? activoId,
        [FromQuery] bool? activos,
        CancellationToken ct)
        => Ok(await _svc.ListarPlanesAsync(activoTipo, activoId, activos, ct));

    [HttpGet("planes/{id:guid}")]
    public async Task<IActionResult> GetPlan(Guid id, CancellationToken ct)
    {
        var p = await _svc.GetPlanAsync(id, ct);
        return p is null ? NotFound() : Ok(p);
    }

    [RequierePermiso(ModuloCodigo.Mantenimiento, AccionPermiso.Crear)]
    [HttpPost("planes")]
    public async Task<IActionResult> CrearPlan([FromBody] CrearPlanRequest req, CancellationToken ct)
    {
        try
        {
            var p = await _svc.CrearPlanAsync(req, ct);
            return CreatedAtAction(nameof(GetPlan), new { id = p.Id }, p);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Mantenimiento, AccionPermiso.Editar)]
    [HttpPut("planes/{id:guid}")]
    public async Task<IActionResult> ActualizarPlan(Guid id, [FromBody] ActualizarPlanRequest req, CancellationToken ct)
    {
        try
        {
            var ok = await _svc.ActualizarPlanAsync(id, req, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Mantenimiento, AccionPermiso.Eliminar)]
    [HttpDelete("planes/{id:guid}")]
    public async Task<IActionResult> DesactivarPlan(Guid id, CancellationToken ct)
    {
        var ok = await _svc.DesactivarPlanAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }

    [RequierePermiso(ModuloCodigo.Mantenimiento, AccionPermiso.Aprobar)]
    [HttpPost("planes/{id:guid}/reactivar")]
    public async Task<IActionResult> ReactivarPlan(Guid id, CancellationToken ct)
    {
        var ok = await _svc.ReactivarPlanAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }

    // ----- Intervenciones -----

    [HttpGet("intervenciones")]
    public async Task<IActionResult> ListarIntervenciones(
        [FromQuery] EstadoIntervencion? estado,
        [FromQuery] TipoIntervencionMantenimiento? tipo,
        [FromQuery] TipoActivoMantenimiento? activoTipo,
        [FromQuery] Guid? activoId,
        [FromQuery] Guid? proveedorId,
        [FromQuery] string? q,
        CancellationToken ct)
        => Ok(await _svc.ListarIntervencionesAsync(estado, tipo, activoTipo, activoId, proveedorId, q, ct));

    [HttpGet("intervenciones/{id:guid}")]
    public async Task<IActionResult> GetIntervencion(Guid id, CancellationToken ct)
    {
        var i = await _svc.GetIntervencionAsync(id, ct);
        return i is null ? NotFound() : Ok(i);
    }

    [RequierePermiso(ModuloCodigo.Mantenimiento, AccionPermiso.Crear)]
    [HttpPost("intervenciones")]
    public async Task<IActionResult> CrearIntervencion([FromBody] CrearIntervencionRequest req, CancellationToken ct)
    {
        try
        {
            var i = await _svc.CrearIntervencionAsync(req, ct);
            return CreatedAtAction(nameof(GetIntervencion), new { id = i.Id }, i);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Mantenimiento, AccionPermiso.Editar)]
    [HttpPut("intervenciones/{id:guid}")]
    public async Task<IActionResult> ActualizarIntervencion(Guid id, [FromBody] ActualizarIntervencionRequest req, CancellationToken ct)
    {
        try
        {
            var ok = await _svc.ActualizarIntervencionAsync(id, req, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Mantenimiento, AccionPermiso.Crear)]
    [HttpPost("intervenciones/{id:guid}/estado")]
    public async Task<IActionResult> CambiarEstadoIntervencion(Guid id, [FromBody] CambiarEstadoIntervencionRequest req, CancellationToken ct)
    {
        try
        {
            var ok = await _svc.CambiarEstadoIntervencionAsync(id, req, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Mantenimiento, AccionPermiso.Aprobar)]
    [HttpPost("intervenciones/{id:guid}/cerrar")]
    public async Task<IActionResult> CerrarIntervencion(Guid id, [FromBody] CerrarIntervencionRequest req, CancellationToken ct)
    {
        try
        {
            var ok = await _svc.CerrarIntervencionAsync(id, req, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Mantenimiento, AccionPermiso.Crear)]
    [HttpPost("intervenciones/{id:guid}/cancelar")]
    public async Task<IActionResult> CancelarIntervencion(Guid id, [FromBody] CancelarIntervencionRequest req, CancellationToken ct)
    {
        try
        {
            var ok = await _svc.CancelarIntervencionAsync(id, req, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ----- Bitacora -----

    [HttpGet("intervenciones/{id:guid}/bitacora")]
    public async Task<IActionResult> ListarBitacora(Guid id, CancellationToken ct)
        => Ok(await _svc.ListarBitacoraAsync(id, ct));

    [RequierePermiso(ModuloCodigo.Mantenimiento, AccionPermiso.Crear)]
    [HttpPost("intervenciones/{id:guid}/bitacora")]
    public async Task<IActionResult> AgregarBitacora(Guid id, [FromBody] AgregarBitacoraRequest req, CancellationToken ct)
    {
        try
        {
            var entrada = await _svc.AgregarEntradaBitacoraAsync(id, req, ct);
            return Ok(entrada);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ----- Cambio de estado del activo -----

    [RequierePermiso(ModuloCodigo.Mantenimiento, AccionPermiso.Crear)]
    [HttpPost("activos/estado")]
    public async Task<IActionResult> CambiarEstadoActivo([FromBody] CambioEstadoActivoRequest req, CancellationToken ct)
    {
        try
        {
            var ok = await _svc.CambiarEstadoActivoAsync(req, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("activos/{tipo}/{id:guid}/historial-estado")]
    public async Task<IActionResult> ListarHistorialEstado(TipoActivoMantenimiento tipo, Guid id, CancellationToken ct)
        => Ok(await _svc.ListarHistorialEstadoAsync(tipo, id, ct));
}
