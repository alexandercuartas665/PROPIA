using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Api.Authorization;
using Propia.Domain.Enums;
using Propia.Application.Servicios;

namespace Propia.Api.Controllers;

/// <summary>
/// Servicios contratados (modulo Finanzas - Servicios y contratos). Un servicio agrupa
/// contratos, tiene ejecutor + contactos (terceros del Directorio), costos y adjuntos.
/// Los contratos se crean/editan por /api/mi-copropiedad/contratos; aqui van el servicio,
/// sus contactos, los adjuntos (servicio y contrato) y la asociacion de contratos.
/// </summary>
[ApiController]
[Route("api/servicios")]

[Authorize]
[RequiereRol("Administrador")]  // S-06: gestion sensible, admin
public class ServiciosController : ControllerBase
{
    private readonly IServiciosService _svc;
    public ServiciosController(IServiciosService svc) => _svc = svc;

    private Guid? UsuarioId() => Guid.TryParse(User.FindFirstValue("user_id"), out var g) ? g : null;

    // ----- Servicio CRUD -----

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct) => Ok(await _svc.ListarAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => await _svc.GetAsync(id, ct) is { } d ? Ok(d) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearServicioRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] ActualizarServicioRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
        => await _svc.EliminarAsync(id, ct) ? NoContent() : NotFound();

    // ----- Contactos -----

    [HttpPost("{id:guid}/contactos")]
    public async Task<IActionResult> AgregarContacto(Guid id, [FromBody] AgregarServicioContactoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarContactoAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("contactos/{contactoId:guid}")]
    public async Task<IActionResult> EliminarContacto(Guid contactoId, CancellationToken ct)
        => await _svc.EliminarContactoAsync(contactoId, ct) ? NoContent() : NotFound();

    // ----- Adjuntos de servicio -----

    [HttpPost("{id:guid}/adjuntos")]
    public async Task<IActionResult> SubirAdjuntoServicio(Guid id, [FromBody] SubirAdjuntoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.SubirAdjuntoServicioAsync(id, req, UsuarioId(), ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("adjuntos/{adjuntoId:guid}")]
    public async Task<IActionResult> EliminarAdjuntoServicio(Guid adjuntoId, CancellationToken ct)
        => await _svc.EliminarAdjuntoServicioAsync(adjuntoId, ct) ? NoContent() : NotFound();

    // ----- Adjuntos de contrato (la carga de contratos) -----

    [HttpGet("contratos/{contratoId:guid}/adjuntos")]
    public async Task<IActionResult> ListarAdjuntosContrato(Guid contratoId, CancellationToken ct)
        => Ok(await _svc.ListarAdjuntosContratoAsync(contratoId, ct));

    [HttpPost("contratos/{contratoId:guid}/adjuntos")]
    public async Task<IActionResult> SubirAdjuntoContrato(Guid contratoId, [FromBody] SubirAdjuntoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.SubirAdjuntoContratoAsync(contratoId, req, UsuarioId(), ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("contratos/adjuntos/{adjuntoId:guid}")]
    public async Task<IActionResult> EliminarAdjuntoContrato(Guid adjuntoId, CancellationToken ct)
        => await _svc.EliminarAdjuntoContratoAsync(adjuntoId, ct) ? NoContent() : NotFound();

    // ----- Asociar contratos a un servicio -----

    [HttpPost("{id:guid}/contratos/{contratoId:guid}")]
    public async Task<IActionResult> AsociarContrato(Guid id, Guid contratoId, CancellationToken ct)
        => await _svc.AsociarContratoAsync(id, contratoId, ct) ? NoContent() : NotFound();

    [HttpDelete("contratos/{contratoId:guid}/servicio")]
    public async Task<IActionResult> DesasociarContrato(Guid contratoId, CancellationToken ct)
        => await _svc.DesasociarContratoAsync(contratoId, ct) ? NoContent() : NotFound();

    // ----- Alertas del servicio -----

    [HttpPost("{id:guid}/alertas")]
    public async Task<IActionResult> AgregarAlerta(Guid id, [FromBody] AgregarAlertaServicioRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarAlertaAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("alertas/{alertaId:guid}/resolver")]
    public async Task<IActionResult> ResolverAlerta(Guid alertaId, CancellationToken ct)
        => await _svc.ResolverAlertaAsync(alertaId, ct) ? NoContent() : NotFound();
}
