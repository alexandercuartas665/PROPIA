using Propia.Domain.Enums;

namespace Propia.Application.InfraestructuraIa;

/// <summary>Vista de una plantilla con sus tools (admin del Super Admin).</summary>
public sealed record AiAgentTemplateDto(
    Guid Id,
    string Name,
    string? Role,
    string? Description,
    AiProvider Provider,
    string? Model,
    string SystemPrompt,
    bool IsActive,
    bool IncludeInOnboarding,
    int SortOrder,
    List<AiAgentTemplateToolDto> Tools);

public sealed record AiAgentTemplateToolDto(string ConnectionCode, string ToolName);

/// <summary>Agente existente en un tenant, ofrecido como fuente para crear una plantilla.</summary>
public sealed record ImportableAgentDto(
    Guid AgentId,
    string AgentName,
    string? Role,
    AiProvider Provider,
    string? Model,
    Guid TenantId,
    string TenantNombre,
    int ToolsCount,
    bool IsActive);

public sealed record SaveAiAgentTemplateRequest(
    string Name,
    string? Role,
    string? Description,
    AiProvider Provider,
    string? Model,
    string SystemPrompt,
    bool IsActive,
    bool IncludeInOnboarding,
    int SortOrder,
    List<AiAgentTemplateToolDto> Tools);

/// <summary>
/// Gestiona plantillas globales de agente IA (Super Admin) y su despliegue automatico a tenants
/// nuevos al final del wizard 2.1. Patron: el admin crea N plantillas, marca las que deben
/// formar parte del onboarding, y cuando un cliente activa su copropiedad, esas plantillas se
/// copian como AiAgent + AiAgentMcpTool reales bajo el tenant nuevo.
/// </summary>
public interface IAiAgentTemplateService
{
    Task<List<AiAgentTemplateDto>> GetListAsync(CancellationToken ct = default);
    Task<AiAgentTemplateDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<AiAgentTemplateDto> SaveAsync(Guid? id, SaveAiAgentTemplateRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid actorId, string actorEmail, string? ip, CancellationToken ct = default);

    /// <summary>Lista agentes activos de TODOS los tenants (bypass RLS). Para el modal "importar de tenant".</summary>
    Task<List<ImportableAgentDto>> ListImportSourcesAsync(CancellationToken ct = default);

    /// <summary>
    /// Crea una plantilla nueva a partir de un agente existente en algun tenant. Copia todos los campos
    /// (Name, Provider, Model, SystemPrompt, tools), aplica reverse-placeholder substitution sobre el
    /// SystemPrompt (reemplaza el nombre del tenant por {COPROPIEDAD_NOMBRE} y el nombre de la
    /// organizacion por {ORGANIZACION_NOMBRE}) para que la plantilla sea reusable en otros tenants.
    /// Por defecto crea la plantilla IsActive=true, IncludeInOnboarding=false (el admin decide despues).
    /// </summary>
    Task<AiAgentTemplateDto> ImportFromAgentAsync(Guid agentId, Guid actorId, string actorEmail, string? ip, CancellationToken ct = default);

    /// <summary>
    /// Despliega TODAS las plantillas con IsActive=true e IncludeInOnboarding=true al tenant indicado.
    /// Debe llamarse DESPUES de que el caller haya seteado app.tenant_id via set_config (para que RLS
    /// permita el INSERT en ai_agents y ai_agent_mcp_tools). Best-effort: si una plantilla falla,
    /// las siguientes lo intentan; devuelve la cantidad de plantillas desplegadas exitosamente.
    /// Aplica reemplazo de placeholders {COPROPIEDAD_NOMBRE} y {ORGANIZACION_NOMBRE} en SystemPrompt.
    /// </summary>
    Task<int> DeployToTenantAsync(Guid tenantId, string copropiedadNombre, string? organizacionNombre, CancellationToken ct = default);
}
