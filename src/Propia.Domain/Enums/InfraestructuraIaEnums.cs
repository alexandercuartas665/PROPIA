namespace Propia.Domain.Enums;

/// <summary>
/// Estado operativo de una linea WhatsApp de la copropiedad (Infraestructura IA, Capa 2).
/// Portado de CUBOT.travels (modulo 1.4). La conexion real (QR, sesion) la gestiona el
/// Evolution Connector; aqui se modela el ciclo de vida y el estado.
/// </summary>
public enum WhatsAppLineStatus
{
    Created = 0,
    Connecting = 1,
    Connected = 2,
    Disconnected = 3,
    Failed = 4,
    Disabled = 5
}

/// <summary>
/// Proveedor de una linea WhatsApp. Evolution = API no oficial por QR (servidor maestro).
/// Cloud = WhatsApp Cloud API oficial de Meta (token + phone number id). Portado de CUBOT.travels.
/// </summary>
public enum WhatsAppProvider
{
    Evolution = 1,
    Cloud = 2
}

/// <summary>Direccion de un mensaje de chat. Portado de CUBOT.travels.</summary>
public enum MessageDirection
{
    Inbound = 0,
    Outbound = 1
}

/// <summary>Tipo de contenido de un mensaje de chat. Portado de CUBOT.travels.</summary>
public enum MessageMediaType
{
    None = 0,
    Image = 1,
    Video = 2,
    Audio = 3,
    Document = 4,
    Location = 5
}

/// <summary>
/// Tipo de recurso que un agente de IA puede usar para entregar informacion al contacto.
/// Portado de CUBOT.travels.
/// </summary>
public enum AgentResourceType
{
    Image = 0,
    Video = 1,
    Audio = 2,
    Pdf = 3,
    Location = 4,
    Text = 5
}

/// <summary>
/// Disparador de una regla de automatizacion (Infraestructura IA). Re-mapeado de CUBOT.travels
/// (que era de ventas: Lead/Pipeline) al dominio de copropiedades. HOY solo ChatSinRespuesta esta
/// implementado end-to-end; el resto es scaffolding (definido en UI + persistencia, sin ejecucion aun).
/// </summary>
public enum AutomationTrigger
{
    /// <summary>Un mensaje entrante lleva N minutos sin respuesta (ThresholdMinutes). [IMPLEMENTADO]</summary>
    ChatSinRespuesta = 0,
    /// <summary>Un PQRSD abierto lleva N minutos sin gestion. [proximamente]</summary>
    PqrsSinRespuesta = 1,
    /// <summary>Una tarea paso su fecha de vencimiento. [proximamente]</summary>
    TareaVencida = 2,
    /// <summary>Dentro de una franja horaria (TimeWindowStart/End). [proximamente]</summary>
    VentanaHoraria = 3
}

/// <summary>
/// Accion que dispara una regla de automatizacion. HOY solo NotificarAdministracion esta
/// implementada (via el motor de notificaciones T.2). El resto es scaffolding.
/// </summary>
public enum AutomationAction
{
    /// <summary>Notifica a la administracion (usuarios Administrador del tenant) via T.2. [IMPLEMENTADO]</summary>
    NotificarAdministracion = 0,
    /// <summary>Crea una tarea en el tablero (usa TareaTitulo). [proximamente]</summary>
    CrearTarea = 1,
    /// <summary>Envia un mensaje automatico al chat (usa MensajePlantilla). [proximamente]</summary>
    AutoResponderChat = 2
}

/// <summary>
/// Tipo de evento en la bitacora de atencion del agente de IA (capa 3). La escribe el
/// AgentDispatcher a medida que atiende cada conversacion. Portado de CUBOT.travels.
/// </summary>
public enum AiAgentRunLogKind
{
    /// <summary>Mensaje entrante del cliente recibido por la linea.</summary>
    Inbound = 0,
    /// <summary>Prompt enviado al proveedor de IA (principal o extractor de cache).</summary>
    Prompt = 1,
    /// <summary>El modelo ejecuto una herramienta MCP (o efecto colateral del turno).</summary>
    Tool = 2,
    /// <summary>Respuesta del agente enviada al cliente por la linea.</summary>
    Reply = 3,
    /// <summary>Nota informativa del proceso (skip por lista negra, contexto enviado, etc.).</summary>
    Info = 4,
    /// <summary>Error durante la atencion.</summary>
    Error = 5
}
