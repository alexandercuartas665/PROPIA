using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Propia.Application.Asambleas;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.SuperAdmin;

/// <summary>
/// Siembra el agente del sistema "Redactor de actas" (modulo 2.8 Asambleas): redacta el acta al cerrar una sesion con
/// quorum, con la plantilla "Acta de asamblea", los datos de la sesion y la transcripcion. Sin tools MCP (un turno).
/// SEMILLA idempotente (como AuxiliarAdministrativoSeeder): solo crea la plantilla global si no existe, con su clave;
/// no pisa su prompt (el Super Admin manda). El agente se crea en cada copropiedad al primer uso (AgentesSistemaService).
/// </summary>
public static class AgenteActaAsambleaSeeder
{
    public static async Task EnsureAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PropiaDbContext>>();

        if (await db.AiAgentTemplates.AnyAsync(t => t.Clave == PlantillaAgente.ClaveActaAsamblea)) return;

        var provider = await db.AiProviderConfigs.AsNoTracking()
            .Where(c => c.IsEnabled)
            .Select(c => (AiProvider?)c.Provider)
            .FirstOrDefaultAsync() ?? AiProvider.Claude;

        db.AiAgentTemplates.Add(new AiAgentTemplate
        {
            Id = Guid.NewGuid(),
            Name = ActaAsambleaAgente.Nombre,
            Role = ActaAsambleaAgente.RoleTag,
            Description = "Redacta el acta de las asambleas y sesiones con la transcripcion y los datos de la sesion (no decide nada).",
            Provider = provider,
            Model = null,
            SystemPrompt = ActaAsambleaAgente.SystemPrompt,
            Clave = PlantillaAgente.ClaveActaAsamblea,
            IsActive = true,
            IncludeInOnboarding = false,
            SortOrder = 70,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        logger.LogWarning("[SEED] AgenteActaAsambleaSeeder: plantilla global creada (provider {Provider}).", provider);
    }
}
