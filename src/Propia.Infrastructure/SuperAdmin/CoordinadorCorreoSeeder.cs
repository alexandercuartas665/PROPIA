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
    private const string Descripcion = "Lee los correos que llegan a los buzones de la copropiedad y decide de que tema son (los temas los define cada copropiedad: recibo de servicio, PQRSD, poliza o contrato, otro, irrelevante y los propios) para enviarlos al area correcta.";

    public static async Task EnsureAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PropiaDbContext>>();

        var existente = await db.AiAgentTemplates.FirstOrDefaultAsync(t => t.Clave == PlantillaAgente.ClaveCoordinadorCorreo);
        if (existente is not null)
        {
            // Temas configurables: si la plantilla conserva intacto el prompt de la Ola 1 (temas fijos), se lleva al
            // prompt nuevo y sube la version (el Super Admin puede "Actualizar copropiedades"). Si el Super Admin ya lo
            // edito, no se toca: el Super Admin manda. Los temas viajan igual en cada mensaje.
            static string N(string? s) => (s ?? "").Replace("\r\n", "\n").Trim();
            if (N(existente.SystemPrompt) == N(CoordinadorCorreoAgente.SystemPromptOla1))
            {
                existente.SystemPrompt = CoordinadorCorreoAgente.SystemPrompt;
                existente.Description = Descripcion;
                existente.Version += 1;
                existente.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
                logger.LogWarning("[SEED] CoordinadorCorreoSeeder: prompt actualizado a temas configurables (version {V}).", existente.Version);
            }
            return;
        }

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
            Description = Descripcion,
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
