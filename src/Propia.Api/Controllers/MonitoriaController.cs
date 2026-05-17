using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Monitoria;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>
/// Modulo 0.3 Monitoria y Auditoria Global - endpoints SuperAdmin (MVP).
/// Acceso restringido a operadores de A&D GROUP (roles Identity SuperAdmin).
/// El cableado de [Authorize(Roles = "...")] queda para Fase 2 cuando se modele
/// claim de SuperAdmin via Identity. Por ahora requiere autenticacion estandar.
/// </summary>
[ApiController]
[Route("api/admin/monitoria")]
[Authorize]
public class MonitoriaController : ControllerBase
{
    private readonly IMonitoriaService _svc;
    public MonitoriaController(IMonitoriaService svc) => _svc = svc;

    // ----- Logs -----

    [HttpPost("logs")]
    public async Task<IActionResult> Registrar([FromBody] RegistrarLogRequest req, CancellationToken ct)
        => Ok(new { id = await _svc.RegistrarLogAsync(req, ct) });

    [HttpGet("logs")]
    public async Task<IActionResult> Logs(
        [FromQuery] SeveridadIncidente? severidad,
        [FromQuery] TipoEventoSistema? tipo,
        [FromQuery] Guid? tenantId,
        [FromQuery] string? modulo,
        [FromQuery] int limite = 200,
        CancellationToken ct = default)
        => Ok(await _svc.ListarLogsAsync(new FiltroLogsRequest(severidad, tipo, tenantId, modulo, null, null, limite), ct));

    // ----- Incidentes -----

    [HttpPost("incidentes")]
    public async Task<IActionResult> AbrirIncidente([FromBody] AbrirIncidenteRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AbrirIncidenteAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("incidentes")]
    public async Task<IActionResult> Incidentes([FromQuery] EstadoIncidente? estado, CancellationToken ct)
        => Ok(await _svc.ListarIncidentesAsync(estado, ct));

    [HttpGet("incidentes/{id:guid}")]
    public async Task<IActionResult> GetIncidente(Guid id, CancellationToken ct)
    {
        var i = await _svc.GetIncidenteAsync(id, ct);
        return i is null ? NotFound() : Ok(i);
    }

    public record AsignarRequest(Guid SuperAdminId);

    [HttpPost("incidentes/{id:guid}/asignar")]
    public async Task<IActionResult> Asignar(Guid id, [FromBody] AsignarRequest req, CancellationToken ct)
        => await _svc.AsignarIncidenteAsync(id, req.SuperAdminId, ct) ? NoContent() : NotFound();

    [HttpPost("incidentes/{id:guid}/estado")]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] CambiarEstadoIncidenteRequest req, CancellationToken ct)
        => await _svc.CambiarEstadoIncidenteAsync(id, req, ct) ? NoContent() : NotFound();

    [HttpPost("incidentes/{id:guid}/resolver")]
    public async Task<IActionResult> Resolver(Guid id, [FromBody] ResolverIncidenteRequest req, CancellationToken ct)
    {
        try { return await _svc.ResolverIncidenteAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ----- Metricas -----

    [HttpPost("metricas/calcular")]
    public async Task<IActionResult> CalcularMetricas(CancellationToken ct)
        => Ok(await _svc.CalcularYGuardarMetricasHoyAsync(ct));

    [HttpGet("metricas")]
    public async Task<IActionResult> Metricas([FromQuery] DateOnly desde, [FromQuery] DateOnly hasta, CancellationToken ct)
        => Ok(await _svc.ListarMetricasAsync(desde, hasta, ct));

    [HttpGet("metricas/ultima")]
    public async Task<IActionResult> UltimaMetrica(CancellationToken ct)
    {
        var m = await _svc.GetMetricaMasRecienteAsync(ct);
        return m is null ? NotFound() : Ok(m);
    }

    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen(CancellationToken ct) => Ok(await _svc.GetResumenAsync(ct));
}
