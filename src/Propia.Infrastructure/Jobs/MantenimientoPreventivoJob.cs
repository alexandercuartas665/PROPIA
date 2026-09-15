using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Mantenimiento;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Jobs;

/// <summary>
/// Motor diario del mantenimiento PREVENTIVO (modulo 2.11, hallazgo M-01).
///
/// Hasta ahora <see cref="MantenimientoPlan.Disparo"/> y <see cref="MantenimientoPlan.DiasAlertaPrevio"/>
/// eran decorativos: nadie los leia, asi que un plan Automatico nunca generaba su intervencion y un plan
/// ConConfirmacion nunca avisaba. Este job cierra ese hueco:
///
///  - Automatico     -> vencido: crea la intervencion preventiva (Origen=Automatico) via
///                      IMantenimientoService, que a su vez crea la tarea vinculada (RN-03), y avanza
///                      ProximaEjecucion.
///  - ConConfirmacion-> vencido: NO crea nada; deja una alerta para que un administrador confirme.
///  - Cualquiera     -> proximo a vencer (dentro de DiasAlertaPrevio): alerta amarilla.
///
/// Corre sin contexto de request, asi que itera los tenants (tabla global) y para cada uno fija
/// ITenantContext + reabre la conexion, igual que ProgramacionTareasJob: sin eso la policy de RLS
/// devuelve cero filas EN SILENCIO con el usuario de aplicacion.
///
/// Idempotencia:
///  - Automatico: al crear la intervencion se avanza ProximaEjecucion, asi que el plan deja de estar
///    vencido y no se vuelve a disparar.
///  - Alertas: antes de insertar se comprueba que no exista ya una alerta ACTIVA para ese plan. Por eso
///    no hace falta una columna nueva en el plan (y por tanto ninguna migracion).
/// </summary>
public class MantenimientoPreventivoJob : IBackgroundJob
{
    public string Nombre => "MantenimientoPreventivo";

    /// <summary>Diario: los planes vencen por dia, no por hora.</summary>
    public int FrecuenciaMinutos => 1440;

    private const string ModuloCodigo = "2.11";

    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IMantenimientoService _mantenimiento;

    public MantenimientoPreventivoJob(
        PropiaDbContext db, ITenantContext tenant, IMantenimientoService mantenimiento)
    {
        _db = db;
        _tenant = tenant;
        _mantenimiento = mantenimiento;
    }

