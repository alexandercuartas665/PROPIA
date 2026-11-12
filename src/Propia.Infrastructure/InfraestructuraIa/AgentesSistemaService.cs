using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <inheritdoc cref="IAgentesSistema"/>
public sealed class AgentesSistemaService : IAgentesSistema
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<AgentesSistemaService> _logger;

    public AgentesSistemaService(PropiaDbContext db, ITenantContext tenant, ILogger<AgentesSistemaService> logger)
    {
        _db = db; _tenant = tenant; _logger = logger;
    }

    // Nombres con los que se crearon los agentes antes de existir la clave (agentes viejos sin vinculo).
    private static string? NombreLegado(string clave) => clave switch
    {
        PlantillaAgente.ClaveDocumental => SuperAdmin.AgenteDocumentalSeeder.AgenteNombre,
        PlantillaAgente.ClaveAuxiliar => AuxiliarAdministrativoAgente.Nombre,
        _ => null
    };

    public async Task<AgenteSistema?> AsegurarAsync(string clave, CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return null;
        var plantilla = await _db.AiAgentTemplates.AsNoTracking().Include(t => t.McpTools)
            .FirstOrDefaultAsync(t => t.Clave == clave, ct);

        // 1) El agente de la copropiedad que salio de esa plantilla (si hay varios, el activo).
        if (plantilla is not null)
        {
            var enlazado = await _db.AiAgents.AsNoTracking()
                .Where(a => a.TemplateId == plantilla.Id)
                .OrderByDescending(a => a.IsActive).ThenBy(a => a.CreatedAt)
                .Select(a => new AgenteSistema(a.Id, a.IsActive))
                .FirstOrDefaultAsync(ct);
            if (enlazado is not null) return enlazado;
        }

        // 2) Legado: un agente con el nombre de siempre que aun no tiene vinculo -> se enlaza (sin huella:
        //    no se sabe que prompt le puso la plantilla, asi que cuenta como "sin comparar", no como editado).
        if (NombreLegado(clave) is { } nombre)
        {
            var legado = await _db.AiAgents
                .Where(a => a.Name == nombre && a.TemplateId == null)
                .OrderByDescending(a => a.IsActive).ThenBy(a => a.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (legado is not null)
            {
                if (plantilla is not null) { legado.TemplateId = plantilla.Id; await _db.SaveChangesAsync(ct); }
                return new AgenteSistema(legado.Id, legado.IsActive);
            }
        }

        // 3) Al usarlo: la copropiedad no lo tiene -> se crea desde la plantilla del Super Admin.
        if (plantilla is null || !plantilla.IsActive) return null;
        var tenant = await _db.Tenants.AsNoTracking().Where(t => t.Id == tid)
            .Select(t => new { t.Nombre, Org = t.Organizacion != null ? t.Organizacion.Nombre : null })
            .FirstOrDefaultAsync(ct);
        if (tenant is null) return null;
        var (agente, tools) = PlantillaAgenteFabrica.Nuevo(plantilla, tid, tenant.Nombre, tenant.Org);
        _db.AiAgents.Add(agente);
        _db.AiAgentMcpTools.AddRange(tools);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("[IA] Agente del sistema '{Clave}' creado al primer uso en la copropiedad {TenantId} (plantilla v{Version}).",
            clave, tid, plantilla.Version);
        return new AgenteSistema(agente.Id, true);
    }
}
