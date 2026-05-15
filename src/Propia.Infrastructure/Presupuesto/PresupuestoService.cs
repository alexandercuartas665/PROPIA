using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Presupuesto;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Presupuesto;

/// <summary>
/// Servicio del modulo 2.6 Presupuesto, Cuotas y Pagos (spec v1.0).
///
/// Decisiones clave (Notas dev spec):
///  - Motor de liquidacion idempotente: insertar mismo (presupuestoId, periodo) dos veces no duplica
///    (constraint unique + chequeo previo).
///  - Snapshot inmutable: el JSON snapshot_calculo captura coeficientes y montos del momento del calculo.
///  - Reliquidacion siempre manual: el sistema nunca recalcula automaticamente cobros emitidos.
///  - Auditoria automatica en cada operacion via RegistrarAuditoriaAsync.
/// </summary>
public class PresupuestoService : IPresupuestoService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;

    public PresupuestoService(PropiaDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    // ===================== Editor de presupuesto =====================

    public async Task<IReadOnlyList<PresupuestoDto>> ListarPresupuestosAsync(CancellationToken ct)
    {
        var lista = await _db.Presupuestos
            .AsNoTracking()
            .OrderByDescending(p => p.VigenciaInicio)
            .ToListAsync(ct);

        var counts = await _db.PresupuestoRubros
            .Where(r => r.Activo)
            .GroupBy(r => r.PresupuestoId)
            .Select(g => new { Id = g.Key, Cant = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Cant, ct);

        var liqs = await _db.Liquidaciones
            .GroupBy(l => l.PresupuestoId)
            .Select(g => new { Id = g.Key, Cant = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Cant, ct);

        return lista.Select(p => new PresupuestoDto(
            p.Id, p.Nombre, p.VigenciaInicio, p.VigenciaFin,
            p.Estado, p.MontoTotal, p.AprobacionTipo, p.AprobacionFecha,
            counts.GetValueOrDefault(p.Id, 0),
            liqs.GetValueOrDefault(p.Id, 0))).ToList();
    }

    public async Task<PresupuestoDetalleDto?> GetPresupuestoDetalleAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.Presupuestos
            .AsNoTracking()
            .Include(x => x.Rubros)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;

        var totalActivo = p.Rubros.Where(r => r.Activo).Sum(r => r.MontoAnual);
        var fondoImprevistos = p.Rubros.FirstOrDefault(r => r.EsFondoImprevistos && r.Activo)?.MontoAnual ?? 0m;
        var minimoLegal = totalActivo * 0.01m;  // Art. 39 Ley 675

        // Cuota proyectada promedio: total mensual / cantidad de unidades activas
        var unidades = await _db.UnidadesPrivadas.CountAsync(ct);
        var mesesVigencia = Math.Max(1, MesesEntreFechas(p.VigenciaInicio, p.VigenciaFin));
        var cuotaPromMes = unidades > 0 ? (totalActivo / mesesVigencia) / unidades : 0m;

        var rubrosDto = p.Rubros
            .OrderBy(r => r.Orden).ThenBy(r => r.Nombre)
            .Select(r => new RubroDto(r.Id, r.Codigo, r.Nombre, r.MontoAnual, r.BaseLiquidacion,
                r.EsFondoImprevistos, r.EsObligatorio, r.Activo, r.Orden, r.NotasInternas))
            .ToList();

        return new PresupuestoDetalleDto(
            p.Id, p.Nombre, p.VigenciaInicio, p.VigenciaFin,
            p.Estado, totalActivo, p.AprobacionTipo, p.AprobacionActaUrl, p.AprobacionFecha,
            p.Notas, rubrosDto, cuotaPromMes,
            fondoImprevistos, minimoLegal,
            fondoImprevistos >= minimoLegal);
    }

    public async Task<PresupuestoDto> CrearPresupuestoAsync(CrearPresupuestoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre obligatorio.");
        if (req.VigenciaFin <= req.VigenciaInicio) throw new InvalidOperationException("VigenciaFin debe ser posterior a VigenciaInicio.");

        var p = new Domain.Entities.Presupuesto
        {
            Nombre = req.Nombre.Trim(),
            VigenciaInicio = req.VigenciaInicio,
            VigenciaFin = req.VigenciaFin,
            Estado = EstadoPresupuesto.Borrador,
            MontoTotal = 0
        };
        _db.Presupuestos.Add(p);
        await _db.SaveChangesAsync(ct);

        if (req.IncluirCatalogoBase)
        {
            var orden = 0;
            foreach (var (codigo, nombre, obligatorio) in RubroCatalogo.Base)
            {
                _db.PresupuestoRubros.Add(new PresupuestoRubro
                {
                    PresupuestoId = p.Id,
                    Codigo = codigo,
                    Nombre = nombre,
                    MontoAnual = 0,
                    BaseLiquidacion = BaseLiquidacion.Coeficiente,
                    EsFondoImprevistos = codigo == RubroCatalogo.FondoImprevistos,
                    EsObligatorio = obligatorio,
                    Activo = true,
                    Orden = ++orden
                });
            }
            await _db.SaveChangesAsync(ct);
        }

        await RegistrarAuditoriaAsync("presupuesto", p.Id, "creado", null,
            JsonSerializer.Serialize(new { p.Nombre, p.VigenciaInicio, p.VigenciaFin }), ct);
        return new PresupuestoDto(p.Id, p.Nombre, p.VigenciaInicio, p.VigenciaFin,
            p.Estado, 0, null, null,
            req.IncluirCatalogoBase ? RubroCatalogo.Base.Length : 0, 0);
    }

    public async Task<bool> ActualizarRubroAsync(Guid rubroId, ActualizarRubroRequest req, CancellationToken ct)
    {
        var r = await _db.PresupuestoRubros.Include(x => x.Presupuesto).FirstOrDefaultAsync(x => x.Id == rubroId, ct);
        if (r is null) return false;
        if (r.Presupuesto!.Estado is EstadoPresupuesto.Cerrado or EstadoPresupuesto.EnEjecucion)
            throw new InvalidOperationException("No se puede editar un presupuesto en ejecucion o cerrado. Usa una nueva vigencia.");
        if (r.EsFondoImprevistos && !req.Activo)
            throw new InvalidOperationException("El Fondo de Imprevistos no se puede desactivar (RN-02).");

        var anterior = JsonSerializer.Serialize(new { r.Nombre, r.MontoAnual, r.BaseLiquidacion, r.Activo });
        if (!r.EsObligatorio) r.Nombre = req.Nombre.Trim();
        r.MontoAnual = req.MontoAnual;
        r.BaseLiquidacion = req.BaseLiquidacion;
        r.Activo = req.Activo;
        r.Orden = req.Orden;
        r.NotasInternas = req.NotasInternas;
        await RecalcularTotalAsync(r.PresupuestoId, ct);
        await _db.SaveChangesAsync(ct);

        await RegistrarAuditoriaAsync("rubro", r.Id, "editado", anterior,
            JsonSerializer.Serialize(new { r.Nombre, r.MontoAnual, r.BaseLiquidacion, r.Activo }), ct);
        return true;
    }

    public async Task<RubroDto> AgregarRubroPersonalizadoAsync(Guid presupuestoId, AgregarRubroPersonalizadoRequest req, CancellationToken ct)
    {
        var p = await _db.Presupuestos.FirstOrDefaultAsync(x => x.Id == presupuestoId, ct)
            ?? throw new InvalidOperationException("Presupuesto no encontrado.");
        if (p.Estado is EstadoPresupuesto.Cerrado or EstadoPresupuesto.EnEjecucion)
            throw new InvalidOperationException("No se puede agregar rubros a un presupuesto en ejecucion o cerrado.");
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre obligatorio.");

        var maxOrden = await _db.PresupuestoRubros.Where(r => r.PresupuestoId == presupuestoId).Select(r => (int?)r.Orden).MaxAsync(ct) ?? 0;
        var codigo = "CUSTOM_" + new string(req.Nombre.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

        var r = new PresupuestoRubro
        {
            PresupuestoId = presupuestoId,
            Codigo = codigo,
            Nombre = req.Nombre.Trim(),
            MontoAnual = req.MontoAnual,
            BaseLiquidacion = req.BaseLiquidacion,
            EsFondoImprevistos = false,
            EsObligatorio = false,
            Activo = true,
            Orden = maxOrden + 1
        };
        _db.PresupuestoRubros.Add(r);
        await RecalcularTotalAsync(presupuestoId, ct);
        await _db.SaveChangesAsync(ct);

        await RegistrarAuditoriaAsync("rubro", r.Id, "creado", null,
            JsonSerializer.Serialize(new { r.Nombre, r.MontoAnual }), ct);

        return new RubroDto(r.Id, r.Codigo, r.Nombre, r.MontoAnual, r.BaseLiquidacion,
            false, false, true, r.Orden, null);
    }

    public async Task<bool> EliminarRubroAsync(Guid rubroId, CancellationToken ct)
    {
        var r = await _db.PresupuestoRubros.Include(x => x.Presupuesto).FirstOrDefaultAsync(x => x.Id == rubroId, ct);
        if (r is null) return false;
        if (r.EsFondoImprevistos || r.EsObligatorio)
            throw new InvalidOperationException("No se puede eliminar un rubro obligatorio (RN-02).");
        if (r.Presupuesto!.Estado is EstadoPresupuesto.EnEjecucion or EstadoPresupuesto.Cerrado)
            throw new InvalidOperationException("No se puede eliminar rubros en presupuesto en ejecucion.");

        _db.PresupuestoRubros.Remove(r);
        await RecalcularTotalAsync(r.PresupuestoId, ct);
        await _db.SaveChangesAsync(ct);
        await RegistrarAuditoriaAsync("rubro", r.Id, "eliminado", JsonSerializer.Serialize(new { r.Nombre }), null, ct);
        return true;
    }

    public async Task<bool> EnviarAAprobacionAsync(Guid presupuestoId, CancellationToken ct)
    {
        var p = await _db.Presupuestos.FirstOrDefaultAsync(x => x.Id == presupuestoId, ct);
        if (p is null) return false;
        if (p.Estado != EstadoPresupuesto.Borrador)
            throw new InvalidOperationException("Solo los borradores pueden enviarse a aprobacion.");
        p.Estado = EstadoPresupuesto.EnAprobacion;
        await _db.SaveChangesAsync(ct);
        await RegistrarAuditoriaAsync("presupuesto", p.Id, "enviado_a_aprobacion", null, null, ct);
        return true;
    }

    public async Task<bool> AprobarPresupuestoAsync(Guid presupuestoId, AprobarPresupuestoRequest req, CancellationToken ct)
    {
        var p = await _db.Presupuestos.FirstOrDefaultAsync(x => x.Id == presupuestoId, ct);
        if (p is null) return false;
        if (p.Estado is not (EstadoPresupuesto.EnAprobacion or EstadoPresupuesto.Borrador))
            throw new InvalidOperationException($"No se puede aprobar un presupuesto en estado {p.Estado}.");

        p.Estado = EstadoPresupuesto.Aprobado;
        p.AprobacionTipo = req.Tipo;
        p.AprobacionFecha = req.Fecha;
        p.AprobacionActaUrl = req.ActaUrl;
        p.AsambleaId = req.AsambleaId;
        await _db.SaveChangesAsync(ct);
        await RegistrarAuditoriaAsync("presupuesto", p.Id, "aprobado", null,
            JsonSerializer.Serialize(new { req.Tipo, req.Fecha }), ct);
        return true;
    }

    public async Task<bool> ActivarVigenciaAsync(Guid presupuestoId, CancellationToken ct)
    {
        var p = await _db.Presupuestos.FirstOrDefaultAsync(x => x.Id == presupuestoId, ct);
        if (p is null) return false;
        if (p.Estado != EstadoPresupuesto.Aprobado)
            throw new InvalidOperationException("Solo presupuestos Aprobados pueden activarse.");

        // RN-01: Solo puede haber una vigencia EnEjecucion. Cerramos las anteriores.
        var enEjecucion = await _db.Presupuestos
            .Where(x => x.Estado == EstadoPresupuesto.EnEjecucion && x.Id != presupuestoId)
            .ToListAsync(ct);
        foreach (var prev in enEjecucion) prev.Estado = EstadoPresupuesto.Cerrado;

        p.Estado = EstadoPresupuesto.EnEjecucion;
        await _db.SaveChangesAsync(ct);
        await RegistrarAuditoriaAsync("presupuesto", p.Id, "activado", null, null, ct);
        return true;
    }

    public async Task<bool> CerrarVigenciaAsync(Guid presupuestoId, CancellationToken ct)
    {
        var p = await _db.Presupuestos.FirstOrDefaultAsync(x => x.Id == presupuestoId, ct);
        if (p is null) return false;
        p.Estado = EstadoPresupuesto.Cerrado;
        await _db.SaveChangesAsync(ct);
        await RegistrarAuditoriaAsync("presupuesto", p.Id, "cerrado", null, null, ct);
        return true;
    }

    public async Task<bool> EliminarBorradorAsync(Guid presupuestoId, CancellationToken ct)
    {
        var p = await _db.Presupuestos.FirstOrDefaultAsync(x => x.Id == presupuestoId, ct);
        if (p is null) return false;
        if (p.Estado != EstadoPresupuesto.Borrador)
            throw new InvalidOperationException("Solo borradores pueden eliminarse. A partir de En aprobacion solo se archiva (RN-03).");
        _db.Presupuestos.Remove(p);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Motor de liquidacion =====================

    public async Task<LiquidacionDto> EmitirLiquidacionAsync(EmitirLiquidacionRequest req, CancellationToken ct)
    {
        var presupuesto = await _db.Presupuestos
            .Include(p => p.Rubros)
            .FirstOrDefaultAsync(p => p.Id == req.PresupuestoId, ct)
            ?? throw new InvalidOperationException("Presupuesto no encontrado.");
        if (presupuesto.Estado != EstadoPresupuesto.EnEjecucion)
            throw new InvalidOperationException("El presupuesto debe estar EnEjecucion para emitir liquidaciones.");

        var periodo = new DateOnly(req.Periodo.Year, req.Periodo.Month, 1);  // Primer dia del mes
        if (periodo < presupuesto.VigenciaInicio || periodo > presupuesto.VigenciaFin)
            throw new InvalidOperationException("El periodo esta fuera de la vigencia del presupuesto.");

        // Idempotencia (Notas dev): si ya existe, devolverla.
        var existente = await _db.Liquidaciones
            .FirstOrDefaultAsync(l => l.PresupuestoId == req.PresupuestoId && l.Periodo == periodo, ct);
        if (existente is not null)
            return await ArmarLiquidacionDto(existente, ct);

        var unidades = await _db.UnidadesPrivadas
            .AsNoTracking()
            .Include(u => u.Torre)
            .ToListAsync(ct);
        if (unidades.Count == 0)
            throw new InvalidOperationException("No hay unidades configuradas. Crea unidades en Mi Copropiedad antes de liquidar.");

        var totalCoef = unidades.Sum(u => u.CoeficientePropiedad);
        if (totalCoef <= 0)
            throw new InvalidOperationException("La suma de coeficientes es 0. Configura los coeficientes en Mi Copropiedad.");

        var mesesVigencia = Math.Max(1, MesesEntreFechas(presupuesto.VigenciaInicio, presupuesto.VigenciaFin));
        var rubrosActivos = presupuesto.Rubros.Where(r => r.Activo).ToList();

        // Snapshot: capturamos todo el contexto del calculo
        var snapshot = new
        {
            calculadoAt = DateTimeOffset.UtcNow,
            presupuestoTotal = rubrosActivos.Sum(r => r.MontoAnual),
            mesesVigencia,
            totalCoeficientes = totalCoef,
            unidades = unidades.Select(u => new { u.Id, u.Numero, u.CoeficientePropiedad }).ToList(),
            rubros = rubrosActivos.Select(r => new { r.Id, r.Codigo, r.Nombre, r.MontoAnual, r.BaseLiquidacion }).ToList()
        };

        var liquidacion = new Liquidacion
        {
            PresupuestoId = req.PresupuestoId,
            Periodo = periodo,
            Estado = EstadoLiquidacion.Emitida,
            MontoTotal = 0,
            SnapshotCalculo = JsonSerializer.Serialize(snapshot),
            EmitidaPor = Guid.Empty  // En produccion: usuario del JWT
        };
        _db.Liquidaciones.Add(liquidacion);
        await _db.SaveChangesAsync(ct);

        decimal montoTotalLiquidacion = 0m;

        foreach (var unidad in unidades)
        {
            decimal montoUnidad = 0m;
            var desglose = new List<RenglonDesgloseDto>();
            foreach (var r in rubrosActivos)
            {
                var aporteRubroMes = r.MontoAnual / mesesVigencia;
                decimal aporteUnidad;
                if (r.BaseLiquidacion == BaseLiquidacion.Coeficiente)
                {
                    aporteUnidad = aporteRubroMes * (unidad.CoeficientePropiedad / totalCoef);
                }
                else  // CuotaFijaPorTipo o Mixto: por simplicidad MVP, repartimos igual entre unidades del mismo tipo
                {
                    var sameType = unidades.Count(u => u.Tipo == unidad.Tipo);
                    aporteUnidad = sameType > 0 ? aporteRubroMes / sameType : 0;
                }
                montoUnidad += aporteUnidad;
                desglose.Add(new RenglonDesgloseDto(r.Codigo, r.Nombre, Math.Round(aporteUnidad, 2), unidad.CoeficientePropiedad));
            }

            _db.LiquidacionUnidades.Add(new LiquidacionUnidad
            {
                LiquidacionId = liquidacion.Id,
                UnidadPrivadaId = unidad.Id,
                Monto = Math.Round(montoUnidad, 2),
                Desglose = JsonSerializer.Serialize(desglose),
                EstadoPago = EstadoPagoLiquidacion.Pendiente
            });
            montoTotalLiquidacion += Math.Round(montoUnidad, 2);
        }

        liquidacion.MontoTotal = montoTotalLiquidacion;
        await _db.SaveChangesAsync(ct);
        await RegistrarAuditoriaAsync("liquidacion", liquidacion.Id, "emitida", null,
            JsonSerializer.Serialize(new { periodo, montoTotal = montoTotalLiquidacion, unidades = unidades.Count }), ct);

        return await ArmarLiquidacionDto(liquidacion, ct);
    }

    public async Task<IReadOnlyList<LiquidacionDto>> ListarLiquidacionesAsync(Guid? presupuestoId, CancellationToken ct)
    {
        var q = _db.Liquidaciones.AsNoTracking().Include(l => l.Presupuesto).AsQueryable();
        if (presupuestoId.HasValue) q = q.Where(l => l.PresupuestoId == presupuestoId.Value);
        var lista = await q.OrderByDescending(l => l.Periodo).ToListAsync(ct);

        var liqIds = lista.Select(l => l.Id).ToList();
        var counts = await _db.LiquidacionUnidades
            .Where(lu => liqIds.Contains(lu.LiquidacionId))
            .GroupBy(lu => lu.LiquidacionId)
            .Select(g => new { Id = g.Key, Cant = g.Count(), Pagadas = g.Count(x => x.EstadoPago == EstadoPagoLiquidacion.Pagado) })
            .ToDictionaryAsync(x => x.Id, x => new { x.Cant, x.Pagadas }, ct);

        return lista.Select(l =>
        {
            counts.TryGetValue(l.Id, out var c);
            return new LiquidacionDto(l.Id, l.PresupuestoId, l.Presupuesto?.Nombre ?? "",
                l.Periodo, l.Estado, l.MontoTotal, l.CreatedAt,
                c?.Cant ?? 0, c?.Pagadas ?? 0);
        }).ToList();
    }

    public async Task<LiquidacionUnidadDto?> GetLiquidacionUnidadAsync(Guid id, CancellationToken ct)
    {
        var lu = await _db.LiquidacionUnidades
            .AsNoTracking()
            .Include(x => x.UnidadPrivada).ThenInclude(u => u!.Torre)
            .Include(x => x.Liquidacion)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (lu is null) return null;
        var desglose = JsonSerializer.Deserialize<List<RenglonDesgloseDto>>(lu.Desglose) ?? new();
        return new LiquidacionUnidadDto(lu.Id, lu.LiquidacionId, lu.Liquidacion!.Periodo,
            lu.UnidadPrivadaId, lu.UnidadPrivada!.Numero, lu.UnidadPrivada.Torre?.Nombre,
            lu.Monto, lu.EstadoPago, desglose);
    }

    // ===================== Panel de recaudo =====================

    public async Task<RecaudoResumenDto> GetRecaudoResumenAsync(DateOnly periodo, CancellationToken ct)
    {
        var primerDia = new DateOnly(periodo.Year, periodo.Month, 1);
        var liqs = await _db.LiquidacionUnidades
            .AsNoTracking()
            .Include(lu => lu.Liquidacion)
            .Where(lu => lu.Liquidacion!.Periodo == primerDia)
            .ToListAsync(ct);

        decimal esperado = liqs.Sum(l => l.Monto);
        decimal recibido = liqs.Where(l => l.EstadoPago == EstadoPagoLiquidacion.Pagado).Sum(l => l.Monto);
        decimal pendiente = esperado - recibido;
        decimal pct = esperado > 0 ? Math.Round(recibido / esperado * 100m, 1) : 0m;
        int total = liqs.Count;
        int pagadas = liqs.Count(l => l.EstadoPago == EstadoPagoLiquidacion.Pagado);
        int pendientes = liqs.Count(l => l.EstadoPago == EstadoPagoLiquidacion.Pendiente);
        int vencidas = liqs.Count(l => l.EstadoPago == EstadoPagoLiquidacion.Vencido);

        return new RecaudoResumenDto(primerDia, esperado, recibido, pendiente, pct, total, pagadas, pendientes, vencidas);
    }

    public async Task<IReadOnlyList<UnidadRecaudoDto>> ListarUnidadesRecaudoAsync(DateOnly periodo, EstadoPagoLiquidacion? estado, CancellationToken ct)
    {
        var primerDia = new DateOnly(periodo.Year, periodo.Month, 1);
        var q = _db.LiquidacionUnidades
            .AsNoTracking()
            .Include(lu => lu.Liquidacion)
            .Include(lu => lu.UnidadPrivada).ThenInclude(u => u!.Torre)
            .Where(lu => lu.Liquidacion!.Periodo == primerDia);
        if (estado.HasValue) q = q.Where(lu => lu.EstadoPago == estado.Value);
        var lista = await q.OrderBy(lu => lu.UnidadPrivada!.Torre!.Nombre).ThenBy(lu => lu.UnidadPrivada!.Numero).ToListAsync(ct);

        return lista.Select(lu => new UnidadRecaudoDto(
            lu.UnidadPrivadaId, lu.UnidadPrivada!.Numero,
            lu.UnidadPrivada.Torre?.Nombre, null,  // PropietarioNombre se llena cuando exista 2.4 vinculo
            lu.Monto, lu.EstadoPago, lu.Id)).ToList();
    }

    // ===================== Pagos manuales =====================

    public async Task<PagoDto> RegistrarPagoManualAsync(RegistrarPagoManualRequest req, CancellationToken ct)
    {
        var lu = await _db.LiquidacionUnidades.Include(x => x.UnidadPrivada).FirstOrDefaultAsync(x => x.Id == req.LiquidacionUnidadId, ct)
            ?? throw new InvalidOperationException("Liquidacion no encontrada.");
        if (req.Monto <= 0) throw new InvalidOperationException("Monto debe ser > 0.");

        var pago = new PagoCuota
        {
            UnidadPrivadaId = lu.UnidadPrivadaId,
            LiquidacionUnidadId = lu.Id,
            Tipo = TipoPago.CuotaOrdinaria,
            Canal = req.Canal,
            Monto = req.Monto,
            ReferenciaExterna = req.ReferenciaExterna,
            FechaPago = req.FechaPago.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            Estado = EstadoPago.Confirmado,
            EsManual = true,
            Notas = req.Notas
        };
        _db.PagosCuotas.Add(pago);

        // Marcar la liquidacion como pagada si el monto cubre el total (MVP simple)
        if (req.Monto >= lu.Monto)
            lu.EstadoPago = EstadoPagoLiquidacion.Pagado;

        await _db.SaveChangesAsync(ct);
        await RegistrarAuditoriaAsync("pago", pago.Id, "registrado_manual", null,
            JsonSerializer.Serialize(new { pago.Monto, req.Canal, req.FechaPago }), ct);

        return await ArmarPagoDto(pago.Id, ct) ?? throw new InvalidOperationException("Pago no encontrado tras crear.");
    }

    public async Task<IReadOnlyList<PagoDto>> ListarPagosAsync(Guid? unidadId, CancellationToken ct)
    {
        var q = _db.PagosCuotas
            .AsNoTracking()
            .Include(p => p.UnidadPrivada)
            .Include(p => p.LiquidacionUnidad).ThenInclude(lu => lu!.Liquidacion)
            .AsQueryable();
        if (unidadId.HasValue) q = q.Where(p => p.UnidadPrivadaId == unidadId.Value);
        var lista = await q.OrderByDescending(p => p.FechaPago).ToListAsync(ct);
        return lista.Select(p => ToPagoDto(p)).ToList();
    }

    // ===================== Cuotas extraordinarias =====================

    public async Task<IReadOnlyList<CuotaExtraordinariaDto>> ListarCuotasExtraordinariasAsync(CancellationToken ct)
    {
        var lista = await _db.CuotasExtraordinarias.AsNoTracking().OrderByDescending(c => c.CreatedAt).ToListAsync(ct);
        var pagosPorCuota = await _db.PagosCuotas
            .Where(p => p.CuotaExtraordinariaId != null && p.Estado == EstadoPago.Confirmado)
            .GroupBy(p => p.CuotaExtraordinariaId!.Value)
            .Select(g => new { Id = g.Key, Monto = g.Sum(x => x.Monto) })
            .ToDictionaryAsync(x => x.Id, x => x.Monto, ct);

        return lista.Select(c => new CuotaExtraordinariaDto(
            c.Id, c.Nombre, c.Proposito, c.MontoTotal, c.FormaRecaudo, c.NumeroCuotas,
            c.BaseLiquidacion, c.Estado, c.FechaInicioRecaudo, c.FechaFinRecaudo,
            pagosPorCuota.GetValueOrDefault(c.Id, 0m))).ToList();
    }

    public async Task<CuotaExtraordinariaDto> CrearCuotaExtraordinariaAsync(CrearCuotaExtraordinariaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre obligatorio.");
        if (req.MontoTotal <= 0) throw new InvalidOperationException("MontoTotal debe ser > 0.");

        var c = new CuotaExtraordinaria
        {
            Nombre = req.Nombre.Trim(),
            Proposito = req.Proposito,
            MontoTotal = req.MontoTotal,
            FormaRecaudo = req.FormaRecaudo,
            NumeroCuotas = req.NumeroCuotas,
            BaseLiquidacion = req.BaseLiquidacion,
            Estado = EstadoCuotaExtraordinaria.PendienteAprobacion,
            CreadaPorPersonaId = Guid.Empty
        };
        _db.CuotasExtraordinarias.Add(c);
        await _db.SaveChangesAsync(ct);
        await RegistrarAuditoriaAsync("cuota_extraordinaria", c.Id, "creada", null,
            JsonSerializer.Serialize(new { c.Nombre, c.MontoTotal }), ct);

        return new CuotaExtraordinariaDto(c.Id, c.Nombre, c.Proposito, c.MontoTotal, c.FormaRecaudo,
            c.NumeroCuotas, c.BaseLiquidacion, c.Estado, null, null, 0m);
    }

    public async Task<bool> AprobarCuotaExtraordinariaAsync(Guid cuotaId, AprobarCuotaExtraordinariaRequest req, CancellationToken ct)
    {
        var c = await _db.CuotasExtraordinarias.FirstOrDefaultAsync(x => x.Id == cuotaId, ct);
        if (c is null) return false;
        if (c.Estado != EstadoCuotaExtraordinaria.PendienteAprobacion)
            throw new InvalidOperationException("Solo cuotas Pendientes pueden aprobarse.");
        if (req.Tipo == TipoAprobacion.Manual && string.IsNullOrEmpty(req.ActaUrl))
            throw new InvalidOperationException("Aprobacion manual requiere acta adjunta (RN-07).");

        c.Estado = EstadoCuotaExtraordinaria.Aprobada;
        c.AprobacionTipo = req.Tipo;
        c.AprobacionActaUrl = req.ActaUrl;
        c.AsambleaId = req.AsambleaId;
        c.FechaInicioRecaudo = req.FechaInicioRecaudo;
        c.FechaFinRecaudo = req.FechaFinRecaudo;
        await _db.SaveChangesAsync(ct);
        await RegistrarAuditoriaAsync("cuota_extraordinaria", c.Id, "aprobada", null,
            JsonSerializer.Serialize(new { req.Tipo, req.FechaInicioRecaudo }), ct);
        return true;
    }

    // ===================== Vista del residente =====================

    public async Task<MiCuotaDto?> GetMiCuotaAsync(Guid unidadId, DateOnly? periodo, CancellationToken ct)
    {
        var unidad = await _db.UnidadesPrivadas.AsNoTracking().Include(u => u.Torre).FirstOrDefaultAsync(u => u.Id == unidadId, ct);
        if (unidad is null) return null;

        var p = periodo.HasValue ? new DateOnly(periodo.Value.Year, periodo.Value.Month, 1) : default;
        var lu = await _db.LiquidacionUnidades
            .AsNoTracking()
            .Include(x => x.Liquidacion)
            .Where(x => x.UnidadPrivadaId == unidadId)
            .Where(x => periodo == null || x.Liquidacion!.Periodo == p)
            .OrderByDescending(x => x.Liquidacion!.Periodo)
            .FirstOrDefaultAsync(ct);

        var desglose = lu is null ? new List<RenglonDesgloseDto>() :
            JsonSerializer.Deserialize<List<RenglonDesgloseDto>>(lu.Desglose) ?? new();

        // Cuotas extraordinarias activas: por simplicidad MVP, mostramos las que estan EnRecaudo
        var extras = await _db.CuotasExtraordinarias
            .AsNoTracking()
            .Where(c => c.Estado == EstadoCuotaExtraordinaria.EnRecaudo || c.Estado == EstadoCuotaExtraordinaria.Aprobada)
            .ToListAsync(ct);

        var totalCoef = await _db.UnidadesPrivadas.SumAsync(u => u.CoeficientePropiedad, ct);
        var extrasDto = extras.Select(e => new CuotaExtraordinariaPendienteDto(
            e.Id, e.Nombre,
            totalCoef > 0 ? Math.Round(e.MontoTotal * (unidad.CoeficientePropiedad / totalCoef), 2) : 0m,
            null, e.NumeroCuotas)).ToList();

        return new MiCuotaDto(
            lu?.Id, lu?.Liquidacion?.Periodo,
            unidad.Id, unidad.Numero, unidad.Torre?.Nombre,
            lu?.Monto ?? 0m, lu?.EstadoPago,
            lu?.Liquidacion?.Periodo.AddMonths(1),  // Vence ultimo dia del mes liquidado, aprox
            desglose, extrasDto);
    }

    // ===================== Auditoria =====================

    public async Task<IReadOnlyList<AuditPresupuestoDto>> ListarAuditoriaAsync(int limit, CancellationToken ct)
    {
        limit = limit <= 0 ? 50 : Math.Min(limit, 500);
        return await _db.AuditLogPresupuestos
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .Select(a => new AuditPresupuestoDto(a.Id, a.CreatedAt, a.Entidad, a.EntidadId,
                a.Accion, a.UsuarioId, a.ValorAnterior, a.ValorNuevo))
            .ToListAsync(ct);
    }

    // ===================== Helpers =====================

    private async Task RecalcularTotalAsync(Guid presupuestoId, CancellationToken ct)
    {
        var p = await _db.Presupuestos.Include(x => x.Rubros).FirstAsync(x => x.Id == presupuestoId, ct);
        p.MontoTotal = p.Rubros.Where(r => r.Activo).Sum(r => r.MontoAnual);
    }

    private async Task<LiquidacionDto> ArmarLiquidacionDto(Liquidacion l, CancellationToken ct)
    {
        var cant = await _db.LiquidacionUnidades.CountAsync(lu => lu.LiquidacionId == l.Id, ct);
        var pagadas = await _db.LiquidacionUnidades.CountAsync(lu => lu.LiquidacionId == l.Id && lu.EstadoPago == EstadoPagoLiquidacion.Pagado, ct);
        var pres = await _db.Presupuestos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == l.PresupuestoId, ct);
        return new LiquidacionDto(l.Id, l.PresupuestoId, pres?.Nombre ?? "",
            l.Periodo, l.Estado, l.MontoTotal, l.CreatedAt, cant, pagadas);
    }

    private async Task<PagoDto?> ArmarPagoDto(Guid pagoId, CancellationToken ct)
    {
        var p = await _db.PagosCuotas
            .AsNoTracking()
            .Include(x => x.UnidadPrivada)
            .Include(x => x.LiquidacionUnidad).ThenInclude(lu => lu!.Liquidacion)
            .FirstOrDefaultAsync(x => x.Id == pagoId, ct);
        return p is null ? null : ToPagoDto(p);
    }

    private static PagoDto ToPagoDto(PagoCuota p) =>
        new(p.Id, p.LiquidacionUnidadId, p.LiquidacionUnidad?.Liquidacion?.Periodo,
            p.UnidadPrivadaId, p.UnidadPrivada?.Numero ?? "",
            p.Tipo, p.Canal, p.Monto, p.FechaPago, p.Estado,
            p.EsManual, p.ReferenciaExterna, p.Notas);

    private static int MesesEntreFechas(DateOnly inicio, DateOnly fin)
    {
        return ((fin.Year - inicio.Year) * 12) + (fin.Month - inicio.Month) + 1;
    }

    private async Task RegistrarAuditoriaAsync(string entidad, Guid entidadId, string accion, string? anterior, string? nuevo, CancellationToken ct)
    {
        _db.AuditLogPresupuestos.Add(new AuditLogPresupuesto
        {
            Entidad = entidad,
            EntidadId = entidadId,
            Accion = accion,
            ValorAnterior = anterior,
            ValorNuevo = nuevo,
            UsuarioId = Guid.Empty  // En produccion: extraer del JWT
        });
        await _db.SaveChangesAsync(ct);
    }
}
