using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Vinculos agente IA &lt;-&gt; lineas WhatsApp ("lineas atendidas"). Una linea solo la atiende un agente
/// a la vez (indice unico filtrado por is_connected). El aislamiento por copropiedad lo da la RLS de
/// Postgres (no hay filtros EF), asi que las consultas ya vienen tenant-scoped. Portado de CUBOT.travels.
/// </summary>
public sealed class AiAgentLineBindingService : IAiAgentLineBindingService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;

    public AiAgentLineBindingService(PropiaDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<AgentLineBindingRowDto>> ListForAgentAsync(Guid agentId, CancellationToken ct = default)
    {
        var lines = await _db.WhatsAppLines.AsNoTracking()
            .OrderBy(l => l.InstanceName)
            .Select(l => new { l.Id, l.InstanceName, l.PhoneNumber })
            .ToListAsync(ct);

        var activeBindings = await _db.AiAgentLineBindings.AsNoTracking()
            .Where(b => b.IsConnected)
            .Select(b => new { b.WhatsAppLineId, b.AgentId, b.AutoConfirm })
            .ToListAsync(ct);

        var agentNames = await _db.AiAgents.AsNoTracking()
            .Select(a => new { a.Id, a.Name })
            .ToDictionaryAsync(a => a.Id, a => a.Name, ct);

        var rows = new List<AgentLineBindingRowDto>(lines.Count);
        foreach (var line in lines)
        {
            var binding = activeBindings.FirstOrDefault(b => b.WhatsAppLineId == line.Id);
            var isBoundHere = binding is not null && binding.AgentId == agentId;
            var boundAgentId = binding?.AgentId;
            var boundAgentName = boundAgentId is Guid bid && agentNames.TryGetValue(bid, out var name) ? name : null;
            rows.Add(new AgentLineBindingRowDto(line.Id, line.InstanceName, line.PhoneNumber,
                isBoundHere, binding?.AutoConfirm ?? true, boundAgentId, boundAgentName));
        }
        return rows;
    }

    public async Task<bool> SetAsync(Guid agentId, SetAgentLineBindingRequest request, CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is not Guid tenantId) { return false; }

        var agent = await _db.AiAgents.FirstOrDefaultAsync(a => a.Id == agentId, ct);
        if (agent is null) { return false; }
        var line = await _db.WhatsAppLines.FirstOrDefaultAsync(l => l.Id == request.WhatsAppLineId, ct);
        if (line is null) { return false; }

        var existingActive = await _db.AiAgentLineBindings
            .Where(b => b.WhatsAppLineId == request.WhatsAppLineId && b.IsConnected)
            .FirstOrDefaultAsync(ct);

        if (!request.Connected)
        {
            // Solo desconectamos si el binding activo pertenece al agente solicitado.
            if (existingActive is null || existingActive.AgentId != agentId) { return false; }
            existingActive.IsConnected = false;
            await _db.SaveChangesAsync(ct);
            return true;
        }

        // Connected = true.
        if (existingActive is not null && existingActive.AgentId != agentId)
        {
            // La linea esta tomada por otro agente. No reasignamos por accidente.
            return false;
        }
        if (existingActive is not null && existingActive.AgentId == agentId)
        {
            if (existingActive.AutoConfirm != request.AutoConfirm)
            {
                existingActive.AutoConfirm = request.AutoConfirm;
                await _db.SaveChangesAsync(ct);
            }
            return true;
        }

        // Reutiliza un binding historico (inactivo) si existe para este agente y linea.
        var historic = await _db.AiAgentLineBindings
            .Where(b => b.WhatsAppLineId == request.WhatsAppLineId && b.AgentId == agentId && !b.IsConnected)
            .FirstOrDefaultAsync(ct);
        if (historic is not null)
        {
            historic.IsConnected = true;
            historic.AutoConfirm = request.AutoConfirm;
            await _db.SaveChangesAsync(ct);
            return true;
        }

        _db.AiAgentLineBindings.Add(new AiAgentLineBinding
        {
            TenantId = tenantId,
            AgentId = agentId,
            WhatsAppLineId = request.WhatsAppLineId,
            IsConnected = true,
            AutoConfirm = request.AutoConfirm
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
