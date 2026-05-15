using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Documentos;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>
/// Endpoints REST del modulo 2.15 Documentos y Archivo Digital (spec v1.0 MVP).
/// Todos los endpoints requieren JWT del tenant (header propia-tenant via middleware).
/// </summary>
[ApiController]
[Route("api/documentos")]
[Authorize]
public class DocumentosController : ControllerBase
{
    private readonly IDocumentosService _svc;

    public DocumentosController(IDocumentosService svc) => _svc = svc;

    // ----- Categorias -----

    [HttpGet("categorias")]
    public async Task<IActionResult> ListarCategorias(CancellationToken ct)
        => Ok(await _svc.ListarCategoriasAsync(ct));

    [HttpGet("categorias/{id:guid}")]
    public async Task<IActionResult> GetCategoria(Guid id, CancellationToken ct)
    {
        var c = await _svc.GetCategoriaAsync(id, ct);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpPost("categorias")]
    public async Task<IActionResult> CrearCategoria([FromBody] CrearCategoriaRequest req, CancellationToken ct)
    {
        try
        {
            var c = await _svc.CrearCategoriaAsync(req, ct);
            return CreatedAtAction(nameof(GetCategoria), new { id = c.Id }, c);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("categorias/{id:guid}")]
    public async Task<IActionResult> ActualizarCategoria(Guid id, [FromBody] ActualizarCategoriaRequest req, CancellationToken ct)
    {
        try
        {
            var ok = await _svc.ActualizarCategoriaAsync(id, req, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("categorias/{id:guid}")]
    public async Task<IActionResult> DesactivarCategoria(Guid id, CancellationToken ct)
    {
        try
        {
            var ok = await _svc.DesactivarCategoriaAsync(id, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ----- Carpetas -----

    [HttpGet("categorias/{categoriaId:guid}/carpetas")]
    public async Task<IActionResult> ListarCarpetas(Guid categoriaId, CancellationToken ct)
        => Ok(await _svc.ListarCarpetasAsync(categoriaId, ct));

    [HttpPost("carpetas")]
    public async Task<IActionResult> CrearCarpeta([FromBody] CrearCarpetaRequest req, CancellationToken ct)
    {
        try
        {
            var c = await _svc.CrearCarpetaAsync(req, ct);
            return Ok(c);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("carpetas/{id:guid}")]
    public async Task<IActionResult> RenombrarCarpeta(Guid id, [FromBody] RenombrarCarpetaRequest req, CancellationToken ct)
    {
        var ok = await _svc.RenombrarCarpetaAsync(id, req, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpDelete("carpetas/{id:guid}")]
    public async Task<IActionResult> DesactivarCarpeta(Guid id, CancellationToken ct)
    {
        try
        {
            var ok = await _svc.DesactivarCarpetaAsync(id, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ----- Etiquetas -----

    [HttpGet("etiquetas")]
    public async Task<IActionResult> ListarEtiquetas(CancellationToken ct)
        => Ok(await _svc.ListarEtiquetasAsync(ct));

    [HttpPost("etiquetas")]
    public async Task<IActionResult> CrearEtiqueta([FromBody] CrearEtiquetaRequest req, CancellationToken ct)
    {
        try
        {
            var e = await _svc.CrearEtiquetaAsync(req, ct);
            return Ok(e);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("etiquetas/{id:guid}")]
    public async Task<IActionResult> ActualizarEtiqueta(Guid id, [FromBody] ActualizarEtiquetaRequest req, CancellationToken ct)
    {
        try
        {
            var ok = await _svc.ActualizarEtiquetaAsync(id, req, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("etiquetas/{id:guid}")]
    public async Task<IActionResult> DesactivarEtiqueta(Guid id, CancellationToken ct)
    {
        try
        {
            var ok = await _svc.DesactivarEtiquetaAsync(id, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ----- Documentos: bandeja, ficha, ciclo de vida -----

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] Guid? categoriaId,
        [FromQuery] Guid? carpetaId,
        [FromQuery] OrigenDocumento? origen,
        [FromQuery] EstadoDocumento? estado,
        [FromQuery] string? visibilidad,
        [FromQuery] string? q,
        [FromQuery] Guid? etiquetaId,
        [FromQuery] bool? destacados,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken ct = default)
    {
        var filtro = new DocumentosFiltro(categoriaId, carpetaId, origen, estado, visibilidad, q, etiquetaId, destacados, page, pageSize);
        return Ok(await _svc.ListarDocumentosAsync(filtro, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var d = await _svc.GetDocumentoAsync(id, ct);
        return d is null ? NotFound() : Ok(d);
    }

    [HttpPost]
    public async Task<IActionResult> Subir([FromBody] SubirDocumentoRequest req, CancellationToken ct)
    {
        try
        {
            var d = await _svc.SubirDocumentoAsync(req, ct);
            return CreatedAtAction(nameof(Get), new { id = d.Id }, d);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (FormatException ex) { return BadRequest(new { error = "Contenido base64 invalido: " + ex.Message }); }
    }

    [HttpPost("{id:guid}/versiones")]
    public async Task<IActionResult> NuevaVersion(Guid id, [FromBody] NuevaVersionRequest req, CancellationToken ct)
    {
        try
        {
            var v = await _svc.SubirNuevaVersionAsync(id, req, ct);
            return Ok(v);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (FormatException ex) { return BadRequest(new { error = "Contenido base64 invalido: " + ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> ActualizarMetadatos(Guid id, [FromBody] ActualizarMetadatosRequest req, CancellationToken ct)
    {
        var ok = await _svc.ActualizarMetadatosAsync(id, req, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpPut("{id:guid}/estado")]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] CambiarEstadoRequest req, CancellationToken ct)
    {
        try
        {
            var ok = await _svc.CambiarEstadoAsync(id, req, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
    {
        var ok = await _svc.EliminarDocumentoAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }

    // ----- Destacados -----

    [HttpPost("{id:guid}/destacado-personal")]
    public async Task<IActionResult> MarcarDestacadoPersonal(Guid id, CancellationToken ct)
        => await _svc.MarcarDestacadoPersonalAsync(id, ct) ? NoContent() : NotFound();

    [HttpDelete("{id:guid}/destacado-personal")]
    public async Task<IActionResult> QuitarDestacadoPersonal(Guid id, CancellationToken ct)
        => await _svc.QuitarDestacadoPersonalAsync(id, ct) ? NoContent() : NotFound();

    [HttpPut("{id:guid}/destacado")]
    public async Task<IActionResult> MarcarDestacadoCopropiedad(Guid id, [FromQuery] bool destacar, CancellationToken ct)
        => await _svc.MarcarDestacadoCopropiedadAsync(id, destacar, ct) ? NoContent() : NotFound();

    // ----- Descarga y vista -----

    [HttpGet("{id:guid}/descargar")]
    public async Task<IActionResult> Descargar(Guid id, [FromQuery] Guid? versionId, CancellationToken ct)
    {
        var dto = await _svc.DescargarAsync(id, versionId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("{id:guid}/vistas")]
    public async Task<IActionResult> RegistrarVista(Guid id, [FromQuery] DispositivoConsumo dispositivo = DispositivoConsumo.Unknown, CancellationToken ct = default)
    {
        await _svc.RegistrarVistaAsync(id, dispositivo, ct);
        return NoContent();
    }

    // ----- Auditoria + estadisticas -----

    [HttpGet("{id:guid}/auditoria")]
    public async Task<IActionResult> ListarAuditoria(Guid id, CancellationToken ct)
        => Ok(await _svc.ListarAuditoriaAsync(id, ct));

    [HttpGet("{id:guid}/estadisticas")]
    public async Task<IActionResult> GetEstadisticas(Guid id, CancellationToken ct)
        => Ok(await _svc.GetEstadisticasAsync(id, ct));

    [HttpGet("resumen")]
    public async Task<IActionResult> GetResumen(CancellationToken ct)
        => Ok(await _svc.GetResumenAsync(ct));
}
