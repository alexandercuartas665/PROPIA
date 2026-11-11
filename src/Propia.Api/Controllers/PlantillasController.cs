using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Api.Authorization;
using Propia.Application.MiCopropiedad;
using Propia.Application.Plantillas;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>
/// Configuracion > Plantillas de documentos: catalogo central que usa todo el sistema. Cada plantilla tiene
/// una categoria (pqrsd, cartera, asambleas...) que define los datos {grupo.campo} que puede insertar y el
/// modulo donde se ofrece. GET abiertos al tenant; escrituras gateadas por el modulo PLANTILLAS.
/// </summary>
[ApiController]
[Authorize]
[Route("api/plantillas")]
public sealed class PlantillasController : ControllerBase
{
    private readonly IPlantillasDocumentoService _svc;

    public PlantillasController(IPlantillasDocumentoService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] string? categoria, CancellationToken ct)
        => Ok(await _svc.ListarAsync(categoria, ct));

    [HttpGet("categorias")]
    public IActionResult Categorias() => Ok(PlantillasCatalogo.Categorias);

    /// <summary>Datos insertables de la categoria: los suyos + los comunes.</summary>
    [HttpGet("tokens")]
    public IActionResult Tokens([FromQuery] string? categoria) => Ok(PlantillasCatalogo.TokensDe(categoria));

    // ---- Campos propios (columna "+" de la vista tabla). Rutas literales: van antes que {id:guid}. ----
    [HttpGet("campos")]
    public async Task<IActionResult> ListarCampos(CancellationToken ct) => Ok(await _svc.ListarCamposAsync(ct));

    [RequierePermiso(ModuloCodigo.Plantillas, AccionPermiso.Editar)]
    [HttpPost("campos")]
    public async Task<IActionResult> CrearCampo([FromBody] CrearCampoDefinicionRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.CrearCampoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Plantillas, AccionPermiso.Editar)]
    [HttpPut("campos/{definicionId:guid}")]
    public async Task<IActionResult> ActualizarCampo(Guid definicionId, [FromBody] ActualizarCampoDefinicionRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarCampoAsync(definicionId, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Plantillas, AccionPermiso.Editar)]
    [HttpDelete("campos/{definicionId:guid}")]
    public async Task<IActionResult> EliminarCampo(Guid definicionId, CancellationToken ct)
        => await _svc.EliminarCampoAsync(definicionId, ct) ? NoContent() : NotFound();

    [HttpGet("campos-valores")]
    public async Task<IActionResult> ListarValores(CancellationToken ct) => Ok(await _svc.ListarValoresAsync(ct));

    [RequierePermiso(ModuloCodigo.Plantillas, AccionPermiso.Editar)]
    [HttpPut("campos-valores/{plantillaId:guid}/{definicionId:guid}")]
    public async Task<IActionResult> SetValor(Guid plantillaId, Guid definicionId, [FromBody] SetCampoValorRequest req, CancellationToken ct)
        => await _svc.SetValorAsync(plantillaId, definicionId, req.Valor, ct) ? NoContent() : NotFound();

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken ct)
        => await _svc.ObtenerAsync(id, ct) is { } dto ? Ok(dto) : NotFound();

    [RequierePermiso(ModuloCodigo.Plantillas, AccionPermiso.Crear)]
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] GuardarPlantillaDocumentoRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.CrearAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Plantillas, AccionPermiso.Editar)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] GuardarPlantillaDocumentoRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarAsync(id, req, ct) is { } dto ? Ok(dto) : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Plantillas, AccionPermiso.Eliminar)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
        => await _svc.EliminarAsync(id, ct) ? NoContent() : NotFound();
}
