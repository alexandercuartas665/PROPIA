using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Api.Authorization;
using Propia.Domain.Enums;
using Propia.Application.Cierre;

namespace Propia.Api.Controllers;

/// <summary>Catalogo configurable de motivos de cierre por copropiedad, separado por modulo (tareas/pqrsd).</summary>
[ApiController]
[Route("api/cierre/motivos")]

[Authorize]
[RequiereRol("Administrador")]  // S-06: gestion sensible, admin
public class MotivosCierreController : ControllerBase
{
    private readonly IMotivosCierreService _svc;
    public MotivosCierreController(IMotivosCierreService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] string modulo, [FromQuery] bool incluirInactivos = false, CancellationToken ct = default)
        => Ok(await _svc.ListarAsync(modulo ?? "", incluirInactivos, ct));

    [HttpPost]
    public async Task<IActionResult> Crear([FromQuery] string modulo, [FromBody] GuardarMotivoCierreRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.CrearAsync(modulo ?? "", req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] GuardarMotivoCierreRequest req, CancellationToken ct)
    {
        var r = await _svc.ActualizarAsync(id, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
        => await _svc.EliminarAsync(id, ct) ? NoContent() : NotFound();
}