    /// <summary>
    /// El dia calendario "hoy" en hora local de la copropiedad (Colombia, UTC-5), a partir de un
    /// instante UTC. El job compara <see cref="MantenimientoPlan.ProximaEjecucion"/> (un DateOnly,
    /// dia calendario) contra este valor. Con UtcNow crudo, entre las 19:00 y las 24:00 hora local
    /// "hoy" ya era el dia siguiente en UTC, asi que un plan podia dispararse hasta 5 horas antes de
    /// tiempo, la vispera (M-09). Se usa el mismo helper de zona que Reservas.
    /// </summary>
    public static DateOnly HoyEnColombia(DateTime utcNow)
    {
        var zonaLocal = Propia.Infrastructure.Programaciones.CronHelper.Zona(null);
        var utc = utcNow.Kind == DateTimeKind.Utc ? utcNow : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, zonaLocal));
    }

    public async Task<object?> EjecutarAsync(CancellationToken ct)
    {
        // "Hoy" en hora local de Colombia, no en UTC (M-09). Ver HoyEnColombia.
        var hoy = HoyEnColombia(DateTime.UtcNow);

        // Tenants es global (sin RLS); nos da la lista para iterar.
        var tenantIds = await _db.Tenants.AsNoTracking().Select(t => t.Id).ToListAsync(ct);

        int tenantsConTrabajo = 0, planesRevisados = 0, intervencionesCreadas = 0;
        int alertasConfirmacion = 0, alertasPorVencer = 0, errores = 0;

        foreach (var tid in tenantIds)
        {
            try
            {
                _tenant.SetTenant(tid);
                await _db.Database.CloseConnectionAsync();   // el interceptor aplica app.tenant_id al reabrir

                // Candidatos: vencidos (<= hoy) y los que entran en ventana de alerta previa.
                // El limite superior se calcula con el mayor DiasAlertaPrevio posible (90) y luego se
                // filtra en memoria por el de cada plan, para no traer toda la tabla.
                var limite = hoy.AddDays(90);
                var planes = await _db.MantenimientoPlanes
                    .Where(p => p.Activo && p.ProximaEjecucion <= limite)
                    .ToListAsync(ct);
                if (planes.Count == 0) continue;

                var trabajoEnTenant = false;

                foreach (var plan in planes)
                {
                    ct.ThrowIfCancellationRequested();
                    planesRevisados++;

                    if (plan.ProximaEjecucion <= hoy)
                    {
                        if (plan.Disparo == DisparoPlanMantenimiento.Automatico)
                        {
                            if (await GenerarIntervencionAsync(plan, hoy, ct))
                            {
                                intervencionesCreadas++;
                                trabajoEnTenant = true;
                            }
                        }
                        else if (await CrearAlertaSiNoExisteAsync(plan, SeveridadAlerta.Critica,
                                     "Mantenimiento pendiente de confirmar",
                                     $"El plan '{plan.Nombre}' vencio el {plan.ProximaEjecucion:dd/MM/yyyy} y requiere confirmacion para generar la intervencion.",
                                     ct))
                        {
                            alertasConfirmacion++;
                            trabajoEnTenant = true;
                        }
                    }
                    else if (plan.ProximaEjecucion <= hoy.AddDays(plan.DiasAlertaPrevio))
                    {
                        var dias = plan.ProximaEjecucion.DayNumber - hoy.DayNumber;
                        if (await CrearAlertaSiNoExisteAsync(plan, SeveridadAlerta.Advertencia,
                                $"Mantenimiento por vencer ({dias} dias)",
                                $"El plan '{plan.Nombre}' vence el {plan.ProximaEjecucion:dd/MM/yyyy}.",
                                ct))
                        {
                            alertasPorVencer++;
                            trabajoEnTenant = true;
                        }
                    }
                }

                await _db.SaveChangesAsync(ct);
                if (trabajoEnTenant) tenantsConTrabajo++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                // Un tenant con datos inconsistentes no puede tumbar el job para los demas.
                // El contador queda en el resultado para que se vea en job_ejecuciones.
                errores++;
            }
        }

        return new
        {
            tenants = tenantIds.Count,
            tenantsConTrabajo,
            planesRevisados,
            intervencionesCreadas,
            alertasConfirmacion,
            alertasPorVencer,
            errores,
            fecha = hoy.ToString("yyyy-MM-dd")
        };
    }

    /// <summary>
    /// Crea la intervencion preventiva del plan y avanza su ProximaEjecucion.
    /// Devuelve false si la creacion fallo (activo borrado, por ejemplo): en ese caso NO se avanza la
    /// fecha, para que el plan siga marcado como vencido y el problema se vea, en vez de saltarse
    /// silenciosamente un mantenimiento.
    /// </summary>
    private async Task<bool> GenerarIntervencionAsync(MantenimientoPlan plan, DateOnly hoy, CancellationToken ct)
    {
        try
        {
            await _mantenimiento.CrearIntervencionAsync(new CrearIntervencionRequest(
                Tipo: TipoIntervencionMantenimiento.Preventivo,
                ActivoTipo: plan.ActivoTipo,
                ActivoId: plan.ActivoId,
                PlanId: plan.Id,
                Origen: OrigenIntervencion.Automatico,
                OrigenReferenciaId: plan.Id,
                Titulo: plan.Nombre,
                Descripcion: plan.Descripcion,
                Prioridad: PrioridadIntervencion.Normal,
                ProveedorId: plan.ProveedorPreferidoId,
                ResponsableInternoId: null,
                FechaProgramada: plan.ProximaEjecucion,
                NotificarResidentes: plan.GeneraNotifResidentes), ct);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        // Avanza desde la fecha que vencia, no desde hoy: si el job no corrio unos dias, la cadencia
        // no se desplaza. Si quedo muy atras, se adelanta hasta pasar de hoy para no generar una
        // rafaga de intervenciones atrasadas en los siguientes ticks.
        var dias = FrecuenciaEnDias(plan.Frecuencia, plan.FrecuenciaDias);
        var proxima = plan.ProximaEjecucion.AddDays(dias);
        while (proxima <= hoy) proxima = proxima.AddDays(dias);
        plan.ProximaEjecucion = proxima;

        // Al generar la intervencion, la alerta que hubiera sobre este plan deja de aplicar.
        await ResolverAlertasDelPlanAsync(plan.Id, ct);
        return true;
    }

    /// <summary>
    /// Inserta la alerta solo si no hay ya una ACTIVA para ese plan. Asi el job puede correr todos los
    /// dias sin acumular una alerta por dia del mismo plan.
    /// </summary>
    private async Task<bool> CrearAlertaSiNoExisteAsync(
        MantenimientoPlan plan, SeveridadAlerta severidad, string titulo, string descripcion, CancellationToken ct)
    {
        var yaHay = await _db.AlertasCopropiedad
            .AnyAsync(a => a.EntidadId == plan.Id
                           && a.ModuloOrigenCodigo == ModuloCodigo
                           && a.Activa, ct);
        if (yaHay) return false;

        _db.AlertasCopropiedad.Add(new AlertaCopropiedad
        {
            // No hay un TipoAlertaDashboard de mantenimiento; se usa Otro y el modulo lo identifica.
            Tipo = TipoAlertaDashboard.Otro,
            Severidad = severidad,
            Titulo = titulo,
            Descripcion = descripcion,
            UrlAccion = "/mantenimiento",
            ModuloOrigenCodigo = ModuloCodigo,
            EntidadId = plan.Id,
            Activa = true
        });
        return true;
    }

    private async Task ResolverAlertasDelPlanAsync(Guid planId, CancellationToken ct)
    {
        var abiertas = await _db.AlertasCopropiedad
            .Where(a => a.EntidadId == planId && a.ModuloOrigenCodigo == ModuloCodigo && a.Activa)
            .ToListAsync(ct);
        foreach (var a in abiertas)
        {
            a.Activa = false;
            a.ResueltaAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Equivalencia en dias de cada frecuencia. Replica la tabla de MantenimientoService (spec 22
    /// notas): MENSUAL son 30 dias fijos, no mes calendario.
    /// </summary>
    private static int FrecuenciaEnDias(FrecuenciaMantenimiento f, int? personalizada) => f switch
    {
        FrecuenciaMantenimiento.Semanal => 7,
        FrecuenciaMantenimiento.Quincenal => 15,
        FrecuenciaMantenimiento.Mensual => 30,
        FrecuenciaMantenimiento.Trimestral => 90,
        FrecuenciaMantenimiento.Semestral => 180,
        FrecuenciaMantenimiento.Anual => 365,
        FrecuenciaMantenimiento.Personalizada => personalizada ?? 30,
        _ => 30
    };
}
