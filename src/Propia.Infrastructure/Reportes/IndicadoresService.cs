using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Reportes;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Reportes;

/// <summary>
/// Agregador cross-modulo de indicadores. Spec 2.16 RN-01: 2.16 NO consulta
/// tablas operativas directamente; consume esta capa.
///
/// MVP: implementacion monolitica - todos los modulos productores en un solo
/// servicio. Cuando los modulos productores crezcan (Fase 2), cada uno expondra
/// su `IIndicadoresFinancieroService`, `IIndicadoresCarteraService`, etc. y este
/// agregador hara dispatch.
///
/// IMPORTANTE: 2.11 Mantenimiento, 2.12 Porteria, 2.13 Reservas aun no estan
/// construidos en esta rama; sus secciones devuelven valores neutros (cero) y
/// el catalogo no expone reportes de esas categorias hasta que el modulo este.
/// </summary>
public class IndicadoresService : IIndicadoresService
{
    private readonly PropiaDbContext _db;

    public IndicadoresService(PropiaDbContext db) => _db = db;

    /// <summary>Convierte DateOnly + TimeOnly a DateTimeOffset UTC (Postgres timestamptz requiere offset 0).</summary>
    private static DateTimeOffset ToUtc(DateOnly d, TimeOnly t)
        => new DateTimeOffset(DateTime.SpecifyKind(d.ToDateTime(t), DateTimeKind.Utc));

    private static DateTimeOffset PeriodoDesdeUtc(DateOnly d) => ToUtc(d, TimeOnly.MinValue);
    private static DateTimeOffset PeriodoHastaUtc(DateOnly d) => ToUtc(d, TimeOnly.MaxValue);

    public async Task<IndicadoresFinancieroDto> GetFinancieroAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var liquidaciones = await _db.LiquidacionUnidades.AsNoTracking()
            .Join(_db.Liquidaciones.AsNoTracking(),
                lu => lu.LiquidacionId, l => l.Id,
                (lu, l) => new { lu, l })
            .Where(x => x.l.Periodo >= desde && x.l.Periodo <= hasta)
            .Select(x => new { x.lu.Monto, x.lu.EstadoPago })
            .ToListAsync(ct);

        var recaudoEsperado = liquidaciones.Sum(x => x.Monto);
        var recaudoRecibido = liquidaciones.Where(x => x.EstadoPago == EstadoPagoLiquidacion.Pagado)
            .Sum(x => x.Monto);
        var cuotasVencidas = liquidaciones.Count(x => x.EstadoPago == EstadoPagoLiquidacion.Vencido);
        var presupuestosActivos = await _db.Presupuestos.AsNoTracking()
            .CountAsync(p => p.Estado == EstadoPresupuesto.EnEjecucion, ct);

        var ejecucion = await _db.PresupuestoRubros.AsNoTracking()
            .Join(_db.Presupuestos.AsNoTracking().Where(p => p.Estado == EstadoPresupuesto.EnEjecucion),
                r => r.PresupuestoId, p => p.Id, (r, p) => r)
            .Where(r => r.Activo)
            .Select(r => new
            {
                r.Nombre,
                Presupuestado = r.MontoAnual,
                Ejecutado = _db.EjecucionesPresupuestales
                    .Where(e => e.PresupuestoRubroId == r.Id && e.Fecha >= desde && e.Fecha <= hasta)
                    .Select(e => (decimal?)e.Monto).Sum() ?? 0m
            })
            .ToListAsync(ct);
        var ejecucionRubros = ejecucion
            .Select(e => new RubroEjecucionDto(
                e.Nombre, e.Presupuestado, e.Ejecutado,
                e.Presupuestado > 0 ? Math.Round(e.Ejecutado / e.Presupuestado * 100m, 2) : (decimal?)null))
            .OrderByDescending(x => x.Ejecutado)
            .ToList();

        // Cuotas extraordinarias activas en el periodo (recaudo real va en futuro - integracion con pagos)
        var cuotasExtras = await _db.CuotasExtraordinarias.AsNoTracking()
            .Where(c => c.Estado == EstadoCuotaExtraordinaria.Aprobada || c.Estado == EstadoCuotaExtraordinaria.EnRecaudo)
            .Where(c => c.FechaInicioRecaudo == null || c.FechaInicioRecaudo <= hasta)
            .Select(c => new CuotaExtraordinariaResumenDto(c.Nombre, c.MontoTotal, 0m))
            .ToListAsync(ct);

