using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Plantilla GLOBAL de agente IA configurada por el Super Admin. NO es tenant-scoped (sin RLS).
/// Cuando se activa una copropiedad nueva via wizard 2.1 paso 5, las plantillas marcadas con
/// IncludeInOnboarding=true se COPIAN al tenant como instancias de AiAgent reales (y sus tools).
///
/// Las plantillas y los agentes de tenant son entidades separadas con lifecycles distintos:
/// la plantilla es un "blueprint" gestionado por la plataforma; los agentes copiados quedan
/// completamente bajo control del tenant (puede editarlos, borrarlos, etc.).
///
/// Placeholders soportados en SystemPrompt al momento de la copia:
///   {COPROPIEDAD_NOMBRE}  - reemplazado por Tenant.Nombre
///   {ORGANIZACION_NOMBRE} - reemplazado por Organizacion.Nombre (si aplica)
/// </summary>
public class AiAgentTemplate : BaseEntity
{
    public string Name { get; set; } = null!;

    /// <summary>Rol/tipo descriptivo (copiloto, clasificador, etc.). Libre.</summary>
    public string? Role { get; set; }

    /// <summary>Descripcion interna del Super Admin (para distinguir plantillas en el panel).</summary>
    public string? Description { get; set; }

    public AiProvider Provider { get; set; } = AiProvider.Claude;

    /// <summary>Modelo concreto del proveedor. Si vacio, el agente copiado usa el por defecto del proveedor.</summary>
    public string? Model { get; set; }

    public string SystemPrompt { get; set; } = "";

    /// <summary>Si la plantilla esta activa (puede deshabilitarse sin borrarla).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Si esta plantilla debe copiarse automaticamente al activar una copropiedad nueva via wizard.
    /// Plantillas con IsActive=false NO se despliegan aunque IncludeInOnboarding=true.
    /// </summary>
    public bool IncludeInOnboarding { get; set; }

    public int SortOrder { get; set; }

    public ICollection<AiAgentTemplateMcpTool> McpTools { get; set; } = new List<AiAgentTemplateMcpTool>();
}

/// <summary>
/// Tool MCP habilitada en una plantilla. Mismo shape que AiAgentMcpTool pero apuntando al template.
/// Cuando se copia la plantilla a un tenant, se crea una fila AiAgentMcpTool por cada una.
/// </summary>
public class AiAgentTemplateMcpTool : BaseEntity
{
    public Guid TemplateId { get; set; }
    public AiAgentTemplate? Template { get; set; }

    public string ConnectionCode { get; set; } = null!;
    public string ToolName { get; set; } = null!;
}
