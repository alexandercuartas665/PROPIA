using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Api.Authorization;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Pqrsd;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;

namespace Propia.Api.Controllers;

/// <summary>Endpoints del modulo 2.9 PQRSD y Convivencia (spec v1.0).</summary>
[ApiController]
[Route("api/pqrsd")]

[Authorize]
public class PqrsdController : ControllerBase
{
    private readonly IPqrsdService _svc;
    private readonly PropiaDbContext _db;
    private readonly IBlobStorage _storage;
    private readonly IPqrsdRespuestaPdfService _respuestaPdf;
    private readonly Propia.Application.Documents.IHtmlToPdfService _htmlToPdf;
    private readonly Propia.Application.Common.IGmailSender _gmail;
    private readonly Propia.Application.InfraestructuraIa.IWhatsAppConnectorService _whatsapp;
    public PqrsdController(IPqrsdService svc, PropiaDbContext db, IBlobStorage storage, IPqrsdRespuestaPdfService respuestaPdf,
        Propia.Application.Documents.IHtmlToPdfService htmlToPdf, Propia.Application.Common.IGmailSender gmail,
        Propia.Application.InfraestructuraIa.IWhatsAppConnectorService whatsapp)
    { _svc = svc; _db = db; _storage = storage; _respuestaPdf = respuestaPdf; _htmlToPdf = htmlToPdf; _gmail = gmail; _whatsapp = whatsapp; }

    private Guid? GetTenantId()
    {
        var raw = User.FindFirstValue("tenant_id");
        return Guid.TryParse(raw, out var g) ? g : null;
    }

