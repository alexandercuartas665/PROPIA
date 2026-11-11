using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Api.Authorization;
using Propia.Application.MiCopropiedad;
using Propia.Application.ServiciosPublicos;
using Propia.Infrastructure.ServiciosPublicos;

namespace Propia.Api.Controllers;

[ApiController]
[Route("api/servicios-publicos")]

[Authorize]
[RequiereRol("Administrador")]  // S-06: gestion sensible, admin
public class ServiciosPublicosController : ControllerBase
{
    private readonly IServiciosPublicosService _svc;
    public ServiciosPublicosController(IServiciosPublicosService svc) => _svc = svc;

    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen(CancellationToken ct) => Ok(await _svc.GetResumenAsync(ct));

    [HttpGet("cuentas")]
    public async Task<IActionResult> ListCuentas(CancellationToken ct) => Ok(await _svc.ListCuentasAsync(ct));

    [HttpGet("cuentas/{id:guid}")]
    public async Task<IActionResult> GetCuenta(Guid id, CancellationToken ct)
        => await _svc.GetCuentaAsync(id, ct) is { } d ? Ok(d) : NotFound();

    [HttpPost("cuentas")]
    public async Task<IActionResult> CrearCuenta([FromBody] CrearCuentaServicioRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearCuentaAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>PUT MERGE: solo lo enviado (para borrar un dato opcional, su clave en Limpiar).</summary>
    [HttpPut("cuentas/{id:guid}")]
    public async Task<IActionResult> ActualizarCuenta(Guid id, [FromBody] ActualizarCuentaServicioRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarCuentaAsync(id, req, ct) is { } d ? Ok(d) : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("cuentas/{id:guid}")]
    public async Task<IActionResult> EliminarCuenta(Guid id, CancellationToken ct)
        => await _svc.EliminarCuentaAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("cuentas/{id:guid}/registros")]
    public async Task<IActionResult> AgregarRegistro(Guid id, [FromBody] CrearRegistroConsumoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarRegistroAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("registros/{registroId:guid}")]
    public async Task<IActionResult> EliminarRegistro(Guid registroId, CancellationToken ct)
        => await _svc.EliminarRegistroAsync(registroId, ct) ? NoContent() : NotFound();

    [HttpPost("cuentas/{id:guid}/reclamaciones")]
    public async Task<IActionResult> AgregarReclamacion(Guid id, [FromBody] CrearReclamacionRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarReclamacionAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ---- Recibos leidos con IA (la lectura es /api/ocr/extraer-agente con el perfil "recibo_servicio") ----

    /// <summary>Valida el recibo contra los servicios: cuenta ACTIVA por referencia, periodo duplicado y topes. NO guarda.</summary>
    [HttpPost("recibos/evaluar")]
    public async Task<IActionResult> EvaluarRecibo([FromBody] ReciboLeidoRequest req, CancellationToken ct)
        => Ok(await _svc.EvaluarReciboAsync(req, ct));

    /// <summary>Registra el recibo (Origen = IA); reemplaza el del mismo periodo y alerta si sale de los topes.</summary>
    [HttpPost("recibos/confirmar")]
    public async Task<IActionResult> ConfirmarRecibo([FromBody] ReciboLeidoRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.ConfirmarReciboAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>"Es nuestro: agregarlo como servicio": crea el servicio con los datos del recibo y lo reevalua.</summary>
    [HttpPost("recibos/agregar-servicio")]
    public async Task<IActionResult> AgregarServicioDesdeRecibo([FromBody] ReciboLeidoRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.AgregarServicioDesdeReciboAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Recibo de ejemplo (alto | ok | ajeno) armado con los servicios reales de la copropiedad, en PDF.</summary>
    [HttpGet("recibos/ejemplo/{clase}")]
    public async Task<IActionResult> Ejemplo(string clase, [FromServices] ReciboEjemploService ejemplos, CancellationToken ct)
    {
        var e = await ejemplos.GenerarAsync(clase, ct);
        if (e is null) return NotFound(new { error = "Agrega primero un servicio con su referencia del contrato en Configuración." });
        return File(e.Pdf, "application/pdf", e.NombreArchivo);
    }

    // ---- Campos propios (columna "+" de Configuracion) ----
    [HttpGet("campos")]
    public async Task<IActionResult> ListarCampos(CancellationToken ct) => Ok(await _svc.ListarCamposAsync(ct));

    [HttpPost("campos")]
    public async Task<IActionResult> CrearCampo([FromBody] CrearCampoDefinicionRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.CrearCampoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("campos/{definicionId:guid}")]
    public async Task<IActionResult> ActualizarCampo(Guid definicionId, [FromBody] ActualizarCampoDefinicionRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarCampoAsync(definicionId, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("campos/{definicionId:guid}")]
    public async Task<IActionResult> EliminarCampo(Guid definicionId, CancellationToken ct)
        => await _svc.EliminarCampoAsync(definicionId, ct) ? NoContent() : NotFound();

    [HttpGet("campos-valores")]
    public async Task<IActionResult> ListarValores(CancellationToken ct) => Ok(await _svc.ListarValoresAsync(ct));

    [HttpPut("campos-valores/{cuentaId:guid}/{definicionId:guid}")]
    public async Task<IActionResult> SetValor(Guid cuentaId, Guid definicionId, [FromBody] SetCampoValorRequest req, CancellationToken ct)
        => await _svc.SetValorAsync(cuentaId, definicionId, req.Valor, ct) ? NoContent() : NotFound();
}
