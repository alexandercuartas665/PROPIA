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

        return new DashboardResumenDto(
            alertas,
            recaudoPct, unidadesEnMora, null,
            tareasActivas, tareasVencidas, tareasUrgentes,
            totalUnidades, torresTotal, zonasTotal,
            feed,
            moduloPresupuestoConfig,
            contratosPorVencer,
            polizasPorVencer);
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