    // --- Catalogo ---
    [HttpGet("categorias")]
    public async Task<IActionResult> ListarCategorias(CancellationToken ct) => Ok(await _svc.ListarCategoriasAsync(ct));

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Crear)]
    [HttpPost("categorias")]
    public async Task<IActionResult> CrearCategoria([FromBody] CrearCategoriaRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearCategoriaAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("categorias/{id:guid}")]
    public async Task<IActionResult> ActualizarCategoria(Guid id, [FromBody] ActualizarCategoriaRequest req, CancellationToken ct)
        => await _svc.ActualizarCategoriaAsync(id, req, ct) ? NoContent() : NotFound();

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Eliminar)]
    [HttpDelete("categorias/{id:guid}")]
    public async Task<IActionResult> EliminarCategoria(Guid id, CancellationToken ct)
        => await _svc.EliminarCategoriaAsync(id, ct) ? NoContent() : NotFound();

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Crear)]
    [HttpPost("categorias/restablecer")]
    public async Task<IActionResult> RestablecerCategorias(CancellationToken ct)
        => Ok(new { restablecidas = await _svc.RestablecerCategoriasBaseAsync(ct) });

    [HttpGet("plazos")]
    public async Task<IActionResult> ListarPlazos(CancellationToken ct) => Ok(await _svc.ListarPlazosAsync(ct));

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("plazos/{tipo}")]
    public async Task<IActionResult> ActualizarPlazo(TipoPqrsd tipo, [FromBody] ActualizarPlazoRequest req, CancellationToken ct)
        => await _svc.ActualizarPlazoAsync(tipo, req, ct) ? NoContent() : NotFound();

    // --- Tipos configurables (catalogo editable por copropiedad) ---
    [HttpGet("tipos")]
    public async Task<IActionResult> ListarTipos([FromQuery] bool incluirInactivos, CancellationToken ct)
        => Ok(await _svc.ListarTiposAsync(incluirInactivos, ct));

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Crear)]
    [HttpPost("tipos")]
    public async Task<IActionResult> CrearTipo([FromBody] GuardarTipoPqrsdRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearTipoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("tipos/{id:guid}")]
    public async Task<IActionResult> ActualizarTipo(Guid id, [FromBody] GuardarTipoPqrsdRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarTipoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Eliminar)]
    [HttpDelete("tipos/{id:guid}")]
    public async Task<IActionResult> EliminarTipo(Guid id, CancellationToken ct)
    {
        try { return await _svc.EliminarTipoAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Bandeja + ficha ---
    [HttpGet("bandeja")]
    public async Task<IActionResult> Bandeja(
        [FromQuery] EstadoPqrsd? estado, [FromQuery] TipoPqrsd? tipo,
        [FromQuery] Guid? categoriaId, [FromQuery] string? q,
        [FromQuery] bool archivados, CancellationToken ct)
        => Ok(await _svc.GetBandejaAsync(estado, tipo, categoriaId, q, archivados, ct));

    // --- Tablero: columnas (estados) configurables ---
    [HttpGet("estados")]
    public async Task<IActionResult> ListarEstados(CancellationToken ct) => Ok(await _svc.ListarEstadosAsync(ct));

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Crear)]
    [HttpPost("estados")]
    public async Task<IActionResult> CrearEstado([FromBody] CrearEstadoPqrsdRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearEstadoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("estados/{id:guid}")]
    public async Task<IActionResult> ActualizarEstado(Guid id, [FromBody] ActualizarEstadoPqrsdRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarEstadoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Eliminar)]
    [HttpDelete("estados/{id:guid}")]
    public async Task<IActionResult> EliminarEstado(Guid id, CancellationToken ct)
    {
        try { return await _svc.EliminarEstadoAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("estados/{id:guid}/orden")]
    public async Task<IActionResult> ReordenarEstado(Guid id, [FromQuery] string direccion, CancellationToken ct)
        => await _svc.ReordenarEstadoAsync(id, direccion, ct) ? NoContent() : NotFound();

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("{id:guid}/mover")]
    public async Task<IActionResult> MoverAEstado(Guid id, [FromBody] MoverExpedienteEstadoRequest req, CancellationToken ct)
    {
        try { return await _svc.MoverAEstadoAsync(id, req.EstadoId, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Tablero: campos dinamicos ---
    [HttpGet("campos")]
    public async Task<IActionResult> ListarCampos(CancellationToken ct) => Ok(await _svc.ListarCamposAsync(ct));

    [HttpGet("campos-archivados")]
    public async Task<IActionResult> ListarCamposArchivados(CancellationToken ct) => Ok(await _svc.ListarCamposArchivadosAsync(ct));

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Crear)]
    [HttpPost("campos")]
    public async Task<IActionResult> CrearCampo([FromBody] GuardarCampoPqrsdRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearCampoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("campos/{id:guid}")]
    public async Task<IActionResult> ActualizarCampo(Guid id, [FromBody] GuardarCampoPqrsdRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarCampoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Eliminar)]
    [HttpDelete("campos/{id:guid}")]
    public async Task<IActionResult> EliminarCampo(Guid id, CancellationToken ct)
        => await _svc.EliminarCampoAsync(id, ct) ? NoContent() : NotFound();

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("campos/{id:guid}/archivar")]
    public async Task<IActionResult> ArchivarCampo(Guid id, [FromQuery] bool archivar, CancellationToken ct)
    {
        try { return await _svc.SetCampoActivoAsync(id, !archivar, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("campos/{id:guid}/orden")]
    public async Task<IActionResult> ReordenarCampo(Guid id, [FromQuery] string direccion, CancellationToken ct)
        => await _svc.ReordenarCampoAsync(id, direccion, ct) ? NoContent() : NotFound();

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("campos/{id:guid}/publico")]
    public async Task<IActionResult> SetCampoPublico(Guid id, [FromQuery] bool mostrar, CancellationToken ct)
        => await _svc.SetCampoPublicoAsync(id, mostrar, ct) ? NoContent() : NotFound();

    // --- Config del formulario publico (campos opcionales visibles) ---
    [HttpGet("formulario-config")]
    public async Task<IActionResult> GetFormularioConfig(CancellationToken ct) => Ok(await _svc.GetFormularioPublicoConfigAsync(ct));

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("formulario-config")]
    public async Task<IActionResult> GuardarFormularioConfig([FromBody] PqrsdFormularioPublicoConfigDto req, CancellationToken ct)
    {
        try { return await _svc.GuardarFormularioPublicoConfigAsync(req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Config: envio de respuestas por WhatsApp (plantilla aprobada + linea) ---
    [HttpGet("config/whatsapp")]
    public async Task<IActionResult> GetWhatsAppCfg(CancellationToken ct) => Ok(await _svc.GetWhatsAppConfigAsync(ct));

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("config/whatsapp")]
    public async Task<IActionResult> GuardarWhatsAppCfg([FromBody] PqrsdWhatsAppConfigDto req, CancellationToken ct)
    {
        try { return await _svc.GuardarWhatsAppConfigAsync(req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Lineas WhatsApp conectadas de la copropiedad (para elegir desde donde enviar).
    [HttpGet("config/whatsapp/lineas")]
    public async Task<IActionResult> GetWhatsAppLineas(CancellationToken ct)
        => Ok(await _db.WhatsAppLines.AsNoTracking()
            .OrderBy(l => l.InstanceName)
            .Select(l => new { id = l.Id, nombre = l.InstanceName, telefono = l.PhoneNumber, conectada = l.Status == Propia.Domain.Enums.WhatsAppLineStatus.Connected })
            .ToListAsync(ct));

    // --- Config: consecutivos de radicado (expediente y respuesta) ---
    [HttpGet("config/consecutivos")]
    public async Task<IActionResult> GetConsecutivos(CancellationToken ct) => Ok(await _svc.GetConsecutivoConfigAsync(ct));

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("config/consecutivos")]
    public async Task<IActionResult> GuardarConsecutivos([FromBody] PqrsdConsecutivoConfigDto req, CancellationToken ct)
    {
        try { return await _svc.GuardarConsecutivoConfigAsync(req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetExpediente(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetExpedienteAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    // --- Tareas enlazadas al PQR (tablero "PQRSD") ---
    [HttpGet("{id:guid}/tareas")]
    public async Task<IActionResult> ListTareasDePqr(Guid id, CancellationToken ct) => Ok(await _svc.ListTareasDePqrAsync(id, ct));
    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/tareas")]
    public async Task<IActionResult> CrearTareaDePqr(Guid id, [FromBody] CrearPqrTareaRequest req, CancellationToken ct)
    {
        var tareaId = await _svc.CrearTareaDePqrAsync(id, req, ct);
        return tareaId is null ? BadRequest(new { error = "No se pudo crear la tarea." }) : Ok(new { id = tareaId });
    }

    // --- Config: tablero destino de las tareas creadas desde PQRSD ---
    [HttpGet("config/tablero-tareas")]
    public async Task<IActionResult> ObtenerTableroTareas(CancellationToken ct)
        => Ok(new { tableroId = await _svc.ObtenerTableroTareasConfigAsync(ct) });

    // Id del tablero PQRSD garantizado (crea el default si hace falta). Lo usa el componente TableroTareas
    // embebido en el modal para cargar las tareas del expediente.
    [HttpGet("tablero-tareas-id")]
    public async Task<IActionResult> ObtenerTableroPqrsdId(CancellationToken ct)
        => Ok(new { tableroId = await _svc.ObtenerTableroPqrsdIdAsync(ct) });
    public record GuardarTableroTareasRequest(Guid? TableroId);
    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("config/tablero-tareas")]
    public async Task<IActionResult> GuardarTableroTareas([FromBody] GuardarTableroTareasRequest req, CancellationToken ct)
    {
        try { await _svc.GuardarTableroTareasConfigAsync(req.TableroId, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Radicacion ---
    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Crear)]
    [HttpPost]
    public async Task<IActionResult> Radicar([FromBody] RadicarPqrsdRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.RadicarAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Vista del residente ---
    [HttpGet("mis-pqrsd")]
    public async Task<IActionResult> MisPqrsd(CancellationToken ct) => Ok(await _svc.ListarMisPqrsdAsync(ct));

    // --- Ciclo de gestion ---
    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("{id:guid}/tomar")]
    public async Task<IActionResult> Tomar(Guid id, [FromBody] TomarExpedienteRequest req, CancellationToken ct)
        => await _svc.TomarExpedienteAsync(id, req, ct) ? NoContent() : NotFound();

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("{id:guid}/responder")]
    public async Task<IActionResult> Responder(Guid id, [FromBody] ResponderExpedienteRequest req, CancellationToken ct)
    {
        try
        {
            var ok = await _svc.ResponderAsync(id, req, ct);
            if (!ok) return NotFound();
            // Genera el PDF de la respuesta y lo adjunta (compartido por defecto: es la respuesta oficial).
            await GenerarAdjuntoRespuestaPdfAsync(id, req.Texto, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Genera el PDF de la respuesta, lo sube al blob y crea el PqrsdAdjunto marcado como compartido.
    // No aborta la respuesta si el PDF falla (best-effort): la respuesta legal ya quedo guardada.
    private async Task GenerarAdjuntoRespuestaPdfAsync(Guid id, string texto, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId is null) return;
        try
        {
            var pdf = await _respuestaPdf.GenerarRespuestaPdfAsync(id, texto, ct);
            if (pdf is null) return;

            var key = $"tenants/{tenantId:N}/pqrsd/{id:N}/{Guid.NewGuid():N}.pdf";
            await using var stream = new System.IO.MemoryStream(pdf.Value.Pdf);
            var url = await _storage.UploadAsync(key, stream, "application/pdf", ct);

            var uid = Guid.TryParse(User.FindFirstValue("user_id"), out var u) ? u : Guid.Empty;
            _db.PqrsdAdjuntos.Add(new PqrsdAdjunto
            {
                ExpedienteId = id,
                NombreArchivo = pdf.Value.FileName,
                TipoMime = "application/pdf",
                TamanioBytes = pdf.Value.Pdf.LongLength,
                UrlStorage = url,
                SubidoPorUsuarioId = uid,
                Texto = "Respuesta oficial (PDF generado automaticamente)",
                Compartido = true
            });
            await _db.SaveChangesAsync(ct);
        }
        catch { /* best-effort: el PDF es un complemento, no debe tumbar la respuesta */ }
    }

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("{id:guid}/inconformidad")]
    public async Task<IActionResult> Inconformidad(Guid id, [FromBody] ManifestarInconformidadRequest req, CancellationToken ct)
    {
        try { return await _svc.ManifestarInconformidadAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Aprobar)]
    [HttpPut("{id:guid}/cerrar")]
    public async Task<IActionResult> Cerrar(Guid id, [FromBody] CerrarDefinitivoRequest req, CancellationToken ct)
    {
        try { return await _svc.CerrarDefinitivoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Tutela ---
    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("{id:guid}/tutela")]
    public async Task<IActionResult> ActivarTutela(Guid id, [FromBody] ActivarTutelaRequest req, CancellationToken ct)
    {
        try { return await _svc.ActivarTutelaAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Prorroga (ampliacion de plazo con motivo, queda en la traza) ---
    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("{id:guid}/prorroga")]
    public async Task<IActionResult> Prorrogar(Guid id, [FromBody] AmpliarPlazoRequest req, CancellationToken ct)
    {
        try { return await _svc.AmpliarPlazoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Archivar / actualizar expediente ---
    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("{id:guid}/archivar")]
    public async Task<IActionResult> Archivar(Guid id, [FromQuery] bool archivar, CancellationToken ct)
        => await _svc.ArchivarExpedienteAsync(id, archivar, ct) ? NoContent() : NotFound();

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("{id:guid}/actualizar")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] ActualizarExpedienteRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarExpedienteAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/actividad")]
    public async Task<IActionResult> ReportarActividad(Guid id, [FromBody] ReportarActividadPqrsdRequest req, CancellationToken ct)
    {
        try { var dto = await _svc.ReportarActividadAsync(id, req, ct); return dto is null ? NotFound() : Created("", dto); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/generar-tarea")]
    public async Task<IActionResult> GenerarTarea(Guid id, CancellationToken ct)
    {
        try { var tid = await _svc.GenerarTareaAsync(id, ct); return tid is null ? NotFound() : Ok(new { tareaId = tid }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Comite ---
    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/comite")]
    public async Task<IActionResult> EscalarAComite(Guid id, [FromBody] EscalarAComiteRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.EscalarAComiteAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("comite/{sesionId:guid}/registrar")]
    public async Task<IActionResult> RegistrarSesionComite(Guid sesionId, [FromBody] RegistrarSesionComiteRequest req, CancellationToken ct)
    {
        try { return await _svc.RegistrarSesionComiteAsync(sesionId, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Contexto humano del expediente: unidad asignada + propietario/residente/etc con contacto ---
    [HttpGet("{id:guid}/contexto")]
    public async Task<IActionResult> GetContexto(Guid id, CancellationToken ct)
    {
        var exp = await _db.PqrsdExpedientes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (exp is null) return NotFound();

        // Buscar UnidadPersona donde PersonaId = RadicadorPersonaId. Tomo la primera asociacion como "unidad del expediente".
        var unidadPersona = await (from up in _db.UnidadPersonas.AsNoTracking()
                                   join u in _db.UnidadesPrivadas.AsNoTracking() on up.UnidadId equals u.Id
                                   where up.PersonaId == exp.RadicadorPersonaId
                                   select new { up.UnidadId, u.Numero, u.TorreId, u.Piso, u.Tipo, u.CoeficientePropiedad })
                                  .FirstOrDefaultAsync(ct);

        PqrsdContextoUnidadDto unidadDto;
        IReadOnlyList<PqrsdContextoPersonaDto> personasDto = new List<PqrsdContextoPersonaDto>();

        if (unidadPersona is not null)
        {
            var torreNombre = unidadPersona.TorreId.HasValue
                ? await _db.Torres.AsNoTracking().Where(t => t.Id == unidadPersona.TorreId).Select(t => t.Nombre).FirstOrDefaultAsync(ct)
                : null;
            unidadDto = new PqrsdContextoUnidadDto(
                unidadPersona.UnidadId, unidadPersona.Numero, torreNombre,
                unidadPersona.Piso, unidadPersona.Tipo.ToString(), unidadPersona.CoeficientePropiedad);

            // Todas las personas de esa unidad con sus datos
            personasDto = await (from up in _db.UnidadPersonas.AsNoTracking()
                                 join p in _db.Personas.AsNoTracking() on up.PersonaId equals p.Id
                                 where up.UnidadId == unidadPersona.UnidadId
                                 select new PqrsdContextoPersonaDto(
                                     p.Id,
                                     (p.Nombres + " " + p.Apellidos).Trim(),
                                     p.Documento, p.Email, p.Telefono,
                                     up.Rol.ToString(),
                                     p.Id == exp.RadicadorPersonaId))
                                .ToListAsync(ct);
        }
        else
        {
            unidadDto = new PqrsdContextoUnidadDto(null, null, null, null, null, null);
            // Aun sin unidad asociada, devolver al menos el radicador
            var rad = await _db.Personas.AsNoTracking().FirstOrDefaultAsync(p => p.Id == exp.RadicadorPersonaId, ct);
            if (rad is not null)
            {
                personasDto = new List<PqrsdContextoPersonaDto>
                {
                    new(rad.Id, (rad.Nombres + " " + rad.Apellidos).Trim(),
                        rad.Documento, rad.Email, rad.Telefono, "Radicador", true)
                };
            }
        }

        return Ok(new PqrsdContextoDto(unidadDto, personasDto));
    }

    // --- Agregar adjunto despues de la radicacion (admin adjunta evidencias/documentos) ---
    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/adjuntos")]
    public async Task<IActionResult> AgregarAdjunto(Guid id, [FromBody] AgregarAdjuntoPqrsdRequest req, CancellationToken ct)
    {
        var exp = await _db.PqrsdExpedientes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (exp is null) return NotFound();
        if (string.IsNullOrWhiteSpace(req.NombreArchivo)) return BadRequest(new { error = "NombreArchivo requerido." });
        var uid2 = Guid.TryParse(User.FindFirstValue("user_id"), out var u2) ? u2 : Guid.Empty;
        var adj = new PqrsdAdjunto
        {
            ExpedienteId = id,
            NombreArchivo = req.NombreArchivo.Trim(),
            TipoMime = req.TipoMime,
            TamanioBytes = req.TamanioBytes,
            UrlStorage = req.UrlStorage,
            SubidoPorUsuarioId = uid2
        };
        _db.PqrsdAdjuntos.Add(adj);
        await _db.SaveChangesAsync(ct);
        var nombre2 = User.FindFirstValue("name") ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.Name) ?? User.FindFirstValue("email");
        return Created("", new PqrsdAdjuntoDto(adj.Id, adj.NombreArchivo, adj.TipoMime, adj.TamanioBytes, adj.UrlStorage, adj.CreatedAt,
            nombre2, uid2 == Guid.Empty ? null : uid2, adj.Texto));
    }

    // --- Subir el binario de un adjunto (documentos e imagenes) y registrarlo ---
    // Acepta un caption opcional 'texto' para el chat de actividad (burbuja con imagen/archivo + texto).
    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/adjuntos/upload")]
    [RequestSizeLimit(52_500_000)]
    public async Task<IActionResult> SubirAdjuntoBinario(Guid id, IFormFile file, [FromForm] string? texto, [FromForm] Guid? respuestaId, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId is null) return BadRequest(new { error = "no_active_tenant" });
        var exp = await _db.PqrsdExpedientes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (exp is null) return NotFound();
        if (file is null || file.Length == 0) return BadRequest(new { error = "Archivo vacio." });
        if (file.Length > 52_428_800) return BadRequest(new { error = "Maximo 50 MB." });

        var ext = System.IO.Path.GetExtension(file.FileName);
        var key = $"tenants/{tenantId:N}/pqrsd/{id:N}/{Guid.NewGuid():N}{ext}";
        await using var stream = file.OpenReadStream();
        // URL relativa al mismo origen (convencion host unificado: nunca absolutizar).
        var url = await _storage.UploadAsync(key, stream, file.ContentType ?? "application/octet-stream", ct);

        var uid = Guid.TryParse(User.FindFirstValue("user_id"), out var u) ? u : Guid.Empty;
        var adj = new PqrsdAdjunto
        {
            ExpedienteId = id,
            NombreArchivo = file.FileName,
            TipoMime = file.ContentType ?? "application/octet-stream",
            TamanioBytes = file.Length,
            UrlStorage = url,
            SubidoPorUsuarioId = uid,
            Texto = string.IsNullOrWhiteSpace(texto) ? null : texto.Trim(),
            RespuestaId = respuestaId
        };
        _db.PqrsdAdjuntos.Add(adj);
        await _db.SaveChangesAsync(ct);
        await _svc.NotificarMencionComentarioAsync(id, texto, ct);

        var nombre = User.FindFirstValue("name") ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.Name) ?? User.FindFirstValue("email");
        return Created("", new PqrsdAdjuntoDto(adj.Id, adj.NombreArchivo, adj.TipoMime, adj.TamanioBytes, adj.UrlStorage, adj.CreatedAt,
            nombre, uid == Guid.Empty ? null : uid, adj.Texto));
    }

    // --- Eliminar un adjunto ---
    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Eliminar)]
    [HttpDelete("{id:guid}/adjuntos/{adjuntoId:guid}")]
    public async Task<IActionResult> EliminarAdjunto(Guid id, Guid adjuntoId, CancellationToken ct)
    {
        var adj = await _db.PqrsdAdjuntos.FirstOrDefaultAsync(a => a.Id == adjuntoId && a.ExpedienteId == id, ct);
        if (adj is null) return NotFound();
        _db.PqrsdAdjuntos.Remove(adj);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // --- Marcar/desmarcar un adjunto como compartido en el link publico de seguimiento ---
    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("{id:guid}/adjuntos/{adjuntoId:guid}/compartir")]
    public async Task<IActionResult> CompartirAdjunto(Guid id, Guid adjuntoId, [FromBody] CompartirAdjuntoRequest req, CancellationToken ct)
        => await _svc.SetAdjuntoCompartidoAsync(id, adjuntoId, req.Compartido, ct) ? NoContent() : NotFound();

    // --- Obtener/crear el token del link publico de seguimiento del expediente ---
    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/compartir")]
    public async Task<IActionResult> CompartirSeguimiento(Guid id, CancellationToken ct)
    {
        var token = await _svc.ObtenerOCrearShareTokenAsync(id, ct);
        return token is null ? NotFound() : Ok(new PqrsdShareLinkDto(token.Value));
    }

    // --- Respuestas tipo correo (borradores con editor enriquecido) ---
    [HttpGet("{id:guid}/respuestas")]
    public async Task<IActionResult> ListarRespuestas(Guid id, CancellationToken ct)
        => Ok(await _svc.ListarRespuestasAsync(id, ct));

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/respuestas")]
    public async Task<IActionResult> CrearRespuesta(Guid id, [FromBody] CrearRespuestaBorradorRequest req, CancellationToken ct)
    {
        var dto = await _svc.CrearRespuestaBorradorAsync(id, req, ct);
        return dto is null ? NotFound() : Created("", dto);
    }

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("{id:guid}/respuestas/{respuestaId:guid}")]
    public async Task<IActionResult> ActualizarRespuesta(Guid id, Guid respuestaId, [FromBody] CrearRespuestaBorradorRequest req, CancellationToken ct)
    {
        var dto = await _svc.ActualizarRespuestaBorradorAsync(id, respuestaId, req, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    // Archiva/desarchiva una respuesta.
    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("{id:guid}/respuestas/{respuestaId:guid}/archivar")]
    public async Task<IActionResult> ArchivarRespuesta(Guid id, Guid respuestaId, [FromBody] ArchivarRespuestaRequest req, CancellationToken ct)
        => await _svc.ArchivarRespuestaAsync(id, respuestaId, req.Archivar, ct) ? NoContent() : NotFound();

    // Historial de versiones del documento de una respuesta.
    [HttpGet("{id:guid}/respuestas/{respuestaId:guid}/versiones")]
    public async Task<IActionResult> ListarVersionesRespuesta(Guid id, Guid respuestaId, CancellationToken ct)
        => Ok(await _svc.ListarVersionesRespuestaAsync(id, respuestaId, ct));

    // Vista previa del documento oficial (membrete + cuerpo) como HTML. El mismo HTML se renderiza luego a PDF.
    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/respuestas/documento-preview")]
    public async Task<IActionResult> PreviewDocumento(Guid id, [FromBody] CrearRespuestaBorradorRequest req, CancellationToken ct)
    {
        var html = await _svc.ComponerDocumentoRespuestaAsync(id, req.CuerpoHtml ?? "", ct);
        return html is null ? NotFound() : Ok(new { html });
    }

    // Genera el PDF oficial (membrete + cuerpo) de una respuesta con Chromium y lo adjunta (compartido).
    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/respuestas/{respuestaId:guid}/pdf")]
    public async Task<IActionResult> GenerarPdfRespuesta(Guid id, Guid respuestaId, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId is null) return Unauthorized();

        var r = await _db.PqrsdRespuestas.FirstOrDefaultAsync(x => x.Id == respuestaId && x.ExpedienteId == id, ct);
        if (r is null) return NotFound();

        var html = await _svc.ComponerDocumentoRespuestaAsync(id, r.CuerpoHtml, ct);
        if (html is null) return NotFound();

        byte[] pdf;
        try { pdf = await _htmlToPdf.RenderAsync(html, ct); }
        catch (Exception) { return StatusCode(500, new { error = "No se pudo generar el PDF." }); }

        var key = $"tenants/{tenantId:N}/pqrsd/{id:N}/{Guid.NewGuid():N}.pdf";
        await using var stream = new System.IO.MemoryStream(pdf);
        var url = await _storage.UploadAsync(key, stream, "application/pdf", ct);

        var uid = Guid.TryParse(User.FindFirstValue("user_id"), out var u) ? u : Guid.Empty;
        var nombre = $"documento-oficial-{respuestaId:N}.pdf";
        _db.PqrsdAdjuntos.Add(new PqrsdAdjunto
        {
            ExpedienteId = id,
            RespuestaId = respuestaId,
            NombreArchivo = nombre,
            TipoMime = "application/pdf",
            TamanioBytes = pdf.LongLength,
            UrlStorage = url,
            SubidoPorUsuarioId = uid,
            Texto = "Documento oficial (PDF con membrete)",
            Compartido = true
        });
        await _db.SaveChangesAsync(ct);
        return Ok(new { url, nombre });
    }

    public record EnviarRespuestaRequest(string? SeguimientoBaseUrl);

    // Envia la respuesta a sus destinatarios segun el CANAL de cada uno:
    //  - Correo: compone el documento, genera el PDF (Chromium), lo adjunta al correo (Gmail) y al expediente.
    //  - WhatsApp: envia la plantilla aprobada de Meta con el LINK de seguimiento como variable (o texto en Evolution).
    // Marca la respuesta como Enviada si al menos un canal tuvo exito.
    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/respuestas/{respuestaId:guid}/enviar")]
    public async Task<IActionResult> EnviarRespuesta(Guid id, Guid respuestaId, [FromBody] EnviarRespuestaRequest? req, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId is null) return Unauthorized();

        var r = await _db.PqrsdRespuestas.Include(x => x.Destinatarios)
            .FirstOrDefaultAsync(x => x.Id == respuestaId && x.ExpedienteId == id, ct);
        if (r is null) return NotFound();

        var emails = r.Destinatarios.Where(d => d.EnviarCorreo && !string.IsNullOrWhiteSpace(d.Email))
            .Select(d => d.Email).Distinct().ToList();
        var wapp = r.Destinatarios.Where(d => d.EnviarWhatsApp && !string.IsNullOrWhiteSpace(d.Telefono)).ToList();
        if (emails.Count == 0 && wapp.Count == 0)
            return BadRequest(new { error = "Agrega al menos un destinatario (correo o WhatsApp)." });

        var exp = await _db.PqrsdExpedientes.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
        var radicado = exp?.NumeroRadicado ?? "";
        var uid = Guid.TryParse(User.FindFirstValue("user_id"), out var u) ? u : Guid.Empty;

        var errores = new List<string>();
        int enviadosCorreo = 0, enviadosWa = 0;

        // ----- Canal correo (Gmail + PDF) -----
        if (emails.Count > 0)
        {
            var html = await _svc.ComponerDocumentoRespuestaAsync(id, r.CuerpoHtml, ct);
            if (html is null) return NotFound();
            byte[] pdf;
            try { pdf = await _htmlToPdf.RenderAsync(html, ct); }
            catch (Exception) { return StatusCode(500, new { error = "No se pudo generar el PDF." }); }

            var asunto = string.IsNullOrWhiteSpace(r.Asunto) ? $"Respuesta a su PQRSD {radicado}".Trim() : r.Asunto!;
            var cuerpo = "<html><body style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#1B2A3A;line-height:1.6\">"
                         + r.CuerpoHtml
                         + "<hr style=\"border:0;border-top:1px solid #E4E9EF;margin:18px 0\">"
                         + "<p style=\"font-size:12px;color:#63748A\">Se adjunta el documento oficial en PDF.</p>"
                         + "</body></html>";
            var adjPdf = new Propia.Application.Common.CorreoAdjunto($"respuesta-{radicado}.pdf", "application/pdf", pdf);

            var envio = await _gmail.SendAsync(tenantId.Value, emails, asunto, cuerpo, new[] { adjPdf }, ct);
            if (envio.Success)
            {
                enviadosCorreo = emails.Count;
                var key = $"tenants/{tenantId:N}/pqrsd/{id:N}/{Guid.NewGuid():N}.pdf";
                await using var stream = new System.IO.MemoryStream(pdf);
                var url = await _storage.UploadAsync(key, stream, "application/pdf", ct);
                _db.PqrsdAdjuntos.Add(new PqrsdAdjunto
                {
                    ExpedienteId = id, RespuestaId = respuestaId,
                    NombreArchivo = $"respuesta-{radicado}.pdf", TipoMime = "application/pdf",
                    TamanioBytes = pdf.LongLength, UrlStorage = url, SubidoPorUsuarioId = uid,
                    Texto = "Respuesta enviada por correo (PDF)", Compartido = true
                });
            }
            else errores.Add("Correo: " + (envio.Error ?? "no se pudo enviar."));
        }

        // ----- Canal WhatsApp (plantilla aprobada con el link de seguimiento) -----
        // Todo el bloque es tolerante a fallos: un problema de WhatsApp NO debe tumbar el envio por correo.
        if (wapp.Count > 0)
        {
            try
            {
                var waCfg = await _svc.GetWhatsAppConfigAsync(ct);
                var lineId = waCfg.LineaId ?? await _db.WhatsAppLines.AsNoTracking()
                    .Where(l => l.Status == Propia.Domain.Enums.WhatsAppLineStatus.Connected)
                    .Select(l => (Guid?)l.Id).FirstOrDefaultAsync(ct);
                if (lineId is null)
                    errores.Add("WhatsApp: no hay una linea de WhatsApp conectada (o configurala en Configurar PQRSD > WhatsApp).");
                else
                {
                    // Link publico de seguimiento (la Web pasa su base URL; fallback a la del request).
                    var token = await _svc.ObtenerOCrearShareTokenAsync(id, ct);
                    var baseUrl = (req?.SeguimientoBaseUrl ?? $"{Request.Scheme}://{Request.Host}").TrimEnd('/');
                    var link = token is null ? baseUrl : $"{baseUrl}/pqr-seguimiento/{tenantId}/{token}";
                    var fallback = $"Respuesta a su PQRSD {radicado}. Consulte el detalle aqui: {link}";

                    foreach (var d in wapp)
                    {
                        try
                        {
                            var res = await _whatsapp.SendTemplateAsync(lineId.Value, d.Telefono!, waCfg.PlantillaNombre ?? "",
                                waCfg.PlantillaIdioma, new[] { link }, fallback, ct);
                            if (res.Ok) enviadosWa++;
                            else errores.Add($"WhatsApp ({d.Telefono}): {res.Error ?? "no se pudo enviar."}");
                        }
                        catch (Exception ex) { errores.Add($"WhatsApp ({d.Telefono}): {ex.Message}"); }
                    }
                }
            }
            catch (Exception ex) { errores.Add("WhatsApp: " + ex.Message); }
        }

        if (enviadosCorreo == 0 && enviadosWa == 0)
            return BadRequest(new { error = string.Join(" | ", errores.DefaultIfEmpty("No se pudo enviar la respuesta.")) });

        r.Enviada = true;
        r.EnviadaAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new { enviadosCorreo, enviadosWhatsapp = enviadosWa, errores });
    }

    // --- Plantillas de respuesta (combinacion de correspondencia) ---
    [HttpGet("plantillas/tokens")]
    public IActionResult ListarTokensPlantilla() => Ok(_svc.ListarTokensPlantilla());

    [HttpGet("plantillas")]
    public async Task<IActionResult> ListarPlantillas(CancellationToken ct) => Ok(await _svc.ListarPlantillasAsync(ct));

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Crear)]
    [HttpPost("plantillas")]
    public async Task<IActionResult> CrearPlantilla([FromBody] GuardarPlantillaRequest req, CancellationToken ct)
        => Ok(await _svc.CrearPlantillaAsync(req, ct));

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut("plantillas/{plantillaId:guid}")]
    public async Task<IActionResult> ActualizarPlantilla(Guid plantillaId, [FromBody] GuardarPlantillaRequest req, CancellationToken ct)
        => await _svc.ActualizarPlantillaAsync(plantillaId, req, ct) ? NoContent() : NotFound();

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Eliminar)]
    [HttpDelete("plantillas/{plantillaId:guid}")]
    public async Task<IActionResult> EliminarPlantilla(Guid plantillaId, CancellationToken ct)
        => await _svc.EliminarPlantillaAsync(plantillaId, ct) ? NoContent() : NotFound();

    // Devuelve el cuerpo de la plantilla con los tokens ya reemplazados por los datos del expediente.
    [HttpGet("{id:guid}/plantillas/{plantillaId:guid}/resuelta")]
    public async Task<IActionResult> ResolverPlantilla(Guid id, Guid plantillaId, CancellationToken ct)
    {
        var html = await _svc.ResolverPlantillaAsync(id, plantillaId, ct);
        return html is null ? NotFound() : Ok(new { html });
    }
}

/// <summary>Body para alternar el flag de adjunto compartido.</summary>
public record CompartirAdjuntoRequest(bool Compartido);
