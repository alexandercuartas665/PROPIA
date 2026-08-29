using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.DashboardCopropiedad;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.DashboardCopropiedad;

/// <summary>Modulo 2.2 Dashboard de la Copropiedad - MVP del spec v1.0.</summary>
public class DashboardCopropiedadService : IDashboardCopropiedadService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _http;

    public DashboardCopropiedadService(PropiaDbContext db, ITenantContext tenantContext, IHttpContextAccessor http)
    {
        _db = db;
        _tenantContext = tenantContext;
        _http = http;
    }

    private Guid GetUsuarioActualId()
    {
        var sub = _http.HttpContext?.User?.FindFirstValue("user_id");
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    public async Task<DashboardResumenDto> GetResumenAsync(CancellationToken ct)
    {
        // 1. Alertas activas
        var alertas = await _db.AlertasCopropiedad.AsNoTracking()
            .Where(a => a.Activa).OrderByDescending(a => a.CreatedAt).Take(20)
            .Select(a => new AlertaDashboardDto(a.Id, a.Tipo, a.Severidad, a.Titulo, a.Descripcion, a.UrlAccion, a.CreatedAt))
            .ToListAsync(ct);

        // 2. Bloque operativo - tareas
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var tareasActivas = await _db.Tareas.AsNoTracking().Where(t => !t.Estado!.EsTerminal).CountAsync(ct);
        var tareasVencidas = await _db.Tareas.AsNoTracking()
            .Where(t => !t.Estado!.EsTerminal && t.FechaVencimiento.HasValue && t.FechaVencimiento.Value < hoy)
            .CountAsync(ct);
        var tareasUrgentes = await (
            from t in _db.Tareas.AsNoTracking().Where(t => !t.Estado!.EsTerminal)
            join e in _db.TareasEstados on t.EstadoId equals e.Id
            orderby (t.FechaVencimiento ?? new DateOnly(9999, 1, 1)), t.Prioridad, t.CreatedAt
            select new TareaResumenDto(
                t.Id, t.NumeroTarea, t.Titulo, e.Nombre, t.Prioridad.ToString(),
                t.FechaVencimiento,
                t.FechaVencimiento.HasValue && t.FechaVencimiento.Value < hoy)
        ).Take(5).ToListAsync(ct);

        // 3. Bloque financiero - recaudo del mes (consulta directa al modulo 2.6)
        var periodoActual = new DateOnly(hoy.Year, hoy.Month, 1);
        decimal? recaudoPct = null;
        int unidadesEnMora = 0;
        var totalLiqUnidades = await _db.LiquidacionUnidades.AsNoTracking()
            .CountAsync(lu => lu.Liquidacion!.Periodo == periodoActual, ct);
        if (totalLiqUnidades > 0)
        {
            var pagadas = await _db.LiquidacionUnidades.AsNoTracking()
                .CountAsync(lu => lu.Liquidacion!.Periodo == periodoActual && lu.EstadoPago == EstadoPagoLiquidacion.Pagado, ct);
            recaudoPct = Math.Round((decimal)pagadas / totalLiqUnidades * 100m, 2);
            unidadesEnMora = await _db.LiquidacionUnidades.AsNoTracking()
                .CountAsync(lu => lu.Liquidacion!.Periodo == periodoActual && lu.EstadoPago == EstadoPagoLiquidacion.Vencido, ct);
        }
        var moduloPresupuestoConfig = await _db.Presupuestos.AsNoTracking()
            .AnyAsync(p => p.Estado == EstadoPresupuesto.EnEjecucion, ct);

        // 4. Resumen copropiedad
        var totalUnidades = await _db.UnidadesPrivadas.CountAsync(ct);
        var torresTotal = await _db.Torres.CountAsync(ct);
        var zonasTotal = await _db.ZonasComunes.CountAsync(ct);

        // 5. Feed
        var feed = await _db.ActividadFeed.AsNoTracking()
            .OrderByDescending(f => f.OcurridoAt).Take(10)
            .Select(f => new ActividadFeedDto(f.Id, f.Tipo, f.ActorNombre, f.Descripcion, f.ModuloCodigo, f.UrlItem, f.OcurridoAt))
            .ToListAsync(ct);

        // 6. Contratos proximos a vencer (Ola 3): semaforo amarillo/rojo por % de dias totales.
        var hoyC = DateOnly.FromDateTime(DateTime.UtcNow);
        var contratosPorVencer = (await _db.ContratosServicio.AsNoTracking()
                .Where(c => c.FechaFin != null)
                .Select(c => new { c.Id, c.Proveedor, c.FechaInicio, c.FechaFin })
                .ToListAsync(ct))
            .Select(c => new
            {
                c.Id,
                c.Proveedor,
                c.FechaFin,
                Sem = MiCopropiedad.MiCopropiedadService.CalcularSemaforoContrato(c.FechaInicio, c.FechaFin, hoyC),
                Dias = c.FechaFin!.Value.DayNumber - hoyC.DayNumber
            })
            .Where(x => x.Sem is SemaforoContrato.Amarillo or SemaforoContrato.Rojo)
            .OrderBy(x => x.Dias)
            .Take(8)
            .Select(x => new ContratoPorVencerDto(x.Id, x.Proveedor, x.FechaFin, x.Dias, x.Sem))
            .ToList();

        // Polizas proximas a vencer (Ola 4c).
        var polizasPorVencer = (await _db.Polizas.AsNoTracking()
                .Where(p => p.FechaFin != null)
                .Select(p => new { p.Id, p.Aseguradora, p.FechaInicio, p.FechaFin })
                .ToListAsync(ct))
            .Select(p => new
            {
                p.Id,
                p.Aseguradora,
                p.FechaFin,
                Sem = MiCopropiedad.MiCopropiedadService.CalcularSemaforoContrato(p.FechaInicio ?? p.FechaFin!.Value, p.FechaFin, hoyC),
                Dias = p.FechaFin!.Value.DayNumber - hoyC.DayNumber
            })
            .Where(x => x.Sem is SemaforoContrato.Amarillo or SemaforoContrato.Rojo)
            .OrderBy(x => x.Dias)
            .Take(8)
            .Select(x => new ContratoPorVencerDto(x.Id, x.Aseguradora, x.FechaFin, x.Dias, x.Sem))
            .ToList();

        // 7. Dashboard v2 - distribucion de tareas activas por etapa (para la grafica de barras).
        //    Se agrupa por NOMBRE de etapa (los tableros repiten nombres tipo "Pendiente"): la vista
        //    es un panorama global de la copropiedad, no de un tablero puntual.
        var tareasPorEtapa = (await (
                from t in _db.Tareas.AsNoTracking().Where(t => !t.Estado!.EsTerminal)
                join e in _db.TareasEstados on t.EstadoId equals e.Id
                group e by e.Nombre into g
                select new { Nombre = g.Key, Color = g.Min(x => x.Color), Cantidad = g.Count() }
            ).ToListAsync(ct))
            .OrderByDescending(x => x.Cantidad)
            .Take(8)
            .Select(x => new TareasPorEtapaDto(x.Nombre, x.Color, x.Cantidad))
            .ToList();

        // 8. Dashboard v2 - PQRSD en gestion (abiertas = ni cerradas ni via agotada, sin archivar).
        var pqrsAbiertasQ = _db.PqrsdExpedientes.AsNoTracking()
            .Where(p => !p.Archivado && p.Estado != EstadoPqrsd.Cerrada && p.Estado != EstadoPqrsd.ViaInternaAgotada);
        var pqrsAbiertas = await pqrsAbiertasQ.CountAsync(ct);
        var pqrsVencidas = await pqrsAbiertasQ.CountAsync(p => p.FechaVencimiento < hoy, ct);
        var pqrsPorVencer = await pqrsAbiertasQ.CountAsync(p => p.FechaVencimiento >= hoy && p.FechaVencimiento.DayNumber - hoy.DayNumber <= 3, ct);
        var pqrsProximas = (await pqrsAbiertasQ
                .OrderBy(p => p.FechaVencimiento)
                .Take(6)
                .Select(p => new { p.Id, p.NumeroRadicado, p.Tipo, p.Descripcion, p.FechaVencimiento })
                .ToListAsync(ct))
            .Select(p =>
            {
                var dias = p.FechaVencimiento.DayNumber - hoy.DayNumber;
                var sem = dias < 0 ? "rojo" : dias <= 3 ? "amarillo" : "verde";
                var resumen = p.Descripcion.Length > 70 ? p.Descripcion[..70] + "..." : p.Descripcion;
                return new PqrDashboardDto(p.Id, p.NumeroRadicado, p.Tipo.ToString(), resumen, p.FechaVencimiento, dias, sem);
            })
            .ToList();

        // 9. Dashboard v2 - novedades de porteria recientes (con nombre del guarda, global sin RLS).
        var hoyUtc = DateTime.UtcNow.Date;
        var novedadesHoy = await _db.NovedadesTurno.AsNoTracking()
            .CountAsync(n => n.CreatedAt >= hoyUtc, ct);
        var novedadesRaw = await _db.NovedadesTurno.AsNoTracking()
            .OrderByDescending(n => n.CreatedAt).Take(6)
            .Select(n => new { n.Id, n.Tipo, n.Descripcion, n.GuardaPersonaId, n.CreatedAt, n.TareaId })
            .ToListAsync(ct);
        var guardaIds = novedadesRaw.Select(n => n.GuardaPersonaId).Distinct().ToList();
        var guardas = await _db.Personas.AsNoTracking()
            .Where(p => guardaIds.Contains(p.Id))
            .Select(p => new { p.Id, Nombre = (p.Nombres + " " + p.Apellidos).Trim() })
            .ToDictionaryAsync(p => p.Id, p => p.Nombre, ct);
        var novedadesPorteria = novedadesRaw
            .Select(n => new NovedadPorteriaDashboardDto(
                n.Id, n.Tipo.ToString(),
                n.Descripcion.Length > 90 ? n.Descripcion[..90] + "..." : n.Descripcion,
                guardas.TryGetValue(n.GuardaPersonaId, out var g) ? g : null,
                n.CreatedAt, n.TareaId != null))
            .ToList();

        // 10. Dashboard v3 - graficas. Volumenes acotados (6 meses / 8 semanas): se traen solo las
        //     fechas y se agrupa en memoria para no depender de traducciones EF de Year/Month/Date.
        var inicioSerie = new DateTimeOffset(new DateTime(hoy.Year, hoy.Month, 1, 0, 0, 0, DateTimeKind.Utc)).AddMonths(-5);
        var fTareas = await _db.Tareas.AsNoTracking().Where(t => t.CreatedAt >= inicioSerie).Select(t => t.CreatedAt).ToListAsync(ct);
        var fPqrs = await _db.PqrsdExpedientes.AsNoTracking().Where(p => p.CreatedAt >= inicioSerie).Select(p => p.CreatedAt).ToListAsync(ct);
        var fNovs = await _db.NovedadesTurno.AsNoTracking().Where(n => n.CreatedAt >= inicioSerie).Select(n => n.CreatedAt).ToListAsync(ct);

        var serieMensual = new List<SerieMensualDto>(6);
        for (var i = 0; i < 6; i++)
        {
            var mes = inicioSerie.AddMonths(i);
            serieMensual.Add(new SerieMensualDto(
                mes.Year, mes.Month,
                fTareas.Count(f => f.Year == mes.Year && f.Month == mes.Month),
                fPqrs.Count(f => f.Year == mes.Year && f.Month == mes.Month),
                fNovs.Count(f => f.Year == mes.Year && f.Month == mes.Month)));
        }

        var pqrsPorTipo = (await _db.PqrsdExpedientes.AsNoTracking()
                .GroupBy(p => p.Tipo)
                .Select(g => new { Tipo = g.Key, C = g.Count() })
                .ToListAsync(ct))
            .OrderByDescending(x => x.C)
            .Select(x => new PqrsPorTipoDto(x.Tipo.ToString(), x.C))
            .ToList();

        // Heatmap: 8 semanas (56 dias) de actividad combinada tareas+pqrs+novedades por dia.
        var inicioHm = DateTimeOffset.UtcNow.Date.AddDays(-55);
        var porDia = fTareas.Concat(fPqrs).Concat(fNovs)
            .Where(f => f >= inicioHm)
            .GroupBy(f => DateOnly.FromDateTime(f.UtcDateTime.Date))
            .ToDictionary(g => g.Key, g => g.Count());
        var actividadDiaria = new List<ActividadDiaDto>(56);
        for (var i = 0; i < 56; i++)
        {
            var dia = DateOnly.FromDateTime(inicioHm.AddDays(i));
            actividadDiaria.Add(new ActividadDiaDto(dia, porDia.GetValueOrDefault(dia)));
        }

        return new DashboardResumenDto(
            alertas,
            recaudoPct, unidadesEnMora, null,
            tareasActivas, tareasVencidas, tareasUrgentes,
            totalUnidades, torresTotal, zonasTotal,
            feed,
            moduloPresupuestoConfig,
            contratosPorVencer,
            polizasPorVencer,
            tareasPorEtapa,
            pqrsAbiertas, pqrsPorVencer, pqrsVencidas, pqrsProximas,
            novedadesHoy, novedadesPorteria,
            serieMensual, pqrsPorTipo, actividadDiaria);
    }

    public async Task<IReadOnlyList<AlertaDashboardDto>> ListarAlertasAsync(CancellationToken ct)
        => await _db.AlertasCopropiedad.AsNoTracking()
            .OrderByDescending(a => a.Activa).ThenByDescending(a => a.CreatedAt)
            .Select(a => new AlertaDashboardDto(a.Id, a.Tipo, a.Severidad, a.Titulo, a.Descripcion, a.UrlAccion, a.CreatedAt))
            .ToListAsync(ct);

    public async Task<AlertaDashboardDto> CrearAlertaAsync(CrearAlertaRequest req, CancellationToken ct)
    {
        var a = new AlertaCopropiedad
        {
            Tipo = req.Tipo,
            Severidad = req.Severidad,
            Titulo = req.Titulo,
            Descripcion = req.Descripcion,
            UrlAccion = req.UrlAccion,
            Activa = true
        };
        _db.AlertasCopropiedad.Add(a);
        await _db.SaveChangesAsync(ct);
        return new AlertaDashboardDto(a.Id, a.Tipo, a.Severidad, a.Titulo, a.Descripcion, a.UrlAccion, a.CreatedAt);
    }

    public async Task<bool> ResolverAlertaAsync(Guid id, CancellationToken ct)
    {
        var a = await _db.AlertasCopropiedad.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return false;
        a.Activa = false;
        a.ResueltaAt = DateTimeOffset.UtcNow;
        a.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<ActividadFeedDto>> ListarFeedAsync(int limit, CancellationToken ct)
        => await _db.ActividadFeed.AsNoTracking()
            .OrderByDescending(f => f.OcurridoAt).Take(Math.Clamp(limit, 1, 100))
            .Select(f => new ActividadFeedDto(f.Id, f.Tipo, f.ActorNombre, f.Descripcion, f.ModuloCodigo, f.UrlItem, f.OcurridoAt))
            .ToListAsync(ct);

    public async Task<ActividadFeedDto> RegistrarEventoFeedAsync(CrearEventoFeedRequest req, CancellationToken ct)
    {
        var f = new ActividadFeed
        {
            Tipo = req.Tipo,
            ActorPersonaId = null,
            ActorNombre = null,
            Descripcion = req.Descripcion,
            ModuloCodigo = req.ModuloCodigo,
            UrlItem = req.UrlItem,
            OcurridoAt = DateTimeOffset.UtcNow
        };
        _db.ActividadFeed.Add(f);
        await _db.SaveChangesAsync(ct);
        return new ActividadFeedDto(f.Id, f.Tipo, f.ActorNombre, f.Descripcion, f.ModuloCodigo, f.UrlItem, f.OcurridoAt);
    }
}
