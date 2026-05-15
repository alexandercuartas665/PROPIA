using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.PanelConsolidado;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.PanelConsolidado;

/// <summary>
/// Modulo 1.1 Panel Consolidado (Capa 1) - MVP.
/// Lee de panel_snapshot_copropiedades. El recalculo se dispara on-demand
/// (en produccion sera job programado).
/// </summary>
public class PanelConsolidadoService : IPanelConsolidadoService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _http;

    public PanelConsolidadoService(PropiaDbContext db, ITenantContext tenantContext, IHttpContextAccessor http)
    {
        _db = db;
        _tenantContext = tenantContext;
        _http = http;
    }

    private async Task<Guid> GetOrganizacionActivaAsync(CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentTenantId
            ?? throw new InvalidOperationException("No hay copropiedad activa en la sesion.");
        var tenant = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == tenantId, ct)
            ?? throw new InvalidOperationException("Tenant invalido.");
        return tenant.OrganizacionId
            ?? throw new InvalidOperationException("La copropiedad no tiene organizacion vinculada.");
    }

    public async Task<PanelResumenDto> GetPanelAsync(CancellationToken ct)
    {
        var orgId = await GetOrganizacionActivaAsync(ct);

        // Asegurar snapshot al primer acceso (MVP: lazy recalc)
        var existeAlguno = await _db.PanelSnapshots.AnyAsync(s => s.OrganizacionId == orgId, ct);
        if (!existeAlguno)
            await RecalcularSnapshotsAsync(ct);

        var snapshots = await _db.PanelSnapshots.AsNoTracking()
            .Where(s => s.OrganizacionId == orgId)
            .Include(s => s.Tenant)
            .OrderBy(s => s.EstadoSalud).ThenBy(s => s.Tenant!.Nombre)
            .ToListAsync(ct);

        var tenantIds = snapshots.Select(s => s.TenantId).ToList();
        var unidadesPorTenant = await _db.UnidadesPrivadas.IgnoreQueryFilters().AsNoTracking()
            .Where(u => tenantIds.Contains(u.TenantId))
            .GroupBy(u => u.TenantId)
            .Select(g => new { g.Key, Cant = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Cant, ct);

        var tarjetas = snapshots.Select(s => new PanelTarjetaDto(
            s.TenantId, s.Tenant!.Nombre, s.Tenant.Ciudad,
            s.Tenant.TipoCopropiedad?.ToString(),
            s.Tenant.FotoFachadaUrl, s.Tenant.LogoUrl,
            unidadesPorTenant.GetValueOrDefault(s.TenantId, 0),
            s.EstadoSalud, s.AlertasCriticas, s.TareasVencidas, s.PqrsdSinResponder,
            s.RecaudoMesPorcentaje, s.CarteraVencidaCop,
            s.ProximoEventoFecha, s.ProximoEventoLabel,
            s.CalculadoAt)).ToList();

        var kpis = new PanelKpisGlobalesDto(
            snapshots.Count,
            snapshots.Count(s => s.EstadoSalud == EstadoSaludCopropiedad.Rojo),
            snapshots.Sum(s => s.TareasVencidas),
            snapshots.Sum(s => s.PqrsdSinResponder),
            snapshots.Where(s => s.RecaudoMesPorcentaje.HasValue).Any()
                ? snapshots.Where(s => s.RecaudoMesPorcentaje.HasValue).Average(s => s.RecaudoMesPorcentaje!.Value)
                : 0m,
            snapshots.Sum(s => s.CarteraVencidaCop ?? 0m));

        var alertasCruzadas = new List<PanelAlertaCruzadaDto>();
        var conTareasVencidas = snapshots.Where(s => s.TareasVencidas > 0).ToList();
        if (conTareasVencidas.Any())
            alertasCruzadas.Add(new PanelAlertaCruzadaDto(
                "Tareas vencidas",
                conTareasVencidas.Sum(s => s.TareasVencidas),
                $"{conTareasVencidas.Sum(s => s.TareasVencidas)} tareas vencidas en {conTareasVencidas.Count} copropiedades",
                conTareasVencidas.Select(s => s.Tenant!.Nombre).ToList()));
        var conPqrsd = snapshots.Where(s => s.PqrsdSinResponder > 0).ToList();
        if (conPqrsd.Any())
            alertasCruzadas.Add(new PanelAlertaCruzadaDto(
                "PQRSD vencidas",
                conPqrsd.Sum(s => s.PqrsdSinResponder),
                $"{conPqrsd.Sum(s => s.PqrsdSinResponder)} PQRSD sin atender en {conPqrsd.Count} copropiedades",
                conPqrsd.Select(s => s.Tenant!.Nombre).ToList()));
        var enCritico = snapshots.Where(s => s.EstadoSalud == EstadoSaludCopropiedad.Rojo).ToList();
        if (enCritico.Any())
            alertasCruzadas.Add(new PanelAlertaCruzadaDto(
                "Copropiedades criticas",
                enCritico.Count,
                $"{enCritico.Count} copropiedades en estado critico",
                enCritico.Select(s => s.Tenant!.Nombre).ToList()));

        return new PanelResumenDto(kpis, tarjetas, alertasCruzadas);
    }

    public async Task<int> RecalcularSnapshotsAsync(CancellationToken ct)
    {
        var orgId = await GetOrganizacionActivaAsync(ct);
        // Tenants activos de la organizacion
        var tenants = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .Where(t => t.OrganizacionId == orgId && t.Estado == EstadoCopropiedad.Activa)
            .Select(t => new { t.Id, t.Nombre })
            .ToListAsync(ct);

        var prev = await _db.PanelSnapshots
            .Where(s => s.OrganizacionId == orgId)
            .ToDictionaryAsync(s => s.TenantId, ct);

        int actualizadas = 0;
        foreach (var t in tenants)
        {
            // Lee de tablas operativas (bypass query filter porque snapshot agrega cross-tenant)
            var tareasVencidas = await _db.Tareas.IgnoreQueryFilters()
                .CountAsync(x => x.TenantId == t.Id
                    && !x.Estado!.EsTerminal
                    && x.FechaVencimiento.HasValue && x.FechaVencimiento.Value < DateOnly.FromDateTime(DateTime.UtcNow), ct);
            var alertasActivas = await _db.AlertasCopropiedad.IgnoreQueryFilters()
                .CountAsync(x => x.TenantId == t.Id && x.Activa, ct);
            // PQRSD: en MVP usamos tareas con prefijo "PQRSD" o tipo etiqueta - placeholder simple
            var pqrsd = 0;

            // Recaudo (modulo 2.6 - placeholder simple usando ultima liquidacion)
            decimal? recaudoPct = await CalcularRecaudoMesAsync(t.Id, ct);

            EstadoSaludCopropiedad salud;
            if (alertasActivas >= 3 || tareasVencidas >= 5 || (recaudoPct.HasValue && recaudoPct < 30))
                salud = EstadoSaludCopropiedad.Rojo;
            else if (alertasActivas > 0 || tareasVencidas > 0 || (recaudoPct.HasValue && recaudoPct < 70))
                salud = EstadoSaludCopropiedad.Amarillo;
            else
                salud = EstadoSaludCopropiedad.Verde;

            if (prev.TryGetValue(t.Id, out var snap))
            {
                snap.EstadoSalud = salud;
                snap.AlertasCriticas = alertasActivas;
                snap.TareasVencidas = tareasVencidas;
                snap.PqrsdSinResponder = pqrsd;
                snap.RecaudoMesPorcentaje = recaudoPct;
                snap.CarteraVencidaCop = null;
                snap.CalculadoAt = DateTimeOffset.UtcNow;
                snap.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                _db.PanelSnapshots.Add(new PanelSnapshotCopropiedad
                {
                    OrganizacionId = orgId,
                    TenantId = t.Id,
                    EstadoSalud = salud,
                    AlertasCriticas = alertasActivas,
                    TareasVencidas = tareasVencidas,
                    PqrsdSinResponder = pqrsd,
                    RecaudoMesPorcentaje = recaudoPct,
                    CalculadoAt = DateTimeOffset.UtcNow
                });
            }
            actualizadas++;
        }

        // Eliminar snapshots de PHs que ya no estan vinculadas a la org
        var tenantIds = tenants.Select(t => t.Id).ToHashSet();
        var orfans = prev.Values.Where(s => !tenantIds.Contains(s.TenantId)).ToList();
        if (orfans.Any()) _db.PanelSnapshots.RemoveRange(orfans);

        await _db.SaveChangesAsync(ct);
        return actualizadas;
    }

    private async Task<decimal?> CalcularRecaudoMesAsync(Guid tenantId, CancellationToken ct)
    {
        // Placeholder MVP: si hay liquidaciones, devolver % pagadas vs total
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var periodo = new DateOnly(hoy.Year, hoy.Month, 1);
        var totalUnidades = await _db.LiquidacionUnidades.IgnoreQueryFilters()
            .CountAsync(lu => lu.TenantId == tenantId && lu.Liquidacion!.Periodo == periodo, ct);
        if (totalUnidades == 0) return null;
        var pagadas = await _db.LiquidacionUnidades.IgnoreQueryFilters()
            .CountAsync(lu => lu.TenantId == tenantId && lu.Liquidacion!.Periodo == periodo && lu.EstadoPago == EstadoPagoLiquidacion.Pagado, ct);
        return Math.Round((decimal)pagadas / totalUnidades * 100m, 2);
    }

    public async Task<IReadOnlyList<PanelFeedEventoDto>> ListarFeedAsync(int limit, CancellationToken ct)
    {
        var orgId = await GetOrganizacionActivaAsync(ct);
        var rows = await (
            from f in _db.PanelFeedEventos.AsNoTracking()
            join t in _db.Tenants.IgnoreQueryFilters().AsNoTracking() on f.TenantId equals t.Id
            where f.OrganizacionId == orgId
            orderby f.OcurridoAt descending
            select new PanelFeedEventoDto(f.Id, f.TenantId, t.Nombre, f.TipoEvento, f.Descripcion, f.UrlAccion, f.OcurridoAt)
        ).Take(Math.Clamp(limit, 1, 100)).ToListAsync(ct);
        return rows;
    }
}
