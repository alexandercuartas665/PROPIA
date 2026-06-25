using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Programaciones;

namespace Propia.Api.Controllers;

/// <summary>
/// Programador de tareas reutilizable (modulo 2.10). CRUD de programaciones que el
/// ProgramacionTareasJob materializa en tareas reales segun su periodicidad. Se invoca
/// desde el componente ProgramadorTareaModal en distintos escenarios (vencimiento de
/// contrato, mantenimientos, etc.).
/// </summary>
[ApiController]
[Route("api/programaciones")]
[Authorize]
public class ProgramacionesController : ControllerBase
{
    private readonly IProgramacionTareasService _svc;
    public ProgramacionesController(IProgramacionTareasService svc) => _svc = svc;

    private Guid UsuarioId() => Guid.TryParse(User.FindFirstValue("user_id"), out var g) ? g : Guid.Empty;

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] string? moduloOrigen, [FromQuery] Guid? entidadOrigenId, CancellationToken ct)
        => Ok(await _svc.ListarAsync(moduloOrigen, entidadOrigenId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => await _svc.GetAsync(id, ct) is { } d ? Ok(d) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearProgramacionRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearAsync(req, UsuarioId(), ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] ActualizarProgramacionRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}/activa")]
    public async Task<IActionResult> ToggleActiva(Guid id, [FromQuery] bool activa, CancellationToken ct)
        => await _svc.ToggleActivaAsync(id, activa, ct) ? NoContent() : NotFound();

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
        => await _svc.EliminarAsync(id, ct) ? NoContent() : NotFound();
}
