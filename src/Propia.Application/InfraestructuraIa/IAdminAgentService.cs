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

    /// <summary>Edita la config base + reacciones del agente. Escribe un log inmutable de Super Admin.</summary>
    Task<AiAgentDto?> UpdateAgentAsync(Guid tenantId, Guid agentId, UpdateAiAgentRequest request,
        Guid actorId, string actorEmail, string? ip, CancellationToken ct = default);

    Task<IReadOnlyList<AgentRunLogConversationDto>> ListLogConversationsAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<AgentRunLogEntryDto>> GetConversationLogAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
}
