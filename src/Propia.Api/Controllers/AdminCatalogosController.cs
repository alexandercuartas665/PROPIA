using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Catalogos;

namespace Propia.Api.Controllers;

/// <summary>
/// CRUD del catalogo GLOBAL de listas (editable solo por A&D). Fuente de "fabrica" de las listas de
/// seleccion del producto; el override por copropiedad se compone encima en cada modulo. Tabla global
/// sin RLS: el aislamiento no aplica; el gate es la policy SuperAdmin.
/// </summary>
[ApiController]
[Route("api/admin/catalogos")]
[Authorize(Policy = AdminController.SuperAdminPolicy)]
public sealed class AdminCatalogosController : ControllerBase
{
    private readonly ICatalogoListasAdmin _svc;
    public AdminCatalogosController(ICatalogoListasAdmin svc) => _svc = svc;

    // Nota: 'lista' va por QUERY (no por ruta) porque sus claves llevan punto (ej. "unidad.tipo") y un
    // segmento final con punto confunde el ruteo. El id (guid) si va por ruta.

    /// <summary>Indice de listas con conteos.</summary>
    [HttpGet]
    public async Task<IActionResult> Listas(CancellationToken ct)
        => Ok(await _svc.ListasAsync(ct));

    /// <summary>Opciones de una lista (incluidas inactivas), en orden.</summary>
    [HttpGet("opciones")]
    public async Task<IActionResult> Opciones([FromQuery] string lista, CancellationToken ct)
        => Ok(await _svc.OpcionesAsync(lista, ct));

    /// <summary>Crea una opcion nueva (clave derivada del label).</summary>
    [HttpPost("opciones")]
    public async Task<IActionResult> Crear([FromQuery] string lista, [FromBody] CrearOpcionCatalogoRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.CrearAsync(lista, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Edita label/color/activo/meta de una opcion (no toca clave ni orden).</summary>
    [HttpPut("opcion/{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] ActualizarOpcionCatalogoRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _svc.ActualizarAsync(id, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Fija el orden de la lista segun la secuencia de ids.</summary>
    [HttpPost("reordenar")]
    public async Task<IActionResult> Reordenar([FromQuery] string lista, [FromBody] List<Guid> ordenIds, CancellationToken ct)
    {
        await _svc.ReordenarAsync(lista, ordenIds ?? new(), ct);
        return NoContent();
    }
}
