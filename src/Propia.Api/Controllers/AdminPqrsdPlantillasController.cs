using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Pqrsd;

namespace Propia.Api.Controllers;

/// <summary>
/// Catalogo GLOBAL de plantillas semilla de respuesta PQRSD (Super Admin, Capa 0). Cada copropiedad
/// nueva hereda una copia al abrir el modulo; editarlas alla no afecta esta semilla.
/// </summary>
[ApiController]
[Route("admin/pqrsd-plantillas-semilla")]
[Authorize(Policy = AdminController.SuperAdminPolicy)]
public class AdminPqrsdPlantillasController : ControllerBase
{
    private readonly IPqrsdPlantillaSemillaService _svc;
    public AdminPqrsdPlantillasController(IPqrsdPlantillaSemillaService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] bool incluirInactivas = true, CancellationToken ct = default)
        => Ok(await _svc.ListarAsync(incluirInactivas, ct));

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] GuardarPlantillaSemillaRequest req, CancellationToken ct)
        => Ok(await _svc.CrearAsync(req, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] GuardarPlantillaSemillaRequest req, CancellationToken ct)
    {
        var dto = await _svc.ActualizarAsync(id, req, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
        => await _svc.EliminarAsync(id, ct) ? NoContent() : NotFound();
}
