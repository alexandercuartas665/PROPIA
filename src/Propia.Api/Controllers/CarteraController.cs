using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Cartera;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>Endpoints del modulo 2.7 Cartera y Estado de Cuenta (spec v1.0).</summary>
[ApiController]
[Route("api/cartera")]
[Authorize]
public class CarteraController : ControllerBase
{
    private readonly ICarteraService _svc;
    private readonly IComprobantePdfService _comprobantes;
    public CarteraController(ICarteraService svc, IComprobantePdfService comprobantes)
    {
        _svc = svc;
        _comprobantes = comprobantes;
    }

    /// <summary>
    /// Devuelve el comprobante de pago en PDF (QuestPDF). Util para enviar como adjunto en correo
    /// al residente o como respaldo de auditoria.
    /// </summary>
    [HttpGet("pagos/{pagoId:guid}/comprobante.pdf")]
    public async Task<IActionResult> ComprobantePago(Guid pagoId, CancellationToken ct)
    {
        var result = await _comprobantes.GenerarComprobantePagoAsync(pagoId, ct);
        if (result is null) return NotFound();
        return File(result.Value.Pdf, "application/pdf", result.Value.FileName);
    }

    // --- Sincronizacion + tablero ---
    [HttpPost("sincronizar")]
    public async Task<IActionResult> Sincronizar(CancellationToken ct)
        => Ok(new { sincronizadas = await _svc.SincronizarDesdePresupuestoAsync(ct) });

    [HttpGet("tablero")]
    public async Task<IActionResult> Tablero(CancellationToken ct) => Ok(await _svc.GetTableroAsync(ct));

    [HttpGet("unidades/{id:guid}")]
    public async Task<IActionResult> GetUnidad(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetUnidadAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("mi-cuota")]
    public async Task<IActionResult> MiCuota([FromQuery] Guid? unidadId, CancellationToken ct)
    {
        var dto = await _svc.GetMiCuotaAsync(unidadId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    // --- Estados de gestion ---
    [HttpGet("estados")]
    public async Task<IActionResult> ListarEstados(CancellationToken ct) => Ok(await _svc.ListarEstadosAsync(ct));

    [HttpPut("unidades/{id:guid}/estado")]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] CambiarEstadoUnidadRequest req, CancellationToken ct)
    {
        try { return await _svc.CambiarEstadoUnidadAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Acuerdos de pago ---
    [HttpGet("acuerdos")]
    public async Task<IActionResult> ListarAcuerdos([FromQuery] EstadoAcuerdoPago? estado, CancellationToken ct)
        => Ok(await _svc.ListarAcuerdosAsync(estado, ct));

    [HttpGet("acuerdos/{id:guid}")]
    public async Task<IActionResult> GetAcuerdo(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetAcuerdoAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("acuerdos")]
    public async Task<IActionResult> CrearAcuerdo([FromBody] CrearAcuerdoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearAcuerdoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("acuerdos/{id:guid}/enviar")]
    public async Task<IActionResult> EnviarParaAceptacion(Guid id, CancellationToken ct)
    {
        try { return await _svc.EnviarParaAceptacionAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("acuerdos/{id:guid}/aceptar")]
    public async Task<IActionResult> AceptarAcuerdo(Guid id, [FromBody] AceptarAcuerdoRequest req, CancellationToken ct)
    {
        try { return await _svc.AceptarAcuerdoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("acuerdos/{id:guid}/cancelar")]
    public async Task<IActionResult> CancelarAcuerdo(Guid id, [FromBody] CancelarAcuerdoRequest req, CancellationToken ct)
    {
        try { return await _svc.CancelarAcuerdoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("acuerdos/pago")]
    public async Task<IActionResult> RegistrarPagoAcuerdo([FromBody] RegistrarPagoAcuerdoRequest req, CancellationToken ct)
        => await _svc.RegistrarPagoAcuerdoAsync(req, ct) ? NoContent() : NotFound();

    // --- Condonaciones ---
    [HttpGet("condonaciones")]
    public async Task<IActionResult> ListarCondonaciones([FromQuery] Guid? unidadId, CancellationToken ct)
        => Ok(await _svc.ListarCondonacionesAsync(unidadId, ct));

    [HttpPost("condonaciones")]
    public async Task<IActionResult> AplicarCondonacion([FromBody] AplicarCondonacionRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AplicarCondonacionAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Paz y salvo ---
    [HttpGet("paz-salvos")]
    public async Task<IActionResult> ListarPazSalvos([FromQuery] Guid? unidadId, CancellationToken ct)
        => Ok(await _svc.ListarPazSalvosAsync(unidadId, ct));

    [HttpPost("paz-salvos")]
    public async Task<IActionResult> EmitirPazSalvo([FromBody] EmitirPazSalvoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.EmitirPazSalvoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("paz-salvos/{id:guid}/anular")]
    public async Task<IActionResult> AnularPazSalvo(Guid id, [FromBody] AnularPazSalvoRequest req, CancellationToken ct)
    {
        try { return await _svc.AnularPazSalvoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Configuracion ---
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig(CancellationToken ct) => Ok(await _svc.GetConfigAsync(ct));

    [HttpPut("config")]
    public async Task<IActionResult> ActualizarConfig([FromBody] CarteraConfigDto req, CancellationToken ct)
        => await _svc.ActualizarConfigAsync(req, ct) ? NoContent() : NotFound();
}
