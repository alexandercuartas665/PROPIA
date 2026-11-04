using Microsoft.EntityFrameworkCore;
using Propia.Application.InfraestructuraIa;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Ola H - lee/guarda la config de enrutamiento del agente (whitelist de tableros + asignados round-robin).
/// La rotacion en si la hace la tool crear_tarea (avanza AssigneeCursor dentro de su transaccion). Portado de ECOREX.
/// </summary>
public sealed class AgentEnrutamientoService : IAgentEnrutamientoService
{
    private readonly PropiaDbContext _db;

    public AgentEnrutamientoService(PropiaDbContext db) => _db = db;

    public async Task<AgentEnrutamientoDto?> GetConfigAsync(Guid agentId, CancellationToken ct = default)
    {
        var agent = await _db.AiAgents.AsNoTracking()
            .Where(a => a.Id == agentId)
            .Select(a => new { a.AllowedBoardIdsJson, a.AssigneeUserIdsJson })
            .FirstOrDefaultAsync(ct);
        if (agent is null) { return null; }

        var tableros = AgentEnrutamientoTareas.ParseIds(agent.AllowedBoardIdsJson);
        var asignadoIds = AgentEnrutamientoTareas.ParseIds(agent.AssigneeUserIdsJson);

        // Resolver nombres de las personas, preservando el ORDEN de rotacion. Omite ids que ya no existen.
        var asignados = new List<AgentAsignadoDto>(asignadoIds.Count);
        if (asignadoIds.Count > 0)
        {
            var nombres = await _db.Personas.AsNoTracking()
                .Where(p => asignadoIds.Contains(p.Id))
                .Select(p => new { p.Id, Nombre = (p.Nombres + " " + p.Apellidos).Trim() })
                .ToDictionaryAsync(p => p.Id, p => p.Nombre, ct);
            foreach (var id in asignadoIds)
            {
                if (nombres.TryGetValue(id, out var n)) { asignados.Add(new AgentAsignadoDto(id, string.IsNullOrWhiteSpace(n) ? "(sin nombre)" : n)); }
            }
        }

        return new AgentEnrutamientoDto(tableros, asignados);
    }

    public async Task<bool> SaveConfigAsync(Guid agentId, AgentEnrutamientoSaveRequest req, CancellationToken ct = default)
    {
        var agent = await _db.AiAgents.FirstOrDefaultAsync(a => a.Id == agentId, ct);
        if (agent is null) { return false; }

        agent.AllowedBoardIdsJson = AgentEnrutamientoTareas.SerializeIds(req.TablerosPermitidos);

        var nuevos = AgentEnrutamientoTareas.ParseIds(AgentEnrutamientoTareas.SerializeIds(req.AsignadoPersonaIds));
        var actuales = AgentEnrutamientoTareas.ParseIds(agent.AssigneeUserIdsJson);
        agent.AssigneeUserIdsJson = AgentEnrutamientoTareas.SerializeIds(req.AsignadoPersonaIds);
        // Si cambia la lista o el orden, el cursor vuelve a 0 (la rotacion reinicia limpia).
        if (!AgentEnrutamientoTareas.SameOrder(actuales, nuevos)) { agent.AssigneeCursor = 0; }

        await _db.SaveChangesAsync(ct);
        return true;
    }
}
