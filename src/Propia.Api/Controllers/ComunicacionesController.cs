using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Comunicaciones;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>
/// Endpoints REST del modulo 2.14 Comunicaciones (spec v1.0 MVP).
/// Endpoints admin requieren JWT; el endpoint publico /api/comunicaciones/c/{token}
/// es accesible sin auth (token reside solo en el WhatsApp del destinatario).
/// </summary>
[ApiController]
[Route("api/comunicaciones")]
[Authorize]
public class ComunicacionesController : ControllerBase
{
    private readonly IComunicacionesService _svc;

    public ComunicacionesController(IComunicacionesService svc) => _svc = svc;

    // ----- Plantillas -----

    [HttpGet("plantillas")]
    public async Task<IActionResult> ListarPlantillas([FromQuery] bool? soloGlobales, CancellationToken ct)
        => Ok(await _svc.ListarPlantillasAsync(soloGlobales, ct));

    [HttpGet("plantillas/{id:guid}")]
    public async Task<IActionResult> GetPlantilla(Guid id, CancellationToken ct)
    {
        var p = await _svc.GetPlantillaAsync(id, ct);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpPost("plantillas")]
    public async Task<IActionResult> CrearPlantilla([FromBody] CrearPlantillaRequest req, CancellationToken ct)
    {
        try
        {
            var p = await _svc.CrearPlantillaTenantAsync(req, ct);
            return CreatedAtAction(nameof(GetPlantilla), new { id = p.Id }, p);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("plantillas/{id:guid}")]
    public async Task<IActionResult> ActualizarPlantilla(Guid id, [FromBody] ActualizarPlantillaRequest req, CancellationToken ct)
    {
        try
        {
            var ok = await _svc.ActualizarPlantillaTenantAsync(id, req, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("plantillas/{id:guid}")]
    public async Task<IActionResult> DesactivarPlantilla(Guid id, CancellationToken ct)
    {
        try
        {
            var ok = await _svc.DesactivarPlantillaTenantAsync(id, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ----- Comunicados -----

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] EstadoComunicado? estado,
        [FromQuery] TipoComunicadoBase? tipo,
        [FromQuery] string? q,
        CancellationToken ct)
        => Ok(await _svc.ListarComunicadosAsync(estado, tipo, q, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var c = await _svc.GetComunicadoAsync(id, ct);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearBorradorRequest req, CancellationToken ct)
    {
        try
        {
            var c = await _svc.CrearBorradorAsync(req, ct);
            return CreatedAtAction(nameof(Get), new { id = c.Id }, c);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] ActualizarBorradorRequest req, CancellationToken ct)
    {
        try
        {
            var ok = await _svc.ActualizarBorradorAsync(id, req, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ----- Segmentos -----

    [HttpPost("{id:guid}/segmentos")]
    public async Task<IActionResult> AgregarSegmento(Guid id, [FromBody] AgregarSegmentoRequest req, CancellationToken ct)
    {
        try
        {
            var s = await _svc.AgregarSegmentoAsync(id, req, ct);
            return Ok(s);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("{id:guid}/segmentos/{segmentoId:guid}")]
    public async Task<IActionResult> RemoverSegmento(Guid id, Guid segmentoId, CancellationToken ct)
    {
        try
        {
            var ok = await _svc.RemoverSegmentoAsync(id, segmentoId, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("{id:guid}/preview-destinatarios")]
    public async Task<IActionResult> Preview(Guid id, CancellationToken ct)
        => Ok(await _svc.PreviewDestinatariosAsync(id, ct));

    // ----- Adjuntos -----

    [HttpPost("{id:guid}/adjuntos")]
    public async Task<IActionResult> AgregarAdjunto(Guid id, [FromBody] CrearAdjuntoRequest req, CancellationToken ct)
    {
        try
        {
            var a = await _svc.AgregarAdjuntoAsync(id, req, ct);
            return Ok(a);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("{id:guid}/adjuntos/{adjuntoId:guid}")]
    public async Task<IActionResult> RemoverAdjunto(Guid id, Guid adjuntoId, CancellationToken ct)
    {
        try
        {
            var ok = await _svc.RemoverAdjuntoAsync(id, adjuntoId, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ----- Envio / Cancelacion -----

    [HttpPost("{id:guid}/enviar")]
    public async Task<IActionResult> Enviar(Guid id, [FromBody] EnviarRequest req, CancellationToken ct)
    {
        try
        {
            var ok = await _svc.EnviarAsync(id, req, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid id, [FromBody] CancelarRequest req, CancellationToken ct)
    {
        try
        {
            var ok = await _svc.CancelarAsync(id, req, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/reenviar-pendientes")]
    public async Task<IActionResult> Reenviar(Guid id, CancellationToken ct)
    {
        try
        {
            var n = await _svc.ReenviarAPendientesAsync(id, ct);
            return Ok(new { reencolados = n });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ----- Acuses -----

    [HttpGet("{id:guid}/acuses")]
    public async Task<IActionResult> Acuses(Guid id, CancellationToken ct)
        => Ok(await _svc.ListarAcusesAsync(id, ct));

    // ----- Resumen -----

    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen(CancellationToken ct)
        => Ok(await _svc.GetResumenAsync(ct));

    // ===========================================================================
    // ENDPOINT PUBLICO (sin auth)
    // ===========================================================================

    /// <summary>
    /// Vista publica del comunicado para el destinatario (sin login).
    /// Spec 2.14 seccion 10.2 - URL https://app.propia.co/c/{token}.
    /// Registra acuse de recibo silencioso (RN-08 + seccion 11.1).
    /// </summary>
    [HttpGet("c/{token:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> VistaPublica(Guid token, CancellationToken ct)
    {
        var userAgent = Request.Headers["User-Agent"].ToString() ?? "";
        var disp = userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase) ? DispositivoAcuse.Mobile
            : (string.IsNullOrEmpty(userAgent) ? DispositivoAcuse.Unknown : DispositivoAcuse.Desktop);

        var view = await _svc.AbrirVistaPublicaAsync(token, disp, ct);
        return view is null ? NotFound(new { error = "Token invalido o expirado." }) : Ok(view);
    }
}
