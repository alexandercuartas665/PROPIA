using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Calendario;
using Propia.Application.Common;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Calendario;

/// <summary>
/// Modulo 1.2 Calendario Multi-Copropiedad (spec v1.0 MVP).
///
/// MVP scope:
///  - Agregador cross-modulo SIN consultar tablas operativas directamente (RN-01 spec 2.16).
///    En MVP es queries directas a Domain entities porque la capa de servicios indicadores
///    de cada modulo no se construyo formal; se reemplaza por queries scoped a OrganizacionId.
///  - Eventos internos CRUD scoped por organizacion.
///  - Config personal por (UsuarioId, OrganizacionId).
///  - Feed iCal con token (sin auth de sesion).
///
/// Diferido a Fase 2: notificaciones T.2, sincronizacion OAuth Google/Outlook,
/// acciones rapidas que crean tareas en 2.10.
/// </summary>
public class CalendarioService : ICalendarioService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _http;
    private readonly Propia.Application.Notificaciones.INotificacionDispatcher _noti;

    public CalendarioService(
        PropiaDbContext db,
        ITenantContext tenantContext,
        IHttpContextAccessor http,
        Propia.Application.Notificaciones.INotificacionDispatcher noti)
    {
        _db = db;
        _tenantContext = tenantContext;
        _http = http;
        _noti = noti;
    }

    private Guid GetUsuarioActualId()
    {
        var sub = _http.HttpContext?.User?.FindFirstValue("user_id");
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    /// <summary>Obtiene la organizacion del usuario actual via su tenant activo.</summary>
    private async Task<Guid> GetOrganizacionIdActualAsync(CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentTenantId
            ?? throw new InvalidOperationException("No hay copropiedad activa para resolver organizacion.");
        var orgId = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId).Select(t => t.OrganizacionId).FirstOrDefaultAsync(ct);
        if (orgId is null)
            throw new InvalidOperationException("La copropiedad activa no esta vinculada a una organizacion.");
        return orgId.Value;
    }

    /// <summary>Lista los tenant IDs del portafolio de la organizacion (cross-tenant via IgnoreQueryFilters).</summary>
    private async Task<List<Guid>> GetTenantsDeOrganizacionAsync(Guid orgId, CancellationToken ct)
    {
        return await _db.Tenants.AsNoTracking()
            .Where(t => t.OrganizacionId == orgId && t.Estado == EstadoCopropiedad.Activa)
            .Select(t => t.Id).ToListAsync(ct);
    }

    // ===========================================================================
    // Agregador de eventos (vista Agenda)
    // ===========================================================================

    public async Task<IReadOnlyList<EventoCalendarioDto>> ListarEventosAsync(FiltroCalendarioDto filtro, CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var tenants = await GetTenantsDeOrganizacionAsync(orgId, ct);
        if (filtro.Copropiedades is { Count: > 0 })
            tenants = tenants.Intersect(filtro.Copropiedades).ToList();
        if (tenants.Count == 0) return Array.Empty<EventoCalendarioDto>();

        var nombrePorTenant = await _db.Tenants.AsNoTracking()
            .Where(t => tenants.Contains(t.Id))
            .Select(t => new { t.Id, t.Nombre })
            .ToDictionaryAsync(t => t.Id, t => t.Nombre, ct);

        var desde = new DateTimeOffset(DateTime.SpecifyKind(filtro.Desde.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc));
        var hasta = new DateTimeOffset(DateTime.SpecifyKind(filtro.Hasta.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc));

        bool wantCat(CategoriaEvento cat) => filtro.Categorias is null || filtro.Categorias.Contains(cat);
        var eventos = new List<EventoCalendarioDto>();

        // Asambleas (2.8)
        if (wantCat(CategoriaEvento.Asamblea))
        {
            var asambleas = await _db.Sesiones.IgnoreQueryFilters().AsNoTracking()
                .Where(s => tenants.Contains(s.TenantId) && s.FechaSesion >= desde && s.FechaSesion <= hasta)
                .Select(s => new { s.Id, s.TenantId, s.Titulo, s.FechaSesion, s.LugarFisico })
                .ToListAsync(ct);
            foreach (var s in asambleas)
            {
                eventos.Add(new EventoCalendarioDto(
                    $"asamblea:{s.Id}", CategoriaEvento.Asamblea, s.Titulo, s.LugarFisico,
                    s.TenantId, nombrePorTenant.TryGetValue(s.TenantId, out var n1) ? n1 : null, null,
                    s.FechaSesion, s.FechaSesion.AddHours(2), false, "America/Bogota",
                    $"/asambleas/{s.Id}", false));
            }
        }

        // Tareas (2.10) - solo las con FechaVencimiento
        if (wantCat(CategoriaEvento.Tarea))
        {
            var tareas = await _db.Tareas.IgnoreQueryFilters().AsNoTracking()
                .Where(t => tenants.Contains(t.TenantId)
                            && t.FechaVencimiento != null
                            && t.FechaVencimiento >= filtro.Desde
                            && t.FechaVencimiento <= filtro.Hasta)
                .Select(t => new { t.Id, t.TenantId, t.Titulo, t.FechaVencimiento })
                .ToListAsync(ct);
            foreach (var t in tareas)
            {
                var dt = new DateTimeOffset(DateTime.SpecifyKind(
                    t.FechaVencimiento!.Value.ToDateTime(new TimeOnly(17, 0)), DateTimeKind.Utc));
                eventos.Add(new EventoCalendarioDto(
                    $"tarea:{t.Id}", CategoriaEvento.Tarea, t.Titulo, null,
                    t.TenantId, nombrePorTenant.TryGetValue(t.TenantId, out var n2) ? n2 : null, null,
                    dt, null, true, "America/Bogota",
                    $"/tareas/{t.Id}", false));
            }
        }

        // PQRSD (2.9) - vencimientos
        if (wantCat(CategoriaEvento.Pqrsd))
        {
            var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
            var pqrsd = await _db.PqrsdExpedientes.IgnoreQueryFilters().AsNoTracking()
                .Where(p => tenants.Contains(p.TenantId)
                            && p.Estado != EstadoPqrsd.Cerrada
                            && p.FechaVencimiento >= filtro.Desde
                            && p.FechaVencimiento <= filtro.Hasta)
                .Select(p => new { p.Id, p.TenantId, p.NumeroRadicado, p.FechaVencimiento, p.Tipo })
                .ToListAsync(ct);
            foreach (var p in pqrsd)
            {
                var dt = new DateTimeOffset(DateTime.SpecifyKind(
                    p.FechaVencimiento.ToDateTime(new TimeOnly(17, 0)), DateTimeKind.Utc));
                eventos.Add(new EventoCalendarioDto(
                    $"pqrsd:{p.Id}", CategoriaEvento.Pqrsd,
                    $"Vence {p.Tipo} {p.NumeroRadicado}", null,
                    p.TenantId, nombrePorTenant.TryGetValue(p.TenantId, out var n3) ? n3 : null, null,
                    dt, null, true, "America/Bogota",
                    $"/pqrsd/{p.Id}", false));
            }
        }

        // Eventos internos
        if (wantCat(CategoriaEvento.Interno))
        {
            var internos = await _db.CalendarioEventos.AsNoTracking()
                .Where(e => e.OrganizacionId == orgId
                            && e.FechaInicio >= desde && e.FechaInicio <= hasta)
                .Where(e => e.TenantId == null || tenants.Contains(e.TenantId.Value))
                .ToListAsync(ct);
            foreach (var e in internos)
            {
                eventos.Add(new EventoCalendarioDto(
                    $"interno:{e.Id}", CategoriaEvento.Interno, e.Titulo, e.Descripcion,
                    e.TenantId,
                    e.TenantId.HasValue && nombrePorTenant.TryGetValue(e.TenantId.Value, out var n4) ? n4 : null,
                    null,
                    e.FechaInicio, e.FechaFin, e.EsDiaCompleto, e.ZonaHoraria,
                    null, true));
            }
        }

        return eventos.OrderBy(e => e.FechaInicio).ToList();
    }

    public async Task<IReadOnlyList<CriticoDto>> ListarCriticosAsync(CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var tenants = await GetTenantsDeOrganizacionAsync(orgId, ct);
        if (tenants.Count == 0) return Array.Empty<CriticoDto>();

        var nombrePorTenant = await _db.Tenants.AsNoTracking()
            .Where(t => tenants.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Nombre, ct);

        var criticos = new List<CriticoDto>();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        // PQRSD vencidas o por vencer en menos de 3 dias (severidad alta)
        var pqrsd = await _db.PqrsdExpedientes.IgnoreQueryFilters().AsNoTracking()
            .Where(p => tenants.Contains(p.TenantId) && p.Estado != EstadoPqrsd.Cerrada
                       && p.FechaVencimiento <= hoy.AddDays(7))
            .Select(p => new { p.Id, p.TenantId, p.NumeroRadicado, p.FechaVencimiento, p.Tipo })
            .ToListAsync(ct);
        foreach (var p in pqrsd)
        {
            var dias = p.FechaVencimiento.DayNumber - hoy.DayNumber;
            var sev = dias <= 0 ? SeveridadCritico.Rojo : (dias <= 2 ? SeveridadCritico.Naranja : SeveridadCritico.Amarillo);
            criticos.Add(new CriticoDto(
                $"pqrsd:{p.Id}", sev,
                $"{p.Tipo} {p.NumeroRadicado} sin respuesta",
                "Plazo legal Ley 1755 de 2015",
                p.TenantId, nombrePorTenant.TryGetValue(p.TenantId, out var n) ? n : null,
                new DateTimeOffset(DateTime.SpecifyKind(p.FechaVencimiento.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)),
                dias,
                $"/pqrsd/{p.Id}"));
        }

        // Tareas vencidas (severidad media)
        var tareas = await _db.Tareas.IgnoreQueryFilters().AsNoTracking()
            .Where(t => tenants.Contains(t.TenantId)
                       && t.FechaVencimiento != null && t.FechaVencimiento < hoy
                       && t.FechaCompletada == null)
            .Select(t => new { t.Id, t.TenantId, t.Titulo, t.FechaVencimiento })
            .Take(20)
            .ToListAsync(ct);
        foreach (var t in tareas)
        {
            var dias = t.FechaVencimiento!.Value.DayNumber - hoy.DayNumber;
            criticos.Add(new CriticoDto(
                $"tarea:{t.Id}", SeveridadCritico.Naranja,
                $"Tarea vencida: {t.Titulo}",
                "Sin completar - bloquea KPIs operativos",
                t.TenantId, nombrePorTenant.TryGetValue(t.TenantId, out var n) ? n : null,
                new DateTimeOffset(DateTime.SpecifyKind(t.FechaVencimiento.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)),
                dias,
                $"/tareas/{t.Id}"));
        }

        return criticos.OrderBy(c => c.Vencimiento).ToList();
    }

    // ===========================================================================
    // Eventos internos
    // ===========================================================================

    public async Task<IReadOnlyList<EventoInternoDto>> ListarEventosInternosAsync(DateOnly? desde, DateOnly? hasta, CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var q = _db.CalendarioEventos.AsNoTracking().Where(e => e.OrganizacionId == orgId);
        if (desde is { } d)
        {
            var dt = new DateTimeOffset(DateTime.SpecifyKind(d.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc));
            q = q.Where(e => e.FechaInicio >= dt);
        }
        if (hasta is { } h)
        {
            var dt = new DateTimeOffset(DateTime.SpecifyKind(h.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc));
            q = q.Where(e => e.FechaInicio <= dt);
        }
        var list = await q.OrderBy(e => e.FechaInicio).Take(200).ToListAsync(ct);
        return await MapEventosAsync(list, ct);
    }

    public async Task<EventoInternoDto?> GetEventoInternoAsync(Guid id, CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var e = await _db.CalendarioEventos.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OrganizacionId == orgId, ct);
        if (e is null) return null;
        var dtos = await MapEventosAsync(new[] { e }, ct);
        return dtos[0];
    }

    private async Task<List<EventoInternoDto>> MapEventosAsync(IEnumerable<CalendarioEvento> eventos, CancellationToken ct)
    {
        var tenantIds = eventos.Where(e => e.TenantId.HasValue).Select(e => e.TenantId!.Value).Distinct().ToList();
        var nombres = await _db.Tenants.AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Nombre, ct);
        return eventos.Select(e => new EventoInternoDto(
            e.Id, e.OrganizacionId, e.TenantId,
            e.TenantId.HasValue && nombres.TryGetValue(e.TenantId.Value, out var n) ? n : null,
            e.Titulo, e.Descripcion, e.Tipo,
            e.FechaInicio, e.FechaFin, e.EsDiaCompleto, e.RecordatorioMinutos,
            e.CreadoPorUsuarioId, e.CreatedAt)).ToList();
    }

    public async Task<EventoInternoDto> CrearEventoInternoAsync(CrearEventoInternoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Titulo)) throw new InvalidOperationException("Titulo obligatorio.");
        if (req.FechaFin.HasValue && req.FechaFin < req.FechaInicio)
            throw new InvalidOperationException("FechaFin debe ser >= FechaInicio.");
        var orgId = await GetOrganizacionIdActualAsync(ct);
        if (req.TenantId is { } tid)
        {
            var pertenece = await _db.Tenants.AnyAsync(t => t.Id == tid && t.OrganizacionId == orgId, ct);
            if (!pertenece) throw new InvalidOperationException("La copropiedad indicada no pertenece a tu organizacion.");
        }
        var e = new CalendarioEvento
        {
            OrganizacionId = orgId,
            TenantId = req.TenantId,
            Titulo = req.Titulo.Trim(),
            Descripcion = req.Descripcion?.Trim(),
            Tipo = req.Tipo,
            FechaInicio = req.FechaInicio,
            FechaFin = req.FechaFin,
            EsDiaCompleto = req.EsDiaCompleto,
            RecordatorioMinutos = req.RecordatorioMinutos,
            CreadoPorUsuarioId = GetUsuarioActualId()
        };
        _db.CalendarioEventos.Add(e);
        await _db.SaveChangesAsync(ct);

        // T.2: confirmacion InApp al creador. Recordatorios delay-based quedan para Fase 2
        // (necesitan cron que escanee CalendarioEvento.FechaInicio - RecordatorioMinutos).
        await _noti.EnviarAsync(new Propia.Application.Notificaciones.EnviarNotificacionRequest(
            Canal: Domain.Enums.CanalNotificacion.InApp,
            Cuerpo: $"Tu evento '{e.Titulo}' quedo agendado para {e.FechaInicio.ToLocalTime():yyyy-MM-dd HH:mm}.",
            TenantId: e.TenantId,
            UsuarioDestinatarioId: e.CreadoPorUsuarioId,
            Asunto: "Evento agendado",
            Prioridad: Domain.Enums.PrioridadNotificacion.Baja,
            ModuloOrigenCodigo: "1.2",
            EntidadOrigenId: e.Id), ct);

        return (await GetEventoInternoAsync(e.Id, ct))!;
    }

    public async Task<bool> ActualizarEventoInternoAsync(Guid id, ActualizarEventoInternoRequest req, CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var e = await _db.CalendarioEventos.FirstOrDefaultAsync(x => x.Id == id && x.OrganizacionId == orgId, ct);
        if (e is null) return false;
        e.Titulo = req.Titulo.Trim();
        e.Descripcion = req.Descripcion?.Trim();
        e.Tipo = req.Tipo;
        e.FechaInicio = req.FechaInicio;
        e.FechaFin = req.FechaFin;
        e.EsDiaCompleto = req.EsDiaCompleto;
        e.RecordatorioMinutos = req.RecordatorioMinutos;
        e.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarEventoInternoAsync(Guid id, CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var e = await _db.CalendarioEventos.FirstOrDefaultAsync(x => x.Id == id && x.OrganizacionId == orgId, ct);
        if (e is null) return false;
        _db.CalendarioEventos.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===========================================================================
    // Configuracion del usuario
    // ===========================================================================

    public async Task<CalendarioConfigDto> GetConfigAsync(CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var userId = GetUsuarioActualId();
        var cfg = await GetOrCreateConfigAsync(userId, orgId, ct);
        return ToDto(cfg);
    }

    private async Task<CalendarioConfigUsuario> GetOrCreateConfigAsync(Guid userId, Guid orgId, CancellationToken ct)
    {
        var cfg = await _db.CalendarioConfigUsuarios
            .FirstOrDefaultAsync(c => c.UsuarioId == userId && c.OrganizacionId == orgId, ct);
        if (cfg is null)
        {
            cfg = new CalendarioConfigUsuario { UsuarioId = userId, OrganizacionId = orgId };
            _db.CalendarioConfigUsuarios.Add(cfg);
            await _db.SaveChangesAsync(ct);
        }
        return cfg;
    }

    private static CalendarioConfigDto ToDto(CalendarioConfigUsuario c)
    {
        IReadOnlyList<Guid>? cops = null;
        IReadOnlyList<CategoriaEvento>? tipos = null;
        if (!string.IsNullOrWhiteSpace(c.FiltroCopropiedadesJson))
            try { cops = JsonSerializer.Deserialize<List<Guid>>(c.FiltroCopropiedadesJson); } catch { }
        if (!string.IsNullOrWhiteSpace(c.FiltroTiposJson))
            try { tipos = JsonSerializer.Deserialize<List<CategoriaEvento>>(c.FiltroTiposJson); } catch { }
        return new CalendarioConfigDto(c.VistaDefault, c.UltimaVista, cops, tipos, c.IcalToken,
            c.AnticipacionAsamblea, c.AnticipacionTarea, c.AnticipacionMantenimiento, c.AnticipacionPqrsd);
    }

    public async Task<CalendarioConfigDto> ActualizarConfigAsync(ActualizarConfigCalendarioRequest req, CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var userId = GetUsuarioActualId();
        var cfg = await GetOrCreateConfigAsync(userId, orgId, ct);
        cfg.VistaDefault = req.VistaDefault;
        cfg.UltimaVista = req.VistaDefault;
        cfg.FiltroCopropiedadesJson = req.FiltroCopropiedades is null ? null : JsonSerializer.Serialize(req.FiltroCopropiedades);
        cfg.FiltroTiposJson = req.FiltroTipos is null ? null : JsonSerializer.Serialize(req.FiltroTipos);
        cfg.AnticipacionAsamblea = req.AnticipacionAsamblea;
        cfg.AnticipacionTarea = req.AnticipacionTarea;
        cfg.AnticipacionMantenimiento = req.AnticipacionMantenimiento;
        cfg.AnticipacionPqrsd = req.AnticipacionPqrsd;
        cfg.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(cfg);
    }

    public async Task<Guid> GenerarOReGenerarIcalTokenAsync(CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var userId = GetUsuarioActualId();
        var cfg = await GetOrCreateConfigAsync(userId, orgId, ct);
        cfg.IcalToken = Guid.NewGuid();
        cfg.IcalTokenGeneradoAt = DateTimeOffset.UtcNow;
        cfg.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return cfg.IcalToken.Value;
    }

    public async Task<bool> RevocarIcalTokenAsync(CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var userId = GetUsuarioActualId();
        var cfg = await _db.CalendarioConfigUsuarios
            .FirstOrDefaultAsync(c => c.UsuarioId == userId && c.OrganizacionId == orgId, ct);
        if (cfg is null) return false;
        cfg.IcalToken = null;
        cfg.IcalTokenGeneradoAt = null;
        cfg.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<string?> GenerarIcsAsync(Guid token, CancellationToken ct)
    {
        var cfg = await _db.CalendarioConfigUsuarios.AsNoTracking()
            .FirstOrDefaultAsync(c => c.IcalToken == token, ct);
        if (cfg is null) return null;

        // Construir feed RFC 5545 minimo
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//PROPIA//Calendario Multi-Copropiedad//ES");
        sb.AppendLine("CALSCALE:GREGORIAN");

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var tenants = await _db.Tenants.AsNoTracking()
            .Where(t => t.OrganizacionId == cfg.OrganizacionId && t.Estado == EstadoCopropiedad.Activa)
            .Select(t => t.Id).ToListAsync(ct);
        var nombres = await _db.Tenants.AsNoTracking()
            .Where(t => tenants.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Nombre, ct);

        // Eventos internos
        var internos = await _db.CalendarioEventos.AsNoTracking()
            .Where(e => e.OrganizacionId == cfg.OrganizacionId
                       && e.FechaInicio >= DateTimeOffset.UtcNow.AddDays(-30)
                       && e.FechaInicio <= DateTimeOffset.UtcNow.AddDays(90))
            .ToListAsync(ct);
        foreach (var e in internos)
        {
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"UID:interno-{e.Id}@propia.local");
            sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
            sb.AppendLine($"DTSTART:{e.FechaInicio.UtcDateTime:yyyyMMddTHHmmssZ}");
            if (e.FechaFin.HasValue)
                sb.AppendLine($"DTEND:{e.FechaFin.Value.UtcDateTime:yyyyMMddTHHmmssZ}");
            sb.AppendLine($"SUMMARY:{EscapeIcs(e.Titulo)}");
            if (!string.IsNullOrWhiteSpace(e.Descripcion))
                sb.AppendLine($"DESCRIPTION:{EscapeIcs(e.Descripcion)}");
            sb.AppendLine("END:VEVENT");
        }

        // Asambleas del portafolio
        var asambleas = await _db.Sesiones.IgnoreQueryFilters().AsNoTracking()
            .Where(s => tenants.Contains(s.TenantId)
                       && s.FechaSesion >= DateTimeOffset.UtcNow.AddDays(-30)
                       && s.FechaSesion <= DateTimeOffset.UtcNow.AddDays(180))
            .Select(s => new { s.Id, s.TenantId, s.Titulo, s.FechaSesion, s.LugarFisico })
            .ToListAsync(ct);
        foreach (var a in asambleas)
        {
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"UID:asamblea-{a.Id}@propia.local");
            sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
            sb.AppendLine($"DTSTART:{a.FechaSesion.UtcDateTime:yyyyMMddTHHmmssZ}");
            sb.AppendLine($"DTEND:{a.FechaSesion.AddHours(2).UtcDateTime:yyyyMMddTHHmmssZ}");
            var cop = nombres.TryGetValue(a.TenantId, out var n) ? $" - {n}" : "";
            sb.AppendLine($"SUMMARY:{EscapeIcs($"Asamblea: {a.Titulo}{cop}")}");
            if (!string.IsNullOrWhiteSpace(a.LugarFisico))
                sb.AppendLine($"LOCATION:{EscapeIcs(a.LugarFisico)}");
            sb.AppendLine("END:VEVENT");
        }

        sb.AppendLine("END:VCALENDAR");
        return sb.ToString();
    }

    private static string EscapeIcs(string s) => s.Replace(",", "\\,").Replace(";", "\\;").Replace("\n", "\\n");

    // ===========================================================================
    // Resumen
    // ===========================================================================

    public async Task<ResumenCalendarioDto> GetResumenAsync(CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var tenants = await GetTenantsDeOrganizacionAsync(orgId, ct);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var en30 = hoy.AddDays(30);

        var filtro = new FiltroCalendarioDto(hoy, en30, null, null);
        var todos = await ListarEventosAsync(filtro, ct);
        var criticos = await ListarCriticosAsync(ct);
        return new ResumenCalendarioDto(
            todos.Count,
            criticos.Count,
            todos.Count(e => e.Categoria == CategoriaEvento.Asamblea),
            todos.Count(e => e.Categoria == CategoriaEvento.Mantenimiento),
            todos.Count(e => e.Categoria == CategoriaEvento.Tarea),
            todos.Count(e => e.Categoria == CategoriaEvento.Interno));
    }
}
