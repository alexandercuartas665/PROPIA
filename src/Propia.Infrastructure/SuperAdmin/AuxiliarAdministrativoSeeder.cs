using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.SuperAdmin;

/// <summary>
/// Siembra el agente "Auxiliar Administrativo": el agente IA de proposito general que la plataforma
/// usa para RELLENAR y COMPLETAR campos de texto del sistema (condiciones de uso de una zona,
/// descripciones, observaciones, etc.). No tiene tools MCP (completado simple de un turno).
///
/// SEMILLA (corre en todos los ambientes, idempotente): solo crea la plantilla global si no existe, con su
/// clave (PlantillaAgente.ClaveAuxiliar); no pisa su prompt ni toca los agentes de las copropiedades.
/// El agente se crea en cada copropiedad al primer uso (AgentesSistemaService) o con "Actualizar
/// copropiedades". La definicion de FABRICA (nombre, rol, prompt) vive en AuxiliarAdministrativoAgente.
/// </summary>
public static class AuxiliarAdministrativoSeeder
{
    public static async Task EnsureAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PropiaDbContext>>();

        var provider = await db.AiProviderConfigs.AsNoTracking()
            .Where(c => c.IsEnabled)
            .Select(c => (AiProvider?)c.Provider)
            .FirstOrDefaultAsync() ?? AiProvider.Claude;

        await EnsureTemplateAsync(db, provider, logger);
    }

    private static async Task EnsureTemplateAsync(PropiaDbContext db, AiProvider provider, ILogger logger)
    {
        // Super Admin manda: si la plantilla ya existe NO se pisa su prompt (las mejoras se hacen en el Super
        // Admin y se propagan con "Actualizar copropiedades"); solo se le asegura la clave del agente del sistema.
        var existente = await db.AiAgentTemplates.FirstOrDefaultAsync(t => t.Clave == PlantillaAgente.ClaveAuxiliar)
            ?? await db.AiAgentTemplates.FirstOrDefaultAsync(t => t.Name == AuxiliarAdministrativoAgente.Nombre && t.PlatformKey == null);
        if (existente is not null)
        {
            if (existente.Clave is null)
            {
                existente.Clave = PlantillaAgente.ClaveAuxiliar;
                existente.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
                logger.LogWarning("[SEED] AuxiliarAdministrativoSeeder: clave asignada a la plantilla existente.");
            }
            return;
        }

        db.AiAgentTemplates.Add(new AiAgentTemplate
        {
            Id = Guid.NewGuid(),
            Name = AuxiliarAdministrativoAgente.Nombre,
            Role = AuxiliarAdministrativoAgente.RoleTag,
            Description = "Redacta y completa campos de texto del sistema (condiciones de uso, descripciones, observaciones).",
            Provider = provider,
            Model = null,
            SystemPrompt = AuxiliarAdministrativoAgente.SystemPrompt,
            Clave = PlantillaAgente.ClaveAuxiliar,
            IsActive = true,
            IncludeInOnboarding = true,
            SortOrder = 60,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        logger.LogWarning("[SEED] AuxiliarAdministrativoSeeder: plantilla global creada (provider {Provider}).", provider);
    }

}
