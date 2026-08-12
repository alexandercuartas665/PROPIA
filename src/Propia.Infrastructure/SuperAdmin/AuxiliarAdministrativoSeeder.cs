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
/// IDEMPOTENTE: (1) crea la plantilla global (para copropiedades nuevas via onboarding); (2) crea el
/// agente en cada tenant existente que aun no lo tenga. Ademas el servicio AsistenteCamposService lo
/// crea al vuelo la primera vez que se le invoca, por lo que en prod aparece con el primer uso.
/// La definicion canonica (nombre, rol, prompt) vive en AuxiliarAdministrativoAgente.
/// </summary>
public static class AuxiliarAdministrativoSeeder
{
    public static async Task EnsureAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PropiaDbContext>>();

        var provider = await db.AiProviderConfigs.AsNoTracking()
            .Where(c => c.IsEnabled)
            .Select(c => (AiProvider?)c.Provider)
            .FirstOrDefaultAsync() ?? AiProvider.Claude;

        await EnsureTemplateAsync(db, provider, logger);
        await EnsureAgentsPerTenantAsync(db, tenant, provider, logger);
    }

    private static async Task EnsureTemplateAsync(PropiaDbContext db, AiProvider provider, ILogger logger)
    {
        var existente = await db.AiAgentTemplates.FirstOrDefaultAsync(t => t.Name == AuxiliarAdministrativoAgente.Nombre);
        if (existente is not null)
        {
            if (existente.SystemPrompt != AuxiliarAdministrativoAgente.SystemPrompt)
            {
                existente.SystemPrompt = AuxiliarAdministrativoAgente.SystemPrompt;
                existente.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
                logger.LogWarning("[SEED] AuxiliarAdministrativoSeeder: prompt de la plantilla actualizado.");
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
            IsActive = true,
            IncludeInOnboarding = true,
            SortOrder = 60,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        logger.LogWarning("[SEED] AuxiliarAdministrativoSeeder: plantilla global creada (provider {Provider}).", provider);
    }

    private static async Task EnsureAgentsPerTenantAsync(PropiaDbContext db, ITenantContext tenant, AiProvider provider, ILogger logger)
    {
        var tenantIds = await db.Tenants.AsNoTracking().Select(t => t.Id).ToListAsync();
        var creados = 0;
        foreach (var tid in tenantIds)
        {
            try
            {
                tenant.SetTenant(tid);
                await db.Database.CloseConnectionAsync(); // el interceptor aplica app.tenant_id al reabrir

                var existente = await db.AiAgents.FirstOrDefaultAsync(
                    a => a.Name == AuxiliarAdministrativoAgente.Nombre || a.Role == AuxiliarAdministrativoAgente.RoleTag);
                if (existente is not null)
                {
                    if (existente.SystemPrompt != AuxiliarAdministrativoAgente.SystemPrompt)
                    {
                        existente.SystemPrompt = AuxiliarAdministrativoAgente.SystemPrompt;
                        existente.UpdatedAt = DateTimeOffset.UtcNow;
                        await db.SaveChangesAsync();
                    }
                    continue;
                }

                db.AiAgents.Add(new AiAgent
                {
                    Id = Guid.NewGuid(),
                    TenantId = tid,
                    Name = AuxiliarAdministrativoAgente.Nombre,
                    Role = AuxiliarAdministrativoAgente.RoleTag,
                    Provider = provider,
                    Model = null,
                    SystemPrompt = AuxiliarAdministrativoAgente.SystemPrompt,
                    IsActive = true,
                    SortOrder = 60,
                    CreatedAt = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync();
                creados++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[SEED] AuxiliarAdministrativoSeeder: no se pudo crear el agente en tenant {TenantId}", tid);
                db.ChangeTracker.Clear();
            }
        }
        tenant.Clear();
        if (creados > 0)
            logger.LogWarning("[SEED] AuxiliarAdministrativoSeeder: agente creado en {N} copropiedad(es).", creados);
    }
}
