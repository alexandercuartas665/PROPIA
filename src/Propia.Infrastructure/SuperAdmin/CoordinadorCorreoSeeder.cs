using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Propia.Application.InfraestructuraIa;
using Propia.Application.Integraciones;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.SuperAdmin;

/// <summary>
/// Siembra el agente del sistema "Coordinador de correo" (lector de buzon): clasifica por tema cada correo que llega al
/// buzon de la copropiedad. Sin tools MCP en Ola 1. SEMILLA idempotente: solo crea la plantilla global si no existe, con
/// su clave; no pisa su prompt (el Super Admin manda). El agente se crea en cada copropiedad al primer uso.
/// </summary>
public static class CoordinadorCorreoSeeder
{
    public static async Task EnsureAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PropiaDbContext>>();

        if (await db.AiAgentTemplates.AnyAsync(t => t.Clave == PlantillaAgente.ClaveCoordinadorCorreo)) return;

        // El proveedor habilitado con la llave guardada mas recientemente (la mas probable de estar vigente).
        var provider = await db.AiProviderConfigs.AsNoTracking()
            .Where(c => c.IsEnabled)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .Select(c => (AiProvider?)c.Provider)
            .FirstOrDefaultAsync() ?? AiProvider.Claude;

        db.AiAgentTemplates.Add(new AiAgentTemplate
        {
            Id = Guid.NewGuid(),
            Name = CoordinadorCorreoAgente.Nombre,
            Role = CoordinadorCorreoAgente.RoleTag,
            Description = "Lee los correos que llegan al buzon de la copropiedad y decide de que tema son (recibo de servicio, PQRSD, poliza o contrato, otro o irrelevante) para enviarlos al area correcta.",
            Provider = provider,
            Model = null,
            SystemPrompt = CoordinadorCorreoAgente.SystemPrompt,
            Clave = PlantillaAgente.ClaveCoordinadorCorreo,
            IsActive = true,
            IncludeInOnboarding = false,
            SortOrder = 80,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        logger.LogWarning("[SEED] CoordinadorCorreoSeeder: plantilla global creada (provider {Provider}).", provider);
    }
}
