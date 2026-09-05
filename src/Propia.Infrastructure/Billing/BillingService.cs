using Microsoft.EntityFrameworkCore;
using Propia.Application.Billing;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Billing;

public class BillingService : IBillingService
{
    private readonly PropiaDbContext _db;

    public BillingService(PropiaDbContext db) => _db = db;

    // ----------------------------- Planes -----------------------------

    public async Task<IReadOnlyList<PlanDto>> ListPlanesAsync(CancellationToken ct)
    {
        return await _db.Planes
            .AsNoTracking()
            .OrderBy(p => p.Estado).ThenBy(p => p.Nombre)
            .Select(p => new PlanDto(
                p.Id, p.Nombre, p.Descripcion,
                p.FeeBase, p.FeeVariablePorUnidad,
                p.CicloMensual, p.CicloAnual, p.DescuentoAnualPct,
                p.LimiteUnidades, p.LimiteUsuarios, p.LimiteStorageGb,
                p.LimiteLineasWhatsapp, p.LimiteLlamadasIaMensual,
                p.DiasTrial, p.Estado,
                _db.Suscripciones.Count(s => s.PlanId == p.Id && s.Estado != EstadoSuscripcion.Cancelada && s.Estado != EstadoSuscripcion.Archivada),
                p.CreatedAt, p.LimiteCopropiedades, p.EsPromocional))
            .ToListAsync(ct);
    }

    public async Task<PlanDto> CrearPlanAsync(CrearPlanRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct)
    {
        ValidatePlanRequest(req.Nombre, req.FeeBase, req.FeeVariablePorUnidad, req.CicloMensual, req.CicloAnual);

        var plan = new Plan
        {
            Nombre = req.Nombre,
            Descripcion = req.Descripcion,
            FeeBase = req.FeeBase,
            FeeVariablePorUnidad = req.FeeVariablePorUnidad,
            CicloMensual = req.CicloMensual,
            CicloAnual = req.CicloAnual,
            DescuentoAnualPct = req.DescuentoAnualPct,
            LimiteUnidades = req.LimiteUnidades,
            LimiteUsuarios = req.LimiteUsuarios,
            LimiteStorageGb = req.LimiteStorageGb,
            LimiteLineasWhatsapp = req.LimiteLineasWhatsapp,
            LimiteLlamadasIaMensual = req.LimiteLlamadasIaMensual,
            LimiteCopropiedades = req.LimiteCopropiedades,
            DiasTrial = req.DiasTrial,
            EsPromocional = req.EsPromocional,
            Estado = EstadoPlan.Activo
        };
        _db.Planes.Add(plan);
        _db.SuperAdminLogs.Add(SuperLog(actorId, actorEmail, "CREATE_PLAN", $"Plan:{plan.Id}", $"Nombre={req.Nombre} FeeBase={req.FeeBase}", ip));
        await _db.SaveChangesAsync(ct);
        return ToDto(plan, 0);
    }

    public async Task<PlanDto?> ActualizarPlanAsync(Guid planId, ActualizarPlanRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct)
    {
        var plan = await _db.Planes.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan is null) return null;

        // RN-14: un plan solo puede desactivarse si no tiene clientes activos asociados
        if (req.Estado != EstadoPlan.Activo && plan.Estado == EstadoPlan.Activo)
        {
            var clientesActivos = await _db.Suscripciones
                .CountAsync(s => s.PlanId == planId &&
                                 s.Estado != EstadoSuscripcion.Cancelada &&
                                 s.Estado != EstadoSuscripcion.Archivada, ct);
            if (clientesActivos > 0)
                throw new InvalidOperationException(
                    $"No se puede desactivar el plan: hay {clientesActivos} suscripcion(es) activa(s) asociada(s).");
        }

        ValidatePlanRequest(req.Nombre, req.FeeBase, req.FeeVariablePorUnidad, req.CicloMensual, req.CicloAnual);

