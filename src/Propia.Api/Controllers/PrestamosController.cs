using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Api.Authorization;
using Propia.Domain.Enums;
using Propia.Application.Prestamos;

namespace Propia.Api.Controllers;

/// <summary>Prestamos de equipos/activos reservables + trazabilidad de entrega/devolucion con fotos.</summary>
[ApiController]
[Route("api/prestamos-equipo")]

[Authorize]
[RequiereRol("Administrador")]  // S-06: gestion sensible, admin
public class PrestamosController : ControllerBase
{
    private readonly IPrestamosService _svc;
    public PrestamosController(IPrestamosService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? equipoId, CancellationToken ct)
        => Ok(await _svc.ListarAsync(equipoId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var p = await _svc.GetAsync(id, ct);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearPrestamoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/entregar")]
    public async Task<IActionResult> Entregar(Guid id, [FromBody] RegistrarEntregaRequest req, CancellationToken ct)
    {
        try { return await _svc.RegistrarEntregaAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/devolver")]
    public async Task<IActionResult> Devolver(Guid id, [FromBody] RegistrarEntregaRequest req, CancellationToken ct)
    {
        try { return await _svc.RegistrarDevolucionAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid id, [FromBody] CancelarPrestamoRequest req, CancellationToken ct)
        => await _svc.CancelarAsync(id, req?.Motivo, ct) ? NoContent() : NotFound();

    [HttpGet("{id:guid}/fotos")]
    public async Task<IActionResult> Fotos(Guid id, CancellationToken ct)
        => Ok(await _svc.ListarFotosAsync("prestamo", id, ct));
}

public record CancelarPrestamoRequest(string? Motivo);
