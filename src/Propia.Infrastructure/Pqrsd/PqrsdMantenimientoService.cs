using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Propia.Application.Common;
using Propia.Application.Notificaciones;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Pqrsd;

/// <summary>
/// Tareas de mantenimiento periodicas del modulo 2.9 PQRSD.
/// Invocado por un BackgroundService (en produccion) o manualmente desde tests.
///
/// Capacidad:
///  - AlertarPlazosAsync (G-01): alertas de plazo legal (80% / vencido). AVISA, no cierra.
///
/// NOTA: el cierre automatico tras la ventana de inconformidad se ELIMINO (decision de Alex 2026-09-13): un
/// PQRSD nunca se cierra solo; el cierre es siempre manual. El job de cierre nocturno se elimino por completo
/// (SOLICITUD_FARO_01).
/// </summary>
public class PqrsdMantenimientoService
{
    private readonly PropiaDbContext _db;
    private readonly ICalendarioHabilService _calendario;
    private readonly INotificacionDispatcher _noti;
    private readonly ITenantContext _tenant;
    private readonly ILogger<PqrsdMantenimientoService> _log;

    public PqrsdMantenimientoService(
        PropiaDbContext db,
        ICalendarioHabilService calendario,
        INotificacionDispatcher noti,
        ITenantContext tenant,
        ILogger<PqrsdMantenimientoService> log)
    {
        _db = db;
        _calendario = calendario;
        _noti = noti;
        _tenant = tenant;
        _log = log;
    }