        plan.Nombre = req.Nombre;
        plan.Descripcion = req.Descripcion;
        plan.FeeBase = req.FeeBase;
        plan.FeeVariablePorUnidad = req.FeeVariablePorUnidad;
        plan.CicloMensual = req.CicloMensual;
        plan.CicloAnual = req.CicloAnual;
        plan.DescuentoAnualPct = req.DescuentoAnualPct;
        plan.LimiteUnidades = req.LimiteUnidades;
        plan.LimiteUsuarios = req.LimiteUsuarios;
        plan.LimiteStorageGb = req.LimiteStorageGb;
        plan.LimiteLineasWhatsapp = req.LimiteLineasWhatsapp;
        plan.LimiteLlamadasIaMensual = req.LimiteLlamadasIaMensual;
        plan.LimiteCopropiedades = req.LimiteCopropiedades;
        plan.DiasTrial = req.DiasTrial;
        plan.EsPromocional = req.EsPromocional;
        plan.Estado = req.Estado;

        _db.SuperAdminLogs.Add(SuperLog(actorId, actorEmail, "UPDATE_PLAN", $"Plan:{plan.Id}", $"Estado={req.Estado}", ip));
        await _db.SaveChangesAsync(ct);

        var count = await _db.Suscripciones.CountAsync(s => s.PlanId == plan.Id && s.Estado != EstadoSuscripcion.Cancelada && s.Estado != EstadoSuscripcion.Archivada, ct);
        return ToDto(plan, count);
    }

    public async Task<bool?> EliminarPlanAsync(Guid planId, Guid actorId, string actorEmail, string? ip, CancellationToken ct)
    {
        var plan = await _db.Planes.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan is null) return null;

        // Un plan no se borra en duro si tiene CUALQUIER suscripcion asociada (FK Restrict + historial).
        // En ese caso se debe archivar, no eliminar.
        var tieneSuscripciones = await _db.Suscripciones.AnyAsync(s => s.PlanId == planId, ct);
        if (tieneSuscripciones)
            throw new InvalidOperationException(
                "No se puede eliminar el plan: tiene suscripciones asociadas. Cambia su estado a 'Archivado' en su lugar.");

        _db.Planes.Remove(plan);
        _db.SuperAdminLogs.Add(SuperLog(actorId, actorEmail, "DELETE_PLAN", $"Plan:{plan.Id}", $"Nombre={plan.Nombre}", ip));
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static void ValidatePlanRequest(string nombre, decimal feeBase, decimal feeVariable, bool cm, bool ca)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new InvalidOperationException("El nombre del plan es obligatorio.");
        if (feeBase < 0 || feeVariable < 0)
            throw new InvalidOperationException("Los fees deben ser >= 0.");
        if (feeBase == 0 && feeVariable == 0)
            throw new InvalidOperationException("Al menos un fee debe ser > 0 (base o variable).");
        if (!cm && !ca)
            throw new InvalidOperationException("Al menos un ciclo (mensual o anual) debe estar habilitado.");
    }

    // ----------------------------- Suscripciones -----------------------------

    public async Task<IReadOnlyList<SuscripcionDto>> ListSuscripcionesAsync(CancellationToken ct)
    {
        return await _db.Suscripciones
            .AsNoTracking()
            .Include(s => s.Plan)
            .Include(s => s.Organizacion)
            .Include(s => s.Copropiedad)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SuscripcionDto(
                s.Id,
                s.OrganizacionId, s.Organizacion != null ? s.Organizacion.Nombre : null,
                s.CopropiedadId, s.Copropiedad != null ? s.Copropiedad.Nombre : null,
                s.PlanId, s.Plan!.Nombre,
                s.Ciclo, s.Estado,
                s.FechaInicio, s.FechaAniversario,
                s.FechaProximoCobro, s.FechaFinTrial,
                s.CreditoAFavor, s.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<SuscripcionDto> CrearSuscripcionAsync(CrearSuscripcionRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct)
    {
        if ((req.OrganizacionId is null) == (req.CopropiedadId is null))
            throw new InvalidOperationException("Debe especificarse OrganizacionId XOR CopropiedadId (no ambos, no ninguno).");

        var plan = await _db.Planes.FirstOrDefaultAsync(p => p.Id == req.PlanId, ct);
        if (plan is null) throw new InvalidOperationException("Plan no encontrado.");
        if (plan.Estado != EstadoPlan.Activo) throw new InvalidOperationException("El plan no esta activo.");
        if (req.Ciclo == CicloFacturacion.Mensual && !plan.CicloMensual) throw new InvalidOperationException("El plan no soporta ciclo mensual.");
        if (req.Ciclo == CicloFacturacion.Anual && !plan.CicloAnual) throw new InvalidOperationException("El plan no soporta ciclo anual.");

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var fechaInicio = hoy;
        var diaAniversario = hoy.Day;
        DateOnly? finTrial = plan.DiasTrial > 0 ? hoy.AddDays(plan.DiasTrial) : null;
        DateOnly proximoCobro = finTrial ?? AvanzarPeriodo(hoy, req.Ciclo);

        var s = new Suscripcion
        {
            OrganizacionId = req.OrganizacionId,
            CopropiedadId = req.CopropiedadId,
            PlanId = req.PlanId,
            Ciclo = req.Ciclo,
            Estado = plan.DiasTrial > 0 ? EstadoSuscripcion.Trial : EstadoSuscripcion.Activa,
            FechaInicio = fechaInicio,
            FechaAniversario = diaAniversario,
            FechaProximoCobro = proximoCobro,
            FechaFinTrial = finTrial
        };
        _db.Suscripciones.Add(s);
        _db.SuscripcionHistorial.Add(new SuscripcionHistorial
        {
            SuscripcionId = s.Id,
            Tipo = TipoEventoSuscripcion.Activacion,
            Origen = OrigenEventoSuscripcion.SuperAdmin,
            ActorId = actorId,
            PlanNuevoId = req.PlanId,
            EstadoNuevo = s.Estado.ToString(),
            Notas = $"Activacion con plan {plan.Nombre}, ciclo {req.Ciclo}"
        });
        _db.SuperAdminLogs.Add(SuperLog(actorId, actorEmail, "CREATE_SUSCRIPCION",
            $"Suscripcion:{s.Id}", $"PlanId={req.PlanId} Ciclo={req.Ciclo} Estado={s.Estado}", ip));
        await _db.SaveChangesAsync(ct);
        return await SuscripcionDtoByIdAsync(s.Id, ct);
    }

    public async Task<SuscripcionDto?> CambiarPlanSuscripcionAsync(Guid suscripcionId, CambiarPlanRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct)
    {
        var s = await _db.Suscripciones.FirstOrDefaultAsync(x => x.Id == suscripcionId, ct);
        if (s is null) return null;

        var planAnteriorId = s.PlanId;
        var planAnterior = await _db.Planes.FirstOrDefaultAsync(p => p.Id == planAnteriorId, ct);

        // Un plan PROMOCIONAL/especial (cortesia asignada por el operador) no se cambia por el flujo
        // normal: hay que retirarlo primero (cancelar/archivar la suscripcion) o usar Forzar=true.
        if (planAnterior?.EsPromocional == true && !req.Forzar)
            throw new InvalidOperationException(
                "Esta suscripcion esta en un plan promocional/especial y no se puede cambiar directamente. " +
                "Retira el plan promocional (cancela o archiva la suscripcion) antes de asignar otro, o confirma el cambio forzado.");

        var planNuevo = await _db.Planes.FirstOrDefaultAsync(p => p.Id == req.NuevoPlanId, ct);
        if (planNuevo is null) throw new InvalidOperationException("Plan destino no encontrado.");
        if (planNuevo.Estado != EstadoPlan.Activo) throw new InvalidOperationException("El plan destino no esta activo.");
        if (string.IsNullOrWhiteSpace(req.Justificacion)) throw new InvalidOperationException("Justificacion obligatoria.");
        var feeAnterior = planAnterior?.FeeBase ?? 0;
        var feeNuevo = planNuevo.FeeBase;
        var tipo = feeNuevo > feeAnterior ? TipoEventoSuscripcion.Upgrade : TipoEventoSuscripcion.Downgrade;

        s.PlanId = req.NuevoPlanId;
        _db.SuscripcionHistorial.Add(new SuscripcionHistorial
        {
            SuscripcionId = s.Id,
            Tipo = tipo,
            Origen = OrigenEventoSuscripcion.SuperAdmin,
            ActorId = actorId,
            PlanAnteriorId = planAnteriorId,
            PlanNuevoId = req.NuevoPlanId,
            Notas = req.Justificacion
        });
        _db.SuperAdminLogs.Add(SuperLog(actorId, actorEmail, $"SUSCRIPCION_{tipo.ToString().ToUpper()}",
            $"Suscripcion:{s.Id}",
            $"De Plan:{planAnteriorId} a Plan:{req.NuevoPlanId}. Justificacion: {req.Justificacion}",
            ip));
        await _db.SaveChangesAsync(ct);
        return await SuscripcionDtoByIdAsync(s.Id, ct);
    }

    public async Task<SuscripcionDto?> CambiarEstadoSuscripcionAsync(Guid suscripcionId, CambiarEstadoSuscripcionRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct)
    {
        var s = await _db.Suscripciones.FirstOrDefaultAsync(x => x.Id == suscripcionId, ct);
        if (s is null) return null;
        if (string.IsNullOrWhiteSpace(req.Justificacion)) throw new InvalidOperationException("Justificacion obligatoria.");

        var estadoAnterior = s.Estado;
        s.Estado = req.NuevoEstado;

        var tipo = req.NuevoEstado switch
        {
            EstadoSuscripcion.Suspendida => TipoEventoSuscripcion.Suspension,
            EstadoSuscripcion.Activa when estadoAnterior == EstadoSuscripcion.Suspendida => TipoEventoSuscripcion.Reactivacion,
            EstadoSuscripcion.EnCancelacion or EstadoSuscripcion.Cancelada or EstadoSuscripcion.Archivada => TipoEventoSuscripcion.Cancelacion,
            _ => TipoEventoSuscripcion.AjusteManual
        };

        _db.SuscripcionHistorial.Add(new SuscripcionHistorial
        {
            SuscripcionId = s.Id,
            Tipo = tipo,
            Origen = OrigenEventoSuscripcion.SuperAdmin,
            ActorId = actorId,
            EstadoAnterior = estadoAnterior.ToString(),
            EstadoNuevo = req.NuevoEstado.ToString(),
            Notas = req.Justificacion
        });
        _db.SuperAdminLogs.Add(SuperLog(actorId, actorEmail, "SUSCRIPCION_CHANGE_STATE",
            $"Suscripcion:{s.Id}",
            $"De {estadoAnterior} a {req.NuevoEstado}. Justificacion: {req.Justificacion}",
            ip));
        await _db.SaveChangesAsync(ct);
        return await SuscripcionDtoByIdAsync(s.Id, ct);
    }

    public async Task<IReadOnlyList<SuscripcionHistorialDto>> GetHistorialAsync(Guid suscripcionId, CancellationToken ct)
    {
        return await _db.SuscripcionHistorial
            .AsNoTracking()
            .Where(h => h.SuscripcionId == suscripcionId)
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new SuscripcionHistorialDto(
                h.Id, h.SuscripcionId, h.Tipo, h.Origen, h.ActorId,
                h.PlanAnteriorId, h.PlanNuevoId, h.EstadoAnterior, h.EstadoNuevo,
                h.MontoProrrateo, h.CreditoGenerado, h.Notas, h.CreatedAt))
            .ToListAsync(ct);
    }

    private async Task<SuscripcionDto> SuscripcionDtoByIdAsync(Guid id, CancellationToken ct)
    {
        var s = await _db.Suscripciones
            .AsNoTracking()
            .Include(x => x.Plan)
            .Include(x => x.Organizacion)
            .Include(x => x.Copropiedad)
            .FirstAsync(x => x.Id == id, ct);
        return new SuscripcionDto(
            s.Id,
            s.OrganizacionId, s.Organizacion?.Nombre,
            s.CopropiedadId, s.Copropiedad?.Nombre,
            s.PlanId, s.Plan!.Nombre,
            s.Ciclo, s.Estado,
            s.FechaInicio, s.FechaAniversario,
            s.FechaProximoCobro, s.FechaFinTrial,
            s.CreditoAFavor, s.CreatedAt);
    }

    private static DateOnly AvanzarPeriodo(DateOnly desde, CicloFacturacion ciclo)
        => ciclo == CicloFacturacion.Mensual ? desde.AddMonths(1) : desde.AddYears(1);

    // ----------------------------- Facturas -----------------------------

    public async Task<IReadOnlyList<FacturaDto>> ListFacturasAsync(Guid? suscripcionId, CancellationToken ct)
    {
        var q = _db.Facturas.AsNoTracking().AsQueryable();
        if (suscripcionId.HasValue) q = q.Where(f => f.SuscripcionId == suscripcionId.Value);
        return await q
            .OrderByDescending(f => f.FechaEmision)
            .Select(f => new FacturaDto(
                f.Id, f.SuscripcionId, f.NumeroFactura, f.Cufe,
                f.PeriodoDesde, f.PeriodoHasta,
                f.Subtotal, f.Descuento, f.Total,
                f.Estado, f.FechaEmision, f.FechaVencimiento, f.FechaPago))
            .ToListAsync(ct);
    }

    public async Task<FacturaDto> GenerarFacturaAsync(GenerarFacturaRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct)
    {
        var s = await _db.Suscripciones.Include(x => x.Plan).FirstOrDefaultAsync(x => x.Id == req.SuscripcionId, ct);
        if (s is null) throw new InvalidOperationException("Suscripcion no encontrada.");
        if (req.PeriodoHasta < req.PeriodoDesde) throw new InvalidOperationException("PeriodoHasta debe ser >= PeriodoDesde.");

        var config = await GetConfigEntityAsync(ct);

        // Calculo simple del MVP: fee base + (fee variable * 1 unidad placeholder).
        // En Fase 2 se calcula contra el conteo real de unidades del tenant y aplican
        // tramos de volumen, cupones y descuento anual.
        var subtotal = s.Plan!.FeeBase;
        if (s.Ciclo == CicloFacturacion.Anual && s.Plan.DescuentoAnualPct > 0)
        {
            subtotal = subtotal * 12m * (1m - s.Plan.DescuentoAnualPct / 100m);
        }
        var descuento = 0m;
        var impuestoPct = config.ImpuestoPct;
        var baseImponible = subtotal - descuento;
        var impuestoValor = Math.Round(baseImponible * impuestoPct / 100m, 2);
        var total = baseImponible + impuestoValor;

        var factura = new Factura
        {
            SuscripcionId = s.Id,
            PeriodoDesde = req.PeriodoDesde,
            PeriodoHasta = req.PeriodoHasta,
            Subtotal = subtotal,
            Descuento = descuento,
            ImpuestoPct = impuestoPct,
            ImpuestoValor = impuestoValor,
            Total = total,
            Estado = EstadoFactura.Pendiente,
            FechaEmision = DateTimeOffset.UtcNow,
            FechaVencimiento = req.PeriodoHasta.AddDays(config.DiasGracia)
        };
        _db.Facturas.Add(factura);
        _db.SuperAdminLogs.Add(SuperLog(actorId, actorEmail, "GENERATE_FACTURA",
            $"Factura:{factura.Id}",
            $"SuscripcionId={s.Id} Periodo={req.PeriodoDesde}/{req.PeriodoHasta} Total={total:0.00}",
            ip));
        await _db.SaveChangesAsync(ct);
        return ToFacturaDto(factura);
    }

    public async Task<FacturaDto?> RegistrarPagoAsync(Guid facturaId, RegistrarPagoFacturaRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct)
    {
        var f = await _db.Facturas.FirstOrDefaultAsync(x => x.Id == facturaId, ct);
        if (f is null) return null;
        if (f.Estado == EstadoFactura.Pagada) return ToFacturaDto(f);

        f.Estado = EstadoFactura.Pagada;
        f.FechaPago = DateTimeOffset.UtcNow;
        f.WompiTransactionId = req.WompiTransactionId;
        if (!string.IsNullOrWhiteSpace(req.NumeroFactura)) f.NumeroFactura = req.NumeroFactura;

        // Registramos intento de cobro exitoso (numero_intento=1 por simplicidad MVP)
        _db.IntentosCobro.Add(new IntentoCobro
        {
            FacturaId = f.Id,
            SuscripcionId = f.SuscripcionId,
            NumeroIntento = 1,
            FechaIntento = DateTimeOffset.UtcNow,
            Resultado = ResultadoIntentoCobro.Exitoso
        });

        _db.SuperAdminLogs.Add(SuperLog(actorId, actorEmail, "REGISTRAR_PAGO",
            $"Factura:{f.Id}",
            $"WompiTxn={req.WompiTransactionId} NumeroFactura={req.NumeroFactura}",
            ip));

        await _db.SaveChangesAsync(ct);
        return ToFacturaDto(f);
    }

    // ----------------------------- Config -----------------------------

    public async Task<BillingConfigDto> GetConfigAsync(CancellationToken ct)
    {
        var c = await GetConfigEntityAsync(ct);
        return ToConfigDto(c);
    }

    public async Task<BillingConfigDto> ActualizarConfigAsync(ActualizarBillingConfigRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct)
    {
        var c = await GetConfigEntityAsync(ct);
        c.DiasGracia = req.DiasGracia;
        c.DiaAlertaMora1 = req.DiaAlertaMora1;
        c.DiaAlertaMora2 = req.DiaAlertaMora2;
        c.DiaSuspension = req.DiaSuspension;
        c.DiaAlertaCancelacion = req.DiaAlertaCancelacion;
        c.DiaCancelacion = req.DiaCancelacion;
        c.ReintentosCobro = req.ReintentosCobro;
        c.DiasEntreReintentos = req.DiasEntreReintentos;
        c.DiasPreavisoCobro = req.DiasPreavisoCobro;
        c.RetencionDatosMeses = req.RetencionDatosMeses;
        c.RetencionFacturasAnios = req.RetencionFacturasAnios;
        c.ImpuestoPct = req.ImpuestoPct;
        c.Moneda = req.Moneda;
        c.ProveedorContable = req.ProveedorContable;

        _db.SuperAdminLogs.Add(SuperLog(actorId, actorEmail, "UPDATE_BILLING_CONFIG", "BillingConfig",
            $"impuesto_pct={req.ImpuestoPct} moneda={req.Moneda} dia_suspension={req.DiaSuspension}", ip));
        await _db.SaveChangesAsync(ct);
        return ToConfigDto(c);
    }

    private async Task<BillingConfig> GetConfigEntityAsync(CancellationToken ct)
    {
        var c = await _db.BillingConfig.FirstOrDefaultAsync(x => x.Id == BillingConfig.SingletonId, ct);
        if (c is null) throw new InvalidOperationException("BillingConfig singleton no fue seedeado - revisar migracion.");
        return c;
    }

    // ----------------------------- Helpers -----------------------------

    private static PlanDto ToDto(Plan p, int suscripcionesActivas) =>
        new(p.Id, p.Nombre, p.Descripcion,
            p.FeeBase, p.FeeVariablePorUnidad,
            p.CicloMensual, p.CicloAnual, p.DescuentoAnualPct,
            p.LimiteUnidades, p.LimiteUsuarios, p.LimiteStorageGb,
            p.LimiteLineasWhatsapp, p.LimiteLlamadasIaMensual,
            p.DiasTrial, p.Estado, suscripcionesActivas, p.CreatedAt, p.LimiteCopropiedades, p.EsPromocional);

    private static FacturaDto ToFacturaDto(Factura f) =>
        new(f.Id, f.SuscripcionId, f.NumeroFactura, f.Cufe,
            f.PeriodoDesde, f.PeriodoHasta,
            f.Subtotal, f.Descuento, f.Total,
            f.Estado, f.FechaEmision, f.FechaVencimiento, f.FechaPago);

    private static BillingConfigDto ToConfigDto(BillingConfig c) =>
        new(c.DiasGracia, c.DiaAlertaMora1, c.DiaAlertaMora2,
            c.DiaSuspension, c.DiaAlertaCancelacion, c.DiaCancelacion,
            c.ReintentosCobro, c.DiasEntreReintentos,
            c.DiasPreavisoCobro, c.RetencionDatosMeses, c.RetencionFacturasAnios,
            c.ImpuestoPct, c.Moneda, c.ProveedorContable);

    private static SuperAdminLog SuperLog(Guid actorId, string actorEmail, string accion, string entidad, string? justif, string? ip) =>
        new()
        {
            ActorId = actorId,
            ActorEmail = actorEmail,
            Accion = accion,
            EntidadAfectada = entidad,
            Justificacion = justif,
            Ip = ip
        };
}
