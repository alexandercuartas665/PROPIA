using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Catalogos;

namespace Propia.Api.Controllers;

/// <summary>
/// Lectura del catalogo GLOBAL de listas para los modulos (cualquier usuario de copropiedad). Entrega
/// la capa de "fabrica" (A&D); el override por copropiedad lo compone cada modulo encima. Solo lectura;
/// la edicion es por la consola A&D (AdminCatalogosController).
/// </summary>
[ApiController]
[Route("api/catalogos")]
[Authorize]
public sealed class CatalogosController : ControllerBase
{
    private readonly ICatalogoListas _catalogo;
    public CatalogosController(ICatalogoListas catalogo) => _catalogo = catalogo;

    /// <summary>Opciones ACTIVAS de una lista, en orden (clave, label, color, orden, meta).</summary>
    [HttpGet]
    public async Task<IActionResult> Opciones([FromQuery] string lista, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(lista)) return BadRequest(new { error = "lista requerida" });
        return Ok(await _catalogo.OpcionesAsync(lista, incluirInactivas: false, ct));
    }
}
