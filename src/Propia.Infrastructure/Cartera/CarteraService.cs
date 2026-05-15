using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Cartera;
using Propia.Application.Common;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Cartera;

/// <summary>
/// Servicio del modulo 2.7 Cartera y Estado de Cuenta (spec v1.0 - MVP).
///
/// Operacion clave: sincroniza la deuda desde LiquidacionUnidad (2.6) y aplica
/// la regla de imputacion configurada al registrar pagos. Mantiene CarteraUnidad
/// como snapshot agregado y un historial append-only.
/// </summary>
public class CarteraService : ICarteraService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _http;

    public CarteraService(PropiaDbContext db, ITenantContext tenantContext, IHttpContextAccessor http)
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

    private Guid? GetPersonaActualId()
    {
        var sub = _http.HttpContext?.User?.FindFirstValue("persona_id");
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    // ===================== Seed lazy =====================

    private async Task AsegurarConfigYEstadosAsync(CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentTenantId
            ?? throw new InvalidOperationException("No hay copropiedad activa.");

        var hayCfg = await _db.CarteraConfigs.AnyAsync(ct);
        if (!hayCfg)
        {
            _db.CarteraConfigs.Add(new CarteraConfig
            {
                TenantId = tenantId,
                ModoCalculoIntereses = ModoCalculoIntereses.PorCuotaIndividual,
                ReglaImputacion = ReglaImputacion.InteresesCapitalAntiguo,
                PazSalvoAutomatico = false,
                PazSalvoCondicionado = true,
                SolicitudResidente = true,
                ValidezPazSalvoDias = 30,
                PlazoAceptacionDias = 5,
                GraciaCuotaAcuerdo = 0,
                TasaMoraMensual = 1.5m,
                PeriodoGraciaDias = 5
            });
            await _db.SaveChangesAsync(ct);
        }

        var hayEstados = await _db.EstadosCarteraConfig.AnyAsync(ct);
        if (!hayEstados)
        {
            foreach (var (nombre, orden, dias, color, esInicial) in EstadoCarteraBase.Base)
            {
                _db.EstadosCarteraConfig.Add(new EstadoCarteraConfig
                {
                    TenantId = tenantId,
                    Nombre = nombre,
                    Orden = orden,
                    DiasAlerta = dias,
                    Color = color,
                    EsInicial = esInicial,
                    Activo = true
                });
            }
            await _db.SaveChangesAsync(ct);
        }
    }

    // ===================== Sincronizacion 2.6 -> 2.7 =====================

    public async Task<int> SincronizarDesdePresupuestoAsync(CancellationToken ct)
    {
        await AsegurarConfigYEstadosAsync(ct);
        var tenantId = _tenantContext.CurrentTenantId!.Value;
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var cfg = await _db.CarteraConfigs.AsNoTracking().FirstAsync(ct);
        var estadoEnMora = await _db.EstadosCarteraConfig.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EsInicial, ct);

        // 1. Liquidaciones unidades con estado distinto a Pagado y Exonerado (incluye Pendiente y Vencido)
        var liqUnidades = await (
            from lu in _db.LiquidacionUnidades.AsNoTracking()
            join l in _db.Liquidaciones.AsNoTracking() on lu.LiquidacionId equals l.Id
            where lu.EstadoPago != EstadoPagoLiquidacion.Pagado
                && lu.EstadoPago != EstadoPagoLiquidacion.Exonerado
            select new { lu, l.Periodo }
        ).ToListAsync(ct);

        var existentes = await _db.DeudaDetalles.ToDictionaryAsync(d => d.LiquidacionUnidadId, ct);
        int sincronizadas = 0;

        foreach (var x in liqUnidades)
        {
            var diasMora = Math.Max(0, hoy.DayNumber - x.Periodo.DayNumber - cfg.PeriodoGraciaDias);
            var inicioMora = x.Periodo.AddDays(cfg.PeriodoGraciaDias);
            // Calculo simple de intereses: tasa mensual / 30 * dias en mora * capital pendiente
            decimal interes = 0m;
            if (diasMora > 0)
                interes = Math.Round(x.lu.Monto * (cfg.TasaMoraMensual / 100m) / 30m * diasMora, 2);

            if (existentes.TryGetValue(x.lu.Id, out var d))
            {
                // Actualizar capital pendiente solo si la liquidacion cambio (caso raro - snapshot 2.6 es inmutable)
                d.InteresAcumulado = interes;
                d.FechaInicioMora = diasMora > 0 ? inicioMora : null;
                d.UpdatedAt = DateTimeOffset.UtcNow;
                d.Estado = d.CapitalPendiente <= 0 ? EstadoDeudaDetalle.Pagado
                    : (d.CapitalPendiente < x.lu.Monto ? EstadoDeudaDetalle.Parcial : EstadoDeudaDetalle.Pendiente);
            }
            else
            {
                _db.DeudaDetalles.Add(new DeudaDetalle
                {
                    TenantId = tenantId,
                    UnidadPrivadaId = x.lu.UnidadPrivadaId,
                    LiquidacionUnidadId = x.lu.Id,
                    Periodo = x.Periodo,
                    Concepto = "Cuota ordinaria",
                    CapitalOriginal = x.lu.Monto,
                    CapitalPendiente = x.lu.Monto,
                    InteresAcumulado = interes,
                    FechaVencimiento = x.Periodo.AddDays(cfg.PeriodoGraciaDias),
                    FechaInicioMora = diasMora > 0 ? inicioMora : null,
                    Estado = EstadoDeudaDetalle.Pendiente
                });
                sincronizadas++;
            }
        }

        // 2. Marcar como Pagado los DeudaDetalle cuya LiquidacionUnidad cambio a Pagado
        var liqPagadas = await _db.LiquidacionUnidades.AsNoTracking()
            .Where(lu => lu.EstadoPago == EstadoPagoLiquidacion.Pagado)
            .Select(lu => lu.Id).ToListAsync(ct);
        foreach (var pagada in liqPagadas)
        {
            if (existentes.TryGetValue(pagada, out var d) && d.Estado != EstadoDeudaDetalle.Pagado)
            {
                d.CapitalPendiente = 0;
                d.Estado = EstadoDeudaDetalle.Pagado;
                d.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);

        // 3. Recomputar CarteraUnidad (snapshot agregado)
        await RecomputarCarteraUnidadAsync(estadoEnMora?.Id, ct);
        return sincronizadas;
    }

    private async Task RecomputarCarteraUnidadAsync(Guid? estadoEnMoraId, CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentTenantId!.Value;
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        var deudas = await _db.DeudaDetalles.AsNoTracking()
            .Where(d => d.Estado != EstadoDeudaDetalle.Pagado && d.Estado != EstadoDeudaDetalle.Condonado)
            .ToListAsync(ct);

        var existentes = await _db.CarteraUnidades.ToDictionaryAsync(c => c.UnidadPrivadaId, ct);

        var agrupado = deudas.GroupBy(d => d.UnidadPrivadaId);
        var idsConDeuda = new HashSet<Guid>();
        foreach (var g in agrupado)
        {
            idsConDeuda.Add(g.Key);
            var capital = g.Sum(d => d.CapitalPendiente);
            var intereses = g.Sum(d => d.InteresAcumulado);
            var primerMora = g.Where(d => d.FechaInicioMora.HasValue).Select(d => d.FechaInicioMora!.Value).DefaultIfEmpty().Min();
            var tieneAcuerdo = await _db.AcuerdosPago.AnyAsync(a => a.UnidadPrivadaId == g.Key && a.Estado == EstadoAcuerdoPago.Vigente, ct);

            if (existentes.TryGetValue(g.Key, out var cu))
            {
                cu.SaldoCapital = capital;
                cu.SaldoIntereses = intereses;
                cu.FechaPrimerMora = primerMora == default ? null : primerMora;
                cu.TieneAcuerdoVigente = tieneAcuerdo;
                if (cu.EstadoGestionId is null && capital > 0 && estadoEnMoraId.HasValue)
                {
                    cu.EstadoGestionId = estadoEnMoraId;
                    cu.FechaCambioEstadoActual = hoy;
                }
                cu.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else if (capital > 0)
            {
                _db.CarteraUnidades.Add(new CarteraUnidad
                {
                    TenantId = tenantId,
                    UnidadPrivadaId = g.Key,
                    SaldoCapital = capital,
                    SaldoIntereses = intereses,
                    FechaPrimerMora = primerMora == default ? null : primerMora,
                    TieneAcuerdoVigente = tieneAcuerdo,
                    EstadoGestionId = estadoEnMoraId,
                    FechaCambioEstadoActual = hoy
                });
            }
        }

        // Unidades que ya no tienen deuda -> saldo cero
        foreach (var (k, cu) in existentes)
        {
            if (!idsConDeuda.Contains(k))
            {
                if (cu.SaldoCapital != 0 || cu.SaldoIntereses != 0)
                {
                    cu.SaldoCapital = 0;
                    cu.SaldoIntereses = 0;
                    cu.FechaPrimerMora = null;
                    cu.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    // ===================== Tablero =====================

    public async Task<CarteraTableroDto> GetTableroAsync(CancellationToken ct)
    {
        await AsegurarConfigYEstadosAsync(ct);

        var rows = await (
            from cu in _db.CarteraUnidades.AsNoTracking().Where(c => c.SaldoCapital > 0)
            join u in _db.UnidadesPrivadas.AsNoTracking() on cu.UnidadPrivadaId equals u.Id
            join t in _db.Torres.AsNoTracking() on u.TorreId equals t.Id into tj
            from t in tj.DefaultIfEmpty()
            join e in _db.EstadosCarteraConfig.AsNoTracking() on cu.EstadoGestionId equals e.Id into ej
            from e in ej.DefaultIfEmpty()
            orderby cu.SaldoCapital descending
            select new
            {
                cu.UnidadPrivadaId,
                u.Numero,
                TorreNombre = t == null ? null : t.Nombre,
                cu.SaldoCapital,
                cu.SaldoIntereses,
                cu.FechaPrimerMora,
                EstadoId = (Guid?)cu.EstadoGestionId,
                EstadoNombre = e == null ? null : e.Nombre,
                EstadoColor = e == null ? null : e.Color,
                cu.TieneAcuerdoVigente
            }
        ).ToListAsync(ct);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var unidadesEnMora = rows.Select(r => new CarteraUnidadListaDto(
            r.UnidadPrivadaId, r.Numero, r.TorreNombre, null,
            r.SaldoCapital, r.SaldoIntereses, r.SaldoCapital + r.SaldoIntereses,
            r.FechaPrimerMora.HasValue ? hoy.DayNumber - r.FechaPrimerMora.Value.DayNumber : 0,
            r.EstadoId, r.EstadoNombre, r.EstadoColor, r.TieneAcuerdoVigente)).ToList();

        decimal aging0_30 = 0, aging31_60 = 0, aging61_90 = 0, agingMas90 = 0;
        int u0_30 = 0, u31_60 = 0, u61_90 = 0, uMas90 = 0;
        foreach (var u in unidadesEnMora)
        {
            if (u.DiasMora <= 30) { aging0_30 += u.SaldoTotal; u0_30++; }
            else if (u.DiasMora <= 60) { aging31_60 += u.SaldoTotal; u31_60++; }
            else if (u.DiasMora <= 90) { aging61_90 += u.SaldoTotal; u61_90++; }
            else { agingMas90 += u.SaldoTotal; uMas90++; }
        }

        var juridicoNombre = EstadoCarteraBase.Juridico;
        var enJuridico = unidadesEnMora.Count(u => u.EstadoGestionNombre == juridicoNombre);
        var conAcuerdo = await _db.AcuerdosPago.AsNoTracking().CountAsync(a => a.Estado == EstadoAcuerdoPago.Vigente, ct);

        var kpis = new CarteraKpisDto(
            unidadesEnMora.Sum(u => u.SaldoTotal),
            unidadesEnMora.Count,
            conAcuerdo,
            enJuridico,
            aging0_30, aging31_60, aging61_90, agingMas90,
            u0_30, u31_60, u61_90, uMas90);

        return new CarteraTableroDto(kpis, unidadesEnMora);
    }

    // ===================== Ficha por unidad =====================

    public async Task<CarteraUnidadDetalleDto?> GetUnidadAsync(Guid unidadPrivadaId, CancellationToken ct)
    {
        await AsegurarConfigYEstadosAsync(ct);
        var unidad = await _db.UnidadesPrivadas.AsNoTracking()
            .Include(u => u.Torre)
            .FirstOrDefaultAsync(u => u.Id == unidadPrivadaId, ct);
        if (unidad is null) return null;

        var cu = await _db.CarteraUnidades.AsNoTracking()
            .Include(c => c.EstadoGestion)
            .FirstOrDefaultAsync(c => c.UnidadPrivadaId == unidadPrivadaId, ct);

        var deudas = await _db.DeudaDetalles.AsNoTracking()
            .Where(d => d.UnidadPrivadaId == unidadPrivadaId)
            .OrderBy(d => d.Periodo)
            .ToListAsync(ct);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var deudasDto = deudas.Select(d => new DeudaDetalleDto(
            d.Id, d.Periodo, d.Concepto, d.CapitalOriginal, d.CapitalPendiente, d.InteresAcumulado,
            d.CapitalPendiente + d.InteresAcumulado,
            d.FechaVencimiento,
            d.FechaInicioMora.HasValue ? hoy.DayNumber - d.FechaInicioMora.Value.DayNumber : 0,
            d.Estado)).ToList();

        var acuerdoVigente = await _db.AcuerdosPago.AsNoTracking()
            .Include(a => a.Cuotas)
            .FirstOrDefaultAsync(a => a.UnidadPrivadaId == unidadPrivadaId
                && (a.Estado == EstadoAcuerdoPago.Vigente || a.Estado == EstadoAcuerdoPago.PendienteAceptacion), ct);

        var condonaciones = await _db.Condonaciones.AsNoTracking()
            .Where(c => c.UnidadPrivadaId == unidadPrivadaId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CondonacionDto(c.Id, c.Tipo, c.MontoCondonado, c.Motivo, c.AutorizadoPorUsuarioId, c.CreatedAt))
            .ToListAsync(ct);

        var pazSalvos = await _db.PazSalvosEmitidos.AsNoTracking()
            .Where(p => p.UnidadPrivadaId == unidadPrivadaId)
            .OrderByDescending(p => p.FechaEmision)
            .Select(p => new PazSalvoDto(p.Id, p.Tipo, p.Estado, p.Condiciones, p.FechaEmision, p.FechaVencimiento,
                p.EmisionTipo, p.CodigoVerificacion, p.EmitidoPorUsuarioId, p.FechaAnulacion, p.MotivoAnulacion))
            .ToListAsync(ct);

        var historial = await _db.CarteraHistorial.AsNoTracking()
            .Where(h => h.UnidadPrivadaId == unidadPrivadaId)
            .OrderByDescending(h => h.OcurridoAt)
            .Take(50)
            .Select(h => new CarteraHistorialDto(h.TipoEvento, h.Descripcion, h.RealizadoPorUsuarioId, h.OcurridoAt))
            .ToListAsync(ct);

        AcuerdoPagoDto? acuerdoDto = null;
        if (acuerdoVigente is not null)
            acuerdoDto = ToAcuerdoDto(acuerdoVigente, unidad.Numero);

        return new CarteraUnidadDetalleDto(
            unidad.Id, unidad.Numero, unidad.Torre?.Nombre, null,
            cu?.SaldoCapital ?? 0, cu?.SaldoIntereses ?? 0, (cu?.SaldoCapital ?? 0) + (cu?.SaldoIntereses ?? 0),
            cu?.FechaPrimerMora.HasValue == true ? hoy.DayNumber - cu.FechaPrimerMora!.Value.DayNumber : 0,
            cu?.EstadoGestionId, cu?.EstadoGestion?.Nombre, cu?.EstadoGestion?.Color,
            cu?.TieneAcuerdoVigente ?? false,
            deudasDto, acuerdoDto, condonaciones, pazSalvos, historial);
    }

    public async Task<MiCuotaDto?> GetMiCuotaAsync(Guid? unidadPrivadaId, CancellationToken ct)
    {
        // En MVP el residente pasa la unidad explicitamente; en Fase 2 se resuelve via UsuarioTenant
        if (!unidadPrivadaId.HasValue) return null;
        var d = await GetUnidadAsync(unidadPrivadaId.Value, ct);
        if (d is null) return null;
        return new MiCuotaDto(d.UnidadNumero, d.SaldoTotal, d.SaldoCapital, d.SaldoIntereses,
            d.TieneAcuerdoVigente, d.Deudas, d.AcuerdoVigente);
    }

    // ===================== Estados de gestion =====================

    public async Task<IReadOnlyList<EstadoCarteraDto>> ListarEstadosAsync(CancellationToken ct)
    {
        await AsegurarConfigYEstadosAsync(ct);
        return await _db.EstadosCarteraConfig.AsNoTracking()
            .OrderBy(e => e.Orden)
            .Select(e => new EstadoCarteraDto(e.Id, e.Nombre, e.Orden, e.DiasAlerta, e.Color, e.EsInicial, e.Activo))
            .ToListAsync(ct);
    }

    public async Task<bool> CambiarEstadoUnidadAsync(Guid unidadPrivadaId, CambiarEstadoUnidadRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Motivo))
            throw new InvalidOperationException("El motivo es obligatorio (validacion seccion 17).");
        var cu = await _db.CarteraUnidades.FirstOrDefaultAsync(c => c.UnidadPrivadaId == unidadPrivadaId, ct);
        if (cu is null) return false;
        var nuevo = await _db.EstadosCarteraConfig.AsNoTracking().FirstOrDefaultAsync(e => e.Id == req.EstadoId, ct)
            ?? throw new InvalidOperationException("Estado destino invalido.");
        var anterior = cu.EstadoGestionId.HasValue
            ? await _db.EstadosCarteraConfig.AsNoTracking().FirstOrDefaultAsync(e => e.Id == cu.EstadoGestionId.Value, ct)
            : null;

        cu.EstadoGestionId = req.EstadoId;
        cu.FechaCambioEstadoActual = DateOnly.FromDateTime(DateTime.UtcNow);
        cu.UpdatedAt = DateTimeOffset.UtcNow;

        await RegistrarHistorial(unidadPrivadaId, TipoEventoCartera.CambioEstadoGestion,
            $"Estado cambiado de {anterior?.Nombre ?? "(ninguno)"} a {nuevo.Nombre}. Motivo: {req.Motivo}",
            new { prev = anterior?.Nombre, nuevo = nuevo.Nombre, motivo = req.Motivo }, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Acuerdos de pago =====================

    public async Task<IReadOnlyList<AcuerdoPagoDto>> ListarAcuerdosAsync(EstadoAcuerdoPago? estado, CancellationToken ct)
    {
        IQueryable<AcuerdoPago> q = _db.AcuerdosPago.AsNoTracking().Include(a => a.Cuotas).Include(a => a.UnidadPrivada);
        if (estado.HasValue) q = q.Where(a => a.Estado == estado.Value);
        var list = await q.OrderByDescending(a => a.CreatedAt).ToListAsync(ct);
        return list.Select(a => ToAcuerdoDto(a, a.UnidadPrivada?.Numero ?? "")).ToList();
    }

    public async Task<AcuerdoPagoDto?> GetAcuerdoAsync(Guid id, CancellationToken ct)
    {
        var a = await _db.AcuerdosPago.AsNoTracking()
            .Include(x => x.Cuotas).Include(x => x.UnidadPrivada)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return a is null ? null : ToAcuerdoDto(a, a.UnidadPrivada?.Numero ?? "");
    }

    public async Task<AcuerdoPagoDto> CrearAcuerdoAsync(CrearAcuerdoRequest req, CancellationToken ct)
    {
        await AsegurarConfigYEstadosAsync(ct);
        if (req.NumeroCuotas < 1) throw new InvalidOperationException("Numero de cuotas debe ser >= 1.");

        // AC-01 / RN-06: solo un acuerdo Vigente o PendienteAceptacion por unidad
        var existeActivo = await _db.AcuerdosPago.AnyAsync(a => a.UnidadPrivadaId == req.UnidadPrivadaId
            && (a.Estado == EstadoAcuerdoPago.Vigente || a.Estado == EstadoAcuerdoPago.PendienteAceptacion), ct);
        if (existeActivo)
            throw new InvalidOperationException("Esta unidad ya tiene un acuerdo de pago Vigente o pendiente de aceptacion.");

        var cu = await _db.CarteraUnidades.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UnidadPrivadaId == req.UnidadPrivadaId, ct)
            ?? throw new InvalidOperationException("La unidad no tiene cartera registrada.");
        if (cu.SaldoCapital <= 0 && cu.SaldoIntereses <= 0)
            throw new InvalidOperationException("No se puede crear acuerdo sin deuda activa (validacion seccion 17).");

        var cfg = await _db.CarteraConfigs.AsNoTracking().FirstAsync(ct);
        var capitalIncluido = cu.SaldoCapital;
        var interesesIncluidos = Math.Max(0, cu.SaldoIntereses - req.InteresesCondonados);
        var montoTotal = capitalIncluido + interesesIncluidos;
        var montoPorCuota = Math.Round(montoTotal / req.NumeroCuotas, 2);
        var capitalPorCuota = Math.Round(capitalIncluido / req.NumeroCuotas, 2);
        var interesPorCuota = Math.Round(interesesIncluidos / req.NumeroCuotas, 2);

        var acuerdo = new AcuerdoPago
        {
            UnidadPrivadaId = req.UnidadPrivadaId,
            Estado = EstadoAcuerdoPago.Borrador,
            MontoTotal = montoTotal,
            CapitalIncluido = capitalIncluido,
            InteresesIncluidos = interesesIncluidos,
            InteresesCondonados = req.InteresesCondonados,
            NumeroCuotas = req.NumeroCuotas,
            FechaPrimeraCuota = req.FechaPrimeraCuota,
            NotasAdmin = req.NotasAdmin,
            CreadoPorUsuarioId = GetUsuarioActualId()
        };
        _db.AcuerdosPago.Add(acuerdo);

        for (int i = 1; i <= req.NumeroCuotas; i++)
        {
            var fecha = req.FechaPrimeraCuota.AddMonths(i - 1);
            // Ultima cuota toma el residual para evitar errores de redondeo
            var monto = (i == req.NumeroCuotas)
                ? montoTotal - (montoPorCuota * (req.NumeroCuotas - 1))
                : montoPorCuota;
            var cap = (i == req.NumeroCuotas)
                ? capitalIncluido - (capitalPorCuota * (req.NumeroCuotas - 1))
                : capitalPorCuota;
            var inte = (i == req.NumeroCuotas)
                ? interesesIncluidos - (interesPorCuota * (req.NumeroCuotas - 1))
                : interesPorCuota;
            _db.AcuerdoCuotas.Add(new AcuerdoCuota
            {
                Acuerdo = acuerdo,
                NumeroCuota = i,
                FechaVencimiento = fecha,
                Monto = monto,
                Capital = cap,
                Intereses = inte,
                Estado = EstadoCuotaAcuerdo.Pendiente
            });
        }

        await RegistrarHistorial(req.UnidadPrivadaId, TipoEventoCartera.AcuerdoCreado,
            $"Acuerdo en borrador: {req.NumeroCuotas} cuotas, total {montoTotal:N0} COP",
            new { numeroCuotas = req.NumeroCuotas, montoTotal, interesesCondonados = req.InteresesCondonados }, ct);
        await _db.SaveChangesAsync(ct);

        var unidad = await _db.UnidadesPrivadas.AsNoTracking().FirstAsync(u => u.Id == req.UnidadPrivadaId, ct);
        var conCuotas = await _db.AcuerdosPago.AsNoTracking().Include(a => a.Cuotas).FirstAsync(a => a.Id == acuerdo.Id, ct);
        return ToAcuerdoDto(conCuotas, unidad.Numero);
    }

    public async Task<bool> EnviarParaAceptacionAsync(Guid acuerdoId, CancellationToken ct)
    {
        var a = await _db.AcuerdosPago.FirstOrDefaultAsync(x => x.Id == acuerdoId, ct);
        if (a is null) return false;
        if (a.Estado != EstadoAcuerdoPago.Borrador)
            throw new InvalidOperationException("Solo se puede enviar a aceptacion un acuerdo en Borrador.");
        var cfg = await _db.CarteraConfigs.AsNoTracking().FirstAsync(ct);
        a.Estado = EstadoAcuerdoPago.PendienteAceptacion;
        a.FechaExpiracion = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(cfg.PlazoAceptacionDias));
        a.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> AceptarAcuerdoAsync(Guid acuerdoId, AceptarAcuerdoRequest req, CancellationToken ct)
    {
        var a = await _db.AcuerdosPago.FirstOrDefaultAsync(x => x.Id == acuerdoId, ct);
        if (a is null) return false;
        if (a.Estado != EstadoAcuerdoPago.PendienteAceptacion)
            throw new InvalidOperationException("El acuerdo no esta en estado PendienteAceptacion.");

        // RN-07: registro inmutable de aceptacion con hash + timestamp + ip + metodo
        var hashSource = $"{a.Id}|{a.MontoTotal}|{a.NumeroCuotas}|{a.FechaPrimeraCuota:yyyy-MM-dd}|{a.CreatedAt:O}";
        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(hashSource)));

        a.Estado = EstadoAcuerdoPago.Vigente;
        a.AceptacionHash = hash;
        a.AceptacionAt = DateTimeOffset.UtcNow;
        a.AceptacionPersonaId = GetPersonaActualId();
        a.AceptacionIp = req.Ip;
        a.AceptacionMetodo = req.MetodoAutenticacion;
        a.UpdatedAt = DateTimeOffset.UtcNow;

        // Marcar TieneAcuerdoVigente en CarteraUnidad
        var cu = await _db.CarteraUnidades.FirstOrDefaultAsync(c => c.UnidadPrivadaId == a.UnidadPrivadaId, ct);
        if (cu is not null) { cu.TieneAcuerdoVigente = true; cu.UpdatedAt = DateTimeOffset.UtcNow; }

        await RegistrarHistorial(a.UnidadPrivadaId, TipoEventoCartera.AcuerdoAceptado,
            $"Acuerdo aceptado por {req.MetodoAutenticacion}. Hash {hash[..16]}...",
            new { acuerdoId = a.Id, hash, ip = req.Ip, metodo = req.MetodoAutenticacion }, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CancelarAcuerdoAsync(Guid acuerdoId, CancelarAcuerdoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Motivo))
            throw new InvalidOperationException("Motivo obligatorio para cancelar.");
        var a = await _db.AcuerdosPago.FirstOrDefaultAsync(x => x.Id == acuerdoId, ct);
        if (a is null) return false;
        if (a.Estado == EstadoAcuerdoPago.Completado)
            throw new InvalidOperationException("No se puede cancelar un acuerdo completado.");
        a.Estado = EstadoAcuerdoPago.Cancelado;
        a.UpdatedAt = DateTimeOffset.UtcNow;
        var cu = await _db.CarteraUnidades.FirstOrDefaultAsync(c => c.UnidadPrivadaId == a.UnidadPrivadaId, ct);
        if (cu is not null) { cu.TieneAcuerdoVigente = false; cu.UpdatedAt = DateTimeOffset.UtcNow; }
        await RegistrarHistorial(a.UnidadPrivadaId, TipoEventoCartera.AcuerdoCancelado,
            $"Acuerdo cancelado. Motivo: {req.Motivo}", new { acuerdoId, motivo = req.Motivo }, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RegistrarPagoAcuerdoAsync(RegistrarPagoAcuerdoRequest req, CancellationToken ct)
    {
        var cuota = await _db.AcuerdoCuotas.Include(c => c.Acuerdo)
            .FirstOrDefaultAsync(c => c.Id == req.CuotaAcuerdoId, ct);
        if (cuota is null) return false;
        if (cuota.Estado == EstadoCuotaAcuerdo.Pagada) return true;
        cuota.Estado = EstadoCuotaAcuerdo.Pagada;
        cuota.FechaPago = req.FechaPago;
        cuota.UpdatedAt = DateTimeOffset.UtcNow;

        // Si todas las cuotas estan pagadas -> Completado
        var pendientes = await _db.AcuerdoCuotas.AnyAsync(c => c.AcuerdoId == cuota.AcuerdoId && c.Estado != EstadoCuotaAcuerdo.Pagada, ct);
        if (!pendientes)
        {
            cuota.Acuerdo!.Estado = EstadoAcuerdoPago.Completado;
            var cu = await _db.CarteraUnidades.FirstOrDefaultAsync(c => c.UnidadPrivadaId == cuota.Acuerdo.UnidadPrivadaId, ct);
            if (cu is not null) { cu.TieneAcuerdoVigente = false; }
        }
        await RegistrarHistorial(cuota.Acuerdo!.UnidadPrivadaId, TipoEventoCartera.PagoRegistrado,
            $"Pago de cuota {cuota.NumeroCuota} del acuerdo registrado: {cuota.Monto:N0}",
            new { cuotaId = cuota.Id, monto = cuota.Monto, fecha = req.FechaPago.ToString("yyyy-MM-dd") }, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Condonaciones =====================

    public async Task<IReadOnlyList<CondonacionDto>> ListarCondonacionesAsync(Guid? unidadId, CancellationToken ct)
    {
        IQueryable<Condonacion> q = _db.Condonaciones.AsNoTracking();
        if (unidadId.HasValue) q = q.Where(c => c.UnidadPrivadaId == unidadId.Value);
        return await q.OrderByDescending(c => c.CreatedAt)
            .Select(c => new CondonacionDto(c.Id, c.Tipo, c.MontoCondonado, c.Motivo, c.AutorizadoPorUsuarioId, c.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<CondonacionDto> AplicarCondonacionAsync(AplicarCondonacionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Motivo))
            throw new InvalidOperationException("Motivo obligatorio.");
        if (req.MontoCondonado <= 0)
            throw new InvalidOperationException("Monto debe ser mayor a 0.");

        var cu = await _db.CarteraUnidades.FirstOrDefaultAsync(c => c.UnidadPrivadaId == req.UnidadPrivadaId, ct)
            ?? throw new InvalidOperationException("La unidad no tiene cartera registrada.");

        var saldoAntes = new { capital = cu.SaldoCapital, intereses = cu.SaldoIntereses };

        // Aplica condonacion segun tipo
        var monto = req.MontoCondonado;
        switch (req.Tipo)
        {
            case TipoCondonacion.Intereses:
                if (monto > cu.SaldoIntereses) monto = cu.SaldoIntereses;
                cu.SaldoIntereses -= monto;
                // Reflejar en deudas: reducir interes_acumulado en orden FIFO
                await AjustarInteresesEnDeudasAsync(req.UnidadPrivadaId, monto, ct);
                break;
            case TipoCondonacion.Capital:
                if (monto > cu.SaldoCapital) monto = cu.SaldoCapital;
                cu.SaldoCapital -= monto;
                await AjustarCapitalEnDeudasAsync(req.UnidadPrivadaId, monto, ct);
                break;
            case TipoCondonacion.Total:
                monto = cu.SaldoCapital + cu.SaldoIntereses;
                await AjustarInteresesEnDeudasAsync(req.UnidadPrivadaId, cu.SaldoIntereses, ct);
                await AjustarCapitalEnDeudasAsync(req.UnidadPrivadaId, cu.SaldoCapital, ct);
                cu.SaldoCapital = 0;
                cu.SaldoIntereses = 0;
                break;
        }
        cu.UpdatedAt = DateTimeOffset.UtcNow;

        var saldoDespues = new { capital = cu.SaldoCapital, intereses = cu.SaldoIntereses };

        var cond = new Condonacion
        {
            UnidadPrivadaId = req.UnidadPrivadaId,
            Tipo = req.Tipo,
            MontoCondonado = monto,
            Motivo = req.Motivo,
            DocumentoSoporteUrl = req.DocumentoSoporteUrl,
            SaldoAntes = JsonSerializer.Serialize(saldoAntes),
            SaldoDespues = JsonSerializer.Serialize(saldoDespues),
            AutorizadoPorUsuarioId = GetUsuarioActualId()
        };
        _db.Condonaciones.Add(cond);

        await RegistrarHistorial(req.UnidadPrivadaId, TipoEventoCartera.CondonacionAplicada,
            $"Condonacion {req.Tipo}: {monto:N0} COP. Motivo: {req.Motivo}",
            new { tipo = req.Tipo.ToString(), monto, motivo = req.Motivo, antes = saldoAntes, despues = saldoDespues }, ct);
        await _db.SaveChangesAsync(ct);
        return new CondonacionDto(cond.Id, cond.Tipo, cond.MontoCondonado, cond.Motivo, cond.AutorizadoPorUsuarioId, cond.CreatedAt);
    }

    private async Task AjustarInteresesEnDeudasAsync(Guid unidadId, decimal monto, CancellationToken ct)
    {
        var deudas = await _db.DeudaDetalles
            .Where(d => d.UnidadPrivadaId == unidadId && d.InteresAcumulado > 0)
            .OrderBy(d => d.Periodo).ToListAsync(ct);
        var rest = monto;
        foreach (var d in deudas)
        {
            if (rest <= 0) break;
            var aplicar = Math.Min(d.InteresAcumulado, rest);
            d.InteresAcumulado -= aplicar;
            rest -= aplicar;
            d.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private async Task AjustarCapitalEnDeudasAsync(Guid unidadId, decimal monto, CancellationToken ct)
    {
        var deudas = await _db.DeudaDetalles
            .Where(d => d.UnidadPrivadaId == unidadId && d.CapitalPendiente > 0)
            .OrderBy(d => d.Periodo).ToListAsync(ct);
        var rest = monto;
        foreach (var d in deudas)
        {
            if (rest <= 0) break;
            var aplicar = Math.Min(d.CapitalPendiente, rest);
            d.CapitalPendiente -= aplicar;
            rest -= aplicar;
            d.Estado = d.CapitalPendiente <= 0 ? EstadoDeudaDetalle.Condonado : EstadoDeudaDetalle.Parcial;
            d.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    // ===================== Paz y salvo =====================

    public async Task<IReadOnlyList<PazSalvoDto>> ListarPazSalvosAsync(Guid? unidadId, CancellationToken ct)
    {
        IQueryable<PazSalvoEmitido> q = _db.PazSalvosEmitidos.AsNoTracking();
        if (unidadId.HasValue) q = q.Where(p => p.UnidadPrivadaId == unidadId.Value);
        return await q.OrderByDescending(p => p.FechaEmision)
            .Select(p => new PazSalvoDto(p.Id, p.Tipo, p.Estado, p.Condiciones, p.FechaEmision, p.FechaVencimiento,
                p.EmisionTipo, p.CodigoVerificacion, p.EmitidoPorUsuarioId, p.FechaAnulacion, p.MotivoAnulacion))
            .ToListAsync(ct);
    }

    public async Task<PazSalvoDto> EmitirPazSalvoAsync(EmitirPazSalvoRequest req, CancellationToken ct)
    {
        await AsegurarConfigYEstadosAsync(ct);
        var cfg = await _db.CarteraConfigs.AsNoTracking().FirstAsync(ct);
        var cu = await _db.CarteraUnidades.AsNoTracking().FirstOrDefaultAsync(c => c.UnidadPrivadaId == req.UnidadPrivadaId, ct);
        var unidad = await _db.UnidadesPrivadas.AsNoTracking().FirstOrDefaultAsync(u => u.Id == req.UnidadPrivadaId, ct)
            ?? throw new InvalidOperationException("Unidad no encontrada.");

        var saldoTotal = (cu?.SaldoCapital ?? 0) + (cu?.SaldoIntereses ?? 0);
        var tieneAcuerdoVigente = cu?.TieneAcuerdoVigente ?? false;

        TipoPazSalvo tipo;
        string? condiciones = req.CondicionesManual;

        if (saldoTotal == 0)
        {
            tipo = TipoPazSalvo.Pleno;
        }
        else if (tieneAcuerdoVigente && cfg.PazSalvoCondicionado)
        {
            tipo = TipoPazSalvo.Condicionado;
            condiciones ??= "Unidad con acuerdo de pago vigente y al dia.";
        }
        else if (req.EmisionTipo == EmisionPazSalvo.Manual)
        {
            // Admin asume responsabilidad (RN-09)
            tipo = TipoPazSalvo.Pleno;
            condiciones = "Emitido manualmente bajo responsabilidad del administrador.";
        }
        else
        {
            throw new InvalidOperationException(
                "No se puede emitir paz y salvo automatico: la unidad tiene saldo pendiente sin acuerdo vigente.");
        }

        var codigo = GenerarCodigoVerificacion(req.UnidadPrivadaId);
        var p = new PazSalvoEmitido
        {
            UnidadPrivadaId = req.UnidadPrivadaId,
            Tipo = tipo,
            Estado = EstadoPazSalvo.Vigente,
            Condiciones = condiciones,
            FechaEmision = DateTimeOffset.UtcNow,
            FechaVencimiento = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(cfg.ValidezPazSalvoDias)),
            EmitidoPorUsuarioId = GetUsuarioActualId(),
            EmisionTipo = req.EmisionTipo,
            CodigoVerificacion = codigo
        };
        _db.PazSalvosEmitidos.Add(p);
        await RegistrarHistorial(req.UnidadPrivadaId, TipoEventoCartera.PazSalvoEmitido,
            $"Paz y salvo {tipo} emitido ({req.EmisionTipo}). Codigo: {codigo}",
            new { tipo = tipo.ToString(), emisionTipo = req.EmisionTipo.ToString(), codigo }, ct);
        await _db.SaveChangesAsync(ct);
        return new PazSalvoDto(p.Id, p.Tipo, p.Estado, p.Condiciones, p.FechaEmision, p.FechaVencimiento,
            p.EmisionTipo, p.CodigoVerificacion, p.EmitidoPorUsuarioId, null, null);
    }

    public async Task<bool> AnularPazSalvoAsync(Guid id, AnularPazSalvoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Motivo)) throw new InvalidOperationException("Motivo obligatorio.");
        var p = await _db.PazSalvosEmitidos.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        if (p.Estado == EstadoPazSalvo.Anulado) return true;
        // Solo se permite modificar campos de anulacion - no se borra el registro (RN-08)
        p.Estado = EstadoPazSalvo.Anulado;
        p.AnuladoPorUsuarioId = GetUsuarioActualId();
        p.FechaAnulacion = DateTimeOffset.UtcNow;
        p.MotivoAnulacion = req.Motivo;
        await RegistrarHistorial(p.UnidadPrivadaId, TipoEventoCartera.PazSalvoAnulado,
            $"Paz y salvo {p.CodigoVerificacion} anulado. Motivo: {req.Motivo}",
            new { codigo = p.CodigoVerificacion, motivo = req.Motivo }, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static string GenerarCodigoVerificacion(Guid unidadId)
    {
        var src = $"{unidadId}|{DateTimeOffset.UtcNow:O}|{Guid.NewGuid()}";
        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(src)));
        return $"PS-{hash[..16]}";
    }

    // ===================== Configuracion =====================

    public async Task<CarteraConfigDto> GetConfigAsync(CancellationToken ct)
    {
        await AsegurarConfigYEstadosAsync(ct);
        var c = await _db.CarteraConfigs.AsNoTracking().FirstAsync(ct);
        return new CarteraConfigDto(c.ModoCalculoIntereses, c.ReglaImputacion, c.PazSalvoAutomatico,
            c.PazSalvoCondicionado, c.SolicitudResidente, c.ValidezPazSalvoDias, c.MensajePazSalvo,
            c.PlazoAceptacionDias, c.GraciaCuotaAcuerdo, c.TasaMoraMensual, c.PeriodoGraciaDias);
    }

    public async Task<bool> ActualizarConfigAsync(CarteraConfigDto req, CancellationToken ct)
    {
        await AsegurarConfigYEstadosAsync(ct);
        var c = await _db.CarteraConfigs.FirstAsync(ct);
        c.ModoCalculoIntereses = req.ModoCalculoIntereses;
        c.ReglaImputacion = req.ReglaImputacion;
        c.PazSalvoAutomatico = req.PazSalvoAutomatico;
        c.PazSalvoCondicionado = req.PazSalvoCondicionado;
        c.SolicitudResidente = req.SolicitudResidente;
        c.ValidezPazSalvoDias = req.ValidezPazSalvoDias;
        c.MensajePazSalvo = req.MensajePazSalvo;
        c.PlazoAceptacionDias = req.PlazoAceptacionDias;
        c.GraciaCuotaAcuerdo = req.GraciaCuotaAcuerdo;
        c.TasaMoraMensual = req.TasaMoraMensual;
        c.PeriodoGraciaDias = req.PeriodoGraciaDias;
        c.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Helpers =====================

    private static AcuerdoPagoDto ToAcuerdoDto(AcuerdoPago a, string unidadNumero)
    {
        var cuotas = a.Cuotas?.OrderBy(c => c.NumeroCuota).Select(c => new AcuerdoCuotaDto(
            c.Id, c.NumeroCuota, c.FechaVencimiento, c.Monto, c.Capital, c.Intereses, c.Estado, c.FechaPago)).ToList()
            ?? new List<AcuerdoCuotaDto>();
        return new AcuerdoPagoDto(
            a.Id, a.UnidadPrivadaId, unidadNumero, a.Estado, a.MontoTotal, a.CapitalIncluido, a.InteresesIncluidos,
            a.InteresesCondonados, a.NumeroCuotas, a.FechaPrimeraCuota, a.NotasAdmin, a.AcuerdoPadreId,
            a.AceptacionAt, a.FechaExpiracion, a.CreatedAt, cuotas);
    }

    private async Task RegistrarHistorial(Guid unidadId, TipoEventoCartera tipo, string desc, object? datos, CancellationToken ct)
    {
        _db.CarteraHistorial.Add(new CarteraHistorial
        {
            UnidadPrivadaId = unidadId,
            TipoEvento = tipo,
            Descripcion = desc.Length > 500 ? desc[..500] : desc,
            DatosAdicionales = datos is null ? null : JsonSerializer.Serialize(datos),
            RealizadoPorUsuarioId = GetUsuarioActualId(),
            OcurridoAt = DateTimeOffset.UtcNow
        });
        await Task.CompletedTask;
    }
}
