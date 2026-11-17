using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Api.Authorization;
using Propia.Application.Siniestros;
using Propia.Domain.Enums;
using Propia.Infrastructure.Storage;

namespace Propia.Api.Controllers;

/// <summary>
/// Modulo Siniestros (2026-11-16). GET abiertos al tenant; escrituras gateadas por permiso SINIESTROS
/// (Admin hace bypass). Las validaciones del servicio (InvalidOperationException) salen como 400.
/// </summary>
[ApiController]
[Authorize]
[Route("api/siniestros")]
public class SiniestrosController : ControllerBase
{
    private readonly ISiniestrosService _svc;
    private readonly IBlobStorage _storage;
    public SiniestrosController(ISiniestrosService svc, IBlobStorage storage) { _svc = svc; _storage = storage; }

    // ---- Siniestros ----
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _svc.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken ct)
        => await _svc.ObtenerAsync(id, ct) is { } s ? Ok(s) : NotFound();

    [HttpPost]
    [RequierePermiso(ModuloCodigo.Siniestros, AccionPermiso.Crear)]
    public Task<IActionResult> Crear([FromBody] CrearSiniestroRequest req, CancellationToken ct)
        => Validar(async () => Created($"/api/siniestros", await _svc.CrearAsync(req, ct)));

    [HttpPut("{id:guid}")]
    [RequierePermiso(ModuloCodigo.Siniestros, AccionPermiso.Editar)]
    public Task<IActionResult> Actualizar(Guid id, [FromBody] ActualizarSiniestroRequest req, CancellationToken ct)
        => Validar(async () => await _svc.ActualizarAsync(id, req, ct) ? NoContent() : NotFound());

    [HttpPut("{id:guid}/estado")]
    [RequierePermiso(ModuloCodigo.Siniestros, AccionPermiso.Editar)]
    public Task<IActionResult> CambiarEstado(Guid id, [FromBody] CambiarEstadoSiniestroRequest req, CancellationToken ct)
        => Validar(async () => await _svc.CambiarEstadoAsync(id, req, ct) ? NoContent() : NotFound());

    [HttpDelete("{id:guid}")]
    [RequierePermiso(ModuloCodigo.Siniestros, AccionPermiso.Eliminar)]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
        => await _svc.EliminarAsync(id, ct) ? NoContent() : NotFound();

    /// <summary>S3: crea la tarea de reparacion (tablero General, ligada al elemento y al siniestro).</summary>
    [HttpPost("{id:guid}/tarea-reparacion")]
    [RequierePermiso(ModuloCodigo.Siniestros, AccionPermiso.Editar)]
    public Task<IActionResult> CrearTareaReparacion(Guid id, CancellationToken ct)
        => Validar(async () => await _svc.CrearTareaReparacionAsync(id, ct) is { } s ? Ok(s) : NotFound());

    // ---- Estados (columnas) ----
    [HttpGet("estados")]
    public async Task<IActionResult> ListEstados(CancellationToken ct) => Ok(await _svc.ListEstadosAsync(ct));

    [HttpPost("estados")]
    [RequierePermiso(ModuloCodigo.Siniestros, AccionPermiso.Editar)]
    public Task<IActionResult> CrearEstado([FromBody] CrearSiniestroEstadoRequest req, CancellationToken ct)
        => Validar(async () => Created("", await _svc.CrearEstadoAsync(req, ct)));

    [HttpPut("estados/{estadoId:guid}")]
    [RequierePermiso(ModuloCodigo.Siniestros, AccionPermiso.Editar)]
    public Task<IActionResult> ActualizarEstado(Guid estadoId, [FromBody] ActualizarSiniestroEstadoRequest req, CancellationToken ct)
        => Validar(async () => await _svc.ActualizarEstadoAsync(estadoId, req, ct) ? NoContent() : NotFound());

    [HttpDelete("estados/{estadoId:guid}")]
    [RequierePermiso(ModuloCodigo.Siniestros, AccionPermiso.Editar)]
    public Task<IActionResult> EliminarEstado(Guid estadoId, CancellationToken ct)
        => Validar(async () => await _svc.EliminarEstadoAsync(estadoId, ct) ? NoContent() : NotFound());

    [HttpPut("estados/orden")]
    [RequierePermiso(ModuloCodigo.Siniestros, AccionPermiso.Editar)]
    public async Task<IActionResult> ReordenarEstados([FromBody] ReordenarSiniestroEstadosRequest req, CancellationToken ct)
    {
        await _svc.ReordenarEstadosAsync(req, ct);
        return NoContent();
    }

    // ---- Campos personalizados (EAV) ----
    [HttpGet("campos")]
    public async Task<IActionResult> ListCampos(CancellationToken ct) => Ok(await _svc.ListCamposAsync(ct));

    [HttpPost("campos")]
    [RequierePermiso(ModuloCodigo.Siniestros, AccionPermiso.Editar)]
    public Task<IActionResult> CrearCampo([FromBody] CrearSiniestroCampoRequest req, CancellationToken ct)
        => Validar(async () => Created("", await _svc.CrearCampoAsync(req, ct)));

    [HttpPut("campos/{campoId:guid}")]
    [RequierePermiso(ModuloCodigo.Siniestros, AccionPermiso.Editar)]
    public Task<IActionResult> ActualizarCampo(Guid campoId, [FromBody] ActualizarSiniestroCampoRequest req, CancellationToken ct)
        => Validar(async () => await _svc.ActualizarCampoAsync(campoId, req, ct) ? NoContent() : NotFound());

    [HttpDelete("campos/{campoId:guid}")]
    [RequierePermiso(ModuloCodigo.Siniestros, AccionPermiso.Editar)]
    public async Task<IActionResult> EliminarCampo(Guid campoId, CancellationToken ct)
        => await _svc.EliminarCampoAsync(campoId, ct) ? NoContent() : NotFound();

    [HttpGet("campos-valores")]
    public async Task<IActionResult> ListTodosCamposValores(CancellationToken ct) => Ok(await _svc.ListTodosCamposValoresAsync(ct));

    [HttpGet("{id:guid}/campos-din")]
    public async Task<IActionResult> ListCamposDin(Guid id, CancellationToken ct) => Ok(await _svc.ListCamposDinAsync(id, ct));

    [HttpPut("{id:guid}/campos/{campoId:guid}")]
    [RequierePermiso(ModuloCodigo.Siniestros, AccionPermiso.Editar)]
    public Task<IActionResult> GuardarCampoValor(Guid id, Guid campoId, [FromBody] GuardarSiniestroCampoValorRequest req, CancellationToken ct)
        => Validar(async () => await _svc.GuardarCampoValorAsync(id, campoId, req, ct) ? NoContent() : NotFound());

    // ---- Documentos ----
    [HttpGet("{id:guid}/documentos")]
    public async Task<IActionResult> ListDocumentos(Guid id, CancellationToken ct) => Ok(await _svc.ListDocumentosAsync(id, ct));

    [HttpPost("{id:guid}/documentos")]
    [RequierePermiso(ModuloCodigo.Siniestros, AccionPermiso.Editar)]
    [RequestSizeLimit(11_000_000)]
    public async Task<IActionResult> SubirDocumento(Guid id, IFormFile file, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("tenant_id"), out var tenantId)) return BadRequest(new { error = "no_active_tenant" });
        if (file is null || file.Length == 0) return BadRequest(new { error = "Archivo vacio." });
        if (file.Length > 10_000_000) return BadRequest(new { error = "Maximo 10 MB." });
        // El siniestro debe existir en la copropiedad ANTES de subir el blob (no dejar archivos huerfanos).
        if (await _svc.ObtenerAsync(id, ct) is null) return NotFound();
        var ext = System.IO.Path.GetExtension(file.FileName);
        var key = $"tenants/{tenantId:N}/siniestros/{id:N}/docs/{Guid.NewGuid():N}{ext}";
        await using var stream = file.OpenReadStream();
        var url = await _storage.UploadAsync(key, stream, file.ContentType, ct);
        var dto = await _svc.AgregarDocumentoAsync(id, file.FileName, url, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpDelete("documentos/{docId:guid}")]
    [RequierePermiso(ModuloCodigo.Siniestros, AccionPermiso.Editar)]
    public async Task<IActionResult> EliminarDocumento(Guid docId, CancellationToken ct)
        => await _svc.EliminarDocumentoAsync(docId, ct) ? NoContent() : NotFound();

    // ---- Actividad ----
    [HttpGet("{id:guid}/comentarios")]
    public async Task<IActionResult> ListComentarios(Guid id, CancellationToken ct) => Ok(await _svc.ListComentariosAsync(id, ct));

    [HttpPost("{id:guid}/comentarios")]
    [RequierePermiso(ModuloCodigo.Siniestros, AccionPermiso.Editar)]
    public Task<IActionResult> AgregarComentario(Guid id, [FromBody] AgregarComentarioSiniestroRequest req, CancellationToken ct)
        => Validar(async () => Created("", await _svc.AgregarComentarioAsync(id, req, ct)));

    private async Task<IActionResult> Validar(Func<Task<IActionResult>> accion)
    {
        try { return await accion(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
