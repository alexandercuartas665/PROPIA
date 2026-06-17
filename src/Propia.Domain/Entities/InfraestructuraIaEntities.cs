using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

// =====================================================================================
// Infraestructura IA (Capa 2 - operativa de copropiedad). Portado de CUBOT.travels.
// TODAS las entidades son TENANT-SCOPED (heredan TenantEntity, llevan tenant_id, RLS).
// Se removio el acoplamiento CRM de CUBOT (Lead/Pipeline/Stage). Las API keys de IA y
// WhatsApp NO viven aqui: se resuelven desde la config global de SuperAdmin
// (AiProviderConfig / EvolutionMasterConfig). El operador de la PH solo elige proveedor.
// =====================================================================================

/// <summary>
/// Linea/instancia WhatsApp de la copropiedad. La conexion real (QR, sesion) la gestiona el
/// Evolution Connector contra el servidor maestro (EvolutionMasterConfig, SuperAdmin); aqui se
/// modela el ciclo de vida, el estado y la asignacion operativa a un usuario de la PH.
/// </summary>
public class WhatsAppLine : TenantEntity
{
    /// <summary>Nombre de la instancia en Evolution (unico por tenant).</summary>
    public string InstanceName { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public WhatsAppLineStatus Status { get; set; } = WhatsAppLineStatus.Created;

    /// <summary>Usuario de la copropiedad (UsuarioTenant) responsable de la linea. Opcional.</summary>
    public Guid? AssignedToUsuarioTenantId { get; set; }

    public DateTimeOffset? LastConnectedAt { get; set; }
    public DateTimeOffset? LastStatusAt { get; set; }
}

/// <summary>
/// Conversacion de WhatsApp con un contacto. Una por (TenantId, ContactPhone).
/// Se removio el LeadId de CUBOT. Opcionalmente se enlaza a una Persona del directorio (2.4).
/// </summary>
public class Conversation : TenantEntity
{
    public string ContactPhone { get; set; } = null!;
    public string? ContactName { get; set; }

    /// <summary>Linea por la que entra/sale la conversacion. Opcional.</summary>
    public Guid? WhatsAppLineId { get; set; }
    public WhatsAppLine? WhatsAppLine { get; set; }

    /// <summary>Enlace opcional al residente/contacto del directorio 2.4.</summary>
    public Guid? PersonaId { get; set; }
    public Persona? Persona { get; set; }

    public DateTimeOffset? LastMessageAt { get; set; }

    public ICollection<Message> Messages { get; set; } = new List<Message>();
}

/// <summary>
/// Mensaje de una conversacion. ExternalId (id de Evolution) da idempotencia a la ingesta
/// entrante: indice unico (TenantId, ExternalId).
/// </summary>
public class Message : TenantEntity
{
    public Guid ConversationId { get; set; }
    public Conversation? Conversation { get; set; }

    public MessageDirection Direction { get; set; }

    /// <summary>Id externo (Evolution). Null en salientes propios antes de confirmar.</summary>
    public string? ExternalId { get; set; }

    public string Body { get; set; } = "";
    public string MessageType { get; set; } = "text";
    public DateTimeOffset SentAt { get; set; }

    /// <summary>Usuario de la PH (UsuarioTenant) que envio el saliente. Null en entrantes.</summary>
    public Guid? SentByUsuarioTenantId { get; set; }

    /// <summary>Nombre del usuario que respondio (snapshot). Null en entrantes.</summary>
    public string? SentByName { get; set; }

    public MessageMediaType MediaType { get; set; } = MessageMediaType.None;

    /// <summary>URL servible del adjunto (IBlobStorage) o, para ubicacion, "lat,lng".</summary>
    public string? MediaUrl { get; set; }

    public string? MediaMimeType { get; set; }
}

/// <summary>
/// Agente de IA configurable de la copropiedad. Define proveedor, modelo, prompt de sistema y si
/// esta en produccion. Los recursos (AiAgentResource) son los archivos/datos que el agente puede
/// usar para responder. La API key NO se guarda aqui (viene de AiProviderConfig global).
/// </summary>
public class AiAgent : TenantEntity
{
    public string Name { get; set; } = null!;

