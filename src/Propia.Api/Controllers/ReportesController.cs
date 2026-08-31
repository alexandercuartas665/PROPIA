using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Reportes;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>
/// Endpoints REST del modulo 2.16 Reportes e Indicadores (spec v1.0 MVP).
/// Todos requieren JWT. Vista consejo y portal transparencia son authenticated tambien
/// pero se filtran por rol en el front (RN-04 y RN-05).
/// </summary>
[ApiController]
[Route("api/reportes")]
[Authorize]
public class ReportesController : ControllerBase
{
    private readonly IReportesService _svc;
    private readonly IContratosPorVencerReporteService _contratosPorVencer;

    public ReportesController(IReportesService svc, IContratosPorVencerReporteService contratosPorVencer)
    {
        _svc = svc;
        _contratosPorVencer = contratosPorVencer;
    }

    // ----- Reporte "Contratos proximos a vencer" (multi-copropiedad, con graficos + Excel) -----

    [HttpPost("contratos-por-vencer")]
    public async Task<IActionResult> ContratosPorVencer([FromBody] ContratosPorVencerFiltro filtro, CancellationToken ct)
        => Ok(await _contratosPorVencer.GetAsync(filtro?.TenantIds, ct));

    [HttpPost("contratos-por-vencer/excel")]
    public async Task<IActionResult> ContratosPorVencerExcel([FromBody] ContratosPorVencerFiltro filtro, CancellationToken ct)
    {
        var (bytes, nombre) = await _contratosPorVencer.ExportarExcelAsync(filtro?.TenantIds, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
    }

    // ----- Catalogo -----

    [HttpGet("categorias")]
    public async Task<IActionResult> ListarCategorias([FromQuery] AudienciaReporte? audiencia, CancellationToken ct)
        => Ok(await _svc.ListarCategoriasAsync(audiencia, ct));

    [HttpGet("catalogo")]
    public async Task<IActionResult> ListarCatalogo(
        [FromQuery] Guid? categoriaId,
        [FromQuery] AudienciaReporte? audiencia,
        CancellationToken ct)
        => Ok(await _svc.ListarCatalogoAsync(categoriaId, audiencia, ct));

    [HttpGet("catalogo/{id:guid}")]
    public async Task<IActionResult> GetCatalogo(Guid id, CancellationToken ct)
    {
        var c = await _svc.GetCatalogoAsync(id, ct);
        return c is null ? NotFound() : Ok(c);
    }

    // ----- Generacion + historial -----

    [HttpPost("generar")]
    public async Task<IActionResult> Generar([FromBody] GenerarReporteRequest req, CancellationToken ct)
    {
        try
        {
            var d = await _svc.GenerarAsync(req, ct);
            return Ok(d);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("historial")]
    public async Task<IActionResult> Historial(
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        [FromQuery] OrigenReporte? origen,
        [FromQuery] Guid? catalogoId,
        CancellationToken ct)
        => Ok(await _svc.ListarHistorialAsync(desde, hasta, origen, catalogoId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var r = await _svc.GetReporteAsync(id, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("{id:guid}/regenerar")]
    public async Task<IActionResult> Regenerar(Guid id, CancellationToken ct)
    {
        try
        {
            var d = await _svc.RegenerarAsync(id, ct);
            return Ok(d);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}/compartir")]
    public async Task<IActionResult> Compartir(Guid id, [FromQuery] bool compartir, CancellationToken ct)
    {
        try
        {
            var ok = await _svc.CompartirConsejoAsync(id, compartir, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ----- Programaciones -----

    [HttpGet("programaciones")]
    public async Task<IActionResult> ListarProgramaciones(CancellationToken ct)
        => Ok(await _svc.ListarProgramacionesAsync(ct));

    [HttpGet("programaciones/{id:guid}")]
    public async Task<IActionResult> GetProgramacion(Guid id, CancellationToken ct)
    {
        var p = await _svc.GetProgramacionAsync(id, ct);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpPost("programaciones")]
    public async Task<IActionResult> CrearProgramacion([FromBody] CrearProgramacionRequest req, CancellationToken ct)
    {
        try
        {
            var p = await _svc.CrearProgramacionAsync(req, ct);
            return CreatedAtAction(nameof(GetProgramacion), new { id = p.Id }, p);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("programaciones/{id:guid}")]
    public async Task<IActionResult> ActualizarProgramacion(Guid id, [FromBody] ActualizarProgramacionRequest req, CancellationToken ct)
    {
        try
        {
            var ok = await _svc.ActualizarProgramacionAsync(id, req, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("programaciones/{id:guid}/pausar")]
    public async Task<IActionResult> Pausar(Guid id, [FromQuery] bool pausar, CancellationToken ct)
        => await _svc.PausarProgramacionAsync(id, pausar, ct) ? NoContent() : NotFound();

    [HttpDelete("programaciones/{id:guid}")]
    public async Task<IActionResult> EliminarProgramacion(Guid id, CancellationToken ct)
        => await _svc.EliminarProgramacionAsync(id, ct) ? NoContent() : NotFound();

    // ----- Semaforos del consejo -----

    [HttpGet("semaforos")]
    public async Task<IActionResult> ListarSemaforos(CancellationToken ct)
        => Ok(await _svc.ListarSemaforosAsync(ct));

    [HttpPut("semaforos/{indicadorKey}")]
    public async Task<IActionResult> GuardarSemaforo(string indicadorKey, [FromBody] GuardarSemaforoRequest req, CancellationToken ct)
        => Ok(await _svc.GuardarSemaforoAsync(indicadorKey, req, ct));

    // ----- Vista consejo + transparencia -----

    [HttpGet("vista-consejo")]
    public async Task<IActionResult> VistaConsejo(
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        CancellationToken ct)
        => Ok(await _svc.GetVistaConsejoAsync(desde, hasta, ct));

    [HttpGet("transparencia")]
    public async Task<IActionResult> Transparencia(
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        CancellationToken ct)
        => Ok(await _svc.GetTransparenciaAsync(desde, hasta, ct));

    // ----- Resumen modulo -----

    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen(CancellationToken ct)
        => Ok(await _svc.GetResumenAsync(ct));
}
