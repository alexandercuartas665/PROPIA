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
    private readonly ILogger<PqrsdMantenimientoService> _log;

    public PqrsdMantenimientoService(
        PropiaDbContext db,
        ICalendarioHabilService calendario,
        INotificacionDispatcher noti,
        ILogger<PqrsdMantenimientoService> log)
    {
        _db = db;
        _calendario = calendario;
        _noti = noti;
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
}
