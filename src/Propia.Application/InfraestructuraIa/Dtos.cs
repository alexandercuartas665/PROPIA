using Propia.Domain.Enums;

namespace Propia.Application.InfraestructuraIa;

// =====================================================================================
// DTOs y contratos del grupo Infraestructura IA (Capa 2, tenant-scoped). Portado de
// CUBOT.travels, adaptado a PROPIA (sin acoplamiento CRM, quota por LimiteLlamadasIaMensual).
// =====================================================================================

// ---------- Lineas WhatsApp ----------

public sealed record WhatsAppLineDto(
    Guid Id,
    string InstanceName,
    string? PhoneNumber,
    WhatsAppLineStatus Status,
    Guid? AssignedToUsuarioTenantId,
    DateTimeOffset? LastConnectedAt,
    DateTimeOffset? LastStatusAt,
    WhatsAppProvider Provider = WhatsAppProvider.Evolution,
    string? CloudPhoneNumberId = null);

public sealed record CreateWhatsAppLineRequest(
    string InstanceName,
    string? PhoneNumber,
    WhatsAppProvider Provider = WhatsAppProvider.Evolution,
    string? CloudPhoneNumberId = null,
    string? CloudBusinessAccountId = null,
    string? CloudAccessToken = null,
    string? CloudWebhookVerifyToken = null);

/// <summary>Resultado de conectar una linea: QR (base64) para escanear o error.</summary>
public sealed record LineConnectResult(bool Ok, string? QrBase64, string? Error);

public sealed record LineSendResult(bool Ok, string? Error);

// ---------- WhatsApp Cloud API (Meta) ----------
public sealed record WhatsAppCloudCredentials(string PhoneNumberId, string AccessToken);
public sealed record WhatsAppCloudCheckResult(bool Ok, string? PhoneNumber, string? VerifiedName, string? Error);
public sealed record WhatsAppCloudSendResult(bool Ok, string? ExternalId, string? Error);
public enum WhatsAppCloudMediaKind { Image, Video, Audio, Document }

// ---------- Agentes de IA ----------

public sealed record AiAgentDto(
    Guid Id,
    string Name,
    string? Role,
    AiProvider Provider,
    string? Model,
    string SystemPrompt,
    bool IsActive,
    int SortOrder,
    int ResourceCount,
    bool ReactionsEnabled = false,
    int ReactionRatioN = 3,
    int ReactionRatioM = 4,
    string? ReactionEmojis = null);

/// <summary>Snapshot de un prompt enrutado en una version historica.</summary>
public sealed record AgentPromptSnapshotDto(string Name, string? Rule, string Body, int SortOrder);

/// <summary>Una version del historial del prompt del agente (ultimas 5).</summary>
public sealed record AiAgentPromptVersionDto(int Index, DateTimeOffset SavedAt, string BasePrompt, List<AgentPromptSnapshotDto> Prompts);

public sealed record AiAgentResourceDto(
    Guid Id,
    Guid AgentId,
    string Name,
    AgentResourceType ResourceType,
    string? Detail,
    string? FileUrl,
    string? FileName,
    int SortOrder);

public sealed record AiAgentPromptDto(
    Guid Id,
    Guid AgentId,
    string Name,
    string? Rule,
    string Body,
    int SortOrder);

public sealed record AiAgentDetailDto(
    AiAgentDto Agent,
    IReadOnlyList<AiAgentResourceDto> Resources,
    IReadOnlyList<AiAgentPromptDto> Prompts);

// Los 5 primeros campos los consume IAiAgentService.CreateAsync (crea el agente apagado). Los campos
// extra (IsActive/SortOrder/Reactions*) solo los honra la API admin (AdminAgentService.CreateAgentAsync),
// que los aplica tras crear; el alta normal (UI) los ignora. Son opcionales -> compatibles hacia atras.
public sealed record CreateAiAgentRequest(string? Name, string? Role, AiProvider Provider, string? Model, string? SystemPrompt,
    bool IsActive = true, int? SortOrder = null,
    bool ReactionsEnabled = false, int ReactionRatioN = 3, int ReactionRatioM = 4, string? ReactionEmojis = null);
public sealed record UpdateAiAgentRequest(string? Name, string? Role, AiProvider Provider, string? Model, string? SystemPrompt,
    bool ReactionsEnabled = false, int ReactionRatioN = 3, int ReactionRatioM = 4, string? ReactionEmojis = null);

/// <summary>Body del PUT admin de tools MCP: reemplaza la seleccion completa del agente. Cada key es el
/// nombre de una tool de la conexion "copropiedades" (descubierta via tools/list).</summary>
public sealed record SetAgentToolsRequest(IReadOnlyList<string> ToolKeys);

/// <summary>Body del POST admin de vinculo linea-agente.</summary>
public sealed record BindLineRequest(Guid WhatsAppLineId);

/// <summary>Linea WhatsApp para el descubrimiento cross-tenant por API admin (incluye el agente que la atiende).</summary>
public sealed record AdminLineDto(
    Guid Id,
    string Label,
    WhatsAppProvider Provider,
    string? Phone,
    WhatsAppLineStatus Estado,
    Guid? BoundAgentId);

/// <summary>Una fila de "lineas WhatsApp atendidas" en el configurador del agente.</summary>
public sealed record AgentLineBindingRowDto(
    Guid WhatsAppLineId,
    string LineInstanceName,
    string? LinePhoneNumber,
    bool IsBoundHere,
    bool AutoConfirm,
    Guid? BoundAgentId,
    string? BoundAgentName);

