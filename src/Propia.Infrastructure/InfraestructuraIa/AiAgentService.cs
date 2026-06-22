using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Gestion de agentes de IA de la copropiedad activa (tenant-scoped, RLS): proveedor, prompt base,
/// encendido, recursos y prompts enrutados. Portado de CUBOT.travels (sin acoplamiento CRM).
/// </summary>
public sealed class AiAgentService : IAiAgentService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;

    public AiAgentService(PropiaDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<AiAgentDto>> ListAsync(CancellationToken ct = default)
    {
        var agents = await _db.AiAgents.AsNoTracking()
            .OrderBy(a => a.SortOrder).ThenBy(a => a.Name)
            .ToListAsync(ct);
        var counts = await _db.AiAgentResources.AsNoTracking()
            .GroupBy(r => r.AgentId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return agents.Select(a => Map(a, counts.TryGetValue(a.Id, out var c) ? c : 0)).ToList();
    }

    public async Task<AiAgentDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var agent = await _db.AiAgents.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
        if (agent is null) { return null; }
        var resources = await _db.AiAgentResources.AsNoTracking()
            .Where(r => r.AgentId == id).OrderBy(r => r.SortOrder)
            .Select(r => MapResource(r)).ToListAsync(ct);
        var prompts = await _db.AiAgentPrompts.AsNoTracking()
            .Where(p => p.AgentId == id).OrderBy(p => p.SortOrder)
            .Select(p => MapPrompt(p)).ToListAsync(ct);
        return new AiAgentDetailDto(Map(agent, resources.Count), resources, prompts);
    }

    public async Task<AiAgentDto?> CreateAsync(CreateAiAgentRequest request, CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is not Guid tenantId) { return null; }
        var nextOrder = (await _db.AiAgents.Select(a => (int?)a.SortOrder).MaxAsync(ct) ?? -1) + 1;
        var agent = new AiAgent
        {
            TenantId = tenantId,
            Name = (request.Name ?? "Agente").Trim(),
            Role = request.Role?.Trim(),
            Provider = request.Provider,
            Model = string.IsNullOrWhiteSpace(request.Model) ? null : request.Model.Trim(),
            SystemPrompt = request.SystemPrompt ?? "",
            IsActive = false,
            SortOrder = nextOrder
        };
        _db.AiAgents.Add(agent);
        await _db.SaveChangesAsync(ct);
        return Map(agent, 0);
    }

    public async Task<AiAgentDto?> UpdateAsync(Guid id, UpdateAiAgentRequest request, CancellationToken ct = default)
    {
        var agent = await _db.AiAgents.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (agent is null) { return null; }
        agent.Name = (request.Name ?? agent.Name).Trim();
        agent.Role = request.Role?.Trim();
        agent.Provider = request.Provider;
        agent.Model = string.IsNullOrWhiteSpace(request.Model) ? null : request.Model.Trim();
        agent.SystemPrompt = request.SystemPrompt ?? "";

        // Reacciones automaticas (emoji). Saneo: M>=1 y 0<=N<=M; emojis vacios -> null. Portado de CUBOT.travels.
        agent.ReactionsEnabled = request.ReactionsEnabled;
        agent.ReactionRatioM = request.ReactionRatioM < 1 ? 1 : request.ReactionRatioM;
        agent.ReactionRatioN = request.ReactionRatioN < 0 ? 0
            : (request.ReactionRatioN > agent.ReactionRatioM ? agent.ReactionRatioM : request.ReactionRatioN);
        agent.ReactionEmojis = string.IsNullOrWhiteSpace(request.ReactionEmojis) ? null : request.ReactionEmojis.Trim();

        // Snapshot del prompt al guardar (max 5 versiones). Portado de CUBOT.travels.
        var prompts = await _db.AiAgentPrompts.AsNoTracking()
            .Where(p => p.AgentId == id).OrderBy(p => p.SortOrder)
            .Select(p => new AgentPromptSnapshotDto(p.Name, p.Rule, p.Body, p.SortOrder))
            .ToListAsync(ct);
        agent.PromptHistoryJson = PushPromptVersion(agent.PromptHistoryJson,
            new StoredPromptVersion(DateTimeOffset.UtcNow, agent.SystemPrompt, prompts));

        await _db.SaveChangesAsync(ct);
        var count = await _db.AiAgentResources.CountAsync(r => r.AgentId == id, ct);
        return Map(agent, count);
    }

    public async Task<AiAgentDto?> SetActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        var agent = await _db.AiAgents.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (agent is null) { return null; }
        agent.IsActive = active;
        await _db.SaveChangesAsync(ct);
        var count = await _db.AiAgentResources.CountAsync(r => r.AgentId == id, ct);
        return Map(agent, count);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var agent = await _db.AiAgents.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (agent is null) { return false; }
        _db.AiAgents.Remove(agent);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<AiAgentResourceDto?> AddResourceAsync(CreateAgentResourceRequest request, CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is not Guid tenantId) { return null; }
        var agent = await _db.AiAgents.FirstOrDefaultAsync(a => a.Id == request.AgentId, ct);
        if (agent is null) { return null; }
        var nextOrder = (await _db.AiAgentResources.Where(r => r.AgentId == request.AgentId).Select(r => (int?)r.SortOrder).MaxAsync(ct) ?? -1) + 1;
        var res = new AiAgentResource
        {
            TenantId = tenantId,
            AgentId = request.AgentId,
            Name = (request.Name ?? "Recurso").Trim(),
            ResourceType = request.ResourceType,
            Detail = request.Detail,
            FileUrl = request.FileUrl,
            FileName = request.FileName,
            SortOrder = nextOrder
        };
        _db.AiAgentResources.Add(res);
        await _db.SaveChangesAsync(ct);
        return MapResource(res);
    }

    public async Task<AiAgentResourceDto?> UpdateResourceAsync(Guid id, UpdateAgentResourceRequest request, CancellationToken ct = default)
    {
        var res = await _db.AiAgentResources.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (res is null) { return null; }
        res.Name = (request.Name ?? res.Name).Trim();
        res.ResourceType = request.ResourceType;
        res.Detail = request.Detail;
        res.FileUrl = request.FileUrl;
        res.FileName = request.FileName;
        await _db.SaveChangesAsync(ct);
        return MapResource(res);
    }

    public async Task<bool> DeleteResourceAsync(Guid id, CancellationToken ct = default)
    {
        var res = await _db.AiAgentResources.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (res is null) { return false; }
        _db.AiAgentResources.Remove(res);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<AiAgentPromptDto?> AddPromptAsync(CreateAgentPromptRequest request, CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is not Guid tenantId) { return null; }
        var agent = await _db.AiAgents.FirstOrDefaultAsync(a => a.Id == request.AgentId, ct);
        if (agent is null) { return null; }
        var nextOrder = (await _db.AiAgentPrompts.Where(p => p.AgentId == request.AgentId).Select(p => (int?)p.SortOrder).MaxAsync(ct) ?? -1) + 1;
        var prompt = new AiAgentPrompt
        {
            TenantId = tenantId,
            AgentId = request.AgentId,
            Name = (request.Name ?? "Prompt").Trim(),
            Rule = string.IsNullOrWhiteSpace(request.Rule) ? null : request.Rule.Trim(),
            Body = request.Body ?? "",
            SortOrder = nextOrder
        };
        _db.AiAgentPrompts.Add(prompt);
        await _db.SaveChangesAsync(ct);
        return MapPrompt(prompt);
    }

    public async Task<AiAgentPromptDto?> UpdatePromptAsync(Guid id, UpdateAgentPromptRequest request, CancellationToken ct = default)
    {
        var prompt = await _db.AiAgentPrompts.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (prompt is null) { return null; }
        prompt.Name = (request.Name ?? prompt.Name).Trim();
        prompt.Rule = string.IsNullOrWhiteSpace(request.Rule) ? null : request.Rule.Trim();
        prompt.Body = request.Body ?? "";
        await _db.SaveChangesAsync(ct);
        return MapPrompt(prompt);
    }

    public async Task<bool> DeletePromptAsync(Guid id, CancellationToken ct = default)
    {
        var prompt = await _db.AiAgentPrompts.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (prompt is null) { return false; }
        _db.AiAgentPrompts.Remove(prompt);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<AgentMcpToolSelection>> GetMcpToolsAsync(Guid agentId, CancellationToken ct = default)
    {
        return await _db.AiAgentMcpTools.AsNoTracking()
            .Where(t => t.AgentId == agentId)
            .OrderBy(t => t.ConnectionCode).ThenBy(t => t.ToolName)
            .Select(t => new AgentMcpToolSelection(t.ConnectionCode, t.ToolName))
            .ToListAsync(ct);
    }

    public async Task<bool> SetMcpToolsAsync(Guid agentId, IReadOnlyList<AgentMcpToolSelection> tools, CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is not Guid tenantId) { return false; }
        var agent = await _db.AiAgents.FirstOrDefaultAsync(a => a.Id == agentId, ct);
        if (agent is null) { return false; }

        // Reemplazo total: borro las actuales y vuelvo a insertar la seleccion (deduplicada
        // y validada contra el catalogo de conexiones conocidas).
        var actuales = await _db.AiAgentMcpTools.Where(t => t.AgentId == agentId).ToListAsync(ct);
        _db.AiAgentMcpTools.RemoveRange(actuales);

        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sel in tools)
        {
            if (string.IsNullOrWhiteSpace(sel.ConnectionCode) || string.IsNullOrWhiteSpace(sel.ToolName)) { continue; }
            if (McpConnectionCatalog.Find(sel.ConnectionCode) is null) { continue; }
            var key = $"{sel.ConnectionCode}|{sel.ToolName}";
            if (!vistos.Add(key)) { continue; }
            _db.AiAgentMcpTools.Add(new AiAgentMcpTool
            {
                TenantId = tenantId,
                AgentId = agentId,
                ConnectionCode = sel.ConnectionCode.Trim(),
                ToolName = sel.ToolName.Trim()
            });
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static AiAgentDto Map(AiAgent a, int resourceCount) =>
        new(a.Id, a.Name, a.Role, a.Provider, a.Model, a.SystemPrompt, a.IsActive, a.SortOrder, resourceCount,
            a.ReactionsEnabled, a.ReactionRatioN, a.ReactionRatioM, a.ReactionEmojis);

    private static AiAgentResourceDto MapResource(AiAgentResource r) =>
        new(r.Id, r.AgentId, r.Name, r.ResourceType, r.Detail, r.FileUrl, r.FileName, r.SortOrder);

    private static AiAgentPromptDto MapPrompt(AiAgentPrompt p) =>
        new(p.Id, p.AgentId, p.Name, p.Rule, p.Body, p.SortOrder);

    // ===== Duplicado + versionado portado de CUBOT.travels =====

    public async Task<AiAgentDto?> DuplicateAsync(Guid sourceId, CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is not Guid tenantId) return null;
        var src = await _db.AiAgents.AsNoTracking().FirstOrDefaultAsync(a => a.Id == sourceId, ct);
        if (src is null) return null;

        var nextOrder = (await _db.AiAgents.Select(a => (int?)a.SortOrder).MaxAsync(ct) ?? -1) + 1;
        // Copia: queda APAGADA, sin tools MCP duplicadas (decision: el usuario elige tools del nuevo).
        var copy = new AiAgent
        {
            TenantId = tenantId,
            Name = $"Copia de {src.Name}",
            Role = src.Role,
            Provider = src.Provider,
            Model = src.Model,
            SystemPrompt = src.SystemPrompt,
            IsActive = false,
            SortOrder = nextOrder,
            PromptHistoryJson = src.PromptHistoryJson,
            ReactionsEnabled = src.ReactionsEnabled,
            ReactionRatioN = src.ReactionRatioN,
            ReactionRatioM = src.ReactionRatioM,
            ReactionEmojis = src.ReactionEmojis
        };
        _db.AiAgents.Add(copy);
        var newId = copy.Id;

        var prompts = await _db.AiAgentPrompts.AsNoTracking().Where(p => p.AgentId == sourceId).ToListAsync(ct);
        foreach (var p in prompts)
        {
            _db.AiAgentPrompts.Add(new AiAgentPrompt
            {
                TenantId = tenantId, AgentId = newId,
                Name = p.Name, Rule = p.Rule, Body = p.Body, SortOrder = p.SortOrder
            });
        }

        var resources = await _db.AiAgentResources.AsNoTracking().Where(r => r.AgentId == sourceId).ToListAsync(ct);
        foreach (var r in resources)
        {
            _db.AiAgentResources.Add(new AiAgentResource
            {
                TenantId = tenantId, AgentId = newId,
                Name = r.Name, ResourceType = r.ResourceType, Detail = r.Detail,
                FileUrl = r.FileUrl, FileName = r.FileName, SortOrder = r.SortOrder
            });
        }

        // Copiamos tambien la seleccion de tools MCP (la copia es identica funcionalmente).
        var tools = await _db.AiAgentMcpTools.AsNoTracking().Where(t => t.AgentId == sourceId).ToListAsync(ct);
        foreach (var t in tools)
        {
            _db.AiAgentMcpTools.Add(new AiAgentMcpTool
            {
                TenantId = tenantId, AgentId = newId,
                ConnectionCode = t.ConnectionCode, ToolName = t.ToolName
            });
        }

        await _db.SaveChangesAsync(ct);
        return Map(copy, resources.Count);
    }

    public async Task<IReadOnlyList<AiAgentPromptVersionDto>> GetPromptHistoryAsync(Guid agentId, CancellationToken ct = default)
    {
        var json = await _db.AiAgents.AsNoTracking()
            .Where(a => a.Id == agentId).Select(a => a.PromptHistoryJson)
            .FirstOrDefaultAsync(ct);
        return DeserializeVersions(json)
            .Select((v, i) => new AiAgentPromptVersionDto(i, v.SavedAt, v.BasePrompt ?? "", v.Prompts ?? new List<AgentPromptSnapshotDto>()))
            .ToList();
    }

    public async Task<AiAgentDetailDto?> RestorePromptVersionAsync(Guid agentId, int versionIndex, CancellationToken ct = default)
    {
        var agent = await _db.AiAgents.FirstOrDefaultAsync(a => a.Id == agentId, ct);
        if (agent is null) return null;
        var list = DeserializeVersions(agent.PromptHistoryJson);
        if (versionIndex < 0 || versionIndex >= list.Count) return null;
        var version = list[versionIndex];

        agent.SystemPrompt = version.BasePrompt ?? "";
        var current = await _db.AiAgentPrompts.Where(p => p.AgentId == agentId).ToListAsync(ct);
        _db.AiAgentPrompts.RemoveRange(current);
        var order = 0;
        foreach (var p in (version.Prompts ?? new List<AgentPromptSnapshotDto>()).OrderBy(p => p.SortOrder))
        {
            _db.AiAgentPrompts.Add(new AiAgentPrompt
            {
                TenantId = agent.TenantId,
                AgentId = agentId,
                Name = string.IsNullOrWhiteSpace(p.Name) ? "Prompt" : p.Name,
                Rule = string.IsNullOrWhiteSpace(p.Rule) ? null : p.Rule,
                Body = p.Body ?? "",
                SortOrder = order++
            });
        }
        // Snapshot de la restauracion: indice 0 del historial siempre coincide con el estado vivo.
        agent.PromptHistoryJson = PushPromptVersion(agent.PromptHistoryJson,
            new StoredPromptVersion(DateTimeOffset.UtcNow, agent.SystemPrompt,
                (version.Prompts ?? new List<AgentPromptSnapshotDto>()).ToList()));

        await _db.SaveChangesAsync(ct);
        return await GetAsync(agentId, ct);
    }

    // ===== Helpers de versionado =====
    private const int MaxPromptVersions = 5;
    private sealed record StoredPromptVersion(DateTimeOffset SavedAt, string BasePrompt, List<AgentPromptSnapshotDto> Prompts);

    private static string PushPromptVersion(string? existingJson, StoredPromptVersion version)
    {
        var list = DeserializeVersions(existingJson);
        if (list.Count > 0 && SameContent(list[0], version) && existingJson is not null) return existingJson;
        list.Insert(0, version);
        if (list.Count > MaxPromptVersions) list = list.Take(MaxPromptVersions).ToList();
        return System.Text.Json.JsonSerializer.Serialize(list);
    }

    private static List<StoredPromptVersion> DeserializeVersions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<StoredPromptVersion>();
        try { return System.Text.Json.JsonSerializer.Deserialize<List<StoredPromptVersion>>(json) ?? new(); }
        catch { return new List<StoredPromptVersion>(); }
    }

    private static bool SameContent(StoredPromptVersion a, StoredPromptVersion b)
    {
        if (!string.Equals(a.BasePrompt ?? "", b.BasePrompt ?? "", StringComparison.Ordinal)) return false;
        var pa = a.Prompts ?? new List<AgentPromptSnapshotDto>();
        var pb = b.Prompts ?? new List<AgentPromptSnapshotDto>();
        if (pa.Count != pb.Count) return false;
        for (var i = 0; i < pa.Count; i++)
        {
            if (!string.Equals(pa[i].Name ?? "", pb[i].Name ?? "", StringComparison.Ordinal)) return false;
            if (!string.Equals(pa[i].Rule ?? "", pb[i].Rule ?? "", StringComparison.Ordinal)) return false;
            if (!string.Equals(pa[i].Body ?? "", pb[i].Body ?? "", StringComparison.Ordinal)) return false;
        }
        return true;
    }
}
