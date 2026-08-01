using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Lectura de la bitacora de atencion del agente (pagina /ia/bitacora). Opera sobre el tenant activo
/// del JWT (query filter + RLS). La escritura la hace el AgentDispatcher. Portado de CUBOT.travels.
/// </summary>
public sealed class AgentRunLogService : IAgentRunLogService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;

    public AgentRunLogService(PropiaDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<AgentRunLogConversationDto>> ListConversationsAsync(CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is not Guid) { return Array.Empty<AgentRunLogConversationDto>(); }

        // Conteo y ultima actividad por conversacion (el filtro por tenant lo aplica el query filter).
        var grouped = await _db.AiAgentRunLogs.AsNoTracking()
            .GroupBy(l => l.ConversationId)
            .Select(g => new { Id = g.Key, Events = g.Count(), Last = g.Max(x => x.OccurredAt) })
            .ToListAsync(ct);
        if (grouped.Count == 0) { return Array.Empty<AgentRunLogConversationDto>(); }

        var convIds = grouped.Select(g => g.Id).ToList();
        var convs = await _db.Conversations.AsNoTracking()
            .Where(c => convIds.Contains(c.Id))
            .Select(c => new { c.Id, c.ContactName, c.ContactPhone, c.WhatsAppLineId })
            .ToListAsync(ct);

        var lineIds = convs.Where(c => c.WhatsAppLineId is not null).Select(c => c.WhatsAppLineId!.Value).Distinct().ToList();
        var lineMap = lineIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.WhatsAppLines.AsNoTracking()
                .Where(l => lineIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, l => l.PhoneNumber ?? l.InstanceName, ct);

        return grouped
            .Select(g =>
            {
                var c = convs.FirstOrDefault(x => x.Id == g.Id);
                string? lineLabel = c?.WhatsAppLineId is Guid wid && lineMap.TryGetValue(wid, out var lbl) ? lbl : null;
                return new AgentRunLogConversationDto(g.Id, c?.ContactName, c?.ContactPhone ?? "(desconocido)", lineLabel, g.Last, g.Events);
            })
            .OrderByDescending(c => c.LastActivityAt)
            .ToList();
    }

    public async Task<IReadOnlyList<AgentRunLogEntryDto>> GetConversationLogAsync(Guid conversationId, CancellationToken ct = default)
        => await _db.AiAgentRunLogs.AsNoTracking()
            .Where(l => l.ConversationId == conversationId)
            .OrderBy(l => l.OccurredAt)
            .Select(l => new AgentRunLogEntryDto(l.OccurredAt, l.Kind, l.Title, l.Content, l.Response))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AgentCacheItemDto>> GetConversationCacheAsync(Guid conversationId, CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is not Guid) { return Array.Empty<AgentCacheItemDto>(); }

        // El agente que atendio se deduce de cualquier entrada de la bitacora (todas comparten AgentId).
        var agentId = await _db.AiAgentRunLogs.AsNoTracking()
            .Where(l => l.ConversationId == conversationId)
            .Select(l => l.AgentId)
            .FirstOrDefaultAsync(ct);
        if (agentId == Guid.Empty) { return Array.Empty<AgentCacheItemDto>(); }

        var fields = await _db.AiAgentCacheFields.AsNoTracking()
            .Where(f => f.AgentId == agentId)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Label)
            .Select(f => new { f.FieldKey, f.Label })
            .ToListAsync(ct);

        var values = await _db.AiAgentCacheValues.AsNoTracking()
            .Where(v => v.AgentId == agentId && v.SessionId == conversationId)
            .ToDictionaryAsync(v => v.FieldKey, v => new { v.Value, v.Source }, ct);

        return fields
            .Select(f => values.TryGetValue(f.FieldKey, out var x)
                ? new AgentCacheItemDto(f.FieldKey, f.Label, x.Value, x.Source)
                : new AgentCacheItemDto(f.FieldKey, f.Label, null, null))
            .ToList();
    }

    public async Task<IReadOnlyList<AgentRunLogMessageDto>> GetConversationMessagesAsync(Guid conversationId, CancellationToken ct = default)
        => await _db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.SentAt)
            .Select(m => new AgentRunLogMessageDto(
                m.SentAt,
                m.Direction == MessageDirection.Inbound ? "inbound" : "outbound",
                m.Body ?? string.Empty,
                m.SentByName))
            .ToListAsync(ct);

    public async Task<(int Logs, int Cache)> ClearAllAsync(CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is not Guid) { return (0, 0); }
        var logs = await _db.AiAgentRunLogs.ExecuteDeleteAsync(ct);
        var cache = await _db.AiAgentCacheValues.ExecuteDeleteAsync(ct);
        return (logs, cache);
    }

    public async Task<(int Logs, int Cache, int Messages)> ResetConversationMemoryAsync(Guid conversationId, CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is not Guid) { return (0, 0, 0); }
        var logs = await _db.AiAgentRunLogs.Where(l => l.ConversationId == conversationId).ExecuteDeleteAsync(ct);
        var cache = await _db.AiAgentCacheValues.Where(v => v.SessionId == conversationId).ExecuteDeleteAsync(ct);
        var messages = await _db.Messages.Where(m => m.ConversationId == conversationId).ExecuteDeleteAsync(ct);
        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId, ct);
        if (conv is not null) { conv.LastMessageAt = null; await _db.SaveChangesAsync(ct); }
        return (logs, cache, messages);
    }
}