/// <summary>Vincula/desvincula una linea a un agente. Connected=false la libera.</summary>
public sealed record SetAgentLineBindingRequest(Guid WhatsAppLineId, bool Connected, bool AutoConfirm = true);

// --- Datos cache del agente (capa 3) ---
/// <summary>Definicion de un dato que el agente debe ir capturando durante la conversacion.</summary>
public sealed record AiAgentCacheFieldDto(Guid Id, Guid AgentId, string FieldKey, string Label, string? Description, int SortOrder, bool IsUpdatable);
public sealed record CreateAgentCacheFieldRequest(string Label, string? Description, bool IsUpdatable = true);
public sealed record UpdateAgentCacheFieldRequest(string Label, string? Description, bool IsUpdatable = true);

/// <summary>Valor percibido por sesion. Sesion = AgentId en pruebas, ConversationId en chat real.</summary>
public sealed record AiAgentCacheValueDto(string FieldKey, string Label, string? Description, string? Value, string? Source, DateTimeOffset? UpdatedAt);
public sealed record SetAgentCacheValueRequest(Guid AgentId, Guid SessionId, string FieldKey, string? Value, string? Source);

public sealed record CreateAgentResourceRequest(Guid AgentId, string? Name, AgentResourceType ResourceType, string? Detail, string? FileUrl, string? FileName);
public sealed record UpdateAgentResourceRequest(string? Name, AgentResourceType ResourceType, string? Detail, string? FileUrl, string? FileName);

public sealed record CreateAgentPromptRequest(Guid AgentId, string? Name, string? Rule, string? Body);
public sealed record UpdateAgentPromptRequest(string? Name, string? Rule, string? Body);

// ---------- Inferencia / chat ----------

/// <summary>Un turno de la conversacion para inferir. Role: "user" o "model".</summary>
public sealed record AiChatTurn(string Role, string Text);

/// <summary>Recurso adjuntable que el agente decidio entregar.</summary>
public sealed record AiChatAttachment(string Name, AgentResourceType ResourceType, string? FileUrl, string? FileName, string? Detail);

/// <summary>Una llamada al LLM registrada para depuracion: que prompt se envio y que respondio.</summary>
public sealed record AiDebugPrompt(string Title, DateTimeOffset SentAt, string Content, string? Response = null);

public sealed record AiChatResult(
    bool Ok,
    string? Text,
    string? Error,
    int InputTokens = 0,
    int OutputTokens = 0,
    IReadOnlyList<AiChatAttachment>? Attachments = null,
    IReadOnlyList<AiDebugPrompt>? DebugPrompts = null);

// ---------- Tool-calling (Fase B: agente como cliente MCP) ----------

/// <summary>Definicion de una tool que se le ofrece al LLM (nombre, descripcion y JSON Schema de parametros).</summary>
public sealed record AiToolSpec(string Name, string? Description, string ParametersJsonSchema);

/// <summary>Una invocacion de tool que pidio el LLM.</summary>
public sealed record AiToolCall(string Id, string Name, string ArgumentsJson);

/// <summary>
/// Mensaje del hilo de tool-calling. Role: "user" | "assistant" | "tool".
/// - assistant con ToolCalls: el modelo pidio ejecutar tools.
/// - tool: resultado de una tool (ToolCallId + Text).
/// </summary>
public sealed record AiToolMessage(
    string Role,
    string? Text,
    IReadOnlyList<AiToolCall>? ToolCalls = null,
    string? ToolCallId = null,
    string? ToolName = null);

/// <summary>Resultado de una ronda de completado con tools: texto final o tools a ejecutar.</summary>
public sealed record AiCompletion(
    bool Ok,
    string? Text,
    string? Error,
    int InputTokens,
    int OutputTokens,
    IReadOnlyList<AiToolCall> ToolCalls);

// ---------- Consumo / cuota ----------

public sealed record AgentUsageDto(
    Guid? AgentId,
    int Calls,
    long InputTokens,
    long OutputTokens,
    long TotalTokens,
    decimal EstimatedCostUsd);

public sealed record AiUsageSummaryDto(
    int TotalCalls,
    long TotalTokens,
    long InputTokens,
    long OutputTokens,
    decimal EstimatedCostUsd,
    IReadOnlyList<AgentUsageDto> ByAgent);

/// <summary>
/// Cuota mensual de IA del plan de la copropiedad. En PROPIA el limite es por LLAMADAS/mes
/// (Plan.LimiteLlamadasIaMensual); null = ilimitado (Enterprise).
/// </summary>
public sealed record AiQuotaDto(bool HasLimit, int MonthlyLimitCalls, int MonthlyUsedCalls)
{
    public bool Exceeded => HasLimit && MonthlyUsedCalls >= MonthlyLimitCalls;
    public int Remaining => HasLimit ? Math.Max(0, MonthlyLimitCalls - MonthlyUsedCalls) : int.MaxValue;
    public int UsedPct => HasLimit && MonthlyLimitCalls > 0
        ? Math.Min(100, (int)(100L * MonthlyUsedCalls / MonthlyLimitCalls))
        : 0;
}

// ---------- Clientes externos (resultados crudos) ----------

public sealed record EvolutionPingResult(bool Reached, bool Authorized, int? StatusCode, string Message);
public sealed record EvolutionInstanceResult(bool Ok, string? QrBase64, string? State, string? Status, string? Error);
public sealed record EvolutionSendResult(bool Ok, string? Error);
