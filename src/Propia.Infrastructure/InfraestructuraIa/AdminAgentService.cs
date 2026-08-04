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
    private readonly IWhatsAppLineService _lines;
    private readonly IAiAgentLineBindingService _bindings;

    public AdminAgentService(PropiaDbContext db, ITenantContext tenant, IAiAgentService agents,
        IAgentRunLogService logs, IWhatsAppLineService lines, IAiAgentLineBindingService bindings)
    {
        _db = db;
        _tenant = tenant;
        _agents = agents;
        _logs = logs;
        _lines = lines;
        _bindings = bindings;
    }

    /// <summary>Escribe un log inmutable de Super Admin (accion cross-tenant sobre otra copropiedad).</summary>
    private void Audit(Guid actorId, string actorEmail, string accion, string entidad, string justificacion, string? ip)
        => _db.SuperAdminLogs.Add(new SuperAdminLog
        {
            ActorId = actorId,
            ActorEmail = actorEmail,
            Accion = accion,
            EntidadAfectada = entidad,
            Justificacion = justificacion,
            Ip = ip
        });

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

    public async Task<AiAgentDto?> CreateAgentAsync(Guid tenantId, CreateAiAgentRequest request,
        Guid actorId, string actorEmail, string? ip, CancellationToken ct = default)
    {
        await ImpersonarAsync(tenantId);

        // Nucleo reutilizado: crea el agente apagado con Name/Role/Provider/Model/SystemPrompt.
        var created = await _agents.CreateAsync(request, ct);
        if (created is null) { return null; }

        // Campos extra que CreateAsync no cubre (reacciones + orden + encendido). Comparten el mismo
        // DbContext del scope, asi que los cambios trackeados se persisten con el SaveChanges de abajo.
        if (request.ReactionsEnabled)
        {
            await _agents.UpdateAsync(created.Id, new UpdateAiAgentRequest(
                request.Name, request.Role, request.Provider, request.Model, request.SystemPrompt,
                request.ReactionsEnabled, request.ReactionRatioN, request.ReactionRatioM, request.ReactionEmojis), ct);
        }
        if (request.SortOrder is int sortOrder)
        {
            var entity = await _db.AiAgents.FirstOrDefaultAsync(a => a.Id == created.Id, ct);
            if (entity is not null) { entity.SortOrder = sortOrder; }
        }
        if (request.IsActive)
        {
            await _agents.SetActiveAsync(created.Id, true, ct);
        }

        Audit(actorId, actorEmail, "AI_AGENT_ADMIN_CREATE", $"Tenant:{tenantId} Agent:{created.Id}",
            "Creacion de agente via API admin de agentes (Capa 6)", ip);
        await _db.SaveChangesAsync(ct);

        var final = await _agents.GetAsync(created.Id, ct);
        return final?.Agent ?? created;
    }

    public async Task<AiAgentDto?> UpdateAgentAsync(Guid tenantId, Guid agentId, UpdateAiAgentRequest request,
        Guid actorId, string actorEmail, string? ip, CancellationToken ct = default)
    {
        await ImpersonarAsync(tenantId);
        var updated = await _agents.UpdateAsync(agentId, request, ct);
        if (updated is null) { return null; }

        Audit(actorId, actorEmail, "AI_AGENT_ADMIN_UPDATE", $"Tenant:{tenantId} Agent:{agentId}",
            "Edicion de agente via API admin de agentes (Capa 6)", ip);
        await _db.SaveChangesAsync(ct);
        return updated;
    }

    public async Task<AiAgentDetailDto?> SetAgentToolsAsync(Guid tenantId, Guid agentId, IReadOnlyList<string> toolKeys,
        Guid actorId, string actorEmail, string? ip, CancellationToken ct = default)
    {
        await ImpersonarAsync(tenantId);

        // Una sola conexion MCP hoy ("copropiedades"); cada key es el nombre de una de sus tools.
        var selections = (toolKeys ?? Array.Empty<string>())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => new AgentMcpToolSelection(McpConnectionCatalog.Copropiedades, k.Trim()))
            .ToList();

        var ok = await _agents.SetMcpToolsAsync(agentId, selections, ct);
        if (!ok) { return null; }

        Audit(actorId, actorEmail, "AI_AGENT_ADMIN_TOOLS", $"Tenant:{tenantId} Agent:{agentId}",
            $"Set {selections.Count} tools MCP via API admin de agentes (Capa 6)", ip);
        await _db.SaveChangesAsync(ct);
        return await _agents.GetAsync(agentId, ct);
    }

    public async Task<IReadOnlyList<AdminLineDto>> ListLinesAsync(Guid tenantId, CancellationToken ct = default)
    {
        await ImpersonarAsync(tenantId);
        var lines = await _lines.ListAsync(ct);
        var activeBindings = await _db.AiAgentLineBindings.AsNoTracking()
            .Where(b => b.IsConnected)
            .Select(b => new { b.WhatsAppLineId, b.AgentId })
            .ToListAsync(ct);
        return lines.Select(l => new AdminLineDto(
            l.Id, l.InstanceName, l.Provider, l.PhoneNumber, l.Status,
            activeBindings.FirstOrDefault(b => b.WhatsAppLineId == l.Id)?.AgentId)).ToList();
    }

    public async Task<bool> BindLineAsync(Guid tenantId, Guid agentId, Guid whatsAppLineId,
        Guid actorId, string actorEmail, string? ip, CancellationToken ct = default)
    {
        await ImpersonarAsync(tenantId);
        var ok = await _bindings.SetAsync(agentId, new SetAgentLineBindingRequest(whatsAppLineId, Connected: true), ct);
        if (!ok) { return false; }

        Audit(actorId, actorEmail, "AI_AGENT_ADMIN_BIND", $"Tenant:{tenantId} Agent:{agentId} Line:{whatsAppLineId}",
            "Vinculo linea-agente via API admin de agentes (Capa 6)", ip);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UnbindLineAsync(Guid tenantId, Guid agentId, Guid whatsAppLineId,
        Guid actorId, string actorEmail, string? ip, CancellationToken ct = default)
    {
        await ImpersonarAsync(tenantId);
        var ok = await _bindings.SetAsync(agentId, new SetAgentLineBindingRequest(whatsAppLineId, Connected: false), ct);
        if (!ok) { return false; }

        Audit(actorId, actorEmail, "AI_AGENT_ADMIN_UNBIND", $"Tenant:{tenantId} Agent:{agentId} Line:{whatsAppLineId}",
            "Desvinculo linea-agente via API admin de agentes (Capa 6)", ip);
        await _db.SaveChangesAsync(ct);
        return true;
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
