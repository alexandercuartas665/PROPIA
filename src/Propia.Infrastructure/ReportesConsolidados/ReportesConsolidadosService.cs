using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.ReportesConsolidados;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.ReportesConsolidados;

/// <summary>
/// Modulo 1.4 Reportes Consolidados (spec v1.0 MVP).
///
/// MVP scope: ver IReportesConsolidadosService.
/// </summary>
public class ReportesConsolidadosService : IReportesConsolidadosService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _http;

    public ReportesConsolidadosService(PropiaDbContext db, ITenantContext tenantContext, IHttpContextAccessor http)
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

    private async Task<Guid> GetOrganizacionIdActualAsync(CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentTenantId
            ?? throw new InvalidOperationException("No hay copropiedad activa.");
        var orgId = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId).Select(t => t.OrganizacionId).FirstOrDefaultAsync(ct);
        if (orgId is null)
            throw new InvalidOperationException("La copropiedad activa no esta vinculada a una organizacion.");
        return orgId.Value;
    }

    private async Task<List<Guid>> GetTenantsDeOrganizacionAsync(Guid orgId, CancellationToken ct) =>
        await _db.Tenants.AsNoTracking()
            .Where(t => t.OrganizacionId == orgId && t.Estado == EstadoCopropiedad.Activa)
            .Select(t => t.Id).ToListAsync(ct);

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // ===========================================================================
    // Plantillas base
    // ===========================================================================

    private static readonly PlantillaBaseDto[] Plantillas = new[]
    {
        new PlantillaBaseDto("salud_portafolio", CategoriaReporteConsolidado.SaludPortafolio,
            "Salud del portafolio",
            "Estado general de cada PH con semaforo + alertas activas + KPIs criticos agregados.",
            false),
        new PlantillaBaseDto("financiero_consolidado", CategoriaReporteConsolidado.FinancieroConsolidado,
            "Financiero consolidado",
            "Recaudo total del portafolio + cartera vencida agregada + ejecucion presupuestal global.",
            false),
        new PlantillaBaseDto("operativo_consolidado", CategoriaReporteConsolidado.OperativoConsolidado,
            "Operativo consolidado",
            "Tareas abiertas, vencidas y cerradas en todo el portafolio con drill-down a PH.",
            false),
        new PlantillaBaseDto("convivencia_pqrsd", CategoriaReporteConsolidado.ConvivenciaPqrsd,
            "Convivencia y PQRSD",
            "Volumen de PQRSD activas y cerradas + tiempos de resolucion + PH con mayor carga.",
            false),
        new PlantillaBaseDto("desempeno_equipo", CategoriaReporteConsolidado.DesempenoEquipo,
            "Desempeno del equipo",
            "Metricas por colaborador: tareas asignadas/completadas/vencidas + PQRSD gestionadas + tiempos.",
            true)   // RN-06: siempre tiene datos nominativos
    };

    public Task<IReadOnlyList<PlantillaBaseDto>> ListarPlantillasBaseAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<PlantillaBaseDto>>(Plantillas);

    // ===========================================================================
    // Reportes guardados
    // ===========================================================================

    public async Task<IReadOnlyList<OrgReporteDto>> ListarReportesAsync(CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var list = await _db.OrgReportes.AsNoTracking()
            .Where(r => r.OrganizacionId == orgId && r.Activo)
            .Select(r => new
            {
                r.Id,
                r.OrganizacionId,
                r.Nombre,
                r.Categoria,
                r.EsPlantillaBase,
                r.TieneDatosNominativos,
                r.CreadoPorUsuarioId,
                r.CreatedAt,
                r.UpdatedAt,
                NumGen = _db.OrgReporteGeneraciones.Count(g => g.ReporteId == r.Id)
            })
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .ToListAsync(ct);
        return list.Select(r => new OrgReporteDto(r.Id, r.OrganizacionId, r.Nombre, r.Categoria,
            r.EsPlantillaBase, r.TieneDatosNominativos, r.CreadoPorUsuarioId, r.CreatedAt, r.UpdatedAt, r.NumGen)).ToList();
    }

    public async Task<OrgReporteDto?> GetReporteAsync(Guid id, CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var r = await _db.OrgReportes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OrganizacionId == orgId && x.Activo, ct);
        if (r is null) return null;
        var numGen = await _db.OrgReporteGeneraciones.CountAsync(g => g.ReporteId == r.Id, ct);
        return new OrgReporteDto(r.Id, r.OrganizacionId, r.Nombre, r.Categoria,
            r.EsPlantillaBase, r.TieneDatosNominativos, r.CreadoPorUsuarioId, r.CreatedAt, r.UpdatedAt, numGen);
    }

    public async Task<OrgReporteDto> CrearReporteAsync(CrearReporteRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre obligatorio.");
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var tieneNominativos = DetectarDatosNominativos(req.Categoria);
        var r = new OrgReporte
        {
            OrganizacionId = orgId,
            Nombre = req.Nombre.Trim(),
            Categoria = req.Categoria,
            EsPlantillaBase = false,
            TieneDatosNominativos = tieneNominativos,
            ConfiguracionJson = string.IsNullOrWhiteSpace(req.ConfiguracionJson) ? "{}" : req.ConfiguracionJson,
            CreadoPorUsuarioId = GetUsuarioActualId(),
            Activo = true
        };
        _db.OrgReportes.Add(r);
        await _db.SaveChangesAsync(ct);
        return (await GetReporteAsync(r.Id, ct))!;
    }

    /// <summary>Detecta automaticamente si la categoria implica datos nominativos (RN-04 + RN-06).</summary>
    private static bool DetectarDatosNominativos(CategoriaReporteConsolidado cat) =>
        cat == CategoriaReporteConsolidado.DesempenoEquipo;

    public async Task<bool> ActualizarReporteAsync(Guid id, ActualizarReporteRequest req, CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var r = await _db.OrgReportes.FirstOrDefaultAsync(x => x.Id == id && x.OrganizacionId == orgId, ct);
        if (r is null) return false;
        if (r.EsPlantillaBase) throw new InvalidOperationException("Las plantillas base no son editables.");
        r.Nombre = req.Nombre.Trim();
        r.ConfiguracionJson = req.ConfiguracionJson;
        r.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarReporteAsync(Guid id, CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var r = await _db.OrgReportes.FirstOrDefaultAsync(x => x.Id == id && x.OrganizacionId == orgId, ct);
        if (r is null) return false;
        if (r.EsPlantillaBase) throw new InvalidOperationException("Las plantillas base no se eliminan.");
        r.Activo = false;
        r.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===========================================================================
    // Generacion + historial
    // ===========================================================================

    public async Task<GeneracionDetalleDto> GenerarAsync(GenerarReporteRequest req, CancellationToken ct)
    {
        if (req.PeriodoHasta < req.PeriodoDesde)
            throw new InvalidOperationException("Periodo invalido.");
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var rep = await _db.OrgReportes.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == req.ReporteId && r.OrganizacionId == orgId && r.Activo, ct)
            ?? throw new InvalidOperationException("Reporte no encontrado.");

        var g = new OrgReporteGeneracion
        {
            ReporteId = rep.Id,
            OrganizacionId = orgId,
            Origen = OrigenGeneracionConsolidada.Manual,
            GeneradoPorUsuarioId = GetUsuarioActualId(),
            PeriodoDesde = req.PeriodoDesde,
            PeriodoHasta = req.PeriodoHasta,
            Estado = EstadoGeneracionConsolidada.Generando,
            Intentos = 1
        };
        _db.OrgReporteGeneraciones.Add(g);
        await _db.SaveChangesAsync(ct);

        try
        {
            var resultado = await ResolverReporteAsync(rep.Categoria, req.PeriodoDesde, req.PeriodoHasta, ct);
            g.ResultadoJson = JsonSerializer.Serialize(resultado, JsonOpts);
            g.Estado = EstadoGeneracionConsolidada.Listo;
            g.GeneradoAt = DateTimeOffset.UtcNow;
            g.UrlExpiracion = DateTimeOffset.UtcNow.AddDays(30);
        }
        catch (Exception ex)
        {
            g.Estado = EstadoGeneracionConsolidada.Error;
            g.ErrorDetalle = ex.Message.Length > 1900 ? ex.Message[..1900] : ex.Message;
        }
        g.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return (await GetGeneracionAsync(g.Id, ct))!;
    }

    private async Task<object> ResolverReporteAsync(CategoriaReporteConsolidado cat, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        return cat switch
        {
            CategoriaReporteConsolidado.SaludPortafolio => await GetIndicadoresPortafolioAsync(ct),
            CategoriaReporteConsolidado.FinancieroConsolidado => await GetFinancieroConsolidadoAsync(desde, hasta, ct),
            CategoriaReporteConsolidado.OperativoConsolidado => await GetOperativoConsolidadoAsync(desde, hasta, ct),
            CategoriaReporteConsolidado.ConvivenciaPqrsd => await GetPqrsdConsolidadoAsync(desde, hasta, ct),
            CategoriaReporteConsolidado.DesempenoEquipo => await GetIndicadoresEquipoAsync(desde, hasta, ct),
            _ => new { mensaje = "Categoria personalizada - sin resolver en MVP" }
        };
    }

    public async Task<IReadOnlyList<GeneracionListaDto>> ListarHistorialAsync(Guid? reporteId, CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var q = _db.OrgReporteGeneraciones.AsNoTracking().Where(g => g.OrganizacionId == orgId);
        if (reporteId is { } rid) q = q.Where(g => g.ReporteId == rid);
        return await q.OrderByDescending(g => g.CreatedAt).Take(100)
            .Select(g => new GeneracionListaDto(
                g.Id, g.ReporteId,
                _db.OrgReportes.Where(r => r.Id == g.ReporteId).Select(r => r.Nombre).FirstOrDefault() ?? "",
                _db.OrgReportes.Where(r => r.Id == g.ReporteId).Select(r => r.Categoria).FirstOrDefault(),
                g.Origen, g.Estado, g.PeriodoDesde, g.PeriodoHasta,
                g.CreatedAt, g.GeneradoAt, g.Intentos))
            .ToListAsync(ct);
    }

    public async Task<GeneracionDetalleDto?> GetGeneracionAsync(Guid id, CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var g = await _db.OrgReporteGeneraciones.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OrganizacionId == orgId, ct);
        if (g is null) return null;
        var rep = await _db.OrgReportes.AsNoTracking().Where(r => r.Id == g.ReporteId)
            .Select(r => new { r.Nombre, r.Categoria }).FirstOrDefaultAsync(ct);
        return new GeneracionDetalleDto(g.Id, g.ReporteId, rep?.Nombre ?? "",
            rep?.Categoria ?? CategoriaReporteConsolidado.Personalizado,
            g.Origen, g.Estado, g.PeriodoDesde, g.PeriodoHasta,
            g.ResultadoJson, g.UrlPdf, g.UrlExcel, g.UrlExpiracion,
            g.ErrorDetalle, g.Intentos, g.CreatedAt, g.GeneradoAt);
    }

    public async Task<GeneracionDetalleDto> RegenerarAsync(Guid generacionId, CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var prev = await _db.OrgReporteGeneraciones.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == generacionId && x.OrganizacionId == orgId, ct)
            ?? throw new InvalidOperationException("Generacion no encontrada.");
        return await GenerarAsync(new GenerarReporteRequest(prev.ReporteId, prev.PeriodoDesde, prev.PeriodoHasta), ct);
    }

    // ===========================================================================
    // Indicadores consolidados cross-tenant
    // ===========================================================================

    public async Task<IndicadoresPortafolioDto> GetIndicadoresPortafolioAsync(CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var tenants = await GetTenantsDeOrganizacionAsync(orgId, ct);
        if (tenants.Count == 0) return new IndicadoresPortafolioDto(0, 0, 0, 0, 0, 0);

        // Alertas activas + tareas vencidas como medida operativa (semaforo simplificado)
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var alertas = await _db.AlertasCopropiedad.IgnoreQueryFilters().AsNoTracking()
            .Where(a => tenants.Contains(a.TenantId) && a.Activa)
            .GroupBy(a => a.TenantId).Select(g => new { TenantId = g.Key, Total = g.Count() })
            .ToListAsync(ct);
        var tareasVencidas = await _db.Tareas.IgnoreQueryFilters().AsNoTracking()
            .Where(t => tenants.Contains(t.TenantId)
                       && t.FechaVencimiento != null && t.FechaVencimiento < hoy
                       && t.FechaCompletada == null)
            .GroupBy(t => t.TenantId).Select(g => new { TenantId = g.Key, Total = g.Count() })
            .ToListAsync(ct);

        int verdes = 0, amarillas = 0, rojas = 0;
        foreach (var tid in tenants)
        {
            var al = alertas.FirstOrDefault(x => x.TenantId == tid)?.Total ?? 0;
            var tv = tareasVencidas.FirstOrDefault(x => x.TenantId == tid)?.Total ?? 0;
            // semaforo simple: rojo si >=3 alertas o >=10 tareas vencidas, amarillo si hay alguna, verde sin nada
            if (al >= 3 || tv >= 10) rojas++;
            else if (al > 0 || tv > 0) amarillas++;
            else verdes++;
        }

        return new IndicadoresPortafolioDto(
            tenants.Count, verdes, amarillas, rojas,
            alertas.Sum(a => a.Total), tareasVencidas.Sum(t => t.Total));
    }

    public async Task<IndicadoresFinancieroConsolidadoDto> GetFinancieroConsolidadoAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var tenants = await GetTenantsDeOrganizacionAsync(orgId, ct);
        if (tenants.Count == 0)
            return new IndicadoresFinancieroConsolidadoDto(0, 0, 0, Array.Empty<RecaudoPorCopropiedadDto>());

        var liqs = await _db.LiquidacionUnidades.IgnoreQueryFilters().AsNoTracking()
            .Join(_db.Liquidaciones.IgnoreQueryFilters().AsNoTracking(),
                lu => lu.LiquidacionId, l => l.Id, (lu, l) => new { lu, l })
            .Where(x => tenants.Contains(x.lu.TenantId)
                       && x.l.Periodo >= desde && x.l.Periodo <= hasta)
            .Select(x => new { x.lu.TenantId, x.lu.Monto, x.lu.EstadoPago })
            .ToListAsync(ct);

        var nombres = await _db.Tenants.AsNoTracking()
            .Where(t => tenants.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Nombre, ct);

        var porCop = tenants.Select(tid =>
        {
            var sub = liqs.Where(x => x.TenantId == tid).ToList();
            var esperado = sub.Sum(x => x.Monto);
            var recibido = sub.Where(x => x.EstadoPago == EstadoPagoLiquidacion.Pagado).Sum(x => x.Monto);
            decimal? pct = esperado > 0 ? Math.Round(recibido / esperado * 100m, 2) : null;
            return new RecaudoPorCopropiedadDto(tid, nombres.GetValueOrDefault(tid, ""), recibido, esperado, pct);
        }).ToList();

        var mora = await _db.CarteraUnidades.IgnoreQueryFilters().AsNoTracking()
            .Where(c => tenants.Contains(c.TenantId))
            .SumAsync(c => (decimal?)(c.SaldoCapital + c.SaldoIntereses), ct) ?? 0;

        var totalEsp = porCop.Sum(x => x.Esperado);
        var totalRec = porCop.Sum(x => x.Recaudado);
        return new IndicadoresFinancieroConsolidadoDto(
            totalRec, mora,
            totalEsp > 0 ? Math.Round(totalRec / totalEsp * 100m, 2) : 0,
            porCop);
    }

    public async Task<IndicadoresOperativoConsolidadoDto> GetOperativoConsolidadoAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var tenants = await GetTenantsDeOrganizacionAsync(orgId, ct);
        if (tenants.Count == 0) return new IndicadoresOperativoConsolidadoDto(0, 0, 0, Array.Empty<TareasPorCopropiedadDto>());

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var desdeDt = new DateTimeOffset(DateTime.SpecifyKind(desde.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc));
        var hastaDt = new DateTimeOffset(DateTime.SpecifyKind(hasta.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc));

        var tareas = await _db.Tareas.IgnoreQueryFilters().AsNoTracking()
            .Where(t => tenants.Contains(t.TenantId))
            .Select(t => new { t.TenantId, t.FechaVencimiento, t.FechaCompletada })
            .ToListAsync(ct);

        var abiertas = tareas.Count(t => t.FechaCompletada == null);
        var vencidas = tareas.Count(t => t.FechaCompletada == null && t.FechaVencimiento != null && t.FechaVencimiento < hoy);
        var completadas30d = tareas.Count(t => t.FechaCompletada != null && t.FechaCompletada >= desdeDt && t.FechaCompletada <= hastaDt);

        var nombres = await _db.Tenants.AsNoTracking()
            .Where(t => tenants.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Nombre, ct);

        var porCop = tenants.Select(tid =>
        {
            var sub = tareas.Where(x => x.TenantId == tid).ToList();
            return new TareasPorCopropiedadDto(tid, nombres.GetValueOrDefault(tid, ""),
                sub.Count(x => x.FechaCompletada == null),
                sub.Count(x => x.FechaCompletada == null && x.FechaVencimiento != null && x.FechaVencimiento < hoy));
        }).ToList();

        return new IndicadoresOperativoConsolidadoDto(abiertas, vencidas, completadas30d, porCop);
    }

    public async Task<IndicadoresPqrsdConsolidadoDto> GetPqrsdConsolidadoAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var tenants = await GetTenantsDeOrganizacionAsync(orgId, ct);
        if (tenants.Count == 0) return new IndicadoresPqrsdConsolidadoDto(0, 0, 0, null, Array.Empty<PqrsdPorCopropiedadDto>());

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var pqrsd = await _db.PqrsdExpedientes.IgnoreQueryFilters().AsNoTracking()
            .Where(p => tenants.Contains(p.TenantId))
            .Select(p => new { p.TenantId, p.Tipo, p.Estado, p.FechaVencimiento, p.RespuestaAdminAt, p.CreatedAt })
            .ToListAsync(ct);

        var activas = pqrsd.Count(p => p.Estado == EstadoPqrsd.Recibida || p.Estado == EstadoPqrsd.EnGestion);
        var vencidas = pqrsd.Count(p => p.Estado != EstadoPqrsd.Cerrada && p.FechaVencimiento < hoy);
        var felicitaciones = pqrsd.Count(p => p.Tipo == TipoPqrsd.Felicitacion);
        var conResp = pqrsd.Where(p => p.RespuestaAdminAt.HasValue).ToList();
        decimal? tiempo = conResp.Count > 0
            ? Math.Round((decimal)conResp.Average(p => (p.RespuestaAdminAt!.Value - p.CreatedAt).TotalDays), 1)
            : null;

        var nombres = await _db.Tenants.AsNoTracking()
            .Where(t => tenants.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Nombre, ct);

        var porCop = tenants.Select(tid =>
        {
            var sub = pqrsd.Where(x => x.TenantId == tid).ToList();
            return new PqrsdPorCopropiedadDto(tid, nombres.GetValueOrDefault(tid, ""),
                sub.Count(x => x.Estado == EstadoPqrsd.Recibida || x.Estado == EstadoPqrsd.EnGestion),
                sub.Count(x => x.Estado != EstadoPqrsd.Cerrada && x.FechaVencimiento < hoy));
        }).ToList();

        return new IndicadoresPqrsdConsolidadoDto(activas, vencidas, felicitaciones, tiempo, porCop);
    }

    public async Task<IndicadoresEquipoDto> GetIndicadoresEquipoAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var tenants = await GetTenantsDeOrganizacionAsync(orgId, ct);
        if (tenants.Count == 0) return new IndicadoresEquipoDto(Array.Empty<DesempenoColaboradorDto>());

        // En MVP cruzamos via Persona asignada de las tareas
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var tareas = await _db.Tareas.IgnoreQueryFilters().AsNoTracking()
            .Where(t => tenants.Contains(t.TenantId) && t.AsignadoPersonaId != null)
            .Select(t => new { t.AsignadoPersonaId, t.TenantId, t.FechaCompletada, t.FechaVencimiento })
            .ToListAsync(ct);

        var personaIds = tareas.Select(t => t.AsignadoPersonaId!.Value).Distinct().ToList();
        var nombres = await _db.Personas.AsNoTracking()
            .Where(p => personaIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Nombres + " " + p.Apellidos, ct);

        var colabs = personaIds.Select(pid =>
        {
            var sub = tareas.Where(t => t.AsignadoPersonaId == pid).ToList();
            return new DesempenoColaboradorDto(
                pid, nombres.GetValueOrDefault(pid, "(sin nombre)"),
                sub.Count,
                sub.Count(s => s.FechaCompletada != null),
                sub.Count(s => s.FechaCompletada == null && s.FechaVencimiento != null && s.FechaVencimiento < hoy),
                sub.Select(s => s.TenantId).Distinct().Count());
        })
        .OrderByDescending(c => c.TareasAsignadas)
        .Take(20)
        .ToList();
        return new IndicadoresEquipoDto(colabs);
    }

    public async Task<ResumenReportesConsolidadosDto> GetResumenAsync(CancellationToken ct)
    {
        var orgId = await GetOrganizacionIdActualAsync(ct);
        var total = await _db.OrgReportes.CountAsync(r => r.OrganizacionId == orgId && r.Activo, ct);
        var plantillas = Plantillas.Length;
        var hace30 = DateTimeOffset.UtcNow.AddDays(-30);
        var gen30 = await _db.OrgReporteGeneraciones.CountAsync(g => g.OrganizacionId == orgId && g.CreatedAt >= hace30, ct);
        return new ResumenReportesConsolidadosDto(total, plantillas, gen30, 0);
    }
}
