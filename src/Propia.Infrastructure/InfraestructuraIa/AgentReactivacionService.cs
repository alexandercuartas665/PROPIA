using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Reactivacion/seguimiento automatico: tras N horas de inactividad del contacto, el agente envia el
/// siguiente paso de una secuencia configurada. Portado de ECOREX (AgentReactivacionService) y adaptado
/// a PROPIA: el barrido corre por TENANT (lo dispara un job que fija el tenant). v1 solo texto libre.
/// La regla de ventana de 24h de Meta solo se aplica a lineas Cloud/YCloud; Evolution no la tiene.
/// </summary>
public sealed class AgentReactivacionService : IAgentReactivacionService
{
    private readonly PropiaDbContext _db;
    private readonly IWhatsAppConnectorService _connector;
    private readonly IListaNegraService _listaNegra;
    private readonly ILogger<AgentReactivacionService> _logger;

    /// <summary>Ventana de servicio de WhatsApp (Meta): fuera de 24h del ultimo entrante Cloud/YCloud no se envia texto libre.</summary>
    private const double ServiceWindowHoras = 24;

    /// <summary>Tope de conversaciones a evaluar por agente y corrida (acota el costo del barrido).</summary>
    private const int MaxConversationsPerAgent = 500;

    public AgentReactivacionService(PropiaDbContext db, IWhatsAppConnectorService connector,
        IListaNegraService listaNegra, ILogger<AgentReactivacionService> logger)
    {
        _db = db;
        _connector = connector;
        _listaNegra = listaNegra;
        _logger = logger;
    }

    public async Task<AgentReactivacionConfig> GetConfigAsync(Guid agentId, CancellationToken ct = default)
    {
        var json = await _db.AiAgents.AsNoTracking()
            .Where(a => a.Id == agentId)
            .Select(a => a.ReactivacionJson)
            .FirstOrDefaultAsync(ct);
        return AgentReactivacionConfig.Parse(json);
    }