    /// <summary>
    /// G-01 (spec 2.9, secciones 11 y 15): alertas automaticas de plazo. Recorre los expedientes activos
    /// (no terminales, no archivados, sin tutela) de todos los tenants; al cruzar el 80% del plazo legal en
    /// dias habiles avisa (Advertencia) y al vencer avisa (Critica). Idempotente por expediente+umbral via
    /// `AlertaPlazoNotificada`. Notifica por TODOS los canales (email/WhatsApp/InApp) al responsable si esta
    /// asignado, si no a los administradores del tenant; crea una alerta de dashboard y deja traza en el
    /// historial del expediente.
    /// </summary>
    public async Task<int> AlertarPlazosAsync(CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        // Sin contexto de request: se itera tenant por tenant (tabla global) y se hace SetTenant para que
        // RLS + el query filter dejen ver los expedientes de cada copropiedad (mismo patron que ContratosVencimientoJob).
        var tenantIds = await _db.Tenants.AsNoTracking().Select(t => t.Id).ToListAsync(ct);
        int alertas = 0;

        try
        {
        foreach (var tid in tenantIds)
        {
            try
            {
                _tenant.SetTenant(tid);
                await _db.Database.CloseConnectionAsync(); // el interceptor aplica app.tenant_id al reabrir

                var activos = await _db.PqrsdExpedientes
                    .Where(e => !e.Archivado && !e.TutelaActiva
                                && e.Estado != EstadoPqrsd.Cerrada && e.Estado != EstadoPqrsd.ViaInternaAgotada)
                    .ToListAsync(ct);
                if (activos.Count == 0) continue;

                List<Guid>? admins = null;
                bool cambios = false;
                var eventos = new List<(Guid persona, string asunto, string cuerpo, Guid exp, PrioridadNotificacion prio)>();

                foreach (var e in activos)
                {
                    int? umbral = null;
                    if (hoy > e.FechaVencimiento) umbral = 100;
                    else
                    {
                        var desde = DateOnly.FromDateTime(e.CreatedAt.UtcDateTime.Date);
                        var total = await _calendario.ContarDiasHabilesAsync(desde, e.FechaVencimiento, ct);
                        var consumidos = await _calendario.ContarDiasHabilesAsync(desde, hoy, ct);
                        var pct = total <= 0 ? 1.0 : (double)consumidos / total;
                        if (pct >= 0.8) umbral = 80;
                    }

                    if (umbral is null)
                    {
                        if (e.AlertaPlazoNotificada != null) { e.AlertaPlazoNotificada = null; cambios = true; }
                        continue;
                    }

                    var ya = e.AlertaPlazoNotificada;
                    var debe = ya is null || (umbral == 100 && ya != 100);  // escala de 80 a 100 avisa de nuevo
                    if (!debe) continue;

                    var critica = umbral == 100;
                    var titulo = critica ? $"PQRSD vencida: {e.NumeroRadicado}" : $"PQRSD por vencer: {e.NumeroRadicado}";
                    var cuerpo = critica
                        ? $"El plazo legal de la PQRSD {e.NumeroRadicado} esta VENCIDO (vencia el {e.FechaVencimiento:dd MMM yyyy}). Responde de inmediato."
                        : $"La PQRSD {e.NumeroRadicado} consumio el 80% de su plazo legal (vence el {e.FechaVencimiento:dd MMM yyyy}).";

                    _db.AlertasCopropiedad.Add(new AlertaCopropiedad
                    {
                        Tipo = TipoAlertaDashboard.PqrsdSinAtender,
                        Severidad = critica ? SeveridadAlerta.Critica : SeveridadAlerta.Advertencia,
                        Titulo = titulo,
                        Descripcion = cuerpo,
                        UrlAccion = "/pqrs",
                        ModuloOrigenCodigo = "2.9",
                        EntidadId = e.Id,
                        Activa = true
                    });

                    // Responsable si esta asignado; si no, administradores del tenant (se cargan una sola vez).
                    var destinos = new List<Guid>();
                    if (e.AsignadoPersonaId is Guid asg && asg != Guid.Empty) destinos.Add(asg);
                    else
                    {
                        admins ??= await _db.UsuariosTenant.AsNoTracking()
                            .Where(u => u.Estado == EstadoUsuarioTenant.Activo && u.Rol == "Administrador")
                            .Select(u => u.PersonaId).Distinct().ToListAsync(ct);
                        destinos.AddRange(admins);
                    }
                    var prio = critica ? PrioridadNotificacion.Critica : PrioridadNotificacion.Alta;
                    foreach (var pid in destinos.Distinct()) eventos.Add((pid, titulo, cuerpo, e.Id, prio));

                    _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
                    {
                        ExpedienteId = e.Id,
                        EstadoAnterior = null,
                        EstadoNuevo = e.Estado,
                        ActorUsuarioId = Guid.Empty,
                        Origen = OrigenCambioEstado.Sistema,
                        Nota = critica ? "Alerta de plazo: plazo legal VENCIDO (sistema)." : "Alerta de plazo: 80% del plazo consumido (sistema)."
                    });

                    e.AlertaPlazoNotificada = umbral;
                    alertas++; cambios = true;
                }

                if (cambios) await _db.SaveChangesAsync(ct);
                foreach (var ev in eventos)
                {
                    try { await _noti.EnviarEventoUsuarioAsync(ev.persona, ev.asunto, ev.cuerpo, "2.9", ev.exp, tid, ev.prio, ct); }
                    catch (Exception ex) { _log.LogWarning(ex, "Alerta de plazo PQRSD: notificacion fallida para persona {Persona}", ev.persona); }
                }
            }
            catch (Exception ex)
            {
                _db.ChangeTracker.Clear();   // N-2: no arrastrar entidades marcadas al siguiente tenant
                _log.LogWarning(ex, "Alerta de plazo PQRSD: fallo en tenant {Tenant}", tid);
            }
        }
        }
        finally
        {
            // N-1: reset del tenant + cierre de conexion para no contaminar el siguiente job del mismo tick.
            _tenant.Clear();
            await _db.Database.CloseConnectionAsync();
        }

        if (alertas > 0) _log.LogInformation("PQRSD alertas de plazo: {Alertas} emitida(s)", alertas);
        return alertas;
    }
}
