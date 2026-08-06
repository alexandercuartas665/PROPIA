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

    // === Proveedor (portado de CUBOT.travels) =================================
    /// <summary>Proveedor de la linea. Inmutable despues del alta.</summary>
    public WhatsAppProvider Provider { get; set; } = WhatsAppProvider.Evolution;

    // === Credenciales Meta Cloud (solo cuando Provider == Cloud) ==============
    /// <summary>ID del numero de telefono en Meta (consola WhatsApp Manager).</summary>
    public string? CloudPhoneNumberId { get; set; }

    /// <summary>ID de la WhatsApp Business Account (WABA) en Meta.</summary>
    public string? CloudBusinessAccountId { get; set; }

    /// <summary>Access token de la app de Meta, cifrado con DataProtection (ISecretProtector).</summary>
    public string? CloudAccessTokenEncrypted { get; set; }

    /// <summary>Verify token del webhook de Meta (cifrado). Usado en el handshake GET de /webhooks/meta.</summary>
    public string? CloudWebhookVerifyTokenEncrypted { get; set; }
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

    /// <summary>Conversacion archivada por el operador. Null = activa. Si llega un mensaje entrante se desarchiva sola.</summary>
    public DateTimeOffset? ArchivedAt { get; set; }

    /// <summary>Punto de reinicio del contexto del agente IA: el dispatcher ignora turnos previos a esta fecha.</summary>
    public DateTimeOffset? AgentContextResetAt { get; set; }

    /// <summary>
    /// Toma de control humana: si tiene valor, el agente IA NO responde automaticamente esta
    /// conversacion (un operador la esta atendiendo desde la bandeja). Se setea sola cuando un
    /// humano envia un mensaje manual, y se limpia al "reiniciar contexto" (el bot retoma). Null
    /// = el agente atiende normalmente.
    /// </summary>
    public DateTimeOffset? IaPausadaAt { get; set; }

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

    /// <summary>Emoji con el que el agente reacciono a este entrante (null = sin reaccion). Una sola por mensaje.</summary>
    public string? Reaction { get; set; }
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

    /// <summary>
    /// Reacciones automaticas (emoji) a los mensajes del contacto, SIN pasar por el LLM (cero tokens).
    /// El dispatcher pone un emoji al azar a algunos entrantes. Portado de CUBOT.travels.
    /// </summary>
    public bool ReactionsEnabled { get; set; }

    /// <summary>Numerador de la frecuencia (ej. 3 de cada 4 -> N=3). Se aplica como probabilidad.</summary>
    public int ReactionRatioN { get; set; } = 3;

    /// <summary>Denominador (ej. 3 de cada 4 -> M=4).</summary>
    public int ReactionRatioM { get; set; } = 4;

    /// <summary>Emojis para reaccionar al azar, separados por coma.</summary>
    public string? ReactionEmojis { get; set; }

    /// <summary>
    /// Si es true, el PRIMER saliente de cada conversacion se envia como mensaje de IMAGEN
    /// (logo de la copropiedad, fallback foto de fachada) con el texto del LLM como caption.
    /// Los demas turnos van como texto normal. Requiere que el tenant tenga LogoUrl/FotoFachadaUrl.
    /// </summary>
    public bool SaludarConLogo { get; set; }

    public ICollection<AiAgentPrompt> Prompts { get; set; } = new List<AiAgentPrompt>();
    public ICollection<AiAgentResource> Resources { get; set; } = new List<AiAgentResource>();
}

/// <summary>
/// Vinculo agente IA &lt;-&gt; linea WhatsApp. Cuando una linea esta atendida (IsConnected) por un agente,
/// cada entrante de esa linea se contesta automaticamente segun el prompt y los recursos del agente.
/// Una linea solo puede estar atendida por un agente a la vez (indice unico filtrado). Portado de
/// CUBOT.travels. El loop de auto-respuesta lo ejecuta el dispatcher (separado).
/// </summary>
public class AiAgentLineBinding : TenantEntity
{
    public Guid AgentId { get; set; }
    public AiAgent? Agent { get; set; }

    public Guid WhatsAppLineId { get; set; }
    public WhatsAppLine? WhatsAppLine { get; set; }

    /// <summary>true = la linea esta atendida por este agente. false = historico inactivo (no recibe trafico).</summary>
    public bool IsConnected { get; set; } = true;

    /// <summary>true = el agente envia la respuesta automaticamente. false = queda como sugerencia para un asesor.</summary>
    public bool AutoConfirm { get; set; } = true;
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
/// Definicion de un dato que el agente debe ir capturando durante la conversacion (datos cache, capa 3).
/// Ej.: "numero de apartamento", "nombre del residente", "tipo de solicitud". El motor de inferencia
/// intenta llenarlo a partir de los mensajes. Portado de CUBOT.travels (sin el mapeo a CRM).
/// </summary>
public class AiAgentCacheField : TenantEntity
{
    public Guid AgentId { get; set; }
    public AiAgent? Agent { get; set; }

