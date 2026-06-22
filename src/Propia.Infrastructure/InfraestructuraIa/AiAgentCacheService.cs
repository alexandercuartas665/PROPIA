using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Datos cache del agente (capa 3): el agente captura datos del residente durante la conversacion.
/// Definicion de campos (tenant-scoped, RLS) + valores percibidos por sesion (AgentId en pruebas,
/// ConversationId en chat real). Aplica la regla sticky (IsUpdatable=false no se sobrescribe).
/// Portado de CUBOT.travels (sin el mapeo a CRM/pipeline). El motor de inferencia llama SetValueAsync.
/// </summary>
public sealed class AiAgentCacheService : IAiAgentCacheService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;

    public AiAgentCacheService(PropiaDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<AiAgentCacheFieldDto>> ListFieldsAsync(Guid agentId, CancellationToken ct = default)
    {
        return await _db.AiAgentCacheFields.AsNoTracking()
            .Where(f => f.AgentId == agentId)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Label)
            .Select(f => new AiAgentCacheFieldDto(f.Id, f.AgentId, f.FieldKey, f.Label, f.Description, f.SortOrder, f.IsUpdatable))
            .ToListAsync(ct);
    }

    public async Task<AiAgentCacheFieldDto?> CreateFieldAsync(Guid agentId, CreateAgentCacheFieldRequest request, CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is not Guid tenantId) { return null; }
        var agent = await _db.AiAgents.FirstOrDefaultAsync(a => a.Id == agentId, ct);
        if (agent is null) { return null; }
        if (string.IsNullOrWhiteSpace(request.Label)) { throw new InvalidOperationException("El nombre del dato es obligatorio."); }

        var existingKeys = await _db.AiAgentCacheFields.AsNoTracking()
            .Where(f => f.AgentId == agentId).Select(f => f.FieldKey).ToListAsync(ct);
        var key = EnsureUniqueKey(Slugify(request.Label), existingKeys);
        var nextOrder = (await _db.AiAgentCacheFields.Where(f => f.AgentId == agentId).Select(f => (int?)f.SortOrder).MaxAsync(ct) ?? -1) + 1;

        var field = new AiAgentCacheField
        {
            TenantId = tenantId,
            AgentId = agentId,
            FieldKey = key,
            Label = request.Label.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsUpdatable = request.IsUpdatable,
            SortOrder = nextOrder
        };
        _db.AiAgentCacheFields.Add(field);
        await _db.SaveChangesAsync(ct);
        return new AiAgentCacheFieldDto(field.Id, field.AgentId, field.FieldKey, field.Label, field.Description, field.SortOrder, field.IsUpdatable);
    }

    public async Task<AiAgentCacheFieldDto?> UpdateFieldAsync(Guid fieldId, UpdateAgentCacheFieldRequest request, CancellationToken ct = default)
    {
        var field = await _db.AiAgentCacheFields.FirstOrDefaultAsync(f => f.Id == fieldId, ct);
        if (field is null) { return null; }
        field.Label = (request.Label ?? field.Label).Trim();
        field.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        field.IsUpdatable = request.IsUpdatable;
        await _db.SaveChangesAsync(ct);
        return new AiAgentCacheFieldDto(field.Id, field.AgentId, field.FieldKey, field.Label, field.Description, field.SortOrder, field.IsUpdatable);
    }

    public async Task<bool> DeleteFieldAsync(Guid fieldId, CancellationToken ct = default)
    {
        var field = await _db.AiAgentCacheFields.FirstOrDefaultAsync(f => f.Id == fieldId, ct);
        if (field is null) { return false; }
        // Borra tambien los valores huerfanos de esa clave.
        var orphans = await _db.AiAgentCacheValues.Where(v => v.AgentId == field.AgentId && v.FieldKey == field.FieldKey).ToListAsync(ct);
        _db.AiAgentCacheValues.RemoveRange(orphans);
        _db.AiAgentCacheFields.Remove(field);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> BulkSetFieldsUpdatableAsync(Guid agentId, bool isUpdatable, CancellationToken ct = default)
    {
        var fields = await _db.AiAgentCacheFields.Where(f => f.AgentId == agentId && f.IsUpdatable != isUpdatable).ToListAsync(ct);
        foreach (var f in fields) { f.IsUpdatable = isUpdatable; }
        if (fields.Count > 0) { await _db.SaveChangesAsync(ct); }
        return fields.Count;
    }

    public async Task<IReadOnlyList<AiAgentCacheValueDto>> GetValuesAsync(Guid agentId, Guid sessionId, CancellationToken ct = default)
    {
        var fields = await _db.AiAgentCacheFields.AsNoTracking()
            .Where(f => f.AgentId == agentId)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Label)
            .ToListAsync(ct);
        var values = await _db.AiAgentCacheValues.AsNoTracking()
            .Where(v => v.AgentId == agentId && v.SessionId == sessionId)
            .ToListAsync(ct);
        return fields.Select(f =>
        {
            var val = values.FirstOrDefault(v => v.FieldKey == f.FieldKey);
            return new AiAgentCacheValueDto(f.FieldKey, f.Label, f.Description, val?.Value, val?.Source,
                val is null ? null : (val.UpdatedAt ?? val.CreatedAt));
        }).ToList();
    }

    public async Task<AiAgentCacheValueDto?> SetValueAsync(SetAgentCacheValueRequest request, CancellationToken ct = default)
    {
        // Resuelve el tenant desde el campo (el motor puede llamar sin contexto de tenant claro).
        var field = await _db.AiAgentCacheFields
            .FirstOrDefaultAsync(f => f.AgentId == request.AgentId && f.FieldKey == request.FieldKey, ct);
        if (field is null) { return null; }

        var entry = await _db.AiAgentCacheValues
            .FirstOrDefaultAsync(v => v.AgentId == request.AgentId && v.SessionId == request.SessionId && v.FieldKey == request.FieldKey, ct);

        if (entry is null)
        {
            entry = new AiAgentCacheValue
            {
                TenantId = field.TenantId,
                AgentId = request.AgentId,
                SessionId = request.SessionId,
                FieldKey = request.FieldKey,
                Value = request.Value,
                Source = request.Source
            };
            _db.AiAgentCacheValues.Add(entry);
        }
        else
        {
            // Sticky: si el campo no es actualizable y ya tenia valor, no lo sobrescribimos.
            if (!field.IsUpdatable && !string.IsNullOrWhiteSpace(entry.Value))
            {
                return new AiAgentCacheValueDto(field.FieldKey, field.Label, field.Description, entry.Value, entry.Source, entry.UpdatedAt ?? entry.CreatedAt);
            }
            entry.Value = request.Value;
            entry.Source = request.Source;
        }
        await _db.SaveChangesAsync(ct);
        return new AiAgentCacheValueDto(field.FieldKey, field.Label, field.Description, entry.Value, entry.Source, entry.UpdatedAt ?? entry.CreatedAt);
    }

    public async Task<int> ClearValuesAsync(Guid agentId, Guid sessionId, CancellationToken ct = default)
    {
        var values = await _db.AiAgentCacheValues.Where(v => v.AgentId == agentId && v.SessionId == sessionId).ToListAsync(ct);
        _db.AiAgentCacheValues.RemoveRange(values);
        if (values.Count > 0) { await _db.SaveChangesAsync(ct); }
        return values.Count;
    }

    // slug: minusculas, sin acentos, no-alfanumerico -> "_".
    private static string Slugify(string label)
    {
        var normalized = label.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) { continue; }
            if (char.IsLetterOrDigit(c)) { sb.Append(c); }
            else if (sb.Length > 0 && sb[^1] != '_') { sb.Append('_'); }
        }
        var slug = sb.ToString().Trim('_');
        return string.IsNullOrEmpty(slug) ? "dato" : slug;
    }

    private static string EnsureUniqueKey(string baseKey, IReadOnlyCollection<string> existingKeys)
    {
        if (!existingKeys.Contains(baseKey)) { return baseKey; }
        var i = 2;
        while (existingKeys.Contains($"{baseKey}_{i}")) { i++; }
        return $"{baseKey}_{i}";
    }
}