    public async Task<bool> SaveConfigAsync(Guid agentId, AgentReactivacionConfig config, CancellationToken ct = default)
    {
        var agent = await _db.AiAgents.FirstOrDefaultAsync(a => a.Id == agentId, ct);
        if (agent is null) { return false; }
        // Config vacia (deshabilitada o sin pasos) -> null: mantiene limpio el campo.
        agent.ReactivacionJson = config.IsNoop ? null : config.Serialize();
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> RunTenantAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var sentCount = 0;

        // Agentes activos con reactivacion configurada (el query filter ya acota al tenant fijado).
        var agents = await _db.AiAgents.AsNoTracking()
            .Where(a => a.IsActive && a.ReactivacionJson != null)
            .Select(a => new { a.Id, a.Name, a.ReactivacionJson })
            .ToListAsync(ct);
        if (agents.Count == 0) { return 0; }

        foreach (var agent in agents)
        {
            var config = AgentReactivacionConfig.Parse(agent.ReactivacionJson);
            if (config.IsNoop) { continue; }
            var pasos = config.PasosActivos;

            // Lineas conectadas a este agente + su proveedor (para la regla de 24h).
            var lineas = await _db.AiAgentLineBindings.AsNoTracking()
                .Where(b => b.AgentId == agent.Id && b.IsConnected)
                .Join(_db.WhatsAppLines.AsNoTracking(), b => b.WhatsAppLineId, l => l.Id, (b, l) => new { l.Id, l.Provider })
                .ToListAsync(ct);
            if (lineas.Count == 0) { continue; }
            var lineIds = lineas.Select(l => l.Id).ToList();
            var provById = lineas.ToDictionary(l => l.Id, l => l.Provider);

            // Conversaciones activas de esas lineas (TRACKED: actualizamos su estado de reactivacion).
            var convs = await _db.Conversations
                .Where(c => c.ArchivedAt == null && c.WhatsAppLineId != null && lineIds.Contains(c.WhatsAppLineId.Value))
                .OrderBy(c => c.LastMessageAt)
                .Take(MaxConversationsPerAgent)
                .ToListAsync(ct);

            foreach (var conv in convs)
            {
                if (ct.IsCancellationRequested) { break; }
                if (conv.WhatsAppLineId is not Guid lineId) { continue; }
                // Atencion humana en curso: no interferir.
                if (conv.IaPausadaAt is not null) { continue; }
                // Lista negra (opt-out): nunca reactivar un numero bloqueado.
                if (await _listaNegra.EstaBloqueadoAsync(conv.ContactPhone, ct)) { continue; }

                // Ultimo entrante del contacto (posterior al reinicio de contexto, si lo hubo).
                var afterReset = conv.AgentContextResetAt;
                var lastInboundAt = await _db.Messages.AsNoTracking()
                    .Where(m => m.ConversationId == conv.Id && m.Direction == MessageDirection.Inbound
                        && (afterReset == null || m.SentAt > afterReset))
                    .Select(m => (DateTimeOffset?)m.SentAt)
                    .MaxAsync(ct);
                if (lastInboundAt is null) { continue; }   // sin entrante que revivir

                // Reinicio: si el contacto respondio DESPUES de nuestro ultimo seguimiento, la secuencia vuelve a 0.
                var paso = conv.ReactivacionUltimoPaso;
                if (conv.ReactivacionUltimoEnvioAt is DateTimeOffset lastSent && lastInboundAt > lastSent)
                {
                    paso = 0;
                    conv.ReactivacionUltimoPaso = 0;
                    conv.ReactivacionUltimoEnvioAt = null;
                }
                if (paso >= pasos.Count) { continue; }   // ya se enviaron todos los pasos; esperando respuesta

                var step = pasos[paso];
                var horas = (now - lastInboundAt.Value).TotalHours;
                if (horas < step.OffsetHoras) { continue; }   // aun no se cumple el offset

                // Regla de Meta (solo Cloud/YCloud): fuera de 24h no se puede texto libre -> se espera.
                var esEvolution = provById.TryGetValue(lineId, out var prov) && prov == WhatsAppProvider.Evolution;
                if (!esEvolution && horas > ServiceWindowHoras)
                {
                    LogOnly(conv, agent.Id, AiAgentRunLogKind.Info, $"Reactivacion paso {paso + 1} en espera",
                        "La ventana de 24h de Meta esta cerrada y v1 no envia plantillas; se espera a que el contacto escriba.");
                    continue;
                }

                var phone = Digits(conv.ContactPhone);
                if (phone.Length == 0) { continue; }

                var res = await _connector.SendTestAsync(lineId, phone, step.MensajeTexto!.Trim(), ct);
                if (!res.Ok)
                {
                    LogOnly(conv, agent.Id, AiAgentRunLogKind.Error, $"Reactivacion paso {paso + 1} fallo", res.Error);
                    continue;   // no avanza: se reintenta la proxima corrida
                }
                PersistSent(conv, agent.Id, paso, now, step.MensajeTexto!.Trim());
                sentCount++;
                // Un solo envio por conversacion por corrida.
            }
        }

        await _db.SaveChangesAsync(ct);
        return sentCount;
    }

    private void PersistSent(Conversation conv, Guid agentId, int pasoIndex, DateTimeOffset now, string body)
    {
        _db.Messages.Add(new Message
        {
            TenantId = conv.TenantId,
            ConversationId = conv.Id,
            Direction = MessageDirection.Outbound,
            ExternalId = null,
            Body = body,
            MessageType = "text",
            SentByName = "Agente IA (reactivacion)",
            SentAt = now
        });
        conv.LastMessageAt = now;
        conv.ReactivacionUltimoPaso = pasoIndex + 1;
        conv.ReactivacionUltimoEnvioAt = now;
        _db.AiAgentRunLogs.Add(new AiAgentRunLog
        {
            TenantId = conv.TenantId,
            ConversationId = conv.Id,
            AgentId = agentId,
            OccurredAt = now,
            Kind = AiAgentRunLogKind.Reactivacion,
            Title = $"Reactivacion paso {pasoIndex + 1} enviado",
            Content = body
        });
    }

    private void LogOnly(Conversation conv, Guid agentId, AiAgentRunLogKind kind, string title, string? content)
    {
        _db.AiAgentRunLogs.Add(new AiAgentRunLog
        {
            TenantId = conv.TenantId,
            ConversationId = conv.Id,
            AgentId = agentId,
            OccurredAt = DateTimeOffset.UtcNow,
            Kind = kind,
            Title = title,
            Content = content
        });
    }

    private static string Digits(string? s) => string.IsNullOrEmpty(s) ? "" : new string(s.Where(char.IsDigit).ToArray());
}