    /// <summary>Clave del dato (slug derivado del nombre). Unica por agente.</summary>
    public string FieldKey { get; set; } = null!;

    /// <summary>Nombre visible del dato (ej. "Numero de apartamento").</summary>
    public string Label { get; set; } = null!;

    /// <summary>De que trata el dato; le sirve al motor para extraerlo del texto del residente.</summary>
    public string? Description { get; set; }

    public int SortOrder { get; set; }

    /// <summary>
    /// Si true, el motor puede sobrescribir el valor durante la conversacion (ej. cambia de unidad).
    /// Si false (sticky), una vez capturado queda fijo y no se actualiza.
    /// </summary>
    public bool IsUpdatable { get; set; } = true;
}

/// <summary>
/// Valor capturado para un campo de cache durante una sesion de conversacion (datos cache, capa 3).
/// SessionId = AgentId en el playground de pruebas, o ConversationId en el chat real. Portado de CUBOT.
/// </summary>
public class AiAgentCacheValue : TenantEntity
{
    public Guid AgentId { get; set; }
    public AiAgent? Agent { get; set; }

    /// <summary>Identificador de la sesion (AgentId para pruebas, ConversationId en chat real).</summary>
    public Guid SessionId { get; set; }

    /// <summary>Clave del dato; coincide con AiAgentCacheField.FieldKey.</summary>
    public string FieldKey { get; set; } = null!;

    /// <summary>Valor capturado por el motor de inferencia.</summary>
    public string? Value { get; set; }

    /// <summary>Fuente del dato ("manual", "inference", "system"). Para auditoria.</summary>
    public string? Source { get; set; }
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

/// <summary>
/// Bitacora de atencion del agente de IA (capa 3). Entidad TENANT-SCOPED (RLS). Persiste, por
/// conversacion, el rastro del proceso del dispatcher: mensaje recibido, prompts enviados a la IA,
/// herramientas ejecutadas y respuesta enviada. Equivale al panel "PROMPTS enviados a la IA" del
/// chat de prueba, pero guardado para revisar la atencion REAL de cada residente. La escribe el
/// AgentDispatcher (best-effort). Portado de CUBOT.travels.
/// </summary>
public class AiAgentRunLog : TenantEntity
{
    public Guid ConversationId { get; set; }
    public Guid AgentId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
    public AiAgentRunLogKind Kind { get; set; }

    /// <summary>Titulo corto del evento (ej. "Mensaje recibido", "Respuesta enviada").</summary>
    public string Title { get; set; } = null!;

    /// <summary>Contenido principal (prompt enviado, texto recibido, etc.).</summary>
    public string? Content { get; set; }

    /// <summary>Respuesta asociada (texto del LLM, resultado de la herramienta, detalle del error).</summary>
    public string? Response { get; set; }
}

/// <summary>
/// Regla de automatizacion (Infraestructura IA). Un disparador + una accion, encendible/apagable.
/// Re-mapeada de CUBOT.travels al dominio de copropiedades (sin Lead/Pipeline/Wompi). Entidad
/// TENANT-SCOPED (RLS). El motor (AutomationService.RunNowAsync + AutomacionesJob) evalua las
/// reglas activas. HOY solo la combinacion ChatSinRespuesta -> NotificarAdministracion se ejecuta;
/// el resto queda como scaffolding (definible en UI pero marcado "proximamente").
/// </summary>
public class AutomationRule : TenantEntity
{
    public string Name { get; set; } = null!;

    public AutomationTrigger Trigger { get; set; } = AutomationTrigger.ChatSinRespuesta;

    /// <summary>Umbral en minutos (ChatSinRespuesta / PqrsSinRespuesta).</summary>
    public int ThresholdMinutes { get; set; } = 30;

    /// <summary>Inicio de la franja horaria "HH:mm" (VentanaHoraria).</summary>
    public string? TimeWindowStart { get; set; }
    /// <summary>Fin de la franja horaria "HH:mm" (VentanaHoraria).</summary>
    public string? TimeWindowEnd { get; set; }

    public AutomationAction Action { get; set; } = AutomationAction.NotificarAdministracion;

    /// <summary>Texto del mensaje (NotificarAdministracion / AutoResponderChat).</summary>
    public string? MensajePlantilla { get; set; }

    /// <summary>Titulo de la tarea a crear (CrearTarea).</summary>
    public string? TareaTitulo { get; set; }

    public bool IsActive { get; set; }

    public int SortOrder { get; set; }

    /// <summary>Cuantas veces disparo su accion (para KPIs).</summary>
    public int ExecutionCount { get; set; }

    /// <summary>Ultima vez que el motor evaluo/ejecuto esta regla.</summary>
    public DateTimeOffset? LastRunAt { get; set; }
}
