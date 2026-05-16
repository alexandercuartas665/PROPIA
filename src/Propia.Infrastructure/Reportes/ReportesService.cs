using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Reportes;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Reportes;

/// <summary>
/// Modulo 2.16 Reportes e Indicadores (spec v1.0 MVP).
///
/// MVP scope: catalogo extensible + generacion sincrona devolviendo JSON
/// estructurado + historial regenerable + programaciones (solo config; T.2
/// hace el despacho real cuando se construya) + vista consejo con KPIs y
/// reportes compartidos + portal transparencia agregado.
///
/// Diferido a Fase 2: asincrono con cola (RN-02), PDF/Excel reales (RN-12),
/// agente IA T.1 (RN-09), despacho real via T.2 (RN-06), cache de transparencia.
/// </summary>
public class ReportesService : IReportesService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _http;
    private readonly IIndicadoresService _indicadores;

    public ReportesService(PropiaDbContext db, ITenantContext tenantContext,
        IHttpContextAccessor http, IIndicadoresService indicadores)
    {
        _db = db;
        _tenantContext = tenantContext;
        _http = http;
        _indicadores = indicadores;
    }

    private Guid GetUsuarioActualId()
    {
        var sub = _http.HttpContext?.User?.FindFirstValue("user_id");
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    private Guid RequireTenantId() =>
        _tenantContext.CurrentTenantId
            ?? throw new InvalidOperationException("No hay copropiedad activa.");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    // ===========================================================================
    // Catalogo
    // ===========================================================================

    public async Task<IReadOnlyList<CategoriaReporteDto>> ListarCategoriasAsync(AudienciaReporte? audiencia, CancellationToken ct)
    {
        var cats = await _db.ReporteCategorias.AsNoTracking()
            .Where(c => c.EsActiva)
            .OrderBy(c => c.Orden).ThenBy(c => c.Nombre)
            .ToListAsync(ct);

        var reportesConteo = await _db.ReporteCatalogo.AsNoTracking()
            .Where(r => r.EsActivo)
            .GroupBy(r => r.CategoriaId)
            .Select(g => new { CategoriaId = g.Key, Total = g.Count() })
            .ToListAsync(ct);

        var audienciaTxt = audiencia switch
        {
            AudienciaReporte.Consejo => "CONSEJO",
            AudienciaReporte.Propietario => "PROPIETARIO",
            _ => "ADMINISTRADOR"
        };

        // Si se filtra por audiencia, solo cuenta reportes que la incluyan.
        if (audiencia.HasValue)
        {
            var matched = await _db.ReporteCatalogo.AsNoTracking()
                .Where(r => r.EsActivo)
                .Select(r => new { r.CategoriaId, r.AudienciasJson })
                .ToListAsync(ct);
            reportesConteo = matched
                .Where(m => m.AudienciasJson.Contains(audienciaTxt))
                .GroupBy(m => m.CategoriaId)
                .Select(g => new { CategoriaId = g.Key, Total = g.Count() })
                .ToList();
        }

        return cats.Select(c => new CategoriaReporteDto(
            c.Id, c.Nombre, c.Icono, c.Color, c.ModuloOrigen, c.Orden, c.EsActiva,
            reportesConteo.FirstOrDefault(r => r.CategoriaId == c.Id)?.Total ?? 0
        )).ToList();
    }

    public async Task<IReadOnlyList<CatalogoReporteDto>> ListarCatalogoAsync(Guid? categoriaId, AudienciaReporte? audiencia, CancellationToken ct)
    {
        var q = _db.ReporteCatalogo.AsNoTracking().Where(r => r.EsActivo);
        if (categoriaId is { } cat) q = q.Where(r => r.CategoriaId == cat);

        var rows = await q
            .Select(r => new
            {
                r.Id,
                r.CategoriaId,
                CategoriaNombre = _db.ReporteCategorias.Where(c => c.Id == r.CategoriaId).Select(c => c.Nombre).FirstOrDefault() ?? "",
                r.Nombre,
                r.Descripcion,
                r.ModuloOrigen,
                r.Clave,
                r.AudienciasJson,
                r.EsActivo,
                r.EsSistema,
                r.Orden
            })
            .OrderBy(r => r.Orden).ThenBy(r => r.Nombre)
            .ToListAsync(ct);

        var audTxt = audiencia switch
        {
            AudienciaReporte.Consejo => "CONSEJO",
            AudienciaReporte.Propietario => "PROPIETARIO",
            AudienciaReporte.Administrador => "ADMINISTRADOR",
            _ => null
        };
        if (audTxt is not null)
            rows = rows.Where(r => r.AudienciasJson.Contains(audTxt)).ToList();

        return rows.Select(r => new CatalogoReporteDto(
            r.Id, r.CategoriaId, r.CategoriaNombre, r.Nombre, r.Descripcion,
            r.ModuloOrigen, r.Clave, ParseAudiencias(r.AudienciasJson),
            r.EsActivo, r.EsSistema, r.Orden)).ToList();
    }

    public async Task<CatalogoReporteDto?> GetCatalogoAsync(Guid id, CancellationToken ct)
    {
        var r = await _db.ReporteCatalogo.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return null;
        var catNombre = await _db.ReporteCategorias.AsNoTracking()
            .Where(c => c.Id == r.CategoriaId).Select(c => c.Nombre).FirstOrDefaultAsync(ct) ?? "";
        return new CatalogoReporteDto(
            r.Id, r.CategoriaId, catNombre, r.Nombre, r.Descripcion,
            r.ModuloOrigen, r.Clave, ParseAudiencias(r.AudienciasJson),
            r.EsActivo, r.EsSistema, r.Orden);
    }

    private static IReadOnlyList<AudienciaReporte> ParseAudiencias(string json)
    {
        try
        {
            var arr = JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
            return arr.Select(s => s switch
            {
                "CONSEJO" => AudienciaReporte.Consejo,
                "PROPIETARIO" => AudienciaReporte.Propietario,
                _ => AudienciaReporte.Administrador
            }).Distinct().ToList();
        }
        catch
        {
            return new List<AudienciaReporte> { AudienciaReporte.Administrador };
        }
    }

    // ===========================================================================
    // Generacion + historial
    // ===========================================================================

    public async Task<ReporteGeneradoDetalleDto> GenerarAsync(GenerarReporteRequest req, CancellationToken ct)
    {
        var tenantId = RequireTenantId();
        var cat = await _db.ReporteCatalogo.AsNoTracking()
            .Where(c => c.Id == req.CatalogoId && c.EsActivo)
            .Select(c => new { c.Id, c.Nombre, c.Clave, c.ModuloOrigen, c.CategoriaId })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Reporte del catalogo no encontrado o inactivo.");

        if (req.PeriodoFin < req.PeriodoInicio)
            throw new InvalidOperationException("Periodo invalido: PeriodoFin < PeriodoInicio.");

        var categoriaNombre = await _db.ReporteCategorias.AsNoTracking()
            .Where(c => c.Id == cat.CategoriaId).Select(c => c.Nombre).FirstOrDefaultAsync(ct) ?? "";

        var generado = new ReporteGenerado
        {
            TenantId = tenantId,
            ReporteCatalogoId = cat.Id,
            NombreReporte = cat.Nombre,
            Categoria = categoriaNombre,
            PeriodoInicio = req.PeriodoInicio,
            PeriodoFin = req.PeriodoFin,
            FiltrosAplicadosJson = req.FiltrosAplicadosJson,
            Origen = OrigenReporte.Manual,
            Estado = EstadoReporteGenerado.Generando,
            GeneradoPorUsuarioId = GetUsuarioActualId()
        };
        _db.ReporteGenerados.Add(generado);
        await _db.SaveChangesAsync(ct);

        try
        {
            var resultado = await ResolverReporteAsync(cat.Clave, cat.ModuloOrigen, req.PeriodoInicio, req.PeriodoFin, ct);
            generado.ResultadoJson = JsonSerializer.Serialize(resultado, JsonOpts);
            generado.Estado = EstadoReporteGenerado.Listo;
            generado.UrlExpiracion = DateTimeOffset.UtcNow.AddDays(30);  // RN-03
        }
        catch (Exception ex)
        {
            generado.Estado = EstadoReporteGenerado.Error;
            generado.ErrorMensaje = ex.Message.Length > 1900 ? ex.Message.Substring(0, 1900) : ex.Message;
        }
        generado.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return (await GetReporteAsync(generado.Id, ct))!;
    }

    public async Task<IReadOnlyList<ReporteGeneradoListaDto>> ListarHistorialAsync(
        DateOnly? desde, DateOnly? hasta, OrigenReporte? origen, Guid? catalogoId, CancellationToken ct)
    {
        var q = _db.ReporteGenerados.AsNoTracking().AsQueryable();
        if (desde is { } d) q = q.Where(r => r.PeriodoInicio >= d);
        if (hasta is { } h) q = q.Where(r => r.PeriodoFin <= h);
        if (origen is { } o) q = q.Where(r => r.Origen == o);
        if (catalogoId is { } cid) q = q.Where(r => r.ReporteCatalogoId == cid);

        return await q.OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReporteGeneradoListaDto(
                r.Id, r.ReporteCatalogoId, r.NombreReporte, r.Categoria,
                r.PeriodoInicio, r.PeriodoFin, r.Origen, r.Estado,
                r.CompartidoConsejo, r.CompartidoAt,
                r.GeneradoPorUsuarioId, r.CreatedAt))
            .Take(200)
            .ToListAsync(ct);
    }

    public async Task<ReporteGeneradoDetalleDto?> GetReporteAsync(Guid id, CancellationToken ct)
    {
        var r = await _db.ReporteGenerados.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return null;
        return new ReporteGeneradoDetalleDto(
            r.Id, r.ReporteCatalogoId, r.NombreReporte, r.Categoria,
            r.PeriodoInicio, r.PeriodoFin, r.Origen, r.Estado, r.ErrorMensaje,
            r.CompartidoConsejo, r.CompartidoAt,
            r.FiltrosAplicadosJson, r.ResultadoJson,
            r.UrlPdf, r.UrlExcel, r.UrlExpiracion,
            r.CreatedAt, r.GeneradoPorUsuarioId);
    }

    public async Task<ReporteGeneradoDetalleDto> RegenerarAsync(Guid id, CancellationToken ct)
    {
        var prev = await _db.ReporteGenerados.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Reporte no encontrado.");
        if (prev.ReporteCatalogoId is null)
            throw new InvalidOperationException("Reporte IA libre no soporta regeneracion automatica en MVP.");
        return await GenerarAsync(new GenerarReporteRequest(
            prev.ReporteCatalogoId.Value, prev.PeriodoInicio, prev.PeriodoFin, prev.FiltrosAplicadosJson), ct);
    }

    public async Task<bool> CompartirConsejoAsync(Guid id, bool compartir, CancellationToken ct)
    {
        var r = await _db.ReporteGenerados.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return false;
        if (r.Estado != EstadoReporteGenerado.Listo && compartir)
            throw new InvalidOperationException("Solo se pueden compartir reportes en estado Listo.");
        r.CompartidoConsejo = compartir;
        r.CompartidoAt = compartir ? DateTimeOffset.UtcNow : null;
        r.CompartidoPorUsuarioId = compartir ? GetUsuarioActualId() : null;
        r.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===========================================================================
    // Engine de reportes - resuelve la clave logica al payload del reporte.
    // ===========================================================================

    private async Task<object> ResolverReporteAsync(string clave, string modulo, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        return clave switch
        {
            "financiero.ejecucion_presupuestal" => await _indicadores.GetFinancieroAsync(desde, hasta, ct),
            "financiero.recaudo" => new
            {
                Periodo = new { Desde = desde, Hasta = hasta },
                Financiero = await _indicadores.GetFinancieroAsync(desde, hasta, ct)
            },
            "financiero.cuotas_extraordinarias" => new
            {
                Periodo = new { Desde = desde, Hasta = hasta },
                CuotasExtraordinarias = (await _indicadores.GetFinancieroAsync(desde, hasta, ct)).CuotasExtraordinarias
            },
            "financiero.informe_asamblea" => new
            {
                Periodo = new { Desde = desde, Hasta = hasta },
                Financiero = await _indicadores.GetFinancieroAsync(desde, hasta, ct),
                Cartera = await _indicadores.GetCarteraAsync(desde, hasta, ct),
                Operativo = await _indicadores.GetOperativoAsync(desde, hasta, ct)
            },
            "cartera.aging" => (await _indicadores.GetCarteraAsync(desde, hasta, ct)).Aging,
            "cartera.por_unidad" => await _indicadores.GetCarteraAsync(desde, hasta, ct),
            "cartera.evolucion" => await _indicadores.GetCarteraAsync(desde, hasta, ct),
            "cartera.acuerdos_activos" => await _indicadores.GetCarteraAsync(desde, hasta, ct),
            "cartera.paz_salvos" => await _indicadores.GetCarteraAsync(desde, hasta, ct),
            "pqrsd.resumen" => await _indicadores.GetPqrsdAsync(desde, hasta, ct),
            "pqrsd.tiempos_respuesta" => await _indicadores.GetPqrsdAsync(desde, hasta, ct),
            "pqrsd.por_categoria" => await _indicadores.GetPqrsdAsync(desde, hasta, ct),
            "pqrsd.felicitaciones" => await _indicadores.GetPqrsdAsync(desde, hasta, ct),
            "operativo.resumen" => await _indicadores.GetOperativoAsync(desde, hasta, ct),
            "operativo.carga_responsable" => (await _indicadores.GetOperativoAsync(desde, hasta, ct)).CargaPorResponsable,
            "operativo.tiempo_cierre" => await _indicadores.GetOperativoAsync(desde, hasta, ct),
            "operativo.avance_proyectos" => await _indicadores.GetOperativoAsync(desde, hasta, ct),
            "mantenimiento.intervenciones" => await _indicadores.GetMantenimientoAsync(desde, hasta, ct),
            "mantenimiento.activos_vencidos" => await _indicadores.GetMantenimientoAsync(desde, hasta, ct),
            "mantenimiento.costos" => await _indicadores.GetMantenimientoAsync(desde, hasta, ct),
            "mantenimiento.mttr" => await _indicadores.GetMantenimientoAsync(desde, hasta, ct),
            "comunicaciones.resumen" => await _indicadores.GetComunicacionesAsync(desde, hasta, ct),
            "comunicaciones.apertura" => await _indicadores.GetComunicacionesAsync(desde, hasta, ct),
            "comunicaciones.eficiencia" => await _indicadores.GetComunicacionesAsync(desde, hasta, ct),
            "documentos.resumen" => await _indicadores.GetDocumentosAsync(desde, hasta, ct),
            _ => throw new InvalidOperationException($"Clave de reporte no soportada en MVP: {clave}")
        };
    }

    // ===========================================================================
    // Programaciones
    // ===========================================================================

    public async Task<IReadOnlyList<ProgramacionListaDto>> ListarProgramacionesAsync(CancellationToken ct)
    {
        var rows = await _db.ReporteProgramaciones.AsNoTracking()
            .Select(p => new
            {
                p.Id,
                p.ReporteCatalogoId,
                CatalogoNombre = _db.ReporteCatalogo.Where(c => c.Id == p.ReporteCatalogoId).Select(c => c.Nombre).FirstOrDefault() ?? "",
                p.Nombre,
                p.Frecuencia,
                p.DiaEnvio,
                p.PeriodoQueCubre,
                p.Formato,
                p.CanalesJson,
                p.Estado,
                p.ProximoEnvio,
                p.UltimoEnvio,
                p.UltimoEnvioExitoso,
                Destinatarios = _db.ReporteProgramacionDestinatarios.Count(d => d.ProgramacionId == p.Id)
            })
            .OrderByDescending(p => p.ProximoEnvio)
            .ToListAsync(ct);

        return rows.Select(r => new ProgramacionListaDto(
            r.Id, r.ReporteCatalogoId, r.CatalogoNombre, r.Nombre, r.Frecuencia,
            r.DiaEnvio, r.PeriodoQueCubre, r.Formato, ParseCanales(r.CanalesJson),
            r.Estado, r.ProximoEnvio, r.UltimoEnvio, r.UltimoEnvioExitoso, r.Destinatarios)).ToList();
    }

    public async Task<ProgramacionListaDto?> GetProgramacionAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.ReporteProgramaciones.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;
        var catNombre = await _db.ReporteCatalogo.AsNoTracking()
            .Where(c => c.Id == p.ReporteCatalogoId).Select(c => c.Nombre).FirstOrDefaultAsync(ct) ?? "";
        var dest = await _db.ReporteProgramacionDestinatarios.AsNoTracking().CountAsync(d => d.ProgramacionId == id, ct);
        return new ProgramacionListaDto(p.Id, p.ReporteCatalogoId, catNombre, p.Nombre,
            p.Frecuencia, p.DiaEnvio, p.PeriodoQueCubre, p.Formato,
            ParseCanales(p.CanalesJson), p.Estado, p.ProximoEnvio, p.UltimoEnvio,
            p.UltimoEnvioExitoso, dest);
    }

    public async Task<ProgramacionListaDto> CrearProgramacionAsync(CrearProgramacionRequest req, CancellationToken ct)
    {
        ValidarProgramacion(req.DiaEnvio, req.Canales, req.Destinatarios);
        var tenantId = RequireTenantId();
        var existeCat = await _db.ReporteCatalogo.AnyAsync(c => c.Id == req.CatalogoId && c.EsActivo, ct);
        if (!existeCat) throw new InvalidOperationException("Reporte del catalogo no encontrado.");

        var p = new ReporteProgramacion
        {
            TenantId = tenantId,
            ReporteCatalogoId = req.CatalogoId,
            Nombre = string.IsNullOrWhiteSpace(req.Nombre) ? null : req.Nombre.Trim(),
            Frecuencia = req.Frecuencia,
            DiaEnvio = req.DiaEnvio,
            PeriodoQueCubre = req.PeriodoQueCubre,
            Formato = req.Formato,
            CanalesJson = JsonSerializer.Serialize(req.Canales.Select(c => c.ToUpperInvariant()).ToArray()),
            FiltrosAplicadosJson = req.FiltrosAplicadosJson,
            Estado = EstadoProgramacion.Activa,
            ProximoEnvio = CalcularProximoEnvio(req.Frecuencia, req.DiaEnvio, DateOnly.FromDateTime(DateTime.UtcNow)),
            CreadoPorUsuarioId = GetUsuarioActualId()
        };
        _db.ReporteProgramaciones.Add(p);
        await _db.SaveChangesAsync(ct);

        foreach (var d in req.Destinatarios)
        {
            _db.ReporteProgramacionDestinatarios.Add(new ReporteProgramacionDestinatario
            {
                TenantId = tenantId,
                ProgramacionId = p.Id,
                PersonaId = d.PersonaId,
                EmailExterno = d.EmailExterno,
                WhatsappExterno = d.WhatsappExterno
            });
        }
        await _db.SaveChangesAsync(ct);
        return (await GetProgramacionAsync(p.Id, ct))!;
    }

    public async Task<bool> ActualizarProgramacionAsync(Guid id, ActualizarProgramacionRequest req, CancellationToken ct)
    {
        ValidarProgramacion(req.DiaEnvio, req.Canales, req.Destinatarios);
        var p = await _db.ReporteProgramaciones.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        var tenantId = RequireTenantId();

        p.Nombre = string.IsNullOrWhiteSpace(req.Nombre) ? null : req.Nombre.Trim();
        p.Frecuencia = req.Frecuencia;
        p.DiaEnvio = req.DiaEnvio;
        p.PeriodoQueCubre = req.PeriodoQueCubre;
        p.Formato = req.Formato;
        p.CanalesJson = JsonSerializer.Serialize(req.Canales.Select(c => c.ToUpperInvariant()).ToArray());
        p.FiltrosAplicadosJson = req.FiltrosAplicadosJson;
        p.ProximoEnvio = CalcularProximoEnvio(req.Frecuencia, req.DiaEnvio, DateOnly.FromDateTime(DateTime.UtcNow));
        p.UpdatedAt = DateTimeOffset.UtcNow;

        // Reemplazar destinatarios.
        var actuales = await _db.ReporteProgramacionDestinatarios.Where(d => d.ProgramacionId == id).ToListAsync(ct);
        _db.ReporteProgramacionDestinatarios.RemoveRange(actuales);
        foreach (var d in req.Destinatarios)
        {
            _db.ReporteProgramacionDestinatarios.Add(new ReporteProgramacionDestinatario
            {
                TenantId = tenantId,
                ProgramacionId = id,
                PersonaId = d.PersonaId,
                EmailExterno = d.EmailExterno,
                WhatsappExterno = d.WhatsappExterno
            });
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> PausarProgramacionAsync(Guid id, bool pausar, CancellationToken ct)
    {
        var p = await _db.ReporteProgramaciones.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        p.Estado = pausar ? EstadoProgramacion.Pausada : EstadoProgramacion.Activa;
        if (!pausar && p.ProximoEnvio is null)
            p.ProximoEnvio = CalcularProximoEnvio(p.Frecuencia, p.DiaEnvio, DateOnly.FromDateTime(DateTime.UtcNow));
        p.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarProgramacionAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.ReporteProgramaciones.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        var dest = await _db.ReporteProgramacionDestinatarios.Where(d => d.ProgramacionId == id).ToListAsync(ct);
        _db.ReporteProgramacionDestinatarios.RemoveRange(dest);
        _db.ReporteProgramaciones.Remove(p);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static void ValidarProgramacion(int diaEnvio, IReadOnlyList<string> canales, IReadOnlyList<DestinatarioInput> destinatarios)
    {
        if (diaEnvio < 1 || diaEnvio > 28)
            throw new InvalidOperationException("DiaEnvio debe estar entre 1 y 28.");
        if (canales is null || canales.Count == 0)
            throw new InvalidOperationException("Debe configurar al menos un canal (EMAIL o WHATSAPP).");
        var canalesUpper = canales.Select(c => c.ToUpperInvariant()).ToList();
        if (canalesUpper.Any(c => c != "EMAIL" && c != "WHATSAPP"))
            throw new InvalidOperationException("Canal no soportado. Use EMAIL o WHATSAPP.");
        if (destinatarios is null || destinatarios.Count == 0)
            throw new InvalidOperationException("Debe agregar al menos un destinatario.");
        foreach (var d in destinatarios)
        {
            var tiene = d.PersonaId.HasValue || !string.IsNullOrWhiteSpace(d.EmailExterno) || !string.IsNullOrWhiteSpace(d.WhatsappExterno);
            if (!tiene)
                throw new InvalidOperationException("Cada destinatario debe tener PersonaId, EmailExterno o WhatsappExterno.");
        }
    }

    private static DateOnly CalcularProximoEnvio(FrecuenciaProgramacion freq, int diaEnvio, DateOnly hoy)
    {
        var meses = freq switch
        {
            FrecuenciaProgramacion.Mensual => 1,
            FrecuenciaProgramacion.Trimestral => 3,
            FrecuenciaProgramacion.Semestral => 6,
            FrecuenciaProgramacion.Anual => 12,
            _ => 1
        };
        var candidato = new DateOnly(hoy.Year, hoy.Month, Math.Min(diaEnvio, 28));
        if (candidato <= hoy)
            candidato = candidato.AddMonths(meses);
        return candidato;
    }

    private static IReadOnlyList<string> ParseCanales(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }

    // ===========================================================================
    // Semaforos
    // ===========================================================================

    public async Task<IReadOnlyList<SemaforoConfigDto>> ListarSemaforosAsync(CancellationToken ct)
    {
        return await _db.ReporteSemaforoConfigs.AsNoTracking()
            .Select(s => new SemaforoConfigDto(s.IndicadorKey, s.UmbralAmarillo, s.UmbralRojo, s.EsAscendente))
            .ToListAsync(ct);
    }

    public async Task<SemaforoConfigDto> GuardarSemaforoAsync(string indicadorKey, GuardarSemaforoRequest req, CancellationToken ct)
    {
        var tenantId = RequireTenantId();
        var existente = await _db.ReporteSemaforoConfigs
            .FirstOrDefaultAsync(s => s.IndicadorKey == indicadorKey, ct);
        if (existente is null)
        {
            existente = new ReporteSemaforoConfig
            {
                TenantId = tenantId,
                IndicadorKey = indicadorKey
            };
            _db.ReporteSemaforoConfigs.Add(existente);
        }
        existente.UmbralAmarillo = req.UmbralAmarillo;
        existente.UmbralRojo = req.UmbralRojo;
        existente.EsAscendente = req.EsAscendente;
        existente.ActualizadoPorUsuarioId = GetUsuarioActualId();
        existente.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new SemaforoConfigDto(existente.IndicadorKey, existente.UmbralAmarillo, existente.UmbralRojo, existente.EsAscendente);
    }

    // ===========================================================================
    // Vistas (consejo + transparencia)
    // ===========================================================================

    public async Task<VistaConsejoDto> GetVistaConsejoAsync(DateOnly? periodoInicio, DateOnly? periodoFin, CancellationToken ct)
    {
        var (desde, hasta) = ResolverPeriodoDefault(periodoInicio, periodoFin);
        var kpis = await _indicadores.GetKpisConsejoAsync(desde, hasta, ct);
        var compartidos = await _db.ReporteGenerados.AsNoTracking()
            .Where(r => r.CompartidoConsejo && r.Estado == EstadoReporteGenerado.Listo)
            .OrderByDescending(r => r.CompartidoAt ?? r.CreatedAt)
            .Take(20)
            .Select(r => new ReporteGeneradoListaDto(
                r.Id, r.ReporteCatalogoId, r.NombreReporte, r.Categoria,
                r.PeriodoInicio, r.PeriodoFin, r.Origen, r.Estado,
                r.CompartidoConsejo, r.CompartidoAt,
                r.GeneradoPorUsuarioId, r.CreatedAt))
            .ToListAsync(ct);
        return new VistaConsejoDto(desde, hasta, kpis, compartidos);
    }

    public async Task<TransparenciaDto> GetTransparenciaAsync(DateOnly? periodoInicio, DateOnly? periodoFin, CancellationToken ct)
    {
        var (desde, hasta) = ResolverPeriodoDefault(periodoInicio, periodoFin);
        return await _indicadores.GetTransparenciaAsync(desde, hasta, ct);
    }

    private static (DateOnly desde, DateOnly hasta) ResolverPeriodoDefault(DateOnly? desde, DateOnly? hasta)
    {
        if (desde.HasValue && hasta.HasValue) return (desde.Value, hasta.Value);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var primero = new DateOnly(hoy.Year, hoy.Month, 1);
        var ultimo = primero.AddMonths(1).AddDays(-1);
        return (desde ?? primero, hasta ?? ultimo);
    }

    // ===========================================================================
    // Resumen
    // ===========================================================================

    public async Task<ResumenReportesDto> GetResumenAsync(CancellationToken ct)
    {
        var totalCatalogo = await _db.ReporteCatalogo.CountAsync(c => c.EsActivo, ct);
        var totalCat = await _db.ReporteCategorias.CountAsync(c => c.EsActiva, ct);
        var hace30 = DateTimeOffset.UtcNow.AddDays(-30);
        var gen30 = await _db.ReporteGenerados.CountAsync(r => r.CreatedAt >= hace30, ct);
        var comp30 = await _db.ReporteGenerados.CountAsync(r => r.CompartidoConsejo && r.CompartidoAt >= hace30, ct);
        var progActivas = await _db.ReporteProgramaciones.CountAsync(p => p.Estado == EstadoProgramacion.Activa, ct);
        return new ResumenReportesDto(totalCatalogo, totalCat, gen30, comp30, progActivas);
    }
}
