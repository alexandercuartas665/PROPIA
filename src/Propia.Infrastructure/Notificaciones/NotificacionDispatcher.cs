using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Propia.Application.Common;
using Propia.Application.Notificaciones;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Notificaciones;

/// <summary>
/// Implementacion T.2 del dispatcher de notificaciones (MVP).
///
/// Provider selector: `Notificaciones:Provider` en config.
///  - "Stub" (default): persiste y marca Enviado inmediato. Reemplaza los 9
///    simulados dispersos en los modulos por un punto unico.
///  - "Sendgrid" / "WhatsAppCloud": Fase 2, hoy fallback a Stub con warning.
///
/// El dispatcher persiste la Notificacion ANTES de invocar el adapter, para
/// garantizar trazabilidad incluso si el provider falla.
/// </summary>
public class NotificacionDispatcher : INotificacionDispatcher
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _http;
    private readonly IConfiguration _config;
    private readonly ILogger<NotificacionDispatcher> _log;

    public NotificacionDispatcher(
        PropiaDbContext db,
        ITenantContext tenantContext,
        IHttpContextAccessor http,
        IConfiguration config,
        ILogger<NotificacionDispatcher> log)
    {
        _db = db;
        _tenantContext = tenantContext;
        _http = http;
        _config = config;
        _log = log;
    }

    private string Provider => _config["Notificaciones:Provider"] ?? "Stub";

    public async Task<ResultadoEnvioNotificacion> EnviarAsync(
        EnviarNotificacionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Cuerpo))
            throw new InvalidOperationException("Cuerpo de la notificacion es obligatorio.");

        var destino = await ResolverDestinoAsync(req, ct);
        if (string.IsNullOrWhiteSpace(destino))
            throw new InvalidOperationException(
                $"No se pudo resolver destino para canal {req.Canal}. " +
                "Provee Destino directo o UsuarioDestinatarioId/PersonaDestinatariaId.");

        // Idempotencia: si existe una notificacion Enviada o Encolada para la misma
        // entidad origen + destinatario + canal, devolvemos esa para no duplicar.
        if (req.EntidadOrigenId is { } eid && !string.IsNullOrWhiteSpace(req.ModuloOrigenCodigo))
        {
            var existente = await _db.Notificaciones.AsNoTracking()
                .Where(n => n.EntidadOrigenId == eid
                            && n.ModuloOrigenCodigo == req.ModuloOrigenCodigo
                            && n.Canal == req.Canal
                            && n.Destino == destino
                            && (n.Estado == EstadoNotificacion.Enviado
                                || n.Estado == EstadoNotificacion.Encolada
                                || n.Estado == EstadoNotificacion.Enviando))
                .Select(n => new { n.Id, n.Estado })
                .FirstOrDefaultAsync(ct);
            if (existente is not null)
                return new ResultadoEnvioNotificacion(existente.Id, existente.Estado, null);
        }

        // Para InApp, si el destino se resolvio a un userId (a partir de PersonaDestinatariaId),
        // poblarmos UsuarioDestinatarioId asi el inbox del usuario lo encuentra.
        Guid? usuarioResuelto = req.UsuarioDestinatarioId;
        if (req.Canal == CanalNotificacion.InApp && usuarioResuelto is null
            && Guid.TryParse(destino, out var parsedUid))
        {
            usuarioResuelto = parsedUid;
        }

        var noti = new Notificacion
        {
            TenantId = req.TenantId ?? _tenantContext.CurrentTenantId,
            UsuarioDestinatarioId = usuarioResuelto,
            PersonaDestinatariaId = req.PersonaDestinatariaId,
            Canal = req.Canal,
            Prioridad = req.Prioridad,
            Estado = EstadoNotificacion.Encolada,
            Destino = destino!,
            Asunto = req.Asunto,
            Cuerpo = req.Cuerpo,
            CuerpoHtml = req.CuerpoHtml,
            MetadataJson = req.MetadataJson,
            ModuloOrigenCodigo = req.ModuloOrigenCodigo,
            EntidadOrigenId = req.EntidadOrigenId,
            CreatedBy = GetUsuarioActualId()
        };
        _db.Notificaciones.Add(noti);
        await _db.SaveChangesAsync(ct);

        try
        {
            noti.Estado = EstadoNotificacion.Enviando;
            noti.Intentos++;
            await _db.SaveChangesAsync(ct);

            await DespacharSegunProviderAsync(noti, ct);

            noti.Estado = EstadoNotificacion.Enviado;
            noti.FechaEnviado = DateTimeOffset.UtcNow;
            noti.UltimoError = null;
            await _db.SaveChangesAsync(ct);
            return new ResultadoEnvioNotificacion(noti.Id, EstadoNotificacion.Enviado, null);
        }
        catch (Exception ex)
        {
            noti.Estado = EstadoNotificacion.Fallido;
            noti.UltimoError = ex.Message;
            noti.FechaProximoIntento = DateTimeOffset.UtcNow.AddMinutes(5);
            await _db.SaveChangesAsync(ct);
            _log.LogWarning(ex, "T.2 envio fallido id={Id} canal={Canal}", noti.Id, noti.Canal);
            return new ResultadoEnvioNotificacion(noti.Id, EstadoNotificacion.Fallido, ex.Message);
        }
    }

    public async Task<IReadOnlyList<ResultadoEnvioNotificacion>> EnviarLoteAsync(
        IEnumerable<EnviarNotificacionRequest> requests, CancellationToken ct)
    {
        var resultados = new List<ResultadoEnvioNotificacion>();
        foreach (var r in requests)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                resultados.Add(await EnviarAsync(r, ct));
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "T.2 lote: error en notificacion individual");
                resultados.Add(new ResultadoEnvioNotificacion(Guid.Empty, EstadoNotificacion.Fallido, ex.Message));
            }
        }
        return resultados;
    }

    public async Task EnviarEventoUsuarioAsync(
        Guid personaId, string asunto, string cuerpo, string moduloOrigen, Guid? entidadOrigenId,
        Guid? tenantId, PrioridadNotificacion prioridad, CancellationToken ct)
    {
        if (personaId == Guid.Empty || string.IsNullOrWhiteSpace(cuerpo)) return;

        // No auto-notificar: si el actor de la accion es el mismo destinatario, no enviamos.
        var actor = await ActorPersonaIdAsync(ct);
        if (actor is { } a && a == personaId) return;

        tenantId ??= _tenantContext.CurrentTenantId;

        // 1) InApp (inbox del usuario).
        await TryEnviarAsync(new EnviarNotificacionRequest(
            Canal: CanalNotificacion.InApp, Cuerpo: cuerpo, TenantId: tenantId,
            PersonaDestinatariaId: personaId, Asunto: asunto, Prioridad: prioridad,
            ModuloOrigenCodigo: moduloOrigen, EntidadOrigenId: entidadOrigenId), ct);

        // 2) Contactos configurados por el usuario (Mi Perfil): correos/telefonos activos.
        var contactos = await _db.UsuarioContactosNotificacion.AsNoTracking()
            .Where(c => c.PersonaId == personaId && c.Activo)
            .Select(c => new { c.Canal, c.Valor })
            .ToListAsync(ct);

        if (contactos.Count == 0)
        {
            // Fallback: el correo de la Persona (si tiene). El dispatcher lo resuelve solo.
            await TryEnviarAsync(new EnviarNotificacionRequest(
                Canal: CanalNotificacion.Email, Cuerpo: cuerpo, TenantId: tenantId,
                PersonaDestinatariaId: personaId, Asunto: asunto, Prioridad: prioridad,
                ModuloOrigenCodigo: moduloOrigen, EntidadOrigenId: entidadOrigenId), ct);
            return;
        }

        foreach (var c in contactos)
        {
            await TryEnviarAsync(new EnviarNotificacionRequest(
                Canal: c.Canal, Cuerpo: cuerpo, TenantId: tenantId,
                PersonaDestinatariaId: personaId, Destino: c.Valor, Asunto: asunto, Prioridad: prioridad,
                ModuloOrigenCodigo: moduloOrigen, EntidadOrigenId: entidadOrigenId), ct);
        }
    }

    private async Task TryEnviarAsync(EnviarNotificacionRequest req, CancellationToken ct)
    {
        try { await EnviarAsync(req, ct); }
        catch (Exception ex) { _log.LogWarning(ex, "T.2 evento-usuario fallo canal={Canal}", req.Canal); }
    }

    private async Task<Guid?> ActorPersonaIdAsync(CancellationToken ct)
    {
        var uid = GetUsuarioActualId();
        if (uid == Guid.Empty) return null;
        var pid = await _db.Users.AsNoTracking().Where(u => u.Id == uid).Select(u => u.PersonaId).FirstOrDefaultAsync(ct);
        return pid == Guid.Empty ? null : pid;
    }

    // -----------------------------------------------------------------------
    // Resolucion de destino
    // -----------------------------------------------------------------------

    private async Task<string?> ResolverDestinoAsync(EnviarNotificacionRequest req, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(req.Destino)) return req.Destino;

        // InApp: el destino es el UsuarioDestinatarioId como string.
        // Si solo viene PersonaDestinatariaId, resolvemos el ApplicationUser asociado.
        // Esto permite que los helpers cross-modulo (NotificarAdminsAsync, etc.)
        // puedan resolver destinatarios InApp desde Persona sin tener que conocer
        // el ApplicationUser.Id de cada admin.
        if (req.Canal == CanalNotificacion.InApp)
        {
            if (req.UsuarioDestinatarioId is { } inAppUid) return inAppUid.ToString();
            if (req.PersonaDestinatariaId is { } inAppPid)
            {
                var userId = await _db.Users.AsNoTracking()
                    .Where(u => u.PersonaId == inAppPid)
                    .Select(u => u.Id)
                    .FirstOrDefaultAsync(ct);
                return userId == Guid.Empty ? null : userId.ToString();
            }
            return null;
        }

        // Email / WhatsApp / Push: resolver desde Persona.
        Guid? personaId = req.PersonaDestinatariaId;
        if (personaId is null && req.UsuarioDestinatarioId is { } uid)
        {
            personaId = await _db.Users.AsNoTracking()
                .Where(u => u.Id == uid)
                .Select(u => u.PersonaId)
                .FirstOrDefaultAsync(ct);
        }
        if (personaId is null) return null;

        var p = await _db.Personas.AsNoTracking()
            .Where(x => x.Id == personaId.Value)
            .Select(x => new { x.Email, x.Telefono })
            .FirstOrDefaultAsync(ct);
        if (p is null) return null;

        return req.Canal switch
        {
            CanalNotificacion.Email => p.Email,
            CanalNotificacion.WhatsApp => p.Telefono,
            CanalNotificacion.Push => p.Telefono,
            _ => null
        };
    }

    // -----------------------------------------------------------------------
    // Despacho por provider (MVP: Stub log estructurado)
    // -----------------------------------------------------------------------

    private Task DespacharSegunProviderAsync(Notificacion noti, CancellationToken ct)
    {
        return Provider.ToLowerInvariant() switch
        {
            "stub" => DespacharStubAsync(noti, ct),
            "sendgrid" => DespacharStubAsync(noti, ct),       // Fase 2: implementacion real
            "whatsappcloud" => DespacharStubAsync(noti, ct),  // Fase 2: implementacion real
            _ => DespacharStubAsync(noti, ct)
        };
    }

    /// <summary>
    /// Stub provider: log estructurado + marcar Enviado. Sin side-effects externos.
    /// Reemplaza los 9 "simulado" diseminados antes de T.2.
    /// </summary>
    private Task DespacharStubAsync(Notificacion noti, CancellationToken ct)
    {
        _log.LogInformation(
            "T.2 STUB enviando id={Id} canal={Canal} destino={Destino} asunto={Asunto} modulo={Modulo}",
            noti.Id, noti.Canal, noti.Destino, noti.Asunto, noti.ModuloOrigenCodigo);
        return Task.CompletedTask;
    }

    private Guid GetUsuarioActualId()
    {
        var sub = _http.HttpContext?.User?.FindFirstValue("user_id");
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }
}
