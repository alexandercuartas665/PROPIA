using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Despachador del agente de IA (auto-respuesta a entrantes). Reutiliza IAiInferenceService.TestChatAsync
/// para reconstruir el prompt completo del agente, y envia la respuesta por IWhatsAppConnectorService.
/// Portado de CUBOT.travels (AgentDispatcher) y SIMPLIFICADO para PROPIA: sin acoplamiento CRM
/// (no crea leads ni pedidos de ventas), y sin reacciones/adjuntos binarios (el connector de PROPIA
/// hoy solo envia texto; se ampliara en una fase posterior).
///
/// Corre SIN JWT de usuario (lo dispara el webhook via la cola). Fija el tenant en contexto con
/// ITenantContext.SetTenant ANTES de consultar: asi el query filter de EF y la RLS de Postgres
/// resuelven al tenant correcto (a diferencia de CUBOT, que usa IgnoreQueryFilters; en PROPIA eso
/// NO evita la RLS de Postgres, hay que fijar el tenant).
/// </summary>
public sealed class AgentDispatcher : IAgentDispatcher
{
    /// <summary>Maximo de turnos que se pasan a la inferencia (contexto). Igual que CUBOT.</summary>
    private const int MaxTurns = 12;

    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAiInferenceService _inference;
    private readonly IWhatsAppConnectorService _connector;
    private readonly IChatBroadcaster? _broadcaster;
    private readonly ILogger<AgentDispatcher> _logger;

    public AgentDispatcher(
        PropiaDbContext db,
        ITenantContext tenant,
        IAiInferenceService inference,
        IWhatsAppConnectorService connector,
        ILogger<AgentDispatcher> logger,
        IChatBroadcaster? broadcaster = null)
    {
        _db = db;
        _tenant = tenant;
        _inference = inference;
        _connector = connector;
        _logger = logger;
        _broadcaster = broadcaster;
    }

    public async Task DispatchAsync(Guid tenantId, Guid conversationId, Guid? whatsAppLineId, string inboundBody, CancellationToken ct = default)
    {
        // Fija el tenant: habilita el query filter de EF + la RLS de Postgres para este tenant.
        _tenant.SetTenant(tenantId);

        if (whatsAppLineId is null)
        {
            _logger.LogInformation("Dispatcher: skip conv {ConvId} (sin whatsAppLineId)", conversationId);
            return;
        }

        // 1) Binding activo linea -> agente. Sin binding, el dispatcher no hace nada.
        var binding = await _db.AiAgentLineBindings.AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.WhatsAppLineId == whatsAppLineId && b.IsConnected)
            .Select(b => new { b.AgentId, b.AutoConfirm })
            .FirstOrDefaultAsync(ct);
        if (binding is null)
        {
            _logger.LogInformation("Dispatcher: skip conv {ConvId} linea {LineId} (sin agente vinculado)", conversationId, whatsAppLineId);
            return;
        }

        // 2) Agente activo (en produccion). Si esta apagado, silencio total (coherente con la UI).
        var agentActive = await _db.AiAgents.AsNoTracking()
            .Where(a => a.Id == binding.AgentId).Select(a => a.IsActive)
            .FirstOrDefaultAsync(ct);
        if (!agentActive)
        {
            _logger.LogInformation("Dispatcher: skip conv {ConvId} (agente {AgentId} apagado)", conversationId, binding.AgentId);
            return;
        }

        // 3) Conversacion: telefono, reinicio de contexto y toma de control humana.
        var conv = await _db.Conversations.AsNoTracking()
            .Where(c => c.Id == conversationId)
            .Select(c => new { c.ContactPhone, c.AgentContextResetAt, c.IaPausadaAt })
            .FirstOrDefaultAsync(ct);
        if (conv is null || string.IsNullOrWhiteSpace(conv.ContactPhone))
        {
            _logger.LogInformation("Dispatcher: skip conv {ConvId} (sin telefono)", conversationId);
            return;
        }

        // Toma de control humana: si un operador esta atendiendo (IaPausadaAt seteado), el bot calla.
        // Se reactiva desde la bandeja con "reiniciar contexto" (limpia IaPausadaAt).
        if (conv.IaPausadaAt is not null)
        {
            _logger.LogInformation("Dispatcher: skip conv {ConvId} (IA pausada, atiende un humano)", conversationId);
            await LogRunAsync(tenantId, conversationId, binding.AgentId, AiAgentRunLogKind.Info,
                "Skip: atencion humana",
                "Un operador esta atendiendo esta conversacion (IA en pausa). El bot no interfiere. " +
                "Para reactivar al bot: usar 'Reiniciar contexto IA' en la bandeja.",
                null, ct);
            return;
        }

