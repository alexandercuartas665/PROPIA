using Propia.Domain.Enums;

namespace Propia.Application.InfraestructuraIa;

/// <summary>Gestion de lineas WhatsApp de la copropiedad activa (tenant-scoped, RLS).</summary>
public interface IWhatsAppLineService
{
    Task<IReadOnlyList<WhatsAppLineDto>> ListAsync(CancellationToken ct = default);

    /// <summary>Crea una linea. Lanza InvalidOperationException si se excede LimiteLineasWhatsapp del plan.</summary>
    Task<WhatsAppLineDto?> CreateAsync(CreateWhatsAppLineRequest request, CancellationToken ct = default);

    Task<WhatsAppLineDto?> ChangeStatusAsync(Guid lineId, WhatsAppLineStatus status, CancellationToken ct = default);

    /// <summary>Asigna (o desasigna con null) la linea a un UsuarioTenant de la copropiedad.</summary>
    Task<WhatsAppLineDto?> AssignAsync(Guid lineId, Guid? usuarioTenantId, CancellationToken ct = default);
}

/// <summary>
/// Conecta las lineas WhatsApp de la copropiedad con el servidor Evolution MAESTRO de la
/// plataforma (configurado en SuperAdmin). Crea instancias, entrega QR, refresca estado y desconecta.
/// </summary>
public interface IWhatsAppConnectorService
{
    /// <summary>true si el servidor maestro Evolution esta configurado (base url + api key).</summary>
    Task<bool> MasterReadyAsync(CancellationToken ct = default);

    /// <summary>Crea/recupera la instancia de la linea en Evolution y devuelve el QR.</summary>
    Task<LineConnectResult> ConnectLineAsync(Guid lineId, CancellationToken ct = default);

    /// <summary>Consulta el estado real en Evolution y actualiza la linea. Null si no existe.</summary>
    Task<WhatsAppLineDto?> RefreshAsync(Guid lineId, CancellationToken ct = default);

    /// <summary>Cierra sesion y elimina la instancia; deja la linea desconectada.</summary>
    Task<bool> DisconnectAsync(Guid lineId, CancellationToken ct = default);

    /// <summary>Envia un mensaje de prueba desde la linea a un numero (con codigo de pais).</summary>
    Task<LineSendResult> SendTestAsync(Guid lineId, string phone, string text, CancellationToken ct = default);
}

/// <summary>Gestion de agentes de IA de la copropiedad: proveedor, prompt, encendido, recursos y prompts enrutados.</summary>
public interface IAiAgentService
{
    Task<IReadOnlyList<AiAgentDto>> ListAsync(CancellationToken ct = default);
    Task<AiAgentDetailDto?> GetAsync(Guid id, CancellationToken ct = default);

