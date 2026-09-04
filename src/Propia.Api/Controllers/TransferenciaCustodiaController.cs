using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Api.Authorization;
using Propia.Application.TransferenciaCustodia;

namespace Propia.Api.Controllers;

/// <summary>
/// Modulo 1.5 Transferencia de Custodia. Spec v1.0 MVP.
/// Implementa los 3 escenarios + acta + aprobacion/rechazo + corte + expediente.
/// </summary>
// S-01b (auditoria): transferir/aprobar/cortar la custodia de un edificio es una accion de
// Administrador. Ademas de la pertenencia (parte saliente/entrante) que valida el servicio, se
// exige rol Administrador en el tenant activo en TODOS los endpoints de custodia.
[ApiController]
[Route("api/custodia")]
[Authorize]
[RequiereRol("Administrador")]
public class TransferenciaCustodiaController : ControllerBase
{
    private readonly ITransferenciaCustodiaService _svc;
    public TransferenciaCustodiaController(ITransferenciaCustodiaService svc) => _svc = svc;

    // ----- Busqueda y listado -----

    [HttpGet("copropiedades/buscar")]
    public async Task<IActionResult> BuscarCopropiedades([FromQuery] string? q, CancellationToken ct)
        => Ok(await _svc.BuscarCopropiedadesAsync(q, ct));

    [HttpGet("mias")]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _svc.ListarMisTransferenciasAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var t = await _svc.GetTransferenciaAsync(id, ct);
        return t is null ? NotFound() : Ok(t);
    }

    [HttpGet("{id:guid}/expediente")]
    public async Task<IActionResult> Expediente(Guid id, CancellationToken ct)
    {
        var e = await _svc.GetExpedienteAsync(id, ct);
        return e is null ? NotFound() : Ok(e);
    }

    // ----- Escenarios -----

    [HttpPost("escenario-a/entrega-voluntaria")]
    public async Task<IActionResult> EscenarioA(
        [FromBody] IniciarEntregaVoluntariaRequest req, CancellationToken ct)
        => Ok(await _svc.IniciarEntregaVoluntariaAsync(req, ct));

    [HttpPost("escenario-b/reclamar")]
    public async Task<IActionResult> EscenarioB(
        [FromBody] ReclamarCustodiaRequest req, CancellationToken ct)
        => Ok(await _svc.ReclamarCustodiaAsync(req, ct));

    [HttpPost("escenario-c/por-copropiedad")]
    public async Task<IActionResult> EscenarioC(
        [FromBody] IniciarPorCopropiedadRequest req, CancellationToken ct)
        => Ok(await _svc.IniciarPorCopropiedadAsync(req, ct));

    // ----- Acta de asamblea -----

    [HttpPost("{id:guid}/acta")]
    public async Task<IActionResult> SubirActa(
        Guid id, [FromBody] SubirActaRequest req, CancellationToken ct)
        => Ok(await _svc.SubirActaAsync(id, req, ct));

    // ----- Aprobacion / Rechazo -----

    [HttpPost("{id:guid}/aprobar")]
    public async Task<IActionResult> Aprobar(
        Guid id, [FromBody] AprobarTransferenciaRequest req, CancellationToken ct)
        => Ok(new { aprobado = await _svc.AprobarComoSalienteAsync(id, req, ct) });

    [HttpPost("{id:guid}/rechazar")]
    public async Task<IActionResult> Rechazar(
        Guid id, [FromBody] RechazarTransferenciaRequest req, CancellationToken ct)
        => Ok(new { rechazado = await _svc.RechazarComoSalienteAsync(id, req, ct) });

    // ----- Cancelacion -----

    public record CancelarRequest(string Motivo);

    [HttpPost("{id:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(
        Guid id, [FromBody] CancelarRequest req, CancellationToken ct)
        => Ok(new { cancelado = await _svc.CancelarAsync(id, req.Motivo, ct) });

    // ----- Ejecucion -----

    [HttpPost("{id:guid}/ejecutar-corte")]
    public async Task<IActionResult> Ejecutar(Guid id, CancellationToken ct)
        => Ok(await _svc.EjecutarCorteAsync(id, ct));

    // ----- Resumen -----

    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen(CancellationToken ct)
        => Ok(await _svc.GetResumenAsync(ct));
}
