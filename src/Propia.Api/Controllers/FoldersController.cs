using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Api.Authorization;
using Propia.Application.Documentos;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>
/// Modulo 2.15 Documentos - vista "Folders" (v2). Arbol de folders, documentos versionados,
/// archivado restaurable, metadatos tipados (folder heredable + documento) y ZIP recursivo.
/// Requiere JWT del tenant. GET abiertos al tenant; escrituras gateadas por permiso.
/// </summary>
[ApiController]
[Route("api/folders")]
[Authorize]
public class FoldersController : ControllerBase
{
    private readonly IFoldersService _svc;
    private readonly IFolderChatService _chat;

    public FoldersController(IFoldersService svc, IFolderChatService chat) { _svc = svc; _chat = chat; }

    // ----- Chat con los documentos (Ola 3) -----
    [HttpGet("{id:guid}/chat")]
    public async Task<IActionResult> Chat(Guid id, CancellationToken ct)
    {
        var dto = await _chat.ListarAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("{id:guid}/chat")]
    public async Task<IActionResult> ChatEnviar(Guid id, [FromBody] EnviarFolderChatRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _chat.EnviarAsync(id, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("{id:guid}/chat")]
    public async Task<IActionResult> ChatLimpiar(Guid id, CancellationToken ct)
        => await _chat.LimpiarAsync(id, ct) ? NoContent() : NotFound();

    /// <summary>Extrae (o reutiliza) el texto del archivo actual del documento para la IA. forzar=true re-extrae.</summary>
    [RequierePermiso(ModuloCodigo.Documentos, AccionPermiso.Editar)]
    [HttpPost("documentos/{documentoId:guid}/indexar")]
    public async Task<IActionResult> Indexar(Guid documentoId, [FromQuery] bool forzar, CancellationToken ct)
    {
        var dto = await _chat.IndexarDocumentoAsync(documentoId, forzar, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    // ----- Folders -----
    [HttpGet("arbol")]
    public async Task<IActionResult> Arbol(CancellationToken ct) => Ok(await _svc.ArbolAsync(ct));

    [HttpGet("tarjetas")]
    public async Task<IActionResult> Tarjetas(CancellationToken ct) => Ok(await _svc.TarjetasAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetFolder(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetFolderAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [RequierePermiso(ModuloCodigo.Documentos, AccionPermiso.Crear)]
    [HttpPost]
    public async Task<IActionResult> CrearFolder([FromBody] CrearFolderRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.CrearFolderAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Documentos, AccionPermiso.Editar)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> ActualizarFolder(Guid id, [FromBody] ActualizarFolderRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarFolderAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Documentos, AccionPermiso.Eliminar)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> EliminarFolder(Guid id, CancellationToken ct)
    {
        try { return await _svc.EliminarFolderAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    /// <summary>ZIP con todos los documentos del folder y sus subfolders (jerarquia como carpetas).</summary>
    [HttpGet("{id:guid}/zip")]
    public async Task<IActionResult> Zip(Guid id, CancellationToken ct)
    {
        var zip = await _svc.DescargarZipAsync(id, ct);
        if (zip is null) return NotFound();
        Response.Headers["X-Archivos"] = zip.Archivos.ToString();
        return File(zip.Contenido, "application/zip", zip.NombreArchivo);
    }

    // ----- Documentos -----
    [HttpGet("documentos/{documentoId:guid}")]
    public async Task<IActionResult> GetDocumento(Guid documentoId, CancellationToken ct)
    {
        var dto = await _svc.GetDocumentoAsync(documentoId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [RequierePermiso(ModuloCodigo.Documentos, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/documentos")]
    public async Task<IActionResult> CrearDocumento(Guid id, [FromBody] CrearDocumentoRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.CrearDocumentoAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Documentos, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/documentos/cargar")]
    public async Task<IActionResult> CargarDocumento(Guid id, [FromBody] CargarDocumentoRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.CargarDocumentoAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Documentos, AccionPermiso.Crear)]
    [HttpPost("documentos/{documentoId:guid}/versiones")]
    public async Task<IActionResult> CargarVersion(Guid documentoId, [FromBody] CargarVersionRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _svc.CargarVersionAsync(documentoId, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Documentos, AccionPermiso.Editar)]
    [HttpPut("documentos/{documentoId:guid}")]
    public async Task<IActionResult> ActualizarDocumento(Guid documentoId, [FromBody] ActualizarDocumentoRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarDocumentoAsync(documentoId, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Documentos, AccionPermiso.Eliminar)]
    [HttpDelete("documentos/{documentoId:guid}")]
    public async Task<IActionResult> EliminarDocumento(Guid documentoId, CancellationToken ct)
    {
        try { return await _svc.EliminarDocumentoAsync(documentoId, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Documentos, AccionPermiso.Editar)]
    [HttpPost("documentos/{documentoId:guid}/archivar")]
    public async Task<IActionResult> Archivar(Guid documentoId, CancellationToken ct)
        => await _svc.ArchivarDocumentoAsync(documentoId, ct) ? NoContent() : NotFound();

    [RequierePermiso(ModuloCodigo.Documentos, AccionPermiso.Editar)]
    [HttpPost("documentos/{documentoId:guid}/restaurar")]
    public async Task<IActionResult> Restaurar(Guid documentoId, CancellationToken ct)
        => await _svc.RestaurarDocumentoAsync(documentoId, ct) ? NoContent() : NotFound();

    [RequierePermiso(ModuloCodigo.Documentos, AccionPermiso.Crear)]
    [HttpPost("documentos/{documentoId:guid}/duplicar")]
    public async Task<IActionResult> Duplicar(Guid documentoId, CancellationToken ct)
    {
        var dto = await _svc.DuplicarDocumentoAsync(documentoId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("documentos/{documentoId:guid}/descargar")]
    public async Task<IActionResult> Descargar(Guid documentoId, CancellationToken ct)
    {
        var dto = await _svc.DescargarDocumentoAsync(documentoId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("documentos/{documentoId:guid}/versiones")]
    public async Task<IActionResult> Versiones(Guid documentoId, CancellationToken ct)
        => Ok(await _svc.ListarVersionesAsync(documentoId, ct));

    [HttpGet("documentos/{documentoId:guid}/versiones/{versionId:guid}/descargar")]
    public async Task<IActionResult> DescargarVersion(Guid documentoId, Guid versionId, CancellationToken ct)
    {
        var dto = await _svc.DescargarVersionAsync(documentoId, versionId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("documentos/{documentoId:guid}/preview")]
    public async Task<IActionResult> Preview(Guid documentoId, CancellationToken ct)
    {
        var dto = await _svc.PreviewDocumentoAsync(documentoId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("documentos/{documentoId:guid}/versiones/{versionId:guid}/preview")]
    public async Task<IActionResult> PreviewVersion(Guid documentoId, Guid versionId, CancellationToken ct)
    {
        var dto = await _svc.PreviewVersionAsync(documentoId, versionId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [RequierePermiso(ModuloCodigo.Documentos, AccionPermiso.Editar)]
    [HttpPut("documentos/{documentoId:guid}/valores")]
    public async Task<IActionResult> GuardarValores(Guid documentoId, [FromBody] GuardarValoresRequest req, CancellationToken ct)
    {
        try { return await _svc.GuardarValoresAsync(documentoId, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Lista plana (selectores de Contratos y Polizas).</summary>
    [HttpGet("lista")]
    public async Task<IActionResult> Lista(CancellationToken ct) => Ok(await _svc.ListaAsync(ct));

    // ----- Metadatos -----
    [HttpGet("documentos/{documentoId:guid}/campos")]
    public async Task<IActionResult> CamposEfectivos(Guid documentoId, CancellationToken ct)
        => Ok(await _svc.CamposEfectivosAsync(documentoId, ct));

    // Catalogo de campos PROPIOS de un folder o documento con el contrato del catalogo transversal
    // (GET lista / POST crear / PUT editar / DELETE), para reusar CrearCampoModal tal cual.
    // dueno = "folder" | "documento".
    [HttpGet("campos-catalogo/{dueno}/{id:guid}")]
    public async Task<IActionResult> CatalogoListar(string dueno, Guid id, CancellationToken ct)
    {
        var (f, d) = Dueno(dueno, id);
        if (f is null && d is null) return NotFound();
        return Ok(await _svc.ListarCamposAsync(f, d, ct));
    }

    [RequierePermiso(ModuloCodigo.Documentos, AccionPermiso.Crear)]
    [HttpPost("campos-catalogo/{dueno}/{id:guid}")]
    public async Task<IActionResult> CatalogoCrear(string dueno, Guid id, [FromBody] Propia.Application.MiCopropiedad.CrearCampoDefinicionRequest req, CancellationToken ct)
    {
        var (f, d) = Dueno(dueno, id);
        if (f is null && d is null) return NotFound();
        try { return Ok(await _svc.CrearCampoAsync(new CrearFolderCampoRequest(f, d, req.Label, req.Tipo, req.Opciones, req.Descripcion), ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Documentos, AccionPermiso.Editar)]
    [HttpPut("campos-catalogo/{dueno}/{id:guid}/{campoId:guid}")]
    public async Task<IActionResult> CatalogoEditar(string dueno, Guid id, Guid campoId, [FromBody] Propia.Application.MiCopropiedad.ActualizarCampoDefinicionRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarCampoAsync(campoId, new ActualizarFolderCampoRequest(req.Label, req.Tipo, req.Opciones ?? "", req.Descripcion ?? "", null, req.Orden), ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Documentos, AccionPermiso.Eliminar)]
    [HttpDelete("campos-catalogo/{dueno}/{id:guid}/{campoId:guid}")]
    public async Task<IActionResult> CatalogoEliminar(string dueno, Guid id, Guid campoId, CancellationToken ct)
        => await _svc.EliminarCampoAsync(campoId, ct) ? NoContent() : NotFound();

    private static (Guid? folderId, Guid? documentoId) Dueno(string dueno, Guid id) => dueno?.ToLowerInvariant() switch
    {
        "folder" => (id, null),
        "documento" => (null, id),
        _ => (null, null)
    };

    [RequierePermiso(ModuloCodigo.Documentos, AccionPermiso.Crear)]
    [HttpPost("campos")]
    public async Task<IActionResult> CrearCampo([FromBody] CrearFolderCampoRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.CrearCampoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Documentos, AccionPermiso.Editar)]
    [HttpPut("campos/{campoId:guid}")]
    public async Task<IActionResult> ActualizarCampo(Guid campoId, [FromBody] ActualizarFolderCampoRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarCampoAsync(campoId, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Documentos, AccionPermiso.Eliminar)]
    [HttpDelete("campos/{campoId:guid}")]
    public async Task<IActionResult> EliminarCampo(Guid campoId, CancellationToken ct)
        => await _svc.EliminarCampoAsync(campoId, ct) ? NoContent() : NotFound();
}
