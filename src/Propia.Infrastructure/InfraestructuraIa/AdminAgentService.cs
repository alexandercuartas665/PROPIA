using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Administracion cross-tenant de agentes para el Super Admin (Capa 6). Reutiliza IAiAgentService +
/// IAgentRunLogService fijando el tenant recibido por ruta. CLAVE (RLS de PROPIA): el token de Super
/// Admin no trae tenant, asi que el TenantMiddleware dejo app.tenant_id en '' (fail-closed). Aqui se
/// fija el tenant en AMBAS capas -ITenantContext (query filter de EF) y app.tenant_id (RLS de
/// Postgres)- igual que TenantMiddleware. Sin la segunda, la RLS bloquearia las lecturas/escrituras.
/// Portado de CUBOT.travels (que solo usaba IgnoreQueryFilters porque no tiene RLS).
/// </summary>
public sealed class AdminAgentService : IAdminAgentService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAiAgentService _agents;
    private readonly IAgentRunLogService _logs;

    public AdminAgentService(PropiaDbContext db, ITenantContext tenant, IAiAgentService agents, IAgentRunLogService logs)
    {
        _db = db;
        _tenant = tenant;
        _agents = agents;
        _logs = logs;
    }

    /// <summary>
    /// Fija el tenant recibido por ruta en el query filter de EF (ITenantContext) Y en la RLS de
    /// Postgres (app.tenant_id en la conexion). Los servicios reutilizados comparten este mismo
    /// scope (DbContext + ITenantContext), asi que operan sobre este tenant.
    /// </summary>
    private async Task ImpersonarAsync(Guid tenantId)
    {
        _tenant.SetTenant(tenantId);
        await _db.Database.ExecuteSqlRawAsync("SELECT set_config('app.tenant_id', {0}, false)", tenantId.ToString());
    }

    public async Task<IReadOnlyList<AiAgentDto>> ListAgentsAsync(Guid tenantId, CancellationToken ct = default)
    {
        await ImpersonarAsync(tenantId);
        return await _agents.ListAsync(ct);
    }

    public async Task<AiAgentDetailDto?> GetAgentAsync(Guid tenantId, Guid agentId, CancellationToken ct = default)
    {
        await ImpersonarAsync(tenantId);
        return await _agents.GetAsync(agentId, ct);
    }

    public async Task<AiAgentDto?> UpdateAgentAsync(Guid tenantId, Guid agentId, UpdateAiAgentRequest request,
        Guid actorId, string actorEmail, string? ip, CancellationToken ct = default)
    {
        await ImpersonarAsync(tenantId);
        var updated = await _agents.UpdateAsync(agentId, request, ct);
        if (updated is null) { return null; }

        // Auditoria: accion cross-tenant del Super Admin sobre datos de otra copropiedad (log inmutable).
        _db.SuperAdminLogs.Add(new SuperAdminLog
        {
            ActorId = actorId,
            ActorEmail = actorEmail,
            Accion = "AI_AGENT_ADMIN_UPDATE",
            EntidadAfectada = $"Tenant:{tenantId} Agent:{agentId}",
            Justificacion = "Edicion de agente via API admin de agentes (Capa 6)",
            Ip = ip
        });
        await _db.SaveChangesAsync(ct);
        return updated;
    }

    public async Task<IReadOnlyList<AgentRunLogConversationDto>> ListLogConversationsAsync(Guid tenantId, CancellationToken ct = default)
    {
        await ImpersonarAsync(tenantId);
        return await _logs.ListConversationsAsync(ct);
    }

    public async Task<IReadOnlyList<AgentRunLogEntryDto>> GetConversationLogAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default)
    {
        await ImpersonarAsync(tenantId);
        return await _logs.GetConversationLogAsync(conversationId, ct);
    }
}
