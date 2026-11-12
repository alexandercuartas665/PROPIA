using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Crea o actualiza el agente de UNA copropiedad a partir de su plantilla del Super Admin. Lo comparten el
/// onboarding (DeployToTenantAsync), "Actualizar copropiedades" y la creacion al primer uso de los agentes
/// del sistema, para que los tres dejen el agente igual: prompt con los placeholders reemplazados, tools
/// de la plantilla, vinculo + version + huella del prompt aplicado.
/// </summary>
internal static class PlantillaAgenteFabrica
{
    /// <summary>Agente nuevo (activo) con las tools de la plantilla que aplican a copropiedades.</summary>
    public static (AiAgent Agente, List<AiAgentMcpTool> Tools) Nuevo(AiAgentTemplate t, Guid tenantId, string copropiedad, string? organizacion)
    {
        var prompt = PlantillaAgente.Render(t.SystemPrompt, copropiedad, organizacion);
        var agent = new AiAgent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            // El nombre admite placeholders (ej. "Asistente de {COPROPIEDAD_NOMBRE}").
            Name = PlantillaAgente.Render(t.Name, copropiedad, organizacion),
            Role = t.Role,
            Provider = t.Provider,
            Model = t.Model,
            SystemPrompt = prompt,
            IsActive = true,
            SortOrder = t.SortOrder,
            CreatedAt = DateTimeOffset.UtcNow,
            TemplateId = t.Id,
            TemplateVersion = t.Version,
            TemplatePromptHash = PlantillaAgente.Hash(prompt)
        };
        return (agent, ToolsFaltantes(t, agent, Array.Empty<AiAgentMcpTool>()));
    }

    /// <summary>Tools de la plantilla que el agente aun no tiene (las de plataforma nunca van a copropiedades).</summary>
    public static List<AiAgentMcpTool> ToolsFaltantes(AiAgentTemplate t, AiAgent agent, IEnumerable<AiAgentMcpTool> actuales)
    {
        var ya = actuales.Select(x => (x.ConnectionCode, x.ToolName)).ToHashSet();
        return t.McpTools
            .Where(x => McpConnectionCatalog.Find(x.ConnectionCode)?.PlatformOnly != true && !ya.Contains((x.ConnectionCode, x.ToolName)))
            .Select(x => new AiAgentMcpTool
            {
                Id = Guid.NewGuid(),
                TenantId = agent.TenantId,
                AgentId = agent.Id,
                ConnectionCode = x.ConnectionCode,
                ToolName = x.ToolName,
                CreatedAt = DateTimeOffset.UtcNow
            }).ToList();
    }

    /// <summary>True si la copropiedad edito el prompt despues de que la plantilla se lo puso.</summary>
    public static bool Editado(AiAgent a)
        => a.TemplatePromptHash is not null && PlantillaAgente.Hash(a.SystemPrompt) != a.TemplatePromptHash;
}
