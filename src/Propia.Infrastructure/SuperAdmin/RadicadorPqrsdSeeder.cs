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
/// Siembra el agente del sistema "Radicador de PQRSD" (PQRSD por correo, Ola P1): lee los correos que el Coordinador
/// clasifico como PQRSD y propone tipo, categoria y unidad para radicarlos. SEMILLA idempotente: solo crea la plantilla
/// global si no existe (con su clave); no pisa su prompt (el Super Admin manda). El agente se crea en cada copropiedad
/// al primer uso.
/// </summary>
public static class RadicadorPqrsdSeeder
{
    private const string Descripcion = "Lee los correos que el Coordinador clasificó como PQRSD y propone cómo radicarlos (tipo, categoría y unidad de la copropiedad). Con confianza alta el sistema radica solo; si no, deja la propuesta para que la administración confirme.";

    public static async Task EnsureAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PropiaDbContext>>();

        var existente = await db.AiAgentTemplates.FirstOrDefaultAsync(t => t.Clave == PlantillaAgente.ClaveRadicadorPqrsd);
        if (existente is not null)
        {
            // Ola P2 (reenvios): si la plantilla conserva intacto el prompt v1, se lleva al nuevo (remitente original) y
            // sube la version. Si el Super Admin ya lo edito, no se toca: la regla fija de reenvios funciona igual.
            static string N(string? s) => (s ?? "").Replace("\r\n", "\n").Trim();
            if (N(existente.SystemPrompt) == N(RadicadorPqrsdCorreoAgente.SystemPromptV1))
            {
                existente.SystemPrompt = RadicadorPqrsdCorreoAgente.SystemPrompt;
                existente.Version += 1;
                existente.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
                logger.LogWarning("[SEED] RadicadorPqrsdSeeder: prompt actualizado a reenvios (version {V}).", existente.Version);
            }
            return;
        }

        // Mismo proveedor que el Coordinador de correo si existe (ya probado con el buzon); si no, el habilitado mas reciente.
        var provider = await db.AiAgentTemplates.AsNoTracking()
            .Where(t => t.Clave == PlantillaAgente.ClaveCoordinadorCorreo).Select(t => (AiProvider?)t.Provider).FirstOrDefaultAsync()
            ?? await db.AiProviderConfigs.AsNoTracking()
                .Where(c => c.IsEnabled)
                .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
                .Select(c => (AiProvider?)c.Provider)
                .FirstOrDefaultAsync()
            ?? AiProvider.Claude;
        var modelo = await db.AiAgentTemplates.AsNoTracking()
            .Where(t => t.Clave == PlantillaAgente.ClaveCoordinadorCorreo).Select(t => t.Model).FirstOrDefaultAsync();

        db.AiAgentTemplates.Add(new AiAgentTemplate
        {
            Id = Guid.NewGuid(),
            Name = RadicadorPqrsdCorreoAgente.Nombre,
            Role = RadicadorPqrsdCorreoAgente.RoleTag,
            Description = Descripcion,
            Provider = provider,
            Model = modelo,
            SystemPrompt = RadicadorPqrsdCorreoAgente.SystemPrompt,
            Clave = PlantillaAgente.ClaveRadicadorPqrsd,
            IsActive = true,
            IncludeInOnboarding = false,
            SortOrder = 81,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        logger.LogWarning("[SEED] RadicadorPqrsdSeeder: plantilla global creada (provider {Provider}).", provider);
    }
}
