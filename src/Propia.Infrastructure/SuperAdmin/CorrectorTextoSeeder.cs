using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.SuperAdmin;

/// <summary>
/// Siembra el agente del sistema "Corrector de redaccion": mejora el texto de las cajas de texto (dictado por voz o
/// escrito) cuando el usuario lo pide. Sin tools MCP (un turno). SEMILLA idempotente: solo crea la plantilla global si no
/// existe, con su clave; no pisa su prompt (el Super Admin manda). El agente se crea en cada copropiedad al primer uso.
/// </summary>
public static class CorrectorTextoSeeder
{
    public static async Task EnsureAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PropiaDbContext>>();

        if (await db.AiAgentTemplates.AnyAsync(t => t.Clave == PlantillaAgente.ClaveCorrectorTexto)) return;

        // El proveedor habilitado con la llave guardada mas recientemente (la mas probable de estar vigente).
        var provider = await db.AiProviderConfigs.AsNoTracking()
            .Where(c => c.IsEnabled)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .Select(c => (AiProvider?)c.Provider)
            .FirstOrDefaultAsync() ?? AiProvider.Claude;

        db.AiAgentTemplates.Add(new AiAgentTemplate
        {
            Id = Guid.NewGuid(),
            Name = CorrectorTextoAgente.Nombre,
            Role = CorrectorTextoAgente.RoleTag,
            Description = "Mejora la redaccion de los textos que se dictan o escriben en los formularios (ortografia, puntuacion, frases del dictado) sin cambiar lo que dicen. El usuario decide si usa la propuesta.",
            Provider = provider,
            Model = null,
            SystemPrompt = CorrectorTextoAgente.SystemPrompt,
            Clave = PlantillaAgente.ClaveCorrectorTexto,
            IsActive = true,
            IncludeInOnboarding = false,
            SortOrder = 75,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        logger.LogWarning("[SEED] CorrectorTextoSeeder: plantilla global creada (provider {Provider}).", provider);
    }
}
