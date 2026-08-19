namespace Propia.Application.InfraestructuraIa;

/// <summary>
/// Administracion de agentes de IA CROSS-TENANT para el Super Admin de plataforma (Capa 6 - API
/// Admin de Agentes). Un cliente de maquina (ej. una instancia de Claude) con un JWT de Super Admin
/// (claim is_super_admin=true) puede leer/editar los agentes de CUALQUIER copropiedad y leer su
/// bitacora. Reutiliza IAiAgentService + IAgentRunLogService, pero fija el tenant recibido por
/// parametro (el token de Super Admin NO trae tenant) tanto en el query filter de EF como en la RLS
/// de Postgres (app.tenant_id). Portado de CUBOT.travels, adaptado a la RLS de PROPIA.
/// </summary>
public interface IAdminAgentService
{
    Task<IReadOnlyList<AiAgentDto>> ListAgentsAsync(Guid tenantId, CancellationToken ct = default);
    Task<AiAgentDetailDto?> GetAgentAsync(Guid tenantId, Guid agentId, CancellationToken ct = default);

    /// <summary>Crea un agente en la copropiedad. Aplica IsActive/SortOrder/Reactions. Audita AI_AGENT_ADMIN_CREATE.</summary>
    Task<AiAgentDto?> CreateAgentAsync(Guid tenantId, CreateAiAgentRequest request,
        Guid actorId, string actorEmail, string? ip, CancellationToken ct = default);

    /// <summary>Edita la config base + reacciones del agente. Escribe un log inmutable de Super Admin.</summary>
    Task<AiAgentDto?> UpdateAgentAsync(Guid tenantId, Guid agentId, UpdateAiAgentRequest request,
        Guid actorId, string actorEmail, string? ip, CancellationToken ct = default);

    /// <summary>Fija la seleccion de tools MCP del agente (conexion "copropiedades"). Devuelve null si el
    /// agente no existe. Audita AI_AGENT_ADMIN_TOOLS.</summary>
    Task<AiAgentDetailDto?> SetAgentToolsAsync(Guid tenantId, Guid agentId, IReadOnlyList<string> toolKeys,
        Guid actorId, string actorEmail, string? ip, CancellationToken ct = default);

    /// <summary>Lineas WhatsApp de la copropiedad (para descubrir por API el numero/linea disponible).</summary>
    Task<IReadOnlyList<AdminLineDto>> ListLinesAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Vincula una linea al agente (la atiende). false si la linea no existe, o esta tomada por
    /// otro agente y reassign=false. Con reassign=true desvincula primero al agente que la atiende (audita
    /// ese UNBIND) y luego vincula. Audita AI_AGENT_ADMIN_BIND.</summary>
    Task<bool> BindLineAsync(Guid tenantId, Guid agentId, Guid whatsAppLineId, bool reassign,
        Guid actorId, string actorEmail, string? ip, CancellationToken ct = default);

    /// <summary>Desvincula una linea del agente. false si ese agente no la atiende. Audita AI_AGENT_ADMIN_UNBIND.</summary>
    Task<bool> UnbindLineAsync(Guid tenantId, Guid agentId, Guid whatsAppLineId,
        Guid actorId, string actorEmail, string? ip, CancellationToken ct = default);

    Task<IReadOnlyList<AgentRunLogConversationDto>> ListLogConversationsAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<AgentRunLogEntryDto>> GetConversationLogAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
}
