using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Presupuesto;
using Propia.Api.Authorization;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>
/// Endpoints del modulo 2.6 Presupuesto, Cuotas y Pagos (spec v1.0).
/// </summary>
[ApiController]
[Route("api/presupuesto")]
[Authorize]
// S-06 (auditoria): RBAC por accion sobre la matriz de permisos (Administrador siempre pasa).
[RequierePermiso(ModuloCodigo.Presupuesto, AccionPermiso.Ver)]
public class PresupuestoController : ControllerBase
{
    private readonly IPresupuestoService _svc;

    public PresupuestoController(IPresupuestoService svc) => _svc = svc;

    // --- Editor ---
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct) => Ok(await _svc.ListarPresupuestosAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detalle(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetPresupuestoDetalleAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [RequierePermiso(ModuloCodigo.Presupuesto, AccionPermiso.Crear)]
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearPresupuestoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearPresupuestoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Presupuesto, AccionPermiso.Editar)]
    [HttpPut("rubros/{id:guid}")]
    public async Task<IActionResult> ActualizarRubro(Guid id, [FromBody] ActualizarRubroRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarRubroAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Presupuesto, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/rubros")]
    public async Task<IActionResult> AgregarRubro(Guid id, [FromBody] AgregarRubroPersonalizadoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarRubroPersonalizadoAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Presupuesto, AccionPermiso.Eliminar)]
    [HttpDelete("rubros/{id:guid}")]
    public async Task<IActionResult> EliminarRubro(Guid id, CancellationToken ct)
    {
        try { return await _svc.EliminarRubroAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Presupuesto, AccionPermiso.Editar)]
    [HttpPut("{id:guid}/enviar-aprobacion")]
    public async Task<IActionResult> EnviarAprobacion(Guid id, CancellationToken ct)
    {
        try { return await _svc.EnviarAAprobacionAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Presupuesto, AccionPermiso.Aprobar)]
    [HttpPut("{id:guid}/aprobar")]
    public async Task<IActionResult> Aprobar(Guid id, [FromBody] AprobarPresupuestoRequest req, CancellationToken ct)
    {
        try { return await _svc.AprobarPresupuestoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Presupuesto, AccionPermiso.Aprobar)]
    [HttpPut("{id:guid}/activar")]
    public async Task<IActionResult> Activar(Guid id, CancellationToken ct)
    {
        try { return await _svc.ActivarVigenciaAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Presupuesto, AccionPermiso.Aprobar)]
    [HttpPut("{id:guid}/cerrar")]
    public async Task<IActionResult> Cerrar(Guid id, CancellationToken ct)
        => await _svc.CerrarVigenciaAsync(id, ct) ? NoContent() : NotFound();

    [RequierePermiso(ModuloCodigo.Presupuesto, AccionPermiso.Eliminar)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> EliminarBorrador(Guid id, CancellationToken ct)
    {
        try { return await _svc.EliminarBorradorAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Liquidacion ---
    [RequierePermiso(ModuloCodigo.Presupuesto, AccionPermiso.Crear)]
    [HttpPost("liquidaciones")]
    public async Task<IActionResult> EmitirLiquidacion([FromBody] EmitirLiquidacionRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.EmitirLiquidacionAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("liquidaciones")]
    public async Task<IActionResult> ListarLiquidaciones([FromQuery] Guid? presupuestoId, CancellationToken ct)
        => Ok(await _svc.ListarLiquidacionesAsync(presupuestoId, ct));

    [HttpGet("liquidaciones/unidad/{id:guid}")]
    public async Task<IActionResult> GetLiquidacionUnidad(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetLiquidacionUnidadAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    // --- Panel de recaudo ---
    [HttpGet("recaudo")]
    public async Task<IActionResult> RecaudoResumen([FromQuery] DateOnly periodo, CancellationToken ct)
        => Ok(await _svc.GetRecaudoResumenAsync(periodo, ct));

    [HttpGet("recaudo/unidades")]
    public async Task<IActionResult> RecaudoUnidades([FromQuery] DateOnly periodo, [FromQuery] EstadoPagoLiquidacion? estado, CancellationToken ct)
        => Ok(await _svc.ListarUnidadesRecaudoAsync(periodo, estado, ct));

    // --- Pagos ---
    [RequierePermiso(ModuloCodigo.Presupuesto, AccionPermiso.Crear)]
    [HttpPost("pagos/manual")]
    public async Task<IActionResult> RegistrarPagoManual([FromBody] RegistrarPagoManualRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.RegistrarPagoManualAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("pagos")]
    public async Task<IActionResult> ListarPagos([FromQuery] Guid? unidadId, CancellationToken ct)
        => Ok(await _svc.ListarPagosAsync(unidadId, ct));

    // --- Cuotas extraordinarias ---
    [HttpGet("cuotas-extraordinarias")]
    public async Task<IActionResult> ListarCuotasExtra(CancellationToken ct)
        => Ok(await _svc.ListarCuotasExtraordinariasAsync(ct));

    [RequierePermiso(ModuloCodigo.Presupuesto, AccionPermiso.Crear)]
    [HttpPost("cuotas-extraordinarias")]
    public async Task<IActionResult> CrearCuotaExtra([FromBody] CrearCuotaExtraordinariaRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearCuotaExtraordinariaAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Presupuesto, AccionPermiso.Aprobar)]
    [HttpPut("cuotas-extraordinarias/{id:guid}/aprobar")]
    public async Task<IActionResult> AprobarCuotaExtra(Guid id, [FromBody] AprobarCuotaExtraordinariaRequest req, CancellationToken ct)
    {
        try { return await _svc.AprobarCuotaExtraordinariaAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Vista del residente ---
    [HttpGet("mi-cuota/{unidadId:guid}")]
    public async Task<IActionResult> MiCuota(Guid unidadId, [FromQuery] DateOnly? periodo, CancellationToken ct)
    {
        var dto = await _svc.GetMiCuotaAsync(unidadId, periodo, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    // --- Auditoria ---
    [HttpGet("auditoria")]
    public async Task<IActionResult> Auditoria([FromQuery] int limit = 50, CancellationToken ct = default)
        => Ok(await _svc.ListarAuditoriaAsync(limit, ct));

    // --- Ejecucion presupuestal (tab Ejecucion) ---
    [HttpGet("{id:guid}/ejecucion")]
    public async Task<IActionResult> Ejecucion(Guid id, CancellationToken ct)
        => Ok(await _svc.GetEjecucionAsync(id, ct));

    [HttpGet("{id:guid}/gastos")]
    public async Task<IActionResult> ListarGastos(Guid id, CancellationToken ct)
        => Ok(await _svc.ListarGastosAsync(id, ct));

    [RequierePermiso(ModuloCodigo.Presupuesto, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/gastos")]
    public async Task<IActionResult> RegistrarGasto(Guid id, [FromBody] RegistrarGastoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.RegistrarGastoAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Presupuesto, AccionPermiso.Eliminar)]
    [HttpDelete("gastos/{gastoId:guid}")]
    public async Task<IActionResult> EliminarGasto(Guid gastoId, CancellationToken ct)
        => await _svc.EliminarGastoAsync(gastoId, ct) ? NoContent() : NotFound();
}
