using Propia.Domain.Enums;

namespace Propia.Application.InfraestructuraIa;

/// <summary>Fila de la lista de conversaciones que el agente ha atendido (panel izquierdo de la bitacora).</summary>
public sealed record AgentRunLogConversationDto(
    Guid ConversationId,
    string? ContactName,
    string ContactPhone,
    string? LineLabel,
    DateTimeOffset? LastActivityAt,
    int Events);

/// <summary>Entrada cronologica de la bitacora para una conversacion (panel derecho).</summary>
public sealed record AgentRunLogEntryDto(
    DateTimeOffset OccurredAt,
    AiAgentRunLogKind Kind,
    string Title,
    string? Content,
    string? Response);

/// <summary>Valor capturado en el cache del agente para una conversacion.</summary>
public sealed record AgentCacheItemDto(string FieldKey, string Label, string? Value, string? Source);

/// <summary>Mensaje del chat con el contacto (inbound u outbound), para el historial de la bitacora.</summary>
public sealed record AgentRunLogMessageDto(
    DateTimeOffset SentAt,
    string Direction,   // "inbound" | "outbound"
    string Body,
    string? SentByName);

/// <summary>
/// Lectura de la bitacora del agente IA: lista de conversaciones atendidas (con conteo de eventos y
/// ultima actividad) y la traza cronologica detallada por conversacion (mensaje recibido, prompts al
/// LLM, herramientas, respuesta enviada, errores) + los datos cache capturados y el historial de chat.
/// La ESCRITURA la hace el AgentDispatcher; este servicio es solo lectura + acciones de limpieza.
/// Portado de CUBOT.travels.
/// </summary>
public interface IAgentRunLogService
{
    Task<IReadOnlyList<AgentRunLogConversationDto>> ListConversationsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AgentRunLogEntryDto>> GetConversationLogAsync(Guid conversationId, CancellationToken ct = default);
    Task<IReadOnlyList<AgentCacheItemDto>> GetConversationCacheAsync(Guid conversationId, CancellationToken ct = default);
    Task<IReadOnlyList<AgentRunLogMessageDto>> GetConversationMessagesAsync(Guid conversationId, CancellationToken ct = default);

    /// <summary>Borra TODA la bitacora del tenant + el cache capturado (deja al agente en cero). No toca mensajes.</summary>
    Task<(int Logs, int Cache)> ClearAllAsync(CancellationToken ct = default);

    /// <summary>Olvida una conversacion: borra sus logs + cache (sessionId = conversationId) + mensajes, y limpia LastMessageAt.</summary>
    Task<(int Logs, int Cache, int Messages)> ResetConversationMemoryAsync(Guid conversationId, CancellationToken ct = default);
}