        // Bitacora: el mensaje recibido abre la ventana de la conversacion en /ia/bitacora.
        await LogRunAsync(tenantId, conversationId, binding.AgentId, AiAgentRunLogKind.Inbound,
            "Mensaje recibido", inboundBody, null, ct);

        // 4) Reconstruye contexto: ultimos N turnos despues del reinicio de contexto, excluyendo
        // mensajes fallidos del bot y placeholders. Inbound -> user, Outbound -> model.
        var resetAt = conv.AgentContextResetAt;
        var recent = await _db.Messages.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.ConversationId == conversationId
                     && !(m.SentByName != null && m.SentByName.StartsWith("IA (ai_failed"))
                     && m.Body != "(solo adjuntos)"
                     && (resetAt == null || m.SentAt > resetAt))
            .OrderByDescending(m => m.SentAt)
            .Take(MaxTurns)
            .OrderBy(m => m.SentAt)
            .Select(m => new { m.Direction, m.Body })
            .ToListAsync(ct);

        // Colapsa turnos consecutivos del mismo rol (los LLM esperan alternancia user/model; varios
        // entrantes seguidos sin respuesta del bot rompen la alternancia y dan respuesta vacia).
        var turns = new List<AiChatTurn>();
        foreach (var m in recent)
        {
            var role = m.Direction == MessageDirection.Inbound ? "user" : "model";
            if (turns.Count > 0 && turns[^1].Role == role)
            {
                turns[^1] = turns[^1] with { Text = $"{turns[^1].Text}\n{m.Body}" };
            }
            else { turns.Add(new AiChatTurn(role, m.Body)); }
        }
        if (turns.Count == 0) { turns.Add(new AiChatTurn("user", inboundBody)); }

        var totalChars = turns.Sum(t => t.Text?.Length ?? 0);
        await LogRunAsync(tenantId, conversationId, binding.AgentId, AiAgentRunLogKind.Info,
            $"Contexto enviado al LLM ({turns.Count} turnos, {totalChars} chars)",
            string.Join('\n', turns.Select((t, i) => $"[{i + 1}] {t.Role}: {Preview(t.Text, 120)}")),
            null, ct);

        // 5) Inferencia (mismo motor que el chat de prueba de /ia/agentes). sessionId = conversationId
        // aisla el cache por conversacion. Sin bearerToken el motor corre el camino simple (sin MCP):
        // el dispatcher corre sin JWT de usuario, asi que por ahora el agente responde de forma
        // conversacional (prompt + recursos + cache), no ejecuta tools MCP (mejora futura).
        _logger.LogInformation("Dispatcher: inferencia agente {AgentId} conv {ConvId} ({TurnCount} turnos)",
            binding.AgentId, conversationId, turns.Count);
        var result = await _inference.TestChatAsync(binding.AgentId, turns,
            systemPromptOverride: null, bearerToken: null, contactPhone: conv.ContactPhone,
            conversationId: conversationId, ct: ct);

        // Bitacora: prompts que ejecuto la inferencia (prompt principal + extractor de cache).
        if (result.DebugPrompts is not null)
        {
            foreach (var d in result.DebugPrompts)
            {
                await LogRunAsync(tenantId, conversationId, binding.AgentId,
                    AiAgentRunLogKind.Prompt, d.Title, d.Content, d.Response, ct);
            }
        }

        if (!result.Ok)
        {
            _logger.LogWarning("Dispatcher: inferencia FALLO agente {AgentId} conv {ConvId}: {Error}",
                binding.AgentId, conversationId, result.Error ?? "(sin detalle)");
            await LogRunAsync(tenantId, conversationId, binding.AgentId, AiAgentRunLogKind.Error,
                "Inferencia fallo", result.Error, null, ct);
            return;
        }

        // Texto a enviar: el texto del LLM + el detalle de los recursos de tipo Texto que el agente
        // decidio entregar ([[enviar: X]]). Los adjuntos binarios (imagen/pdf/audio) no se envian en
        // esta fase (el connector de PROPIA solo hace texto); quedan anotados en la bitacora.
        var textToSend = (result.Text ?? string.Empty).Trim();
        var skippedBinary = 0;
        if (result.Attachments is { Count: > 0 })
        {
            foreach (var a in result.Attachments)
            {
                if (a.ResourceType == AgentResourceType.Text && !string.IsNullOrWhiteSpace(a.Detail))
                {
                    textToSend = string.IsNullOrWhiteSpace(textToSend) ? a.Detail!.Trim() : $"{textToSend}\n\n{a.Detail!.Trim()}";
                }
                else { skippedBinary++; }
            }
        }

        if (string.IsNullOrWhiteSpace(textToSend))
        {
            await LogRunAsync(tenantId, conversationId, binding.AgentId, AiAgentRunLogKind.Error,
                "Texto vacio del LLM",
                skippedBinary > 0
                    ? $"El agente solo intento enviar {skippedBinary} adjunto(s) binario(s), que aun no se envian en esta fase."
                    : "El modelo devolvio una respuesta vacia (contexto largo, filtro de seguridad o prompt mal armado).",
                result.Error, ct);
            return;
        }

        // 6) Modo sugerencia (AutoConfirm = false): guarda como borrador y NO envia.
        if (!binding.AutoConfirm)
        {
            await PersistOutboundAsync(tenantId, conversationId, textToSend, "ai_draft", ct);
            await LogRunAsync(tenantId, conversationId, binding.AgentId, AiAgentRunLogKind.Info,
                "Respuesta en borrador (sin enviar)",
                "El vinculo linea-agente esta en modo sugerencia (AutoConfirm=false): la respuesta queda como borrador para que un operador la revise.",
                textToSend, ct);
            return;
        }

        // 7) Envio real por la linea (router Evolution/Cloud interno del connector).
        LineSendResult send;
        try { send = await _connector.SendTestAsync(whatsAppLineId.Value, conv.ContactPhone, textToSend, ct); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dispatcher: envio fallo conv {ConvId}", conversationId);
            send = new LineSendResult(false, ex.Message);
        }

        await PersistOutboundAsync(tenantId, conversationId, textToSend, send.Ok ? "ai_sent" : "ai_failed", ct);

        await LogRunAsync(tenantId, conversationId, binding.AgentId,
            send.Ok ? AiAgentRunLogKind.Reply : AiAgentRunLogKind.Error,
            send.Ok ? "Respuesta enviada" : "Envio fallo",
            textToSend,
            send.Ok
                ? (skippedBinary > 0 ? $"OK (se omitieron {skippedBinary} adjunto(s) binario(s) en esta fase)" : "OK")
                : send.Error,
            ct);
    }

    /// <summary>Persiste el saliente del agente y lo difunde a la bandeja en vivo (SignalR).</summary>
    private async Task PersistOutboundAsync(Guid tenantId, Guid conversationId, string body, string sourceTag, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var msg = new Message
        {
            TenantId = tenantId,
            ConversationId = conversationId,
            Direction = MessageDirection.Outbound,
            ExternalId = null, // el connector de PROPIA no devuelve MessageId todavia
            Body = body,
            MessageType = "text",
            SentAt = now,
            MediaType = MessageMediaType.None,
            SentByName = $"IA ({sourceTag})"
        };
        _db.Messages.Add(msg);

        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId, ct);
        if (conv is not null) { conv.LastMessageAt = now; }

        await _db.SaveChangesAsync(ct);

        if (_broadcaster is not null)
        {
            try
            {
                var dto = new MensajeDto(msg.Id, msg.ConversationId, msg.Direction.ToString(),
                    msg.Body, msg.MessageType, msg.SentAt, msg.SentByName,
                    msg.MediaType.ToString(), msg.MediaUrl, msg.MediaMimeType, msg.ExternalId);
                await _broadcaster.NotifyMessageAddedAsync(tenantId, conversationId, dto, ct);
            }
            catch { /* el despacho no debe fallar si SignalR esta caido */ }
        }
    }

    /// <summary>Escribe una entrada en la bitacora del agente. Best-effort: si falla no rompe el flujo.</summary>
    private async Task LogRunAsync(Guid tenantId, Guid conversationId, Guid agentId,
        AiAgentRunLogKind kind, string title, string? content, string? response, CancellationToken ct)
    {
        try
        {
            _db.AiAgentRunLogs.Add(new AiAgentRunLog
            {
                TenantId = tenantId,
                ConversationId = conversationId,
                AgentId = agentId,
                Kind = kind,
                OccurredAt = DateTimeOffset.UtcNow,
                Title = title.Length > 200 ? title[..200] : title,
                Content = content,
                Response = response
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo escribir bitacora del agente conv {ConvId}", conversationId);
        }
    }

    private static string Preview(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) { return string.Empty; }
        var clean = s.Replace('\r', ' ').Replace('\n', ' ');
        return clean.Length <= max ? clean : clean[..max] + "...";
    }
}
