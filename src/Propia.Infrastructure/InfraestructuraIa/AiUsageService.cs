using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Registro y reporte de consumo de IA de la copropiedad + control de cuota del plan.
/// Adaptado de CUBOT: en PROPIA la cuota es por LLAMADAS/mes (Plan.LimiteLlamadasIaMensual),
/// no por tokens. El costo en USD se sigue estimando para el dashboard.
/// </summary>
public sealed class AiUsageService : IAiUsageService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;

    public AiUsageService(PropiaDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task RecordAsync(Guid? agentId, AiProvider provider, string model, int inputTokens, int outputTokens, string source, bool success, CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is not Guid tenantId) { return; }

        var log = new AiUsageLog
        {
            TenantId = tenantId,
            AgentId = agentId,
            Provider = provider,
            Model = model,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = inputTokens + outputTokens,
            EstimatedCostUsd = AiCostEstimator.Estimate(provider, inputTokens, outputTokens),
            Source = string.IsNullOrWhiteSpace(source) ? "chat" : source,
            Success = success
        };
        _db.AiUsageLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<AiUsageSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var rows = await _db.AiUsageLogs.AsNoTracking()
            .Select(l => new { l.AgentId, l.InputTokens, l.OutputTokens, l.TotalTokens, l.EstimatedCostUsd })
            .ToListAsync(ct);

        var byAgent = rows
            .GroupBy(r => r.AgentId)
            .Select(g => new AgentUsageDto(
                g.Key,
                g.Count(),
                g.Sum(x => (long)x.InputTokens),
                g.Sum(x => (long)x.OutputTokens),
                g.Sum(x => (long)x.TotalTokens),
                g.Sum(x => x.EstimatedCostUsd)))
            .ToList();

        return new AiUsageSummaryDto(
            rows.Count,
            rows.Sum(x => (long)x.TotalTokens),
            rows.Sum(x => (long)x.InputTokens),
            rows.Sum(x => (long)x.OutputTokens),
            rows.Sum(x => x.EstimatedCostUsd),
            byAgent);
    }

    public async Task<AiQuotaDto> GetQuotaAsync(CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is not Guid tenantId) { return new AiQuotaDto(false, 0, 0); }

        // Llamadas del mes en curso (UTC). AiUsageLogs ya esta filtrado por tenant (RLS + HasQueryFilter).
        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var used = await _db.AiUsageLogs.AsNoTracking()
            .CountAsync(l => l.CreatedAt >= monthStart, ct);

        // Limite del plan vigente de la copropiedad (Suscripcion es global, filtro explicito).
        var limite = await _db.Suscripciones.AsNoTracking()
            .Where(s => s.CopropiedadId == tenantId && s.Estado != EstadoSuscripcion.Cancelada)
            .OrderByDescending(s => s.FechaInicio)
            .Select(s => s.Plan!.LimiteLlamadasIaMensual)
            .FirstOrDefaultAsync(ct);

        return limite is int max
            ? new AiQuotaDto(true, max, used)
            : new AiQuotaDto(false, 0, used);
    }
}
