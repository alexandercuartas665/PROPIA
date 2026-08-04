namespace Propia.Application.Common;

/// <summary>
/// Contexto fuera-de-banda de la llamada del agente a una tool MCP: el telefono REAL del contacto y
/// la conversacion desde la que escribe. Lo llena un middleware (AgentCallContextMiddleware) leyendo
/// headers server-set (X-Contact-Phone / X-Conversation-Id) que pone el McpGateway a partir del
/// contactPhone de la conversacion; NUNCA es un argumento del LLM (anti-suplantacion: que el usuario
/// "diga" otro numero no sirve, se usa el numero real desde el que escribe). Scoped por request:
/// cada POST a /mcp corre en su propio scope DI y una tool lo inyecta igual que ITenantContext.
/// </summary>
public interface IAgentCallContext
{
    /// <summary>Telefono real del contacto de la conversacion (tal cual llego; normalizar al usar).</summary>
    string? ContactPhone { get; }

    /// <summary>Id de la conversacion en curso, si aplica.</summary>
    Guid? ConversationId { get; }

    /// <summary>Fija el contexto (lo llama el middleware desde los headers server-set).</summary>
    void Set(string? contactPhone, Guid? conversationId);
}
