using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Notificaciones;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Notificaciones;

/// <summary>
/// Servicio T.2 de lectura del inbox + operaciones sobre notificaciones ya despachadas.
/// El despacho lo hace INotificacionDispatcher; este servicio cubre el lado consumidor
/// (inbox InApp del usuario, panel de admin para reintentos, KPIs).
/// </summary>
public class NotificacionesService : INotificacionesService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _http;
    private readonly INotificacionDispatcher _dispatcher;

    public NotificacionesService(
        PropiaDbContext db,
        ITenantContext tenantContext,
        IHttpContextAccessor http,
        INotificacionDispatcher dispatcher)
    {
        _db = db;
        _tenantContext = tenantContext;
        _http = http;
        _dispatcher = dispatcher;
    }

    private Guid GetUsuarioActualId()
    {
        var sub = _http.HttpContext?.User?.FindFirstValue("user_id");
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    private Guid? GetPersonaActualId()
        => Guid.TryParse(_http.HttpContext?.User?.FindFirstValue("persona_id"), out var id) ? id : null;

    // Auditoria UX S-05 (2026-11-25, decision de Alex: "solo las propias"): cada usuario ve SOLO las notificaciones
    // dirigidas a el (por usuario o por persona). Antes el filtro era "mias O de mi copropiedad": la campana le mostraba
    // a cualquiera las de todos (63 "copias" = el mismo aviso enviado a 63 personas) y "Marcar todas leidas" fallaba en
    // silencio con las ajenas. Fuga de privacidad corregida tambien en el detalle y el resumen.
    private IQueryable<Domain.Entities.Notificacion> Propias()
    {
        var userId = GetUsuarioActualId();
        var personaId = GetPersonaActualId();
        if (userId == Guid.Empty && personaId is null) return _db.Notificaciones.AsNoTracking().Where(n => false);
        return _db.Notificaciones.AsNoTracking()
            .Where(n => (userId != Guid.Empty && n.UsuarioDestinatarioId == userId)
                        || (personaId != null && n.PersonaDestinatariaId == personaId));
    }

    public async Task<IReadOnlyList<NotificacionDto>> ListarAsync(
        FiltroNotificacionesRequest filtro, CancellationToken ct)
    {
        // Aislamiento: SOLO las notificaciones dirigidas al usuario actual (ver Propias()).
        var q = Propias();

        if (filtro.Estado is { } estado) q = q.Where(n => n.Estado == estado);
        if (filtro.Canal is { } canal) q = q.Where(n => n.Canal == canal);
        if (!string.IsNullOrWhiteSpace(filtro.ModuloOrigenCodigo))
            q = q.Where(n => n.ModuloOrigenCodigo == filtro.ModuloOrigenCodigo);

        return await q
            .OrderByDescending(n => n.CreatedAt)
            .Take(Math.Clamp(filtro.Limite, 1, 500))
            .Select(n => Map(n))
            .ToListAsync(ct);
    }

    public async Task<NotificacionDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var n = await Propias().FirstOrDefaultAsync(x => x.Id == id, ct);
        return n is null ? null : Map(n);
    }

    public async Task<bool> MarcarLeidoAsync(Guid id, CancellationToken ct)
    {
        var userId = GetUsuarioActualId();
        var n = await _db.Notificaciones.FirstOrDefaultAsync(
            x => x.Id == id && x.UsuarioDestinatarioId == userId, ct);
        if (n is null) return false;
        if (n.Canal != CanalNotificacion.InApp)
            throw new InvalidOperationException("Solo InApp soporta marca de lectura.");
        if (n.Estado == EstadoNotificacion.Leido) return true;

        n.Estado = EstadoNotificacion.Leido;
        n.FechaLeido = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ResultadoEnvioNotificacion> ReintentarAsync(Guid id, CancellationToken ct)
    {
        var n = await _db.Notificaciones.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Notificacion no encontrada.");
        if (n.Estado != EstadoNotificacion.Fallido)
            throw new InvalidOperationException(
                $"Solo notificaciones Fallidas pueden reintentarse (estado actual: {n.Estado}).");

        var req = new EnviarNotificacionRequest(
            Canal: n.Canal,
            Cuerpo: n.Cuerpo,
            TenantId: n.TenantId,
            UsuarioDestinatarioId: n.UsuarioDestinatarioId,
            PersonaDestinatariaId: n.PersonaDestinatariaId,
            Destino: n.Destino,
            Asunto: n.Asunto,
            CuerpoHtml: n.CuerpoHtml,
            Prioridad: n.Prioridad,
            ModuloOrigenCodigo: n.ModuloOrigenCodigo,
            EntidadOrigenId: n.EntidadOrigenId,
            MetadataJson: n.MetadataJson);

        // Borramos la fallida e insertamos una nueva (idempotencia ya bloqueada porque
        // la entidad origen no tiene Enviado/Encolada despues del Fallido).
        _db.Notificaciones.Remove(n);
        await _db.SaveChangesAsync(ct);

        return await _dispatcher.EnviarAsync(req, ct);
    }

    public async Task<ResumenNotificacionesDto> GetResumenAsync(CancellationToken ct)
    {
        var userId = GetUsuarioActualId();
        var baseQ = Propias();

        var encoladas = await baseQ.CountAsync(n => n.Estado == EstadoNotificacion.Encolada, ct);
        var enviadas = await baseQ.CountAsync(n => n.Estado == EstadoNotificacion.Enviado, ct);
        var fallidas = await baseQ.CountAsync(n => n.Estado == EstadoNotificacion.Fallido, ct);
        var inAppNoLeidas = await baseQ.CountAsync(n =>
            n.Canal == CanalNotificacion.InApp
            && n.UsuarioDestinatarioId == userId
            && n.Estado == EstadoNotificacion.Enviado, ct);

        return new ResumenNotificacionesDto(encoladas, enviadas, fallidas, inAppNoLeidas);
    }

    private static NotificacionDto Map(Domain.Entities.Notificacion n) =>
        new(n.Id, n.TenantId, n.UsuarioDestinatarioId, n.PersonaDestinatariaId,
            n.Canal, n.Prioridad, n.Estado, n.Destino, n.Asunto, n.Cuerpo,
            n.ModuloOrigenCodigo, n.EntidadOrigenId, n.Intentos, n.UltimoError,
            n.FechaEnviado, n.FechaLeido, n.CreatedAt);
}
