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
/// Capacidades MVP:
///  - CerrarVencidosTrasInconformidadAsync: cierra automaticamente expedientes
///    en estado Respondida cuya ventana de inconformidad ya vencio sin que
///    el ciudadano la haya activado. Spec 2.9 RN-06.
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
    /// Recorre todos los tenants y cierra los expedientes Respondida cuya ventana
    /// de inconformidad expiro (RespuestaAdminAt + DiasInconformidad de la config
    /// del tipo). El usuario que cierra es null = cierre automatico del sistema.
    /// </summary>
    public async Task<int> CerrarVencidosTrasInconformidadAsync(CancellationToken ct)
    {
        // IgnoreQueryFilters porque corremos en contexto background sin tenant.
        var candidatos = await _db.PqrsdExpedientes.IgnoreQueryFilters()
            .Where(e => e.Estado == EstadoPqrsd.Respondida
                        && e.InconformidadTexto == null
                        && e.RespuestaAdminAt != null)
            .ToListAsync(ct);
        if (candidatos.Count == 0) return 0;

        // Cargar plazos por tenant+tipo para evitar N+1.
        var keys = candidatos.Select(c => new { c.TenantId, c.Tipo }).Distinct().ToList();
        var tenantIds = keys.Select(k => k.TenantId).Distinct().ToList();
        var plazos = await _db.PqrsdConfiguracionPlazos.IgnoreQueryFilters()
            .Where(p => tenantIds.Contains(p.TenantId))
            .ToListAsync(ct);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        int cerrados = 0;
        var notiBatch = new List<EnviarNotificacionRequest>();
        foreach (var exp in candidatos)
        {
            var plazo = plazos.FirstOrDefault(p => p.TenantId == exp.TenantId && p.Tipo == exp.Tipo);
            var diasInconformidad = plazo?.DiasInconformidad ?? 10;
            var respondidaDate = DateOnly.FromDateTime(exp.RespuestaAdminAt!.Value.UtcDateTime);
            var vence = await _calendario.SumarDiasHabilesAsync(respondidaDate, diasInconformidad, ct);
            if (hoy <= vence) continue;

            exp.Estado = EstadoPqrsd.Cerrada;
            exp.FechaCierre = DateTimeOffset.UtcNow;
            exp.CerradoPorUsuarioId = null; // cierre del sistema
            exp.UpdatedAt = DateTimeOffset.UtcNow;
            _db.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
            {
                TenantId = exp.TenantId,
                ExpedienteId = exp.Id,
                EstadoAnterior = EstadoPqrsd.Respondida,
                EstadoNuevo = EstadoPqrsd.Cerrada,
                ActorUsuarioId = Guid.Empty, // sistema
                Origen = OrigenCambioEstado.Sistema,
                Nota = $"Cierre automatico: venció ventana inconformidad ({diasInconformidad} dias habiles desde respuesta)."
            });

            // T.2: notifica al radicador (PersonaDestinatariaId), si existe.
            if (exp.RadicadorPersonaId != Guid.Empty)
            {
                notiBatch.Add(new EnviarNotificacionRequest(
                    Canal: CanalNotificacion.InApp,
                    Cuerpo: $"Tu PQRSD {exp.NumeroRadicado} fue cerrada automaticamente al vencer la ventana de inconformidad ({diasInconformidad} dias habiles).",
                    TenantId: exp.TenantId,
                    PersonaDestinatariaId: exp.RadicadorPersonaId,
                    Asunto: $"PQRSD cerrada: {exp.NumeroRadicado}",
                    Prioridad: PrioridadNotificacion.Normal,
                    ModuloOrigenCodigo: "2.9",
                    EntidadOrigenId: exp.Id));
            }
            cerrados++;
        }

        if (cerrados > 0)
        {
            await _db.SaveChangesAsync(ct);
            if (notiBatch.Count > 0)
            {
                try { await _noti.EnviarLoteAsync(notiBatch, ct); }
                catch (Exception ex) { _log.LogWarning(ex, "T.2 lote fallido en cierre automatico"); }
            }
            _log.LogInformation("PQRSD cierre automatico: {Cerrados} expediente(s) cerrados", cerrados);
        }
        return cerrados;
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
            catch (Exception ex) { _log.LogWarning(ex, "Alerta de plazo PQRSD: fallo en tenant {Tenant}", tid); }
        }

        if (alertas > 0) _log.LogInformation("PQRSD alertas de plazo: {Alertas} emitida(s)", alertas);
        return alertas;
    }
}
