using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Api.Authorization;
using Propia.Application.Asambleas;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

[ApiController]
[Route("api/asambleas")]

[Authorize]
public class AsambleaController : ControllerBase
{
    private readonly IAsambleaService _svc;
    private readonly IAsambleaConvocatoriaService _convocatoria;
    private readonly IAsambleaActaService _acta;
    private readonly Propia.Infrastructure.Storage.IBlobStorage _storage;
    private readonly ILogger<AsambleaController> _log;
    public AsambleaController(IAsambleaService svc, IAsambleaConvocatoriaService convocatoria, IAsambleaActaService acta,
        Propia.Infrastructure.Storage.IBlobStorage storage, ILogger<AsambleaController> log)
    {
        _svc = svc; _convocatoria = convocatoria; _acta = acta; _storage = storage; _log = log;
    }

    // Bandeja + ficha
    [HttpGet]
    public async Task<IActionResult> Bandeja([FromQuery] EstadoSesion? estado, [FromQuery] TipoSesion? tipo, CancellationToken ct)
        => Ok(await _svc.GetBandejaAsync(estado, tipo, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSesion(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetSesionAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    // Creacion + edicion
    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Crear)]
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearSesionRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearSesionAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] ActualizarSesionRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarSesionAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPost("{id:guid}/puntos")]
    public async Task<IActionResult> AgregarPunto(Guid id, [FromBody] CrearPuntoRequest req, CancellationToken ct)
    {
        try { return await _svc.AgregarPuntoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpDelete("puntos/{id:guid}")]
    public async Task<IActionResult> EliminarPunto(Guid id, CancellationToken ct)
    {
        try { return await _svc.EliminarPuntoAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPut("{id:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid id, [FromBody] CancelarSesionRequest req, CancellationToken ct)
    {
        try { return await _svc.CancelarSesionAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/segunda-convocatoria")]
    public async Task<IActionResult> SegundaConvocatoria(Guid id, CancellationToken ct)
    {
        try { return Ok(new { id = await _svc.ConvocarSegundaAsync(id, ct) }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPut("{id:guid}/instalar")]
    public async Task<IActionResult> Instalar(Guid id, [FromBody] InstalarSesionRequest req, CancellationToken ct)
    {
        try { return await _svc.InstalarAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPut("puntos/{id:guid}/iniciar")]
    public async Task<IActionResult> IniciarPunto(Guid id, CancellationToken ct)
    {
        try { return await _svc.IniciarPuntoAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPut("puntos/{id:guid}/tratado")]
    public async Task<IActionResult> MarcarTratado(Guid id, CancellationToken ct)
    {
        try { return await _svc.MarcarTratadoAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Transcripcion (ola 4): bitacora de voz + eventos del sistema
    [HttpGet("{id:guid}/transcripcion")]
    public async Task<IActionResult> Transcripcion(Guid id, [FromQuery] DateTimeOffset? desde, CancellationToken ct)
        => Ok(await _svc.GetTranscripcionAsync(id, desde, ct));

    // Sala virtual (ola 5): configuracion, acceso del moderador y grabacion
    [HttpGet("sala/config")]
    public IActionResult SalaConfig() => Ok(_svc.ConfigSala());

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPost("{id:guid}/sala/acceso")]
    public async Task<IActionResult> SalaAcceso(Guid id, CancellationToken ct)
    {
        try { return await _svc.AccesoSalaAdminAsync(id, ct) is { } a ? Ok(a) : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPut("{id:guid}/sala/grabacion")]
    public async Task<IActionResult> Grabacion(Guid id, [FromQuery] bool activa, CancellationToken ct)
    {
        try { return await (activa ? _svc.IniciarGrabacionAsync(id, ct) : _svc.DetenerGrabacionAsync(id, ct)) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (HttpRequestException ex) { _log.LogWarning(ex, "LiveKit no respondio (grabacion)"); return StatusCode(502, new { error = "El servidor de video no respondió. Intenta de nuevo." }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPut("{id:guid}/transcripcion/activa")]
    public async Task<IActionResult> ActivarTranscripcion(Guid id, [FromBody] ActivarTranscripcionRequest req, CancellationToken ct)
    {
        try { return await _svc.ActivarTranscripcionAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Linea de voz ("Mi voz a texto" del navegador o el proveedor de voz a texto).</summary>
    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPost("{id:guid}/transcripcion")]
    public async Task<IActionResult> AgregarTranscripcion(Guid id, [FromBody] AgregarTranscripcionRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.AgregarTranscripcionAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Crear)]
    [HttpPost("votaciones/{id:guid}/votar-bloque")]
    public async Task<IActionResult> VotarBloque(Guid id, [FromBody] VotarBloqueRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.VotarBloqueAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Eliminar)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
    {
        try { return await _svc.EliminarSesionAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPut("puntos/{id:guid}")]
    public async Task<IActionResult> ActualizarPunto(Guid id, [FromBody] ActualizarPuntoRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarPuntoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/documentos")]
    public async Task<IActionResult> AgregarDocumento(Guid id, [FromBody] AgregarDocumentoRequest req, CancellationToken ct)
        => Created("", await _svc.AgregarDocumentoAsync(id, req, ct));

    /// <summary>Sube un documento a la sesion (informe, cotizacion, poder escaneado...). Max 10 MB.</summary>
    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/documentos/archivo")]
    [RequestSizeLimit(11_000_000)]
    public async Task<IActionResult> SubirDocumento(Guid id, IFormFile file, [FromForm] TipoDocumentoSesion? tipo, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "Archivo vacio." });
        if (file.Length > 10_000_000) return BadRequest(new { error = "Maximo 10 MB." });
        var t = tipo ?? TipoDocumentoSesion.Otro;
        if (t is TipoDocumentoSesion.Convocatoria or TipoDocumentoSesion.Acta or TipoDocumentoSesion.Grabacion)
            return BadRequest(new { error = "Ese tipo de documento lo genera el sistema." });
        var tenant = Guid.TryParse(User.FindFirstValue("tenant_id"), out var tid) ? tid.ToString("N") : "sin-tenant";
        var ext = System.IO.Path.GetExtension(file.FileName);
        var key = $"tenants/{tenant}/asambleas/{id:N}/docs/{Guid.NewGuid():N}{ext}";
        try
        {
            await using var stream = file.OpenReadStream();
            var url = await _storage.UploadAsync(key, stream, file.ContentType, ct);
            var dto = await _svc.AgregarDocumentoAsync(id, new AgregarDocumentoRequest(null, file.FileName, null, url,
                file.ContentType, file.Length, VisibilidadDocumento.PreAsamblea, t), ct);
            return Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Eliminar)]
    [HttpDelete("documentos/{id:guid}")]
    public async Task<IActionResult> EliminarDocumento(Guid id, CancellationToken ct)
    {
        try { return await _svc.EliminarDocumentoAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Citacion
    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPut("{id:guid}/citar")]
    public async Task<IActionResult> Citar(Guid id, [FromBody] EnviarCitacionRequest req, CancellationToken ct)
    {
        try
        {
            if (!await _svc.EnviarCitacionAsync(id, req, ct)) return NotFound();
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("FUERA_DE_PLAZO:", StringComparison.Ordinal))
        {
            // Extraordinaria/consejo/comite dentro del plazo: el cliente pide confirmacion y reintenta.
            return Conflict(new { error = ex.Message["FUERA_DE_PLAZO:".Length..].Trim(), requiereConfirmacion = true });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }

        // La citacion ya quedo hecha; si la convocatoria en PDF falla, se avisa (se puede subir a mano en Documentos).
        try
        {
            var doc = await _convocatoria.GenerarAsync(id, ct);
            return Ok(new { convocatoria = doc is not null, aviso = (string?)null });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "No se pudo generar la convocatoria de la sesion {SesionId}", id);
            return Ok(new { convocatoria = false, aviso = "La sesion quedo citada, pero no se pudo generar la convocatoria en PDF. Puedes subirla en Documentos." });
        }
    }

    // Poderes
    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/poderes")]
    public async Task<IActionResult> OtorgarPoder(Guid id, [FromBody] OtorgarPoderRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.OtorgarPoderAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPut("poderes/{id:guid}/decidir")]
    public async Task<IActionResult> DecidirPoder(Guid id, [FromBody] DecidirPoderRequest req, CancellationToken ct)
    {
        try { return await _svc.DecidirPoderAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Sala
    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPut("{id:guid}/abrir-sala")]
    public async Task<IActionResult> AbrirSala(Guid id, CancellationToken ct)
    {
        try { return await _svc.AbrirSalaAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPut("{id:guid}/check-in")]
    public async Task<IActionResult> CheckIn(Guid id, [FromBody] CheckInParticipanteRequest req, CancellationToken ct)
    {
        try { return await _svc.CheckInParticipanteAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Enlace self-service (Ola 1) para que un copropietario/apoderado registre asistencia y vote sin
    // login. Solo se emite si la persona tiene unidades habilitadas en la sesion. Vence 2 dias despues
    // de la fecha de la sesion (o de hoy, si la sesion ya paso).
    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpGet("{id:guid}/enlace-residente/{personaId:guid}")]
    public async Task<IActionResult> EnlaceResidente(Guid id, Guid personaId,
        [FromServices] IAsambleaTokenService tokens, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("tenant_id"), out var tenantId)) return Forbid();
        var vista = await _svc.GetSesionParaResidenteAsync(id, personaId, ct);
        if (vista is null) return NotFound();
        if (vista.MisUnidades.Count == 0)
            return BadRequest(new { error = "La persona no es propietaria ni apoderada con poder aprobado en esta sesion." });

        var expira = AsambleaEnlace.Vencimiento(vista.FechaSesion, DateTimeOffset.UtcNow);
        var token = tokens.Emitir(new AsambleaEnlaceToken(tenantId, id, personaId), expira);
        return Ok(new EnlaceResidenteDto(token, AsambleaEnlace.Ruta(tenantId, token), expira));
    }

    // Via logueada (Ola 1.6): el copropietario/apoderado CON cuenta pide su propio enlace. Sin permiso
    // de admin: la persona sale del JWT (nunca del cliente) y aplica el mismo gate de propiedad. 403 si
    // no tiene unidades habilitadas en la sesion (la ficha lo usa para mostrar u ocultar el boton).
    [HttpGet("{id:guid}/mi-enlace")]
    public async Task<IActionResult> MiEnlace(Guid id, [FromServices] IAsambleaTokenService tokens, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("tenant_id"), out var tenantId)
            || !Guid.TryParse(User.FindFirstValue("persona_id"), out var personaId))
            return Forbid();
        var vista = await _svc.GetSesionParaResidenteAsync(id, personaId, ct);
        if (vista is null) return NotFound();
        if (vista.MisUnidades.Count == 0)
            return StatusCode(403, new { error = "No eres propietario ni apoderado con poder aprobado en esta asamblea." });

        var expira = AsambleaEnlace.Vencimiento(vista.FechaSesion, DateTimeOffset.UtcNow);
        var token = tokens.Emitir(new AsambleaEnlaceToken(tenantId, id, personaId), expira);
        return Ok(new EnlaceResidenteDto(token, AsambleaEnlace.Ruta(tenantId, token), expira));
    }

    // Envia por correo el enlace self-service a cada propietario/apoderado habilitado (o a los
    // PersonaIds indicados). Responde de inmediato; los correos salen en segundo plano. La base del
    // enlace es App:PublicBaseUrl (prod); si no esta configurada, el origen del Web que llama.
    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPost("{id:guid}/enviar-enlaces")]
    public async Task<IActionResult> EnviarEnlaces(Guid id, [FromBody] EnviarEnlacesRequest req,
        [FromServices] IAsambleaEnlacesService enlaces, [FromServices] IConfiguration config, CancellationToken ct)
    {
        var baseUrl = (config["App:PublicBaseUrl"] ?? "").Trim();
        if (string.IsNullOrEmpty(baseUrl)) baseUrl = (req.BaseUrl ?? "").Trim();
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var u) || (u.Scheme != Uri.UriSchemeHttps && u.Scheme != Uri.UriSchemeHttp))
            return BadRequest(new { error = "No se pudo determinar la direccion publica de la aplicacion." });

        try
        {
            var r = await enlaces.ProgramarEnvioAsync(id, req.PersonaIds, u.GetLeftPart(UriPartial.Authority), ct);
            return r is null ? NotFound() : Ok(r);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Estado del ultimo envio de enlaces (enviados / fallidos / motivos); 204 si no hubo envio.
    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpGet("{id:guid}/enviar-enlaces/estado")]
    public IActionResult EstadoEnvioEnlaces(Guid id, [FromServices] IAsambleaEnlacesService enlaces)
        => enlaces.EstadoEnvio(id) is { } e ? Ok(e) : NoContent();

    // Votaciones
    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/votaciones")]
    public async Task<IActionResult> AbrirVotacion(Guid id, [FromBody] AbrirVotacionRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AbrirVotacionAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Crear)]
    [HttpPost("votaciones/{id:guid}/votar")]
    public async Task<IActionResult> Votar(Guid id, [FromBody] EmitirVotoRequest req, CancellationToken ct)
    {
        try { return await _svc.EmitirVotoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Aprobar)]
    [HttpPut("votaciones/{id:guid}/cerrar")]
    public async Task<IActionResult> CerrarVotacion(Guid id, CancellationToken ct)
    {
        try { return Ok(await _svc.CerrarVotacionAsync(id, new CerrarVotacionRequest(), ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Cierre
    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Aprobar)]
    [HttpPut("{id:guid}/cerrar")]
    public async Task<IActionResult> CerrarSesion(Guid id, [FromBody] CerrarSesionRequest req, CancellationToken ct)
    {
        try
        {
            if (!await _svc.CerrarSesionAsync(id, req, ct)) return NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        // Con quorum: borrador base del acta (numero AAAA-NNN) y redaccion por IA en segundo plano.
        if (await _acta.PrepararAsync(id, ct) is Guid actaId) _acta.ProgramarRedaccion(actaId);
        return NoContent();
    }

    // Acta (ola 3): borrador por IA, edicion, doble firma, publicacion
    /// <summary>Genera (si falta) y redacta el acta de una sesion Cerrada; sirve tambien para actas viejas sin borrador.</summary>
    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/acta")]
    public async Task<IActionResult> GenerarActa(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GenerarActaAsync(id, ct);
        if (dto is null) return NotFound();
        if (dto.Estado == EstadoActa.Borrador && await _acta.PrepararAsync(id, ct) is Guid actaId) _acta.ProgramarRedaccion(actaId);
        return NoContent();
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPost("actas/{id:guid}/regenerar")]
    public async Task<IActionResult> RegenerarActa(Guid id, CancellationToken ct)
    {
        try
        {
            if (!await _acta.RegenerarAsync(id, ct)) return NotFound();
            _acta.ProgramarRedaccion(id);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPut("actas/{id:guid}/borrador")]
    public async Task<IActionResult> GuardarBorradorActa(Guid id, [FromBody] GuardarBorradorActaRequest req, CancellationToken ct)
    {
        try { return await _acta.GuardarBorradorAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Envia al presidente o al secretario el codigo para firmar (en Development vuelve en la respuesta).</summary>
    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPost("actas/{id:guid}/codigo-firma")]
    public async Task<IActionResult> CodigoFirma(Guid id, [FromBody] SolicitarCodigoFirmaRequest req, CancellationToken ct)
    {
        try { return Ok(await _acta.SolicitarCodigoAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Firma del presidente o del secretario: desde su cuenta (la persona del usuario es la designada; no requiere
    /// permiso del modulo) o con el codigo de su correo. Con las dos firmas el acta queda inmutable.</summary>
    [HttpPut("actas/{id:guid}/firmar")]
    public async Task<IActionResult> FirmarActa(Guid id, [FromBody] FirmarActaRolRequest req, CancellationToken ct)
    {
        try { return await _acta.FirmarAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Publica el acta firmada: PDF con membrete en Documentos + comunicado en Borrador para los propietarios.</summary>
    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Aprobar)]
    [HttpPut("actas/{id:guid}/publicar")]
    public async Task<IActionResult> PublicarActa(Guid id, CancellationToken ct)
    {
        try { return Ok(await _acta.PublicarAsync(id, $"{Request.Scheme}://{Request.Host}", ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Config
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig(CancellationToken ct) => Ok(await _svc.GetConfigAsync(ct));

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPut("config")]
    public async Task<IActionResult> ActualizarConfig([FromBody] ActualizarAsambleaConfigRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarConfigAsync(req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