        return new IndicadoresFinancieroDto(
            recaudoEsperado, recaudoRecibido,
            recaudoEsperado > 0 ? Math.Round(recaudoRecibido / recaudoEsperado * 100m, 2) : (decimal?)null,
            cuotasVencidas, presupuestosActivos, ejecucionRubros, cuotasExtras);
    }

    public async Task<IndicadoresCarteraDto> GetCarteraAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var cartera = await _db.CarteraUnidades.AsNoTracking()
            .Select(c => new
            {
                c.UnidadPrivadaId,
                c.SaldoCapital,
                c.SaldoIntereses,
                c.TieneAcuerdoVigente,
                c.FechaPrimerMora
            })
            .ToListAsync(ct);
        var conMora = cartera.Where(c => c.SaldoCapital + c.SaldoIntereses > 0).ToList();
        var totalMora = conMora.Sum(c => c.SaldoCapital + c.SaldoIntereses);
        var acuerdos = await _db.AcuerdosPago.AsNoTracking()
            .CountAsync(a => a.Estado == EstadoAcuerdoPago.Vigente, ct);
        var desdeDt = PeriodoDesdeUtc(desde);
        var hastaDt = PeriodoHastaUtc(hasta);
        var pazSalvos = await _db.PazSalvosEmitidos.AsNoTracking()
            .CountAsync(p => p.FechaEmision >= desdeDt && p.FechaEmision <= hastaDt, ct);

        // Aging por dias desde FechaPrimerMora hasta hoy.
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        decimal d0_30 = 0, d31_60 = 0, d61_90 = 0, dMas90 = 0;
        foreach (var c in conMora)
        {
            var saldo = c.SaldoCapital + c.SaldoIntereses;
            var dias = c.FechaPrimerMora is { } fpm ? hoy.DayNumber - fpm.DayNumber : 0;
            if (dias <= 30) d0_30 += saldo;
            else if (dias <= 60) d31_60 += saldo;
            else if (dias <= 90) d61_90 += saldo;
            else dMas90 += saldo;
        }

        var unidades = await _db.UnidadesPrivadas.AsNoTracking()
            .Select(u => new { u.Id, u.Numero })
            .ToListAsync(ct);
        var nombrePorUnidad = unidades.ToDictionary(u => u.Id, u => u.Numero);

        var top5 = conMora
            .OrderByDescending(c => c.SaldoCapital + c.SaldoIntereses)
            .Take(5)
            .Select(c => new TopUnidadMoraDto(
                nombrePorUnidad.TryGetValue(c.UnidadPrivadaId, out var n) ? n : "(unidad)",
                c.SaldoCapital + c.SaldoIntereses,
                c.FechaPrimerMora is { } fpm ? hoy.DayNumber - fpm.DayNumber : 0))
            .ToList();

        return new IndicadoresCarteraDto(
            totalMora, conMora.Count, acuerdos, pazSalvos,
            new AgingDto(d0_30, d31_60, d61_90, dMas90),
            top5);
    }

    public async Task<IndicadoresPqrsdDto> GetPqrsdAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var desdeDt = PeriodoDesdeUtc(desde);
        var hastaDt = PeriodoHastaUtc(hasta);
        var pqrsd = await _db.PqrsdExpedientes.AsNoTracking()
            .Where(p => p.CreatedAt >= desdeDt && p.CreatedAt <= hastaDt)
            .Select(p => new { p.Tipo, p.Estado, p.FechaVencimiento, p.RespuestaAdminAt, p.CreatedAt })
            .ToListAsync(ct);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var resueltas = pqrsd.Count(p => p.Estado == EstadoPqrsd.Cerrada);
        var vencidas = pqrsd.Count(p => p.Estado != EstadoPqrsd.Cerrada && p.FechaVencimiento < hoy);
        var enTramite = pqrsd.Count(p => p.Estado == EstadoPqrsd.Recibida || p.Estado == EstadoPqrsd.EnGestion);
        var felicitaciones = pqrsd.Count(p => p.Tipo == TipoPqrsd.Felicitacion);

        var conRespuesta = pqrsd.Where(p => p.RespuestaAdminAt.HasValue).ToList();
        decimal? tiempoPromedio = conRespuesta.Count > 0
            ? Math.Round((decimal)conRespuesta.Average(p => (p.RespuestaAdminAt!.Value - p.CreatedAt).TotalDays), 1)
            : null;

        var porTipo = pqrsd.GroupBy(p => p.Tipo)
            .Select(g => new PqrsdPorTipoDto(
                g.Key.ToString(),
                g.Count(),
                g.Count(p => p.Estado == EstadoPqrsd.Cerrada),
                g.Count(p => p.Estado != EstadoPqrsd.Cerrada && p.FechaVencimiento < hoy)))
            .ToList();

        return new IndicadoresPqrsdDto(pqrsd.Count, resueltas, vencidas, enTramite, felicitaciones, tiempoPromedio, porTipo);
    }

    public async Task<IndicadoresOperativoDto> GetOperativoAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var desdeDt = PeriodoDesdeUtc(desde);
        var hastaDt = PeriodoHastaUtc(hasta);
        var tareas = await _db.Tareas.AsNoTracking()
            .Where(t => t.CreatedAt >= desdeDt && t.CreatedAt <= hastaDt)
            .Select(t => new
            {
                t.Id,
                t.EstadoId,
                EstadoNombre = _db.TareasEstados.Where(e => e.Id == t.EstadoId).Select(e => e.Nombre).FirstOrDefault() ?? "",
                EstadoTerminal = _db.TareasEstados.Where(e => e.Id == t.EstadoId).Select(e => e.EsTerminal).FirstOrDefault(),
                t.FechaVencimiento,
                t.FechaCompletada,
                t.AsignadoPersonaId,
                AsignadoNombre = t.AsignadoPersonaId == null
                    ? null
                    : _db.Personas.Where(p => p.Id == t.AsignadoPersonaId).Select(p => p.Nombres + " " + p.Apellidos).FirstOrDefault(),
                t.PadreId,
                t.CreatedAt
            })
            .ToListAsync(ct);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var completadas = tareas.Count(t => t.EstadoNombre == EstadoTareaBase.Completada);
        var vencidas = tareas.Count(t => !t.EstadoTerminal && t.FechaVencimiento.HasValue && t.FechaVencimiento.Value < hoy);
        var enProgreso = tareas.Count(t => t.EstadoNombre == EstadoTareaBase.EnProgreso);
        var proyectosActivos = await _db.Tareas.AsNoTracking()
            .Where(t => t.PadreId == null)
            .CountAsync(t => !_db.TareasEstados.Where(e => e.Id == t.EstadoId).Select(e => e.EsTerminal).First(), ct);

        var cerradas = tareas.Where(t => t.FechaCompletada.HasValue).ToList();
        decimal? tiempoPromedio = cerradas.Count > 0
            ? Math.Round((decimal)cerradas.Average(t => (t.FechaCompletada!.Value - t.CreatedAt).TotalDays), 1)
            : null;

        var carga = tareas.Where(t => t.AsignadoPersonaId.HasValue && !string.IsNullOrEmpty(t.AsignadoNombre))
            .GroupBy(t => t.AsignadoNombre!)
            .Select(g => new CargaResponsableDto(
                g.Key,
                g.Count(t => !t.EstadoTerminal),
                g.Count(t => t.EstadoNombre == EstadoTareaBase.Completada)))
            .OrderByDescending(x => x.Activas)
            .Take(10)
            .ToList();

        return new IndicadoresOperativoDto(
            tareas.Count, completadas, vencidas, enProgreso,
            proyectosActivos, tiempoPromedio, carga);
    }

    public Task<IndicadoresMantenimientoDto> GetMantenimientoAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        // Modulo 2.11 vive en su propia rama; en esta rama no hay entidades
        // de intervencion. Devolvemos un agregado a partir de EquipoActivo
        // (categoria Operativo vs FueraDeServicio se materializara con merge 2.11).
        var activosTotal = _db.EquiposActivos.AsNoTracking().Count();
        return Task.FromResult(new IndicadoresMantenimientoDto(
            Intervenciones: 0, Preventivos: 0, Correctivos: 0,
            ActivosVencidos: 0, ActivosOk: activosTotal,
            PorActivo: Array.Empty<MantenimientoPorActivoDto>()));
    }

    public async Task<IndicadoresComunicacionesDto> GetComunicacionesAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var desdeDt = PeriodoDesdeUtc(desde);
        var hastaDt = PeriodoHastaUtc(hasta);
        var comunicados = await _db.Comunicados.AsNoTracking()
            .Where(c => c.CreatedAt >= desdeDt && c.CreatedAt <= hastaDt)
            .Select(c => new
            {
                c.Estado,
                c.TipoComunicado,
                c.TotalDestinatarios,
                Aperturas = _db.ComunicadoAcuses.Count(a => a.ComunicadoId == c.Id)
            })
            .ToListAsync(ct);

        var enviados = comunicados.Count(c => c.Estado == EstadoComunicado.Enviado);
        var programados = comunicados.Count(c => c.Estado == EstadoComunicado.Programado);
        var cancelados = comunicados.Count(c => c.Estado == EstadoComunicado.Cancelado);

        var conAcuse = comunicados.Where(c => c.TotalDestinatarios.HasValue && c.TotalDestinatarios.Value > 0).ToList();
        decimal? tasaPromedio = conAcuse.Count > 0
            ? Math.Round((decimal)conAcuse.Average(c => (double)c.Aperturas / c.TotalDestinatarios!.Value * 100), 2)
            : null;

        var porTipo = comunicados.GroupBy(c => c.TipoComunicado)
            .Select(g => new ComunicadosPorTipoDto(g.Key.ToString(), g.Count()))
            .ToList();

        return new IndicadoresComunicacionesDto(enviados, programados, cancelados, tasaPromedio, porTipo);
    }

    public async Task<IndicadoresDocumentosDto> GetDocumentosAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var desdeDt = PeriodoDesdeUtc(desde);
        var hastaDt = PeriodoHastaUtc(hasta);
        var total = await _db.Documentos.AsNoTracking().CountAsync(d => d.Activo, ct);
        var nuevos = await _db.Documentos.AsNoTracking()
            .CountAsync(d => d.Activo && d.CreatedAt >= desdeDt && d.CreatedAt <= hastaDt, ct);
        var nuevasVersiones = await _db.DocumentoVersiones.AsNoTracking()
            .CountAsync(v => v.CreatedAt >= desdeDt && v.CreatedAt <= hastaDt, ct);
        var compartidos = await _db.Documentos.AsNoTracking()
            .CountAsync(d => d.Activo && (d.Visibilidad == "EQUIPO" || d.Visibilidad == "PUBLICO"), ct);
        var tamano = await _db.Documentos.AsNoTracking()
            .Where(d => d.Activo && d.VersionActualId != null)
            .Join(_db.DocumentoVersiones, d => d.VersionActualId, v => v.Id, (d, v) => (long?)v.TamanoBytes)
            .SumAsync(ct) ?? 0;
        return new IndicadoresDocumentosDto(total, nuevos, nuevasVersiones, compartidos, tamano);
    }

    public async Task<IndicadoresContratosDto> GetContratosSegurosAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        // Mapea el enum de semaforo al string usado por la UI ("ninguno" -> "neutro").
        static string SemStr(SemaforoContrato s) => s switch
        {
            SemaforoContrato.Verde => "verde",
            SemaforoContrato.Amarillo => "amarillo",
            SemaforoContrato.Rojo => "rojo",
            _ => "neutro"
        };

        // ----- Contratos (2.5) -----
        var contratos = await _db.ContratosServicio.AsNoTracking()
            .Select(c => new { c.Id, c.Proveedor, c.TipoContrato, c.FechaInicio, c.FechaFin, c.ValorTotal })
            .ToListAsync(ct);

        var contratosCalc = contratos.Select(c =>
        {
            var sem = MiCopropiedad.MiCopropiedadService.CalcularSemaforoContrato(c.FechaInicio, c.FechaFin, hoy);
            var vencido = c.FechaFin is { } f && f < hoy;
            return new
            {
                c.Id, c.Proveedor, c.TipoContrato, c.FechaFin, c.ValorTotal, sem, vencido,
                dias = c.FechaFin is { } ff ? ff.DayNumber - hoy.DayNumber : (int?)null
            };
        }).ToList();

        var contratosActivos = contratosCalc.Count(c => !c.vencido);
        var contratosVencidos = contratosCalc.Count(c => c.vencido);
        var contratosPorVencer = contratosCalc.Count(c => !c.vencido && c.sem is SemaforoContrato.Amarillo or SemaforoContrato.Rojo);
        var valorContratado = contratosCalc.Where(c => !c.vencido).Sum(c => c.ValorTotal ?? 0m);
        var contratosProximos = contratosCalc
            .Where(c => c.sem is SemaforoContrato.Amarillo or SemaforoContrato.Rojo)
            .OrderBy(c => c.dias ?? int.MaxValue)
            .Take(50)
            .Select(c => new ItemVencimientoDto(
                c.Id, c.Proveedor, c.TipoContrato?.ToString(), c.FechaFin, c.dias, SemStr(c.sem), c.ValorTotal))
            .ToList();

        // ----- Seguros (polizas) -----
        var polizas = await _db.Polizas.AsNoTracking()
            .Select(p => new { p.Id, p.Aseguradora, p.NumeroPoliza, p.FechaInicio, p.FechaFin, p.ValorPoliza })
            .ToListAsync(ct);

        var polizasCalc = polizas.Select(p =>
        {
            var ini = p.FechaInicio ?? p.FechaFin ?? hoy;
            var sem = MiCopropiedad.MiCopropiedadService.CalcularSemaforoContrato(ini, p.FechaFin, hoy);
            var vencido = p.FechaFin is { } f && f < hoy;
            return new
            {
                p.Id, p.Aseguradora, p.NumeroPoliza, p.FechaFin, p.ValorPoliza, sem, vencido,
                dias = p.FechaFin is { } ff ? ff.DayNumber - hoy.DayNumber : (int?)null
            };
        }).ToList();

        var polizasActivas = polizasCalc.Count(p => !p.vencido);
        var polizasVencidas = polizasCalc.Count(p => p.vencido);
        var polizasPorVencer = polizasCalc.Count(p => !p.vencido && p.sem is SemaforoContrato.Amarillo or SemaforoContrato.Rojo);
        var valorAsegurado = polizasCalc.Where(p => !p.vencido).Sum(p => p.ValorPoliza ?? 0m);
        var polizasProximas = polizasCalc
            .Where(p => p.sem is SemaforoContrato.Amarillo or SemaforoContrato.Rojo)
            .OrderBy(p => p.dias ?? int.MaxValue)
            .Take(50)
            .Select(p => new ItemVencimientoDto(
                p.Id, p.Aseguradora, p.NumeroPoliza, p.FechaFin, p.dias, SemStr(p.sem), p.ValorPoliza))
            .ToList();

        return new IndicadoresContratosDto(
            contratosActivos, contratosPorVencer, contratosVencidos, valorContratado, contratosProximos,
            polizasActivas, polizasPorVencer, polizasVencidas, valorAsegurado, polizasProximas);
    }

    public async Task<KpisConsejoDto> GetKpisConsejoAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var fin = await GetFinancieroAsync(desde, hasta, ct);
        var car = await GetCarteraAsync(desde, hasta, ct);
        var pqr = await GetPqrsdAsync(desde, hasta, ct);
        var ope = await GetOperativoAsync(desde, hasta, ct);
        var man = await GetMantenimientoAsync(desde, hasta, ct);
        var com = await GetComunicacionesAsync(desde, hasta, ct);

        var semaforos = await _db.Set<ReporteSemaforoConfig>().AsNoTracking().ToListAsync(ct);
        ReporteSemaforoConfig? cfg(string key) => semaforos.FirstOrDefault(s => s.IndicadorKey == key);

        return new KpisConsejoDto(
            Recaudo: BuildKpi("recaudo_pct", "Recaudo del periodo",
                fin.RecaudoPct ?? 0, "%", cfg("recaudo_pct"),
                ascDefault: true, defAmar: 85m, defRojo: 70m),
            Mora: BuildKpi("mora_total", "Mora total",
                car.TotalMora, "COP", cfg("mora_total"),
                ascDefault: false, defAmar: 10_000_000m, defRojo: 30_000_000m),
            PqrsdVencidas: BuildKpi("pqrsd_vencidas", "PQRSD vencidas",
                pqr.Vencidas, "PQRSD", cfg("pqrsd_vencidas"),
                ascDefault: false, defAmar: 3m, defRojo: 7m),
            TareasActivas: BuildKpi("tareas_activas", "Tareas activas",
                ope.TotalTareas - ope.Completadas, "tareas", cfg("tareas_activas"),
                ascDefault: false, defAmar: 25m, defRojo: 50m),
            MantenimientoPendiente: BuildKpi("mantenimiento_vencidos", "Activos con mant. vencido",
                man.ActivosVencidos, "activos", cfg("mantenimiento_vencidos"),
                ascDefault: false, defAmar: 1m, defRojo: 3m),
            ComunicacionesEnviadas: BuildKpi("comunicaciones_enviados", "Comunicados enviados",
                com.Enviados, "envios", cfg("comunicaciones_enviados"),
                ascDefault: true, defAmar: 1m, defRojo: 0m));
    }

    public async Task<TransparenciaDto> GetTransparenciaAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var fin = await GetFinancieroAsync(desde, hasta, ct);
        var pqr = await GetPqrsdAsync(desde, hasta, ct);
        var ope = await GetOperativoAsync(desde, hasta, ct);

        // Ejecucion presupuestal: mes actual del presupuesto en ejecucion / 12.
        var presupuesto = await _db.Presupuestos.AsNoTracking()
            .Where(p => p.Estado == EstadoPresupuesto.EnEjecucion)
            .Select(p => new { p.VigenciaInicio, p.VigenciaFin })
            .FirstOrDefaultAsync(ct);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        int mesActual = 0;
        decimal ejecPct = 0m;
        if (presupuesto is not null)
        {
            mesActual = Math.Clamp(
                (hoy.Year - presupuesto.VigenciaInicio.Year) * 12 + hoy.Month - presupuesto.VigenciaInicio.Month + 1,
                0, 12);
            ejecPct = Math.Round((decimal)mesActual / 12m * 100m, 1);
        }

        return new TransparenciaDto(
            RecaudoPct: fin.RecaudoPct,
            RecaudoMes: fin.RecaudoRecibido,
            RecaudoMeta: fin.RecaudoEsperado,
            EjecucionPresupuestalPct: ejecPct,
            MesPresupuestoActual: mesActual,
            PqrsdRadicadas: pqr.Total,
            PqrsdResueltas: pqr.Resueltas,
            PqrsdEnTramite: pqr.EnTramite,
            TareasCompletadas: ope.Completadas,
            ProyectosActivos: ope.ProyectosActivos);
    }

    private static KpiDto BuildKpi(
        string key, string etiqueta, decimal valor, string unidad,
        ReporteSemaforoConfig? cfg,
        bool ascDefault, decimal defAmar, decimal defRojo)
    {
        var asc = cfg?.EsAscendente ?? ascDefault;
        var amar = cfg?.UmbralAmarillo ?? defAmar;
        var rojo = cfg?.UmbralRojo ?? defRojo;
        string semaforo;
        if (asc)
        {
            // mas es mejor: rojo <= rojo, amarillo <= amar, verde > amar
            if (valor <= rojo) semaforo = "rojo";
            else if (valor <= amar) semaforo = "amarillo";
            else semaforo = "verde";
        }
        else
        {
            // menos es mejor: verde <= amar, amarillo <= rojo, rojo > rojo
            if (valor <= amar) semaforo = "verde";
            else if (valor <= rojo) semaforo = "amarillo";
            else semaforo = "rojo";
        }
        return new KpiDto(key, etiqueta, valor, unidad, semaforo, null);
    }
}
