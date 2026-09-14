using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Propia.Api.Authorization;
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
    private readonly IRolesService _roles;
    public TareasController(ITareasService svc, IBlobStorage storage, IUsuariosService usuarios, IRolesService roles)
    {
        _svc = svc;
        _storage = storage;
        _usuarios = usuarios;
        _roles = roles;
    }

    /// <summary>
    /// Permisos del usuario actual SOBRE ESTE MODULO. La UI lo usa para no ofrecer botones que el
    /// backend va a rechazar con 403: configurar el tablero (estados, etiquetas, tableros y campos)
    /// exige Aprobar, no basta con poder crear o editar tareas. Es un GET de solo lectura sobre el
    /// propio usuario, asi que no lleva RequierePermiso: preguntar "que puedo hacer yo" no filtra nada.
    /// </summary>
    [HttpGet("permisos")]
    public async Task<IActionResult> MisPermisos(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("persona_id"), out var personaId))
            return Ok(new PermisosTareasDto(false, false, false, false, false));

        // Mismo criterio que RequierePermisoFilter, para que la UI y el backend no se contradigan.
        var rol = await _roles.GetRolActorAsync(personaId, ct);
        if (string.Equals(rol, "Administrador", StringComparison.OrdinalIgnoreCase))
            return Ok(new PermisosTareasDto(true, true, true, true, true));

        var permisos = await _roles.GetPermisosEfectivosAsync(personaId, ct);
        bool Tiene(AccionPermiso a) =>
            permisos.Any(p => p.ModuloCodigo == ModuloCodigo.Tareas && p.Accion == a && p.Habilitado);
        return Ok(new PermisosTareasDto(
            Tiene(AccionPermiso.Ver), Tiene(AccionPermiso.Crear), Tiene(AccionPermiso.Editar),
            Tiene(AccionPermiso.Eliminar), Tiene(AccionPermiso.Aprobar)));
    }

    // --- Estados ---
    // CONFIGURAR el tablero (estados, etiquetas, tableros, campos y quien trabaja en el) no es lo mismo
    // que TRABAJAR en el. Estos 18 endpoints usaban Crear/Editar/Eliminar, los mismos permisos que usar
    // el modulo: cualquiera que pudiera crear una tarea podia tambien crear columnas, borrar etiquetas o
    // invitar gente de afuera. Pasan a Aprobar, que hoy solo tiene Administrador. Las acciones SOBRE una
    // tarea (comentar, adjuntar, etiquetar, colaboradores, dependencias) se quedan como estaban.
    [HttpGet("estados")]
    public async Task<IActionResult> ListarEstados(CancellationToken ct) => Ok(await _svc.ListarEstadosAsync(ct));

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Aprobar)]
    [HttpPost("estados")]
    public async Task<IActionResult> CrearEstado([FromBody] CrearEstadoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearEstadoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Aprobar)]
    [HttpPut("estados/{id:guid}")]
    public async Task<IActionResult> ActualizarEstado(Guid id, [FromBody] ActualizarEstadoRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarEstadoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Aprobar)]
    [HttpDelete("estados/{id:guid}")]
    public async Task<IActionResult> EliminarEstado(Guid id, CancellationToken ct)
    {
        try { return await _svc.EliminarEstadoAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Etiquetas ---
    [HttpGet("etiquetas")]
    public async Task<IActionResult> ListarEtiquetas([FromQuery] Guid? tableroId, CancellationToken ct) => Ok(await _svc.ListarEtiquetasAsync(tableroId, ct));

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Aprobar)]
    [HttpPost("etiquetas")]
    public async Task<IActionResult> CrearEtiqueta([FromBody] CrearEtiquetaRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearEtiquetaAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Aprobar)]
    [HttpPut("etiquetas/{id:guid}")]
    public async Task<IActionResult> ActualizarEtiqueta(Guid id, [FromBody] ActualizarEtiquetaRequest req, CancellationToken ct)
        => await _svc.ActualizarEtiquetaAsync(id, req, ct) ? NoContent() : NotFound();

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Aprobar)]
    [HttpDelete("etiquetas/{id:guid}")]
    public async Task<IActionResult> EliminarEtiqueta(Guid id, CancellationToken ct)
        => await _svc.EliminarEtiquetaAsync(id, ct) ? NoContent() : NotFound();

    // --- Tareas ---
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] Guid? estadoId, [FromQuery] PrioridadTarea? prioridad,
        [FromQuery] Guid? asignadoPersonaId, [FromQuery] Guid? padreId,
        [FromQuery] bool? soloRaiz, [FromQuery] string? q, [FromQuery] bool verCerradas, CancellationToken ct)
        => Ok(await _svc.ListarTareasAsync(estadoId, prioridad, asignadoPersonaId, padreId, soloRaiz, q, ct, verCerradas: verCerradas));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetTareaAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Crear)]
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearTareaRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearTareaAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DbUpdateException) { return BadRequest(new { error = "Hay un dato relacionado que no existe o no pertenece a esta copropiedad." }); }
    }

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Editar)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] ActualizarTareaRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarTareaAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DbUpdateException) { return BadRequest(new { error = "Hay un dato relacionado que no existe o no pertenece a esta copropiedad." }); }
    }

    // Edicion inline de un solo campo (vista tabla tipo Excel): titulo, descripcion, valor,
    // prioridad, fechaVencimiento, fechaInicio, asignados. Preserva el resto de la tarea.
    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Editar)]
    [HttpPatch("{id:guid}/inline")]
    public async Task<IActionResult> ActualizarInline(Guid id, [FromBody] InlineUpdateTareaRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarCampoInlineAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DbUpdateException) { return BadRequest(new { error = "Hay un dato relacionado que no existe o no pertenece a esta copropiedad." }); }
    }

    // Set del valor de UN campo personalizado (TableroCampo) de la tarea, inline.
    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Editar)]
    [HttpPut("{id:guid}/campo-valor/{campoId:guid}")]
    public async Task<IActionResult> SetCampoValor(Guid id, Guid campoId, [FromBody] SetCampoValorRequest req, CancellationToken ct)
    {
        try { return await _svc.SetCampoValorAsync(id, campoId, req.Valor, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Duplicar la tarea como una nueva ("(copia)"), sin subtareas hijas.
    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/duplicar")]
    public async Task<IActionResult> Duplicar(Guid id, CancellationToken ct)
    {
        // T-03b: duplicar tambien puede rechazar (p. ej. no queda ninguna columna abierta donde
        // poner la copia). Sin este catch la negativa salia como 500.
        try
        {
            var nueva = await _svc.DuplicarTareaAsync(id, ct);
            return nueva is null ? NotFound() : Ok(nueva);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Copiar la tarea N veces con opciones (titulo, etapa, que conservar). Copias independientes con traza.
    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/copiar")]
    public async Task<IActionResult> Copiar(Guid id, [FromBody] CopiarTareaRequest req, CancellationToken ct)
    {
        // T-03b: elegir un estado terminal para las copias se rechaza. Sin este catch salia como 500.
        try
        {
            var copias = await _svc.CopiarTareaAsync(id, req, ct);
            return copias.Count == 0 ? NotFound() : Ok(copias);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Editar)]
    [HttpPut("{id:guid}/estado")]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] CambiarEstadoRequest req, CancellationToken ct)
    {
        try { return await _svc.CambiarEstadoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Comentarios / Etiquetas / Colaboradores ---
    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/comentarios")]
    public async Task<IActionResult> AgregarComentario(Guid id, [FromBody] CrearComentarioRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarComentarioAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/etiquetas")]
    public async Task<IActionResult> AsignarEtiqueta(Guid id, [FromBody] AsignarEtiquetaRequest req, CancellationToken ct)
        => await _svc.AsignarEtiquetaAsync(id, req, ct) ? NoContent() : NotFound();

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Eliminar)]
    [HttpDelete("{id:guid}/etiquetas/{etiquetaId:guid}")]
    public async Task<IActionResult> RemoverEtiqueta(Guid id, Guid etiquetaId, CancellationToken ct)
        => await _svc.RemoverEtiquetaAsync(id, etiquetaId, ct) ? NoContent() : NotFound();

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/colaboradores")]
    public async Task<IActionResult> AgregarColaborador(Guid id, [FromBody] AgregarColaboradorRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarColaboradorAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DbUpdateException) { return BadRequest(new { error = "Hay un dato relacionado que no existe o no pertenece a esta copropiedad." }); }
    }

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Eliminar)]
    [HttpDelete("{id:guid}/colaboradores/{colaboradorId:guid}")]
    public async Task<IActionResult> RemoverColaborador(Guid id, Guid colaboradorId, CancellationToken ct)
        => await _svc.RemoverColaboradorAsync(id, colaboradorId, ct) ? NoContent() : NotFound();

    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen(CancellationToken ct) => Ok(await _svc.GetResumenAsync(ct));

    // --- Dependencias (Fase 2) ---

    [HttpGet("{id:guid}/dependencias")]
    public async Task<IActionResult> ListarDependencias(Guid id, CancellationToken ct)
        => Ok(await _svc.ListarDependenciasAsync(id, ct));

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/dependencias")]
    public async Task<IActionResult> AgregarDependencia(Guid id, [FromBody] AgregarDependenciaRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarDependenciaAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Eliminar)]
    [HttpDelete("{id:guid}/dependencias/{dependenciaId:guid}")]
    public async Task<IActionResult> RemoverDependencia(Guid id, Guid dependenciaId, CancellationToken ct)
        => await _svc.RemoverDependenciaAsync(id, dependenciaId, ct) ? NoContent() : NotFound();

    // --- Bulk actions (Fase 2) ---
    // H-4: los lotes gatean con Editar (no Crear), igual que su equivalente individual (PUT {id}/estado
    // y el inline). Cambiar estado/prioridad/asignado de tarjetas existentes es editar, no crear.

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Editar)]
    [HttpPost("bulk/estado")]
    public async Task<IActionResult> BulkCambiarEstado([FromBody] BulkCambiarEstadoRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.BulkCambiarEstadoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Editar)]
    [HttpPost("bulk/prioridad")]
    public async Task<IActionResult> BulkCambiarPrioridad([FromBody] BulkCambiarPrioridadRequest req, CancellationToken ct)
        => Ok(await _svc.BulkCambiarPrioridadAsync(req, ct));

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Editar)]
    [HttpPost("bulk/asignado")]
    public async Task<IActionResult> BulkAsignarPersona([FromBody] BulkAsignarPersonaRequest req, CancellationToken ct)
    {
        // T-02: si el asignado no es de esta copropiedad, el servicio falla el lote ENTERO antes
        // de tocar ninguna tarea, y aqui se traduce a 400 legible en vez de 500.
        try { return Ok(await _svc.BulkAsignarPersonaAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DbUpdateException) { return BadRequest(new { error = "Hay un dato relacionado que no existe o no pertenece a esta copropiedad." }); }
    }

    // ----- Tableros de trabajo (2.10) -----
    [HttpGet("tableros")]
    public async Task<IActionResult> ListarTableros(CancellationToken ct) => Ok(await _svc.ListarTablerosAsync(ct));

    [HttpGet("tableros/{id:guid}")]
    public async Task<IActionResult> GetTablero(Guid id, CancellationToken ct)
    {
        var t = await _svc.GetTableroAsync(id, ct);
        return t is null ? NotFound() : Ok(t);
    }

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Aprobar)]
    [HttpPost("tableros")]
    public async Task<IActionResult> CrearTablero([FromBody] GuardarTableroRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearTableroAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Aprobar)]
    [HttpPut("tableros/{id:guid}")]
    public async Task<IActionResult> ActualizarTablero(Guid id, [FromBody] GuardarTableroRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarTableroAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Aprobar)]
    [HttpDelete("tableros/{id:guid}")]
    public async Task<IActionResult> EliminarTablero(Guid id, CancellationToken ct)
        => await _svc.EliminarTableroAsync(id, ct) ? NoContent() : NotFound();

    // Enlazar/desenlazar una persona a un tablero (2.5.D: desde el modulo Usuarios).
    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Aprobar)]
    [HttpPost("tableros/{id:guid}/usuarios/{personaId:guid}")]
    public async Task<IActionResult> AgregarUsuarioTablero(Guid id, Guid personaId, CancellationToken ct)
        => await _svc.AgregarUsuarioTableroAsync(id, personaId, ct) ? NoContent() : NotFound();

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Aprobar)]
    [HttpDelete("tableros/{id:guid}/usuarios/{personaId:guid}")]
    public async Task<IActionResult> QuitarUsuarioTablero(Guid id, Guid personaId, CancellationToken ct)
        => await _svc.QuitarUsuarioTableroAsync(id, personaId, ct) ? NoContent() : NotFound();

    // Invitar al tablero un USUARIO DEL SISTEMA por su correo EXACTO, aunque sea de otro cliente/tenant.
    // Lo agrega de inmediato (el tablero le queda compartido). No expone el directorio de otros clientes.
    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Aprobar)]
    [HttpPost("tableros/{id:guid}/usuarios/por-correo")]
    public async Task<IActionResult> AgregarUsuarioTableroPorCorreo(Guid id, [FromBody] AgregarPorCorreoBody body, CancellationToken ct)
    {
        var r = await _svc.AgregarUsuarioTableroPorCorreoAsync(id, body.Email ?? "", ct);
        return r.Ok ? Ok(r) : BadRequest(r);
    }

    public record AgregarPorCorreoBody(string Email);

    // Invitar a un externo (por email) a colaborar en el tablero: crea la persona si no existe,
    // genera el link de aceptacion y envia el correo. Devuelve la invitacion (con LinkAceptacion).
    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Aprobar)]
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
    public async Task<IActionResult> GetTableroBoard(Guid id, [FromQuery] bool verCerradas,
        [FromQuery] string? origenCodigo, [FromQuery] Guid? origenEntidadId, CancellationToken ct)
    {
        var b = await _svc.GetTableroBoardAsync(id, ct, verCerradas, origenCodigo, origenEntidadId);
        return b is null ? NotFound() : Ok(b);
    }

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Aprobar)]
    [HttpPost("tableros/{id:guid}/campos")]
    public async Task<IActionResult> AgregarCampo(Guid id, [FromBody] GuardarCampoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarCampoAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Aprobar)]
    [HttpPut("tableros/{id:guid}/campos/{campoId:guid}")]
    public async Task<IActionResult> ActualizarCampo(Guid id, Guid campoId, [FromBody] GuardarCampoRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarCampoAsync(id, campoId, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Aprobar)]
    [HttpDelete("tableros/{id:guid}/campos/{campoId:guid}")]
    public async Task<IActionResult> EliminarCampo(Guid id, Guid campoId, CancellationToken ct)
        => await _svc.EliminarCampoAsync(id, campoId, ct) ? NoContent() : NotFound();

    // Reordena un campo: direccion < 0 lo sube, direccion >= 0 lo baja (intercambia con el vecino).
    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Aprobar)]
    [HttpPut("tableros/{id:guid}/campos/{campoId:guid}/orden")]
    public async Task<IActionResult> ReordenarCampo(Guid id, Guid campoId, [FromQuery] int direccion, CancellationToken ct)
        => await _svc.ReordenarCampoAsync(id, campoId, direccion, ct) ? NoContent() : NotFound();

    // Archiva (archivar=true) o restaura (archivar=false) un campo, conservando sus valores.
    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Aprobar)]
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

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Editar)]
    [HttpPut("{id:guid}/progreso")]
    public async Task<IActionResult> ActualizarProgreso(Guid id, [FromBody] ActualizarProgresoRequest req, CancellationToken ct)
        => await _svc.ActualizarProgresoAsync(id, req.Progreso, ct) ? NoContent() : NotFound();

    // --- Eliminar tarjeta (soft-delete) ---
    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Eliminar)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
        => await _svc.EliminarTareaAsync(id, ct) ? NoContent() : NotFound();

    // --- Adjuntos de la tarjeta ---
    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/adjuntos")]
    [RequestSizeLimit(11_000_000)]
    public async Task<IActionResult> SubirAdjunto(Guid id, IFormFile file, [FromForm] string? texto, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId is null) return BadRequest(new { error = "no_active_tenant" });
        if (file is null || file.Length == 0) return BadRequest(new { error = "Archivo vacio." });
        if (file.Length > 10_000_000) return BadRequest(new { error = "Maximo 10 MB." });
        var ext = System.IO.Path.GetExtension(file.FileName);
        var key = $"tenants/{tenantId:N}/tareas/{id:N}/{Guid.NewGuid():N}{ext}";
        await using var stream = file.OpenReadStream();
        var url = Absolutizar(await _storage.UploadAsync(key, stream, file.ContentType ?? "application/octet-stream", ct));
        var dto = await _svc.AgregarAdjuntoAsync(id, file.FileName, url, texto, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [RequierePermiso(ModuloCodigo.Tareas, AccionPermiso.Eliminar)]
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