    /// <summary>Rol/tipo descriptivo (copiloto, clasificador, etc.). Libre.</summary>
    public string? Role { get; set; }

    public AiProvider Provider { get; set; } = AiProvider.Claude;

    /// <summary>Modelo concreto del proveedor (opcional; si vacio se usa el por defecto global).</summary>
    public string? Model { get; set; }

    public string SystemPrompt { get; set; } = "";

    public bool IsActive { get; set; }

    public int SortOrder { get; set; }

    /// <summary>
    /// Red de seguridad: ultimas 5 versiones del prompt base + enrutados. Cada UpdateAsync hace push
    /// al inicio del array. Si el usuario borra/edita algo por error puede restaurar una version
    /// anterior. Portado de CUBOT.travels.
    /// </summary>
    public string? PromptHistoryJson { get; set; }

    public ICollection<AiAgentPrompt> Prompts { get; set; } = new List<AiAgentPrompt>();
    public ICollection<AiAgentResource> Resources { get; set; } = new List<AiAgentResource>();
}

/// <summary>
/// Prompt enrutado de un agente. Un agente tiene un prompt base (AiAgent.SystemPrompt) y
/// opcionalmente varios prompts con nombre + regla; el enrutador aplica el que coincide.
/// </summary>
public class AiAgentPrompt : TenantEntity
{
    public Guid AgentId { get; set; }
    public AiAgent? Agent { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Regla/criterio en lenguaje natural que indica cuando usar este prompt.</summary>
    public string? Rule { get; set; }

    public string Body { get; set; } = "";

    public int SortOrder { get; set; }
}

/// <summary>
/// Recurso que un agente puede usar para entregar informacion al contacto (imagen/video/audio/
/// pdf/ubicacion/texto), con un detalle y opcionalmente un archivo cargado via IBlobStorage.
/// </summary>
public class AiAgentResource : TenantEntity
{
    public Guid AgentId { get; set; }
    public AiAgent? Agent { get; set; }

    public string Name { get; set; } = null!;
    public AgentResourceType ResourceType { get; set; } = AgentResourceType.Text;

    /// <summary>Detalle/descripcion (o el texto si es Text; o "lat,lng" si Location).</summary>
    public string? Detail { get; set; }

    /// <summary>URL servible del archivo (IBlobStorage). Null para texto/ubicacion.</summary>
    public string? FileUrl { get; set; }

    public string? FileName { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>
/// Herramienta MCP habilitada para un agente. Cada fila = (agente, conexion MCP, tool) que el
/// agente PUEDE usar. La ausencia de fila = no habilitada. El catalogo de conexiones es estatico
/// (McpConnectionCatalog) y la lista de tools se descubre en vivo del servidor MCP. El agente
/// nunca puede usar mas que estas tools, y al ejecutarlas hereda el tenant + permisos del usuario.
/// </summary>
public class AiAgentMcpTool : TenantEntity
{
    public Guid AgentId { get; set; }
    public AiAgent? Agent { get; set; }

    /// <summary>Codigo de la conexion MCP (ej. "copropiedades"). Ver McpConnectionCatalog.</summary>
    public string ConnectionCode { get; set; } = null!;

    /// <summary>Nombre de la tool tal cual la expone el servidor MCP (ej. "micopropiedad_resumen").</summary>
    public string ToolName { get; set; } = null!;
}

/// <summary>
/// Registro de consumo de IA de la copropiedad. TODA ejecucion (prueba, chat, automatizacion)
/// deja un registro con proveedor, modelo, tokens y costo estimado. Base para control de plan
/// (LimiteLlamadasIaMensual) y para el modulo de Metricas.
/// </summary>
public class AiUsageLog : TenantEntity
{
    /// <summary>Agente que origino el consumo (null si fue ejecucion sin agente concreto).</summary>
    public Guid? AgentId { get; set; }

    public AiProvider Provider { get; set; }
    public string Model { get; set; } = "";

    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }

    /// <summary>Costo estimado en USD (tabla de tarifas aproximada por proveedor).</summary>
    public decimal EstimatedCostUsd { get; set; }

    /// <summary>Origen del consumo: test, chat, automation.</summary>
    public string Source { get; set; } = "chat";

    public bool Success { get; set; }
}