    Task<AiAgentDto?> CreateAsync(CreateAiAgentRequest request, CancellationToken ct = default);
    Task<AiAgentDto?> UpdateAsync(Guid id, UpdateAiAgentRequest request, CancellationToken ct = default);
    Task<AiAgentDto?> SetActiveAsync(Guid id, bool active, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    Task<AiAgentResourceDto?> AddResourceAsync(CreateAgentResourceRequest request, CancellationToken ct = default);
    Task<AiAgentResourceDto?> UpdateResourceAsync(Guid id, UpdateAgentResourceRequest request, CancellationToken ct = default);
    Task<bool> DeleteResourceAsync(Guid id, CancellationToken ct = default);

    Task<AiAgentPromptDto?> AddPromptAsync(CreateAgentPromptRequest request, CancellationToken ct = default);
    Task<AiAgentPromptDto?> UpdatePromptAsync(Guid id, UpdateAgentPromptRequest request, CancellationToken ct = default);
    Task<bool> DeletePromptAsync(Guid id, CancellationToken ct = default);

    // ----- Tools MCP habilitadas por agente (Capa MCP) -----
    /// <summary>Tools MCP que el agente tiene habilitadas (la seleccion persistida).</summary>
    Task<IReadOnlyList<AgentMcpToolSelection>> GetMcpToolsAsync(Guid agentId, CancellationToken ct = default);

    /// <summary>Reemplaza por completo la seleccion de tools MCP del agente. Devuelve false si el agente no existe.</summary>
    Task<bool> SetMcpToolsAsync(Guid agentId, IReadOnlyList<AgentMcpToolSelection> tools, CancellationToken ct = default);

    // ----- Duplicado + versionado de prompts (portado de CUBOT.travels) -----
    /// <summary>Duplica un agente: copia toda la config (prompts, recursos, MCP tools, PromptHistory) menos
    /// los datos vivos (no se duplican vinculos a lineas). El nuevo agente queda apagado.</summary>
    Task<AiAgentDto?> DuplicateAsync(Guid sourceId, CancellationToken ct = default);

    /// <summary>Lista las ultimas 5 versiones del prompt + enrutados del agente (red de seguridad).</summary>
    Task<IReadOnlyList<AiAgentPromptVersionDto>> GetPromptHistoryAsync(Guid agentId, CancellationToken ct = default);

    /// <summary>Restaura una version anterior del prompt (reemplaza SystemPrompt + AiAgentPrompts).</summary>
    Task<AiAgentDetailDto?> RestorePromptVersionAsync(Guid agentId, int versionIndex, CancellationToken ct = default);
}

/// <summary>Inferencia de IA (probar un agente). Resuelve credenciales del proveedor desde la config global de SuperAdmin.</summary>
public interface IAiInferenceService
{
    /// <summary>
    /// Prueba un agente. Si el agente tiene tools MCP habilitadas y se pasa bearerToken (el JWT
    /// del usuario), corre el loop de function-calling ejecutando las tools con ese token (hereda
    /// tenant + permisos). Sin tools o sin token: completado simple de un turno.
    /// </summary>
    Task<AiChatResult> TestChatAsync(Guid agentId, IReadOnlyList<AiChatTurn> turns, string? systemPromptOverride = null, string? bearerToken = null, string? contactPhone = null, Guid? conversationId = null, CancellationToken ct = default);
}

/// <summary>Registro y reporte de consumo de IA de la copropiedad + control de cuota del plan.</summary>
public interface IAiUsageService
{
    Task RecordAsync(Guid? agentId, AiProvider provider, string model, int inputTokens, int outputTokens, string source, bool success, CancellationToken ct = default);
    Task<AiUsageSummaryDto> GetSummaryAsync(CancellationToken ct = default);
    Task<AiQuotaDto> GetQuotaAsync(CancellationToken ct = default);
}

// ---------- Clientes HTTP externos (impl en Infrastructure) ----------

/// <summary>Cliente HTTP del servidor Evolution API (WhatsApp). La API key va en el header "apikey".</summary>
public interface IEvolutionApiClient
{
    Task<EvolutionPingResult> CheckAsync(string baseUrl, string apiKey, CancellationToken ct = default);
    Task<EvolutionInstanceResult> CreateInstanceAsync(string baseUrl, string apiKey, string instanceName, CancellationToken ct = default);
    Task<EvolutionInstanceResult> ConnectAsync(string baseUrl, string apiKey, string instanceName, CancellationToken ct = default);
    Task<EvolutionInstanceResult> GetStateAsync(string baseUrl, string apiKey, string instanceName, CancellationToken ct = default);
    Task<bool> DeleteInstanceAsync(string baseUrl, string apiKey, string instanceName, CancellationToken ct = default);
    Task<EvolutionSendResult> SendTextAsync(string baseUrl, string apiKey, string instanceName, string phone, string text, CancellationToken ct = default);
    Task<EvolutionSendResult> SetWebhookAsync(string baseUrl, string apiKey, string instanceName, string webhookUrl, string token, CancellationToken ct = default);
}

/// <summary>Cliente HTTP de inferencia para los proveedores de IA (Claude, Gemini, OpenAI, DeepSeek).</summary>
public interface IAiProviderClient
{
    Task<AiChatResult> CompleteAsync(AiProvider provider, string apiKey, string? baseUrl, string model,
        string systemPrompt, IReadOnlyList<AiChatTurn> turns, CancellationToken ct = default);

    /// <summary>
    /// Una ronda de completado CON tools (function-calling). Devuelve texto final o la lista de
    /// tools que el modelo quiere ejecutar. El orquestador (AiInferenceService) las ejecuta y
    /// vuelve a llamar con los resultados en el historial. Soporta OpenAI/DeepSeek/Gemini
    /// (formato OpenAI) y Claude (formato nativo).
    /// </summary>
    Task<AiCompletion> CompleteWithToolsAsync(AiProvider provider, string apiKey, string? baseUrl, string model,
        string systemPrompt, IReadOnlyList<AiToolMessage> messages, IReadOnlyList<AiToolSpec> tools, CancellationToken ct = default);
}
