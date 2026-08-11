using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Tareas;
using Propia.Application.UsuariosAccesos;
using Propia.Domain.Enums;
using Propia.Infrastructure.Storage;

namespace Propia.Api.Controllers;

[ApiController]
[Route("api/tareas")]
[Authorize]
public class TareasController : ControllerBase
{
    private readonly ITareasService _svc;
    private readonly IBlobStorage _storage;
    private readonly IUsuariosService _usuarios;
    public TareasController(ITareasService svc, IBlobStorage storage, IUsuariosService usuarios)
    {
        _svc = svc;
        _storage = storage;
        _usuarios = usuarios;
    }

    // --- Estados ---
    [HttpGet("estados")]
    public async Task<IActionResult> ListarEstados(CancellationToken ct) => Ok(await _svc.ListarEstadosAsync(ct));

    [HttpPost("estados")]
    public async Task<IActionResult> CrearEstado([FromBody] CrearEstadoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearEstadoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("estados/{id:guid}")]
    public async Task<IActionResult> ActualizarEstado(Guid id, [FromBody] ActualizarEstadoRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarEstadoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("estados/{id:guid}")]
    public async Task<IActionResult> EliminarEstado(Guid id, CancellationToken ct)
    {
        try { return await _svc.EliminarEstadoAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Etiquetas ---
    [HttpGet("etiquetas")]
    public async Task<IActionResult> ListarEtiquetas([FromQuery] Guid? tableroId, CancellationToken ct) => Ok(await _svc.ListarEtiquetasAsync(tableroId, ct));

    [HttpPost("etiquetas")]
    public async Task<IActionResult> CrearEtiqueta([FromBody] CrearEtiquetaRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearEtiquetaAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("etiquetas/{id:guid}")]
    public async Task<IActionResult> ActualizarEtiqueta(Guid id, [FromBody] ActualizarEtiquetaRequest req, CancellationToken ct)
        => await _svc.ActualizarEtiquetaAsync(id, req, ct) ? NoContent() : NotFound();

    [HttpDelete("etiquetas/{id:guid}")]
    public async Task<IActionResult> EliminarEtiqueta(Guid id, CancellationToken ct)
        => await _svc.EliminarEtiquetaAsync(id, ct) ? NoContent() : NotFound();

    // --- Tareas ---
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] Guid? estadoId, [FromQuery] PrioridadTarea? prioridad,
        [FromQuery] Guid? asignadoPersonaId, [FromQuery] Guid? padreId,
        [FromQuery] bool? soloRaiz, [FromQuery] string? q, CancellationToken ct)
        => Ok(await _svc.ListarTareasAsync(estadoId, prioridad, asignadoPersonaId, padreId, soloRaiz, q, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetTareaAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearTareaRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearTareaAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] ActualizarTareaRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarTareaAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Edicion inline de un solo campo (vista tabla tipo Excel): titulo, descripcion, valor,
    // prioridad, fechaVencimiento, fechaInicio, asignados. Preserva el resto de la tarea.
    [HttpPatch("{id:guid}/inline")]
    public async Task<IActionResult> ActualizarInline(Guid id, [FromBody] InlineUpdateTareaRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarCampoInlineAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Set del valor de UN campo personalizado (TableroCampo) de la tarea, inline.
    [HttpPut("{id:guid}/campo-valor/{campoId:guid}")]
    public async Task<IActionResult> SetCampoValor(Guid id, Guid campoId, [FromBody] SetCampoValorRequest req, CancellationToken ct)
    {
        try { return await _svc.SetCampoValorAsync(id, campoId, req.Valor, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Duplicar la tarea como una nueva ("(copia)"), sin subtareas hijas.
    [HttpPost("{id:guid}/duplicar")]
    public async Task<IActionResult> Duplicar(Guid id, CancellationToken ct)
    {
        var nueva = await _svc.DuplicarTareaAsync(id, ct);
        return nueva is null ? NotFound() : Ok(nueva);
    }

    // Copiar la tarea N veces con opciones (titulo, etapa, que conservar). Copias independientes con traza.
    [HttpPost("{id:guid}/copiar")]
    public async Task<IActionResult> Copiar(Guid id, [FromBody] CopiarTareaRequest req, CancellationToken ct)
    {
        var copias = await _svc.CopiarTareaAsync(id, req, ct);
        return copias.Count == 0 ? NotFound() : Ok(copias);
    }

    [HttpPut("{id:guid}/estado")]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] CambiarEstadoRequest req, CancellationToken ct)
    {
        try { return await _svc.CambiarEstadoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Comentarios / Etiquetas / Colaboradores ---
    [HttpPost("{id:guid}/comentarios")]
    public async Task<IActionResult> AgregarComentario(Guid id, [FromBody] CrearComentarioRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarComentarioAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/etiquetas")]
    public async Task<IActionResult> AsignarEtiqueta(Guid id, [FromBody] AsignarEtiquetaRequest req, CancellationToken ct)
        => await _svc.AsignarEtiquetaAsync(id, req, ct) ? NoContent() : NotFound();

    [HttpDelete("{id:guid}/etiquetas/{etiquetaId:guid}")]
    public async Task<IActionResult> RemoverEtiqueta(Guid id, Guid etiquetaId, CancellationToken ct)
        => await _svc.RemoverEtiquetaAsync(id, etiquetaId, ct) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/colaboradores")]
    public async Task<IActionResult> AgregarColaborador(Guid id, [FromBody] AgregarColaboradorRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarColaboradorAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("{id:guid}/colaboradores/{colaboradorId:guid}")]
    public async Task<IActionResult> RemoverColaborador(Guid id, Guid colaboradorId, CancellationToken ct)
        => await _svc.RemoverColaboradorAsync(id, colaboradorId, ct) ? NoContent() : NotFound();

    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen(CancellationToken ct) => Ok(await _svc.GetResumenAsync(ct));

    // --- Dependencias (Fase 2) ---

    [HttpGet("{id:guid}/dependencias")]
    public async Task<IActionResult> ListarDependencias(Guid id, CancellationToken ct)
        => Ok(await _svc.ListarDependenciasAsync(id, ct));

    [HttpPost("{id:guid}/dependencias")]
    public async Task<IActionResult> AgregarDependencia(Guid id, [FromBody] AgregarDependenciaRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarDependenciaAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("{id:guid}/dependencias/{dependenciaId:guid}")]
    public async Task<IActionResult> RemoverDependencia(Guid id, Guid dependenciaId, CancellationToken ct)
        => await _svc.RemoverDependenciaAsync(id, dependenciaId, ct) ? NoContent() : NotFound();

    // --- Bulk actions (Fase 2) ---

    [HttpPost("bulk/estado")]
    public async Task<IActionResult> BulkCambiarEstado([FromBody] BulkCambiarEstadoRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.BulkCambiarEstadoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("bulk/prioridad")]
    public async Task<IActionResult> BulkCambiarPrioridad([FromBody] BulkCambiarPrioridadRequest req, CancellationToken ct)
        => Ok(await _svc.BulkCambiarPrioridadAsync(req, ct));

    [HttpPost("bulk/asignado")]
    public async Task<IActionResult> BulkAsignarPersona([FromBody] BulkAsignarPersonaRequest req, CancellationToken ct)
        => Ok(await _svc.BulkAsignarPersonaAsync(req, ct));

    // ----- Tableros de trabajo (2.10) -----
    [HttpGet("tableros")]
    public async Task<IActionResult> ListarTableros(CancellationToken ct) => Ok(await _svc.ListarTablerosAsync(ct));

    [HttpGet("tableros/{id:guid}")]
    public async Task<IActionResult> GetTablero(Guid id, CancellationToken ct)
    {
        var t = await _svc.GetTableroAsync(id, ct);
        return t is null ? NotFound() : Ok(t);
    }

    [HttpPost("tableros")]
    public async Task<IActionResult> CrearTablero([FromBody] GuardarTableroRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearTableroAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("tableros/{id:guid}")]
    public async Task<IActionResult> ActualizarTablero(Guid id, [FromBody] GuardarTableroRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarTableroAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("tableros/{id:guid}")]
    public async Task<IActionResult> EliminarTablero(Guid id, CancellationToken ct)
        => await _svc.EliminarTableroAsync(id, ct) ? NoContent() : NotFound();

    // Enlazar/desenlazar una persona a un tablero (2.5.D: desde el modulo Usuarios).
    [HttpPost("tableros/{id:guid}/usuarios/{personaId:guid}")]
    public async Task<IActionResult> AgregarUsuarioTablero(Guid id, Guid personaId, CancellationToken ct)
        => await _svc.AgregarUsuarioTableroAsync(id, personaId, ct) ? NoContent() : NotFound();

    [HttpDelete("tableros/{id:guid}/usuarios/{personaId:guid}")]
    public async Task<IActionResult> QuitarUsuarioTablero(Guid id, Guid personaId, CancellationToken ct)
        => await _svc.QuitarUsuarioTableroAsync(id, personaId, ct) ? NoContent() : NotFound();

    // Invitar a un externo (por email) a colaborar en el tablero: crea la persona si no existe,
    // genera el link de aceptacion y envia el correo. Devuelve la invitacion (con LinkAceptacion).
    [HttpPost("tableros/{id:guid}/invitar-externo")]
    public async Task<IActionResult> InvitarExternoTablero(Guid id, [FromBody] InvitarExternoTableroBody body, CancellationToken ct)
    {
        try
        {
            var req = new InvitarExternoTableroRequest(body.Email, body.Nombre, body.RolId, id);
            return Ok(await _usuarios.InvitarExternoTableroAsync(req, ct));
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    public record InvitarExternoTableroBody(string Email, string Nombre, Guid RolId);

    [HttpGet("tableros/{id:guid}/board")]
    public async Task<IActionResult> GetTableroBoard(Guid id, CancellationToken ct)
    {
        var b = await _svc.GetTableroBoardAsync(id, ct);
        return b is null ? NotFound() : Ok(b);
    }

    [HttpPost("tableros/{id:guid}/campos")]
    public async Task<IActionResult> AgregarCampo(Guid id, [FromBody] GuardarCampoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarCampoAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("tableros/{id:guid}/campos/{campoId:guid}")]
    public async Task<IActionResult> ActualizarCampo(Guid id, Guid campoId, [FromBody] GuardarCampoRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarCampoAsync(id, campoId, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("tableros/{id:guid}/campos/{campoId:guid}")]
    public async Task<IActionResult> EliminarCampo(Guid id, Guid campoId, CancellationToken ct)
        => await _svc.EliminarCampoAsync(id, campoId, ct) ? NoContent() : NotFound();

    // Reordena un campo: direccion < 0 lo sube, direccion >= 0 lo baja (intercambia con el vecino).
    [HttpPut("tableros/{id:guid}/campos/{campoId:guid}/orden")]
    public async Task<IActionResult> ReordenarCampo(Guid id, Guid campoId, [FromQuery] int direccion, CancellationToken ct)
        => await _svc.ReordenarCampoAsync(id, campoId, direccion, ct) ? NoContent() : NotFound();

    // Archiva (archivar=true) o restaura (archivar=false) un campo, conservando sus valores.
    [HttpPut("tableros/{id:guid}/campos/{campoId:guid}/archivar")]
    public async Task<IActionResult> ArchivarCampo(Guid id, Guid campoId, [FromQuery] bool archivar, CancellationToken ct)
    {
        try { return await _svc.SetCampoActivoAsync(id, campoId, !archivar, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    // Campos archivados de un tablero (para restaurar).
    [HttpGet("tableros/{id:guid}/campos-archivados")]
    public async Task<IActionResult> CamposArchivados(Guid id, CancellationToken ct)
        => Ok(await _svc.ListarCamposArchivadosAsync(id, ct));

    [HttpPut("{id:guid}/progreso")]
    public async Task<IActionResult> ActualizarProgreso(Guid id, [FromBody] ActualizarProgresoRequest req, CancellationToken ct)
        => await _svc.ActualizarProgresoAsync(id, req.Progreso, ct) ? NoContent() : NotFound();

    // --- Eliminar tarjeta (soft-delete) ---
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
        => await _svc.EliminarTareaAsync(id, ct) ? NoContent() : NotFound();

    // --- Adjuntos de la tarjeta ---
    [HttpPost("{id:guid}/adjuntos")]
    [RequestSizeLimit(11_000_000)]
    public async Task<IActionResult> SubirAdjunto(Guid id, IFormFile file, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId is null) return BadRequest(new { error = "no_active_tenant" });
        if (file is null || file.Length == 0) return BadRequest(new { error = "Archivo vacio." });
        if (file.Length > 10_000_000) return BadRequest(new { error = "Maximo 10 MB." });
        var ext = System.IO.Path.GetExtension(file.FileName);
        var key = $"tenants/{tenantId:N}/tareas/{id:N}/{Guid.NewGuid():N}{ext}";
        await using var stream = file.OpenReadStream();
        var url = Absolutizar(await _storage.UploadAsync(key, stream, file.ContentType ?? "application/octet-stream", ct));
        var dto = await _svc.AgregarAdjuntoAsync(id, file.FileName, url, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpDelete("{id:guid}/adjuntos/{adjuntoId:guid}")]
    public async Task<IActionResult> EliminarAdjunto(Guid id, Guid adjuntoId, CancellationToken ct)
        => await _svc.EliminarAdjuntoAsync(id, adjuntoId, ct) ? NoContent() : NotFound();

    private Guid? GetTenantId()
    {
        var raw = User.FindFirstValue("tenant_id");
        return Guid.TryParse(raw, out var g) ? g : null;
    }

    private string Absolutizar(string url) => url.StartsWith('/') ? $"{Request.Scheme}://{Request.Host}{url}" : url;
}
