using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Historial;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>
/// Historial relacionado cross-modulo: para una entidad de la copropiedad (unidad, zona o equipo)
/// devuelve sus tareas, PQRSD y mantenimientos vinculados, ordenados por fecha. Base de las
/// pestanas "Historial" de las fichas de Zonas, Equipos y Unidades.
/// </summary>
[ApiController]
[Route("api/historial")]
[Authorize]
public class HistorialController : ControllerBase
{
    private readonly IHistorialRelacionadoService _svc;

    public HistorialController(IHistorialRelacionadoService svc) => _svc = svc;

    /// <summary>Historial relacionado de una entidad. tipo: 1=Unidad, 2=ZonaComun, 3=Equipo.</summary>
    [HttpGet("{tipo}/{id:guid}")]
    public async Task<IActionResult> Get(TipoEntidadHistorial tipo, Guid id, CancellationToken ct)
        => Ok(await _svc.GetAsync(tipo, id, ct));
}
