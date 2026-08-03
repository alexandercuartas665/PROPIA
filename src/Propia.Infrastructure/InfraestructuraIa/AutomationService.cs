using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Application.Notificaciones;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Motor de reglas de automatizacion de la copropiedad (Infraestructura IA). Re-mapeado de
/// CUBOT.travels (dominio de ventas) al de copropiedades. CRUD + corrida. HOY solo ejecuta de
/// verdad la combinacion ChatSinRespuesta -> NotificarAdministracion (via el motor T.2); el resto
/// queda como scaffolding (definible en UI, marcado "proximamente"). Un job en background llama a
/// RunNowAsync periodicamente por tenant.
/// </summary>
public sealed class AutomationService : IAutomationService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly INotificacionDispatcher _notificaciones;

    public AutomationService(PropiaDbContext db, ITenantContext tenant, INotificacionDispatcher notificaciones)
    {
        _db = db;
        _tenant = tenant;
        _notificaciones = notificaciones;
    }

    private static AutomationRuleDto ToDto(AutomationRule r) => new(
        r.Id, r.Name, r.Trigger, r.ThresholdMinutes, r.TimeWindowStart, r.TimeWindowEnd,
        r.Action, r.MensajePlantilla, r.TareaTitulo, r.IsActive, r.SortOrder, r.ExecutionCount, r.LastRunAt,
        IAutomationService.IsImplemented(r.Trigger, r.Action));

    public async Task<IReadOnlyList<AutomationRuleDto>> ListAsync(CancellationToken ct = default)
        => await _db.AutomationRules.AsNoTracking()
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Name)
            .Select(r => new AutomationRuleDto(
                r.Id, r.Name, r.Trigger, r.ThresholdMinutes, r.TimeWindowStart, r.TimeWindowEnd,
                r.Action, r.MensajePlantilla, r.TareaTitulo, r.IsActive, r.SortOrder, r.ExecutionCount, r.LastRunAt,
                r.Trigger == AutomationTrigger.ChatSinRespuesta && r.Action == AutomationAction.NotificarAdministracion))
            .ToListAsync(ct);

    public async Task<AutomationRuleDto?> CreateAsync(SaveAutomationRuleRequest req, CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is not Guid tenantId) return null;
        if (string.IsNullOrWhiteSpace(req.Name)) return null;

        var maxOrder = await _db.AutomationRules.AsNoTracking().Select(r => (int?)r.SortOrder).MaxAsync(ct) ?? -1;
        var rule = new AutomationRule
        {
            TenantId = tenantId,
            Name = req.Name.Trim(),
            Trigger = req.Trigger,
            ThresholdMinutes = req.ThresholdMinutes > 0 ? req.ThresholdMinutes : 30,
            TimeWindowStart = req.TimeWindowStart,
            TimeWindowEnd = req.TimeWindowEnd,
            Action = req.Action,
            MensajePlantilla = req.MensajePlantilla,
            TareaTitulo = req.TareaTitulo,
            IsActive = false, // arranca apagada; el operador la enciende cuando este lista
            SortOrder = maxOrder + 1
        };
        _db.AutomationRules.Add(rule);
        await _db.SaveChangesAsync(ct);
        return ToDto(rule);
    }

    public async Task<AutomationRuleDto?> UpdateAsync(Guid id, SaveAutomationRuleRequest req, CancellationToken ct = default)
    {
        var rule = await _db.AutomationRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null) return null;
        if (!string.IsNullOrWhiteSpace(req.Name)) rule.Name = req.Name.Trim();
        rule.Trigger = req.Trigger;
        rule.ThresholdMinutes = req.ThresholdMinutes > 0 ? req.ThresholdMinutes : 30;
        rule.TimeWindowStart = req.TimeWindowStart;
        rule.TimeWindowEnd = req.TimeWindowEnd;
        rule.Action = req.Action;
        rule.MensajePlantilla = req.MensajePlantilla;
        rule.TareaTitulo = req.TareaTitulo;
        await _db.SaveChangesAsync(ct);
        return ToDto(rule);
    }

    public async Task<bool> SetActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        var rule = await _db.AutomationRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null) return false;
        rule.IsActive = active;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var rule = await _db.AutomationRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null) return false;
        _db.AutomationRules.Remove(rule);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<AutomationRunResult> RunNowAsync(CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is not Guid tenantId) return new AutomationRunResult(0, 0);

        // Solo las reglas activas cuya combinacion trigger+accion tiene ejecucion real hoy.
        var reglas = await _db.AutomationRules
            .Where(r => r.IsActive
                     && r.Trigger == AutomationTrigger.ChatSinRespuesta
                     && r.Action == AutomationAction.NotificarAdministracion)
            .ToListAsync(ct);
        if (reglas.Count == 0) return new AutomationRunResult(0, 0);

        // Personas de administracion del tenant (destinatarias de la notificacion InApp).
        var adminPersonaIds = await _db.UsuariosTenant.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.Estado == EstadoUsuarioTenant.Activo && u.Rol == "Administrador")
            .Select(u => u.PersonaId)
            .Distinct()
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        int actionsFired = 0;

        foreach (var regla in reglas)
        {
            var cutoff = now.AddMinutes(-regla.ThresholdMinutes);
            var desde = regla.LastRunAt; // para no re-notificar la misma conversacion en cada corrida

            // Conversaciones no archivadas cuyo ULTIMO mensaje es entrante y ya paso el umbral sin respuesta.
            var candidatas = await _db.Conversations.AsNoTracking()
                .Where(c => c.ArchivedAt == null)
                .Select(c => new
                {
                    c.Id,
                    c.ContactPhone,
                    c.ContactName,
                    Last = _db.Messages.Where(m => m.ConversationId == c.Id)
                        .OrderByDescending(m => m.SentAt)
                        .Select(m => new { m.Direction, m.SentAt })
                        .FirstOrDefault()
                })
                .ToListAsync(ct);

            var pendientes = candidatas
                .Where(c => c.Last != null
                         && c.Last.Direction == MessageDirection.Inbound
                         && c.Last.SentAt < cutoff
                         && (desde == null || c.Last.SentAt > desde))
                .Take(200)
                .ToList();

            foreach (var c in pendientes)
            {
                var nombre = string.IsNullOrWhiteSpace(c.ContactName) ? c.ContactPhone : c.ContactName!;
                var cuerpo = string.IsNullOrWhiteSpace(regla.MensajePlantilla)
                    ? $"El contacto {nombre} ({c.ContactPhone}) lleva mas de {regla.ThresholdMinutes} min sin respuesta en WhatsApp."
                    : $"{regla.MensajePlantilla} - Contacto: {nombre} ({c.ContactPhone}).";

                if (adminPersonaIds.Count == 0)
                {
                    // Sin destinatarios no hay a quien notificar, pero contamos que la regla evaluo.
                    continue;
                }

                foreach (var personaId in adminPersonaIds)
                {
                    try
                    {
                        await _notificaciones.EnviarAsync(new EnviarNotificacionRequest(
                            Canal: CanalNotificacion.InApp,
                            Cuerpo: cuerpo,
                            TenantId: tenantId,
                            PersonaDestinatariaId: personaId,
                            Asunto: "Chat de WhatsApp sin respuesta",
                            Prioridad: PrioridadNotificacion.Normal,
                            ModuloOrigenCodigo: "automatizaciones",
                            EntidadOrigenId: c.Id), ct);
                    }
                    catch { /* una notificacion fallida no aborta el resto */ }
                }
                actionsFired++;
            }

            regla.ExecutionCount += pendientes.Count;
            regla.LastRunAt = now;
        }

        await _db.SaveChangesAsync(ct);
        return new AutomationRunResult(reglas.Count, actionsFired);
    }

    public async Task<int> SeedDefaultsAsync(CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is not Guid tenantId) return 0;
        if (await _db.AutomationRules.AsNoTracking().AnyAsync(ct)) return 0;

        var seeds = new List<AutomationRule>
        {
            new() { TenantId = tenantId, Name = "Aviso: chat sin respuesta 30 min", Trigger = AutomationTrigger.ChatSinRespuesta,
                    ThresholdMinutes = 30, Action = AutomationAction.NotificarAdministracion, IsActive = true, SortOrder = 0,
                    MensajePlantilla = "Hay un chat de WhatsApp sin respuesta." },
            new() { TenantId = tenantId, Name = "PQRSD sin gestion 24 h", Trigger = AutomationTrigger.PqrsSinRespuesta,
                    ThresholdMinutes = 1440, Action = AutomationAction.CrearTarea, IsActive = false, SortOrder = 1,
                    TareaTitulo = "Gestionar PQRSD sin respuesta" },
            new() { TenantId = tenantId, Name = "Tarea vencida -> avisar administracion", Trigger = AutomationTrigger.TareaVencida,
                    Action = AutomationAction.NotificarAdministracion, IsActive = false, SortOrder = 2 },
            new() { TenantId = tenantId, Name = "Auto-respuesta nocturna", Trigger = AutomationTrigger.VentanaHoraria,
                    TimeWindowStart = "22:00", TimeWindowEnd = "06:00", Action = AutomationAction.AutoResponderChat, IsActive = false, SortOrder = 3,
                    MensajePlantilla = "Gracias por escribir. Nuestro horario de atencion es de 6:00 a 22:00. Te responderemos pronto." }
        };
        _db.AutomationRules.AddRange(seeds);
        await _db.SaveChangesAsync(ct);
        return seeds.Count;
    }
}
