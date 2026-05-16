using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.ReportesConsolidados;

namespace Propia.Api.Controllers;

/// <summary>
/// Modulo 1.4 Reportes Consolidados de la Organizacion. Spec v1.0 MVP.
/// Cross-tenant: el servicio resuelve internamente que tenants pertenecen al portafolio
/// de la organizacion del usuario actual (RN-01 + RN-03).
/// </summary>
[ApiController]
[Route("api/reportes-org")]
[Authorize]
public class ReportesConsolidadosController : ControllerBase
{
    private readonly IReportesConsolidadosService _svc;
    public ReportesConsolidadosController(IReportesConsolidadosService svc) => _svc = svc;

    // ----- Plantillas + reportes guardados -----

    [HttpGet("plantillas")]
    public async Task<IActionResult> ListarPlantillas(CancellationToken ct)
        => Ok(await _svc.ListarPlantillasBaseAsync(ct));

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _svc.ListarReportesAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var r = await _svc.GetReporteAsync(id, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearReporteRequest req, CancellationToken ct)
        => Ok(await _svc.CrearReporteAsync(req, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] ActualizarReporteRequest req, CancellationToken ct)
        => Ok(new { actualizado = await _svc.ActualizarReporteAsync(id, req, ct) });

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
        => Ok(new { eliminado = await _svc.EliminarReporteAsync(id, ct) });

    // ----- Generacion + historial -----

    [HttpPost("generar")]
    public async Task<IActionResult> Generar([FromBody] GenerarReporteRequest req, CancellationToken ct)
        => Ok(await _svc.GenerarAsync(req, ct));

    [HttpGet("historial")]
    public async Task<IActionResult> Historial([FromQuery] Guid? reporteId, CancellationToken ct)
        => Ok(await _svc.ListarHistorialAsync(reporteId, ct));

    [HttpGet("generaciones/{id:guid}")]
    public async Task<IActionResult> GetGeneracion(Guid id, CancellationToken ct)
    {
        var g = await _svc.GetGeneracionAsync(id, ct);
        return g is null ? NotFound() : Ok(g);
    }

    [HttpPost("generaciones/{id:guid}/regenerar")]
    public async Task<IActionResult> Regenerar(Guid id, CancellationToken ct)
        => Ok(await _svc.RegenerarAsync(id, ct));

    // ----- Indicadores consolidados cross-tenant -----

    [HttpGet("indicadores/portafolio")]
    public async Task<IActionResult> Portafolio(CancellationToken ct)
        => Ok(await _svc.GetIndicadoresPortafolioAsync(ct));

    [HttpGet("indicadores/financiero")]
    public async Task<IActionResult> Financiero(
        [FromQuery] DateOnly desde, [FromQuery] DateOnly hasta, CancellationToken ct)
        => Ok(await _svc.GetFinancieroConsolidadoAsync(desde, hasta, ct));

    [HttpGet("indicadores/operativo")]
    public async Task<IActionResult> Operativo(
        [FromQuery] DateOnly desde, [FromQuery] DateOnly hasta, CancellationToken ct)
        => Ok(await _svc.GetOperativoConsolidadoAsync(desde, hasta, ct));

    [HttpGet("indicadores/pqrsd")]
    public async Task<IActionResult> Pqrsd(
        [FromQuery] DateOnly desde, [FromQuery] DateOnly hasta, CancellationToken ct)
        => Ok(await _svc.GetPqrsdConsolidadoAsync(desde, hasta, ct));

    [HttpGet("indicadores/equipo")]
    public async Task<IActionResult> Equipo(
        [FromQuery] DateOnly desde, [FromQuery] DateOnly hasta, CancellationToken ct)
        => Ok(await _svc.GetIndicadoresEquipoAsync(desde, hasta, ct));

    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen(CancellationToken ct)
        => Ok(await _svc.GetResumenAsync(ct));
}
