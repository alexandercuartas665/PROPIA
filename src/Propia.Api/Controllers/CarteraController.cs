using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Cartera;
using Propia.Api.Authorization;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>Endpoints del modulo 2.7 Cartera y Estado de Cuenta (spec v1.0).</summary>
[ApiController]
[Route("api/cartera")]
[Authorize]
// S-06 (auditoria): RBAC por accion sobre la matriz de permisos (Administrador siempre pasa).
[RequierePermiso(ModuloCodigo.Cartera, AccionPermiso.Ver)]
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
    [RequierePermiso(ModuloCodigo.Cartera, AccionPermiso.Editar)]
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

    [RequierePermiso(ModuloCodigo.Cartera, AccionPermiso.Editar)]
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

    [RequierePermiso(ModuloCodigo.Cartera, AccionPermiso.Crear)]
    [HttpPost("acuerdos")]
    public async Task<IActionResult> CrearAcuerdo([FromBody] CrearAcuerdoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearAcuerdoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Cartera, AccionPermiso.Editar)]
    [HttpPut("acuerdos/{id:guid}/enviar")]
    public async Task<IActionResult> EnviarParaAceptacion(Guid id, CancellationToken ct)
    {
        try { return await _svc.EnviarParaAceptacionAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Cartera, AccionPermiso.Editar)]
    [HttpPut("acuerdos/{id:guid}/aceptar")]
    public async Task<IActionResult> AceptarAcuerdo(Guid id, [FromBody] AceptarAcuerdoRequest req, CancellationToken ct)
    {
        // El metodo de aceptacion y la IP se derivan del lado servidor: no se confia en lo que
        // envie el cliente (spoofeable). La IP sale de la conexion y el metodo identifica al
        // usuario autenticado que acepto desde la consola, para dejar trazabilidad real.
        var email = User.FindFirst("email")?.Value
                    ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                    ?? "administrador";
        // AceptacionMetodo y AceptacionIp son varchar(50): recortamos para no desbordar la columna
        // (emails largos harian fallar SaveChanges con 22001 / 500). Ver PropiaDbContext AcuerdoPago.
        var metodo = $"Consola administrador ({email})";
        if (metodo.Length > 50) metodo = metodo[..50];
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? req.Ip ?? "desconocida";
        if (ip.Length > 50) ip = ip[..50];
        var seguro = new AceptarAcuerdoRequest(metodo, ip);
        try { return await _svc.AceptarAcuerdoAsync(id, seguro, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Cartera, AccionPermiso.Editar)]
    [HttpPut("acuerdos/{id:guid}/cancelar")]
    public async Task<IActionResult> CancelarAcuerdo(Guid id, [FromBody] CancelarAcuerdoRequest req, CancellationToken ct)
    {
        try { return await _svc.CancelarAcuerdoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Cartera, AccionPermiso.Crear)]
    [HttpPost("acuerdos/pago")]
    public async Task<IActionResult> RegistrarPagoAcuerdo([FromBody] RegistrarPagoAcuerdoRequest req, CancellationToken ct)
        => await _svc.RegistrarPagoAcuerdoAsync(req, ct) ? NoContent() : NotFound();

    // --- Condonaciones ---
    [HttpGet("condonaciones")]
    public async Task<IActionResult> ListarCondonaciones([FromQuery] Guid? unidadId, CancellationToken ct)
        => Ok(await _svc.ListarCondonacionesAsync(unidadId, ct));

    [RequierePermiso(ModuloCodigo.Cartera, AccionPermiso.Aprobar)]
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

    [RequierePermiso(ModuloCodigo.Cartera, AccionPermiso.Crear)]
    [HttpPost("paz-salvos")]
    public async Task<IActionResult> EmitirPazSalvo([FromBody] EmitirPazSalvoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.EmitirPazSalvoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Cartera, AccionPermiso.Editar)]
    [HttpPut("paz-salvos/{id:guid}/anular")]
    public async Task<IActionResult> AnularPazSalvo(Guid id, [FromBody] AnularPazSalvoRequest req, CancellationToken ct)
    {
        try { return await _svc.AnularPazSalvoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Configuracion ---
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig(CancellationToken ct) => Ok(await _svc.GetConfigAsync(ct));

    [RequierePermiso(ModuloCodigo.Cartera, AccionPermiso.Editar)]
    [HttpPut("config")]
    public async Task<IActionResult> ActualizarConfig([FromBody] CarteraConfigDto req, CancellationToken ct)
        => await _svc.ActualizarConfigAsync(req, ct) ? NoContent() : NotFound();
}
