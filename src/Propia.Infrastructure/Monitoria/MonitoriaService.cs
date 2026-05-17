using Microsoft.EntityFrameworkCore;
using Propia.Application.Monitoria;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Monitoria;

/// <summary>
/// Modulo 0.3 Monitoria y Auditoria Global - implementacion (MVP).
/// Consume PropiaDbContext con IgnoreQueryFilters en todas las queries cross-tenant
/// porque opera en contexto SuperAdmin (sin tenant activo).
/// </summary>
public class MonitoriaService : IMonitoriaService
{
    private readonly PropiaDbContext _db;
    public MonitoriaService(PropiaDbContext db) => _db = db;

    // =======================================================================
    // Logs (append-only)
    // =======================================================================

    public async Task<Guid> RegistrarLogAsync(RegistrarLogRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Mensaje))
            throw new InvalidOperationException("Mensaje obligatorio.");
        var log = new SistemaLog
        {
            TipoEvento = req.TipoEvento,
            Severidad = req.Severidad,
            TenantId = req.TenantId,
            ActorUsuarioId = req.ActorUsuarioId,
            Mensaje = req.Mensaje.Length > 2000 ? req.Mensaje.Substring(0, 2000) : req.Mensaje,
            ModuloOrigenCodigo = req.ModuloOrigenCodigo,
            DetalleJson = req.DetalleJson,
            Ip = req.Ip,
            UserAgent = req.UserAgent
        };
        _db.SistemaLogs.Add(log);
        await _db.SaveChangesAsync(ct);
        return log.Id;
    }

    public async Task<IReadOnlyList<SistemaLogDto>> ListarLogsAsync(
        FiltroLogsRequest filtro, CancellationToken ct)
    {
        IQueryable<SistemaLog> q = _db.SistemaLogs.AsNoTracking();
        if (filtro.Severidad is { } s) q = q.Where(l => l.Severidad == s);
        if (filtro.TipoEvento is { } t) q = q.Where(l => l.TipoEvento == t);
        if (filtro.TenantId is { } tid) q = q.Where(l => l.TenantId == tid);
        if (!string.IsNullOrWhiteSpace(filtro.ModuloOrigenCodigo))
            q = q.Where(l => l.ModuloOrigenCodigo == filtro.ModuloOrigenCodigo);
        if (filtro.DesdeUtc is { } d) q = q.Where(l => l.CreatedAt >= d);
        if (filtro.HastaUtc is { } h) q = q.Where(l => l.CreatedAt <= h);

        return await q.OrderByDescending(l => l.CreatedAt)
            .Take(Math.Clamp(filtro.Limite, 1, 1000))
            .Select(l => new SistemaLogDto(l.Id, l.TipoEvento, l.Severidad, l.TenantId,
                l.ActorUsuarioId, l.Mensaje, l.ModuloOrigenCodigo, l.DetalleJson, l.Ip, l.CreatedAt))
            .ToListAsync(ct);
    }

    // =======================================================================
    // Incidentes
    // =======================================================================

    public async Task<SistemaIncidenteDto> AbrirIncidenteAsync(
        AbrirIncidenteRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Titulo))
            throw new InvalidOperationException("Titulo obligatorio.");
        var inc = new SistemaIncidente
        {
            Severidad = req.Severidad,
            Titulo = req.Titulo.Trim(),
            Descripcion = req.Descripcion?.Trim(),
            ServicioAfectado = req.ServicioAfectado?.Trim(),
            TenantImpactadoId = req.TenantImpactadoId,
            Estado = EstadoIncidente.Abierto,
            DetectadoAt = DateTimeOffset.UtcNow
        };
        _db.SistemaIncidentes.Add(inc);

        // Log automatico: cada incidente abierto deja rastro en logs.
        _db.SistemaLogs.Add(new SistemaLog
        {
            TipoEvento = TipoEventoSistema.ErrorInfraestructura,
            Severidad = req.Severidad,
            TenantId = req.TenantImpactadoId,
            Mensaje = $"Incidente abierto: {req.Titulo}",
            ModuloOrigenCodigo = "0.3"
        });

        await _db.SaveChangesAsync(ct);
        return Map(inc);
    }

    public async Task<IReadOnlyList<SistemaIncidenteDto>> ListarIncidentesAsync(
        EstadoIncidente? estado, CancellationToken ct)
    {
        IQueryable<SistemaIncidente> q = _db.SistemaIncidentes.AsNoTracking();
        if (estado is { } e) q = q.Where(i => i.Estado == e);
        return await q.OrderByDescending(i => i.DetectadoAt).Take(500)
            .Select(i => Map(i)).ToListAsync(ct);
    }

    public async Task<SistemaIncidenteDto?> GetIncidenteAsync(Guid id, CancellationToken ct)
    {
        var i = await _db.SistemaIncidentes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return i is null ? null : Map(i);
    }

    public async Task<bool> AsignarIncidenteAsync(Guid id, Guid superAdminId, CancellationToken ct)
    {
        var i = await _db.SistemaIncidentes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (i is null) return false;
        i.AsignadoSuperAdminId = superAdminId;
        i.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CambiarEstadoIncidenteAsync(
        Guid id, CambiarEstadoIncidenteRequest req, CancellationToken ct)
    {
        var i = await _db.SistemaIncidentes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (i is null) return false;
        if (i.Estado == req.NuevoEstado) return true;
        i.Estado = req.NuevoEstado;
        i.UpdatedAt = DateTimeOffset.UtcNow;
        if (req.NuevoEstado == EstadoIncidente.Resuelto || req.NuevoEstado == EstadoIncidente.Cerrado
            || req.NuevoEstado == EstadoIncidente.FalsoPositivo)
        {
            i.ResueltoAt ??= DateTimeOffset.UtcNow;
        }
        _db.SistemaLogs.Add(new SistemaLog
        {
            TipoEvento = TipoEventoSistema.ConfiguracionGlobalCambiada,
            Severidad = SeveridadIncidente.Info,
            Mensaje = $"Incidente {i.Id} estado -> {req.NuevoEstado}: {req.Nota ?? ""}".Trim(),
            ModuloOrigenCodigo = "0.3"
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ResolverIncidenteAsync(
        Guid id, ResolverIncidenteRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.CausaRaiz) || string.IsNullOrWhiteSpace(req.SolucionAplicada))
            throw new InvalidOperationException("CausaRaiz y SolucionAplicada obligatorios.");
        var i = await _db.SistemaIncidentes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (i is null) return false;
        i.Estado = EstadoIncidente.Resuelto;
        i.CausaRaiz = req.CausaRaiz.Trim();
        i.SolucionAplicada = req.SolucionAplicada.Trim();
        i.ResueltoAt = DateTimeOffset.UtcNow;
        i.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // =======================================================================
    // Metricas
    // =======================================================================

    public async Task<MetricaUsoDiariaDto> CalcularYGuardarMetricasHoyAsync(CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var ahora = DateTimeOffset.UtcNow;
        var hace24h = ahora.AddDays(-1);

        // Conteos cross-tenant (IgnoreQueryFilters)
        var totalTenants = await _db.Tenants.IgnoreQueryFilters().CountAsync(ct);
        var tenantsActivos = await _db.Tenants.IgnoreQueryFilters()
            .CountAsync(t => t.Estado == EstadoCopropiedad.Activa, ct);
        var totalOrgs = await _db.Organizaciones.CountAsync(ct);
        var totalUsuarios = await _db.Users.CountAsync(ct);
        var totalSuperAdmins = await _db.SuperAdminUsuarios.CountAsync(ct);

        var tareas24h = await _db.Tareas.IgnoreQueryFilters().CountAsync(t => t.CreatedAt >= hace24h, ct);
        var pqrsd24h = await _db.PqrsdExpedientes.IgnoreQueryFilters().CountAsync(p => p.CreatedAt >= hace24h, ct);
        var comunic24h = await _db.Comunicados.IgnoreQueryFilters()
            .CountAsync(c => c.FechaEnvio != null && c.FechaEnvio >= hace24h, ct);
        var notis24h = await _db.Notificaciones.AsNoTracking()
            .CountAsync(n => n.CreatedAt >= hace24h && n.Estado == EstadoNotificacion.Enviado, ct);
        var incAbiertos = await _db.SistemaIncidentes.AsNoTracking()
            .CountAsync(i => i.Estado == EstadoIncidente.Abierto || i.Estado == EstadoIncidente.EnInvestigacion, ct);
        var incCriticos = await _db.SistemaIncidentes.AsNoTracking()
            .CountAsync(i => (i.Estado == EstadoIncidente.Abierto || i.Estado == EstadoIncidente.EnInvestigacion)
                             && i.Severidad == SeveridadIncidente.Critico, ct);

        // Upsert: si ya hay metrica de hoy, la actualiza
        var existente = await _db.MetricasUsoDiarias.FirstOrDefaultAsync(m => m.Fecha == hoy, ct);
        if (existente is null)
        {
            existente = new MetricaUsoDiaria { Fecha = hoy };
            _db.MetricasUsoDiarias.Add(existente);
        }
        existente.TotalTenants = totalTenants;
        existente.TenantsActivos = tenantsActivos;
        existente.TotalOrganizaciones = totalOrgs;
        existente.TotalUsuarios = totalUsuarios;
        existente.TotalSuperAdmins = totalSuperAdmins;
        existente.TareasCreadas24h = tareas24h;
        existente.PqrsdsRadicadas24h = pqrsd24h;
        existente.ComunicadosEnviados24h = comunic24h;
        existente.NotificacionesDespachadas24h = notis24h;
        existente.IncidentesAbiertos = incAbiertos;
        existente.IncidentesCriticos = incCriticos;
        existente.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return MapMetrica(existente);
    }

    public async Task<IReadOnlyList<MetricaUsoDiariaDto>> ListarMetricasAsync(
        DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        return await _db.MetricasUsoDiarias.AsNoTracking()
            .Where(m => m.Fecha >= desde && m.Fecha <= hasta)
            .OrderBy(m => m.Fecha)
            .Select(m => MapMetrica(m))
            .ToListAsync(ct);
    }

    public async Task<MetricaUsoDiariaDto?> GetMetricaMasRecienteAsync(CancellationToken ct)
    {
        var m = await _db.MetricasUsoDiarias.AsNoTracking()
            .OrderByDescending(x => x.Fecha).FirstOrDefaultAsync(ct);
        return m is null ? null : MapMetrica(m);
    }

    // =======================================================================
    // Resumen
    // =======================================================================

    public async Task<ResumenMonitoriaDto> GetResumenAsync(CancellationToken ct)
    {
        var hace24h = DateTimeOffset.UtcNow.AddDays(-1);
        var logsUlt24h = await _db.SistemaLogs.AsNoTracking().CountAsync(l => l.CreatedAt >= hace24h, ct);
        var logsError24h = await _db.SistemaLogs.AsNoTracking()
            .CountAsync(l => l.CreatedAt >= hace24h
                             && (l.Severidad == SeveridadIncidente.Error
                                 || l.Severidad == SeveridadIncidente.Critico), ct);
        var incAbiertos = await _db.SistemaIncidentes.AsNoTracking()
            .CountAsync(i => i.Estado == EstadoIncidente.Abierto || i.Estado == EstadoIncidente.EnInvestigacion, ct);
        var incCriticos = await _db.SistemaIncidentes.AsNoTracking()
            .CountAsync(i => (i.Estado == EstadoIncidente.Abierto || i.Estado == EstadoIncidente.EnInvestigacion)
                             && i.Severidad == SeveridadIncidente.Critico, ct);
        var ultimaMetricaAt = await _db.MetricasUsoDiarias.AsNoTracking()
            .OrderByDescending(m => m.Fecha)
            .Select(m => (DateTimeOffset?)m.UpdatedAt!)
            .FirstOrDefaultAsync(ct);
        return new ResumenMonitoriaDto(logsUlt24h, logsError24h, incAbiertos, incCriticos, ultimaMetricaAt);
    }

    // =======================================================================
    // Jobs nocturnos
    // =======================================================================

    public async Task<IReadOnlyList<JobEjecucionDto>> ListarJobsAsync(
        string? jobName, int limite, CancellationToken ct)
    {
        IQueryable<JobEjecucion> q = _db.JobEjecuciones.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(jobName)) q = q.Where(j => j.JobName == jobName);
        var rows = await q.OrderByDescending(j => j.IniciadoAt)
            .Take(Math.Clamp(limite, 1, 500)).ToListAsync(ct);
        return rows.Select(j => new JobEjecucionDto(
            j.Id, j.JobName, j.IniciadoAt, j.CompletadoAt, j.Estado,
            j.ResultadoJson, j.Error, j.EjecutadoPorHost,
            j.CompletadoAt is { } c ? (int)(c - j.IniciadoAt).TotalMilliseconds : null)).ToList();
    }

    public async Task<IReadOnlyList<JobEstadoDto>> GetEstadoJobsAsync(CancellationToken ct)
    {
        // Conocemos los jobs registrados via convencion del nombre. Si quieres
        // hacerlo dinamico, el scheduler puede exponer la lista. Por ahora hardcoded.
        var conocidos = new (string Name, int FrecMin)[]
        {
            ("PqrsdCierreNocturno", 60 * 6),
            ("MetricasDiarias", 60 * 12)
        };

        var resultado = new List<JobEstadoDto>();
        var hace7d = DateTimeOffset.UtcNow.AddDays(-7);
        foreach (var (name, frec) in conocidos)
        {
            var ultima = await _db.JobEjecuciones.AsNoTracking()
                .Where(j => j.JobName == name)
                .OrderByDescending(j => j.IniciadoAt).FirstOrDefaultAsync(ct);
            var exitosa = await _db.JobEjecuciones.AsNoTracking()
                .Where(j => j.JobName == name && j.Estado == EstadoEjecucionJob.Exitoso)
                .OrderByDescending(j => j.IniciadoAt).FirstOrDefaultAsync(ct);
            var fallidas = await _db.JobEjecuciones.AsNoTracking()
                .CountAsync(j => j.JobName == name
                                 && j.Estado == EstadoEjecucionJob.Fallido
                                 && j.IniciadoAt >= hace7d, ct);
            resultado.Add(new JobEstadoDto(name, frec,
                ultima?.IniciadoAt, ultima?.Estado, exitosa?.IniciadoAt, fallidas));
        }
        return resultado;
    }

    // =======================================================================
    // Helpers
    // =======================================================================

    private static SistemaIncidenteDto Map(SistemaIncidente i) =>
        new(i.Id, i.Severidad, i.Estado, i.Titulo, i.Descripcion, i.ServicioAfectado,
            i.TenantImpactadoId, i.AsignadoSuperAdminId, i.DetectadoAt, i.ResueltoAt,
            i.CausaRaiz, i.SolucionAplicada, i.CreatedAt);

    private static MetricaUsoDiariaDto MapMetrica(MetricaUsoDiaria m) =>
        new(m.Fecha, m.TotalTenants, m.TenantsActivos, m.TotalOrganizaciones,
            m.TotalUsuarios, m.TotalSuperAdmins, m.TareasCreadas24h, m.PqrsdsRadicadas24h,
            m.ComunicadosEnviados24h, m.NotificacionesDespachadas24h,
            m.IncidentesAbiertos, m.IncidentesCriticos);
}
