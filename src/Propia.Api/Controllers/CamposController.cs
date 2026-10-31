using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Catalogos;

namespace Propia.Api.Controllers;

/// <summary>
/// Opciones EFECTIVAS de una lista del sistema para la copropiedad activa (Ola 4, modelo por-tenant). Lee la
/// copia del tenant (materializada perezosamente desde el catalogo global A&D), NO el catalogo global en vivo.
/// GET abierto al tenant (convencion RBAC: lecturas abiertas). La edicion del catalogo global sigue en la
/// consola A&D; la edicion de la copia del tenant va por el editor de columna de cada modulo.
/// </summary>
[ApiController]
[Route("api/campos")]
[Authorize]
public sealed class CamposController : ControllerBase
{
    private readonly IOpcionesEfectivas _opciones;
    private readonly IListasTenant _listas;
    public CamposController(IOpcionesEfectivas opciones, IListasTenant listas)
    {
        _opciones = opciones;
        _listas = listas;
    }

    /// <summary>Opciones de una lista para la copropiedad activa, en orden. incluirOcultas=true trae tambien las desactivadas.</summary>
    [HttpGet("opciones")]
    public async Task<IActionResult> Opciones([FromQuery] string lista, [FromQuery] bool incluirOcultas, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(lista)) return BadRequest(new { error = "lista requerida" });
        return Ok(await _opciones.OpcionesAsync(lista, incluirOcultas, ct));
    }

    /// <summary>Guarda el estado editado de la lista (write-side del editor de columna, Ola 4). Devuelve cuantos
    /// cambios se aplicaron. El universo de esta lista vive POR TENANT (copia independiente), no en el catalogo global.</summary>
    [HttpPost("opciones")]
    public async Task<IActionResult> GuardarOpciones([FromQuery] string lista, [FromBody] List<OpcionGuardarDto> opciones, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(lista)) return BadRequest(new { error = "lista requerida" });
        if (opciones is null) return BadRequest(new { error = "opciones requeridas" });
        try { return Ok(new { cambios = await _listas.GuardarOpcionesAsync(lista, opciones, ct) }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    /// <summary>"Re-sembrar listas base" POR TENANT: realinea las bases desde el catalogo global respetando lo
    /// propio (custom) y lo overrideado. lista=null reaplica todas las presentes. Devuelve cuantos cambios aplico.</summary>
    [HttpPost("reaplicar-bases")]
    public async Task<IActionResult> ReaplicarBases([FromQuery] string? lista, CancellationToken ct)
        => Ok(new { cambios = await _listas.ReaplicarBasesAsync(string.IsNullOrWhiteSpace(lista) ? null : lista, ct) });
}
