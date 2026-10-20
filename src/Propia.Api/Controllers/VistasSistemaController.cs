using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Api.Authorization;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>
/// "Vistas del sistema" del modulo 2.3 Mi Copropiedad: explorador de datos cuyas vistas (definicion) se
/// guardan COMPARTIDAS por copropiedad. Opera sobre el tenant activo (RLS). GET abierto al tenant; las
/// escrituras exigen permiso de edicion de Mi Copropiedad.
/// </summary>
[ApiController]
[Route("api/mi-copropiedad/vistas")]
[Authorize]
public class VistasSistemaController : ControllerBase
{
    private readonly IVistaSistemaService _svc;
    public VistasSistemaController(IVistaSistemaService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct) => Ok(await _svc.ListarAsync(ct));

    [RequierePermiso(ModuloCodigo.MiCopropiedad, AccionPermiso.Editar)]
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] GuardarVistaRequest req, CancellationToken ct) =>
        Created("", await _svc.CrearAsync(req, ct));

    [RequierePermiso(ModuloCodigo.MiCopropiedad, AccionPermiso.Editar)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] GuardarVistaRequest req, CancellationToken ct) =>
        await _svc.ActualizarAsync(id, req, ct) ? NoContent() : NotFound();

    [RequierePermiso(ModuloCodigo.MiCopropiedad, AccionPermiso.Editar)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct) =>
        await _svc.EliminarAsync(id, ct) ? NoContent() : NotFound();

    [RequierePermiso(ModuloCodigo.MiCopropiedad, AccionPermiso.Editar)]
    [HttpPut("orden")]
    public async Task<IActionResult> Reordenar([FromBody] ReordenarVistasRequest req, CancellationToken ct)
    {
        await _svc.ReordenarAsync(req.Ids ?? new(), ct);
        return NoContent();
    }

    // Siembra las vistas de ejemplo. reemplazar=false solo si la copropiedad no tiene vistas (primer
    // arranque); reemplazar=true = "Restablecer ejemplos". Devuelve la lista final.
    [RequierePermiso(ModuloCodigo.MiCopropiedad, AccionPermiso.Editar)]
    [HttpPost("sembrar")]
    public async Task<IActionResult> Sembrar([FromBody] List<GuardarVistaRequest> semillas, [FromQuery] bool reemplazar, CancellationToken ct) =>
        Ok(await _svc.SembrarAsync(semillas ?? new(), reemplazar, ct));
}
