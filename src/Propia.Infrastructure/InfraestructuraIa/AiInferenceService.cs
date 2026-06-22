using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Integraciones;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Inferencia de IA para probar un agente. Resuelve las credenciales del proveedor desde la config
/// GLOBAL de SuperAdmin (AiProviderConfig, cifrada con ISecretProtector); el tenant solo elige el
/// proveedor/modelo. Aplica control de cuota del plan y entrega de recursos. Portado de CUBOT.travels.
/// </summary>
public sealed class AiInferenceService : IAiInferenceService
{
    private readonly PropiaDbContext _db;
    private readonly ISecretProtector _secret;
    private readonly IAiProviderClient _client;
    private readonly IAiUsageService _usage;
    private readonly IMcpGateway _mcp;
    private readonly ITenantContext _tenant;
    private readonly IListaNegraService _listaNegra;
    private readonly IAiAgentCacheService _cache;

    /// <summary>Maximo de rondas de tool-calling para evitar bucles infinitos del modelo.</summary>
    private const int MaxToolRounds = 6;

    public AiInferenceService(PropiaDbContext db, ISecretProtector secret, IAiProviderClient client, IAiUsageService usage, IMcpGateway mcp, ITenantContext tenant, IListaNegraService listaNegra, IAiAgentCacheService cache)
    {
        _db = db;
        _secret = secret;
        _client = client;
        _usage = usage;
        _mcp = mcp;
        _tenant = tenant;
        _listaNegra = listaNegra;
        _cache = cache;
    }

    private sealed record CacheFieldInfo(string FieldKey, string Label, string? Description, bool IsUpdatable);

    public async Task<AiChatResult> TestChatAsync(Guid agentId, IReadOnlyList<AiChatTurn> turns, string? systemPromptOverride = null, string? bearerToken = null, string? contactPhone = null, Guid? conversationId = null, CancellationToken ct = default)
    {
        var agent = await _db.AiAgents.AsNoTracking().FirstOrDefaultAsync(a => a.Id == agentId, ct);
        if (agent is null) { return new AiChatResult(false, null, "El agente no existe."); }

        // Lista negra: ningun agente debe responder a numeros bloqueados.
        // Portado de CUBOT.travels: chequeo ANTES de gastar tokens.
        if (!string.IsNullOrWhiteSpace(contactPhone))
        {
            if (await _listaNegra.EstaBloqueadoAsync(contactPhone, ct))
            {
                return new AiChatResult(false, null, "El numero esta en la lista negra del tenant. Ningun agente responde.");
            }
        }

        // Reset de contexto: si la conversacion tiene AgentContextResetAt seteado, el dispatcher
        // debe filtrar los turnos enviados a este metodo segun el caller. Aqui solo registramos el
        // momento para futura logica de cache/embedings por sesion.
        if (conversationId.HasValue)
        {
            var resetAt = await _db.Conversations.AsNoTracking()
                .Where(c => c.Id == conversationId.Value)
                .Select(c => c.AgentContextResetAt)
                .FirstOrDefaultAsync(ct);
            // El caller (futuro AgentDispatcher) ya filtra los turns por SentAt > resetAt antes de
            // llamarnos. Aqui no podemos filtrar porque AiChatTurn no tiene timestamp.
            _ = resetAt;
        }

        // La cuenta del proveedor (API key, modelo, base url) la define el SuperAdmin (config global).
        var providerCfg = await _db.AiProviderConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.Provider == agent.Provider, ct);
        if (providerCfg is null || !providerCfg.IsEnabled || string.IsNullOrWhiteSpace(providerCfg.ApiKeyEncrypted))
        {
            return new AiChatResult(false, null, $"El proveedor {agent.Provider} no esta habilitado en la plataforma.");
        }

        string apiKey;
        try { apiKey = _secret.Unprotect(providerCfg.ApiKeyEncrypted); }
        catch { return new AiChatResult(false, null, "La API key del proveedor esta cifrada con una version anterior. Vuelve a guardarla en Servidores de IA."); }

        var meta = AiProviderCatalog.For(agent.Provider);
        var model = !string.IsNullOrWhiteSpace(agent.Model) ? agent.Model!
            : !string.IsNullOrWhiteSpace(providerCfg.Model) ? providerCfg.Model!
            : meta.DefaultModel;

        if (turns.Count == 0) { return new AiChatResult(false, null, "Escribe un mensaje para probar el agente."); }

        // Control de cuota: si el plan tiene limite y ya se agoto el mes, no se ejecuta.
        var quota = await _usage.GetQuotaAsync(ct);
        if (quota.Exceeded)
        {
            return new AiChatResult(false, null, $"Alcanzaste el limite de llamadas de IA de tu plan este mes ({quota.MonthlyLimitCalls:N0}). Actualiza tu plan para seguir usando los agentes.");
        }

        var resources = await _db.AiAgentResources.AsNoTracking()
            .Where(r => r.AgentId == agentId).OrderBy(r => r.SortOrder)
            .Select(r => new AiChatAttachment(r.Name, r.ResourceType, r.FileUrl, r.FileName, r.Detail))
            .ToListAsync(ct);

        // Datos cache: la sesion es el AgentId en el playground, o el ConversationId en chat real.
        var sessionId = conversationId ?? agentId;
        var cacheFields = await _db.AiAgentCacheFields.AsNoTracking()
            .Where(f => f.AgentId == agentId).OrderBy(f => f.SortOrder).ThenBy(f => f.Label)
            .Select(f => new CacheFieldInfo(f.FieldKey, f.Label, f.Description, f.IsUpdatable))
            .ToListAsync(ct);
        var cacheValues = await _db.AiAgentCacheValues.AsNoTracking()
            .Where(v => v.AgentId == agentId && v.SessionId == sessionId)
            .ToDictionaryAsync(v => v.FieldKey, v => v.Value, ct);

        var systemPrompt = await BuildSystemPrompt(agentId, systemPromptOverride ?? agent.SystemPrompt, resources, ct);
        systemPrompt = AppendCacheState(systemPrompt, cacheFields, cacheValues);

        // Contexto de la copropiedad ACTIVA (la seleccionada por el usuario): se antepone al
        // prompt para que el agente sepa SIEMPRE sobre que copropiedad responde, sin hardcodear.
        systemPrompt = await PrependContextoCopropiedadAsync(systemPrompt, ct);

        AiChatResult result;
        List<AiDebugPrompt> debug;

        // Tools MCP habilitadas para este agente. Si hay alguna y tenemos token del usuario,
        // corremos el loop de function-calling; si no, el camino simple de siempre.
        var toolSpecs = await BuildToolSpecsAsync(agentId, bearerToken, ct);
        if (toolSpecs.Count > 0 && !string.IsNullOrEmpty(bearerToken))
        {
            var systemPromptConTools = systemPrompt
                + "\n\nHERRAMIENTAS: tienes herramientas para consultar y gestionar datos REALES de esta copropiedad. "
                + "Cuando te pregunten por unidades, torres, contratos, zonas, equipos, finanzas, consejo o cambios, USA las herramientas para responder con datos exactos; nunca inventes cifras. "
                + "Si una pregunta abarca varios temas, llama a TODAS las herramientas necesarias antes de responder. "
                + "Las herramientas de creacion son dry-run por defecto: confirma con el usuario antes de ejecutar cambios reales.";
            result = await RunToolLoopAsync(agent, model, apiKey, providerCfg.BaseUrl, systemPromptConTools, turns, toolSpecs, resources, bearerToken!, ct);
            debug = result.DebugPrompts?.ToList() ?? new();
        }
        else
        {
            var raw = await _client.CompleteAsync(agent.Provider, apiKey, providerCfg.BaseUrl, model, systemPrompt, turns, ct);

            // Bitacora de depuracion: registra el prompt exacto que se envio al LLM + su respuesta cruda.
            var mainResponse = raw.Ok
                ? (string.IsNullOrWhiteSpace(raw.Text) ? "(el LLM devolvio texto vacio)" : raw.Text)
                : raw.Error;
            debug = new List<AiDebugPrompt>
            {
                new("Prompt principal del agente (base + enrutador + recursos + contexto)", DateTimeOffset.UtcNow, systemPrompt, mainResponse)
            };

            if (raw.Ok)
            {
                await _usage.RecordAsync(agent.Id, agent.Provider, model, raw.InputTokens, raw.OutputTokens, "test", true, ct);
            }

            if (raw.Ok && !string.IsNullOrEmpty(raw.Text))
            {
                var (cleanText, attachments) = ExtractAttachments(raw.Text!, resources);
                result = raw with { Text = cleanText, Attachments = attachments };
            }
            else { result = raw; }
        }

        // Datos cache: tras responder, una segunda llamada (mas barata) infiere los datos cache del
        // residente y los persiste (respeta sticky). No debe romper la respuesta si falla.
        if (result.Ok && cacheFields.Count > 0 && !string.IsNullOrWhiteSpace(result.Text))
        {
            try
            {
                await ExtractAndStoreCacheUpdatesAsync(agentId, sessionId, agent.Provider, apiKey, providerCfg.BaseUrl, model,
                    cacheFields, cacheValues, turns, result.Text!, debug, ct);
            }
            catch { /* la extraccion no debe romper la respuesta */ }
        }

        return result with { DebugPrompts = debug };
    }

    /// <summary>
    /// Arma las specs de tools (nombre, descripcion, JSON schema) de las tools MCP habilitadas
    /// para el agente, descubriendo en vivo el schema desde el servidor MCP. Tambien deja listo
    /// el mapa tool -> conexion para ejecutarlas. Si una conexion no responde, se omite.
    /// </summary>
    private async Task<IReadOnlyList<AiToolSpec>> BuildToolSpecsAsync(Guid agentId, string? bearerToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(bearerToken)) { return Array.Empty<AiToolSpec>(); }

        var seleccion = await _db.AiAgentMcpTools.AsNoTracking()
            .Where(t => t.AgentId == agentId)
            .Select(t => new { t.ConnectionCode, t.ToolName })
            .ToListAsync(ct);
        if (seleccion.Count == 0) { return Array.Empty<AiToolSpec>(); }

        _toolToConnection.Clear();
        var specs = new List<AiToolSpec>();
        foreach (var grupo in seleccion.GroupBy(s => s.ConnectionCode))
        {
            var habilitadas = grupo.Select(g => g.ToolName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            try
            {
                var tools = await _mcp.ListToolsAsync(grupo.Key, bearerToken, ct);
                foreach (var t in tools.Where(t => habilitadas.Contains(t.Name)))
                {
                    _toolToConnection[t.Name] = grupo.Key;
                    specs.Add(new AiToolSpec(t.Name, t.Description, t.InputSchemaJson ?? "{}"));
                }
            }
            catch { /* conexion MCP caida: se omiten sus tools */ }
        }
        return specs;
    }

    private readonly Dictionary<string, string> _toolToConnection = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Loop de function-calling: el modelo pide tools, las ejecutamos via el gateway MCP (que
    /// hereda el tenant + permisos del usuario por el bearer), realimentamos y repetimos hasta
    /// la respuesta final o el maximo de rondas.
    /// </summary>
    private async Task<AiChatResult> RunToolLoopAsync(
        Domain.Entities.AiAgent agent, string model, string apiKey, string? baseUrl, string systemPrompt,
        IReadOnlyList<AiChatTurn> turns, IReadOnlyList<AiToolSpec> toolSpecs,
        IReadOnlyList<AiChatAttachment> resources, string bearerToken, CancellationToken ct)
    {
        var msgs = new List<AiToolMessage>(turns.Select(t => new AiToolMessage(t.Role, t.Text)));
        int inTok = 0, outTok = 0;
        // Bitacora de depuracion: el prompt principal (con herramientas) + una entrada por cada ronda de tools.
        var debug = new List<AiDebugPrompt>();

        for (var ronda = 0; ronda < MaxToolRounds; ronda++)
        {
            var comp = await _client.CompleteWithToolsAsync(agent.Provider, apiKey, baseUrl, model, systemPrompt, msgs, toolSpecs, ct);
            inTok += comp.InputTokens;
            outTok += comp.OutputTokens;

            if (!comp.Ok)
            {
                debug.Insert(0, new AiDebugPrompt("Prompt principal del agente (con herramientas MCP)", DateTimeOffset.UtcNow, systemPrompt, comp.Error));
                return new AiChatResult(false, null, comp.Error, inTok, outTok, null, debug);
            }

            if (comp.ToolCalls.Count == 0)
            {
                await _usage.RecordAsync(agent.Id, agent.Provider, model, inTok, outTok, "test", true, ct);
                var (clean, attachments) = ExtractAttachments(comp.Text ?? "", resources);
                debug.Insert(0, new AiDebugPrompt("Prompt principal del agente (con herramientas MCP)", DateTimeOffset.UtcNow, systemPrompt,
                    string.IsNullOrWhiteSpace(comp.Text) ? "(el LLM devolvio texto vacio)" : comp.Text));
                return new AiChatResult(true, clean, null, inTok, outTok, attachments, debug);
            }

            // El modelo pidio ejecutar tools: registramos su turno y ejecutamos cada una.
            msgs.Add(new AiToolMessage("assistant", comp.Text, comp.ToolCalls));
            var pasoSb = new StringBuilder();
            var resSb = new StringBuilder();
            foreach (var call in comp.ToolCalls)
            {
                string resultado;
                try
                {
                    if (!_toolToConnection.TryGetValue(call.Name, out var conexion))
                    {
                        resultado = $"La tool '{call.Name}' no esta habilitada para este agente.";
                    }
                    else
                    {
                        resultado = await _mcp.CallToolAsync(conexion, call.Name, ParseArgs(call.ArgumentsJson), bearerToken, ct);
                    }
                }
                catch (Exception ex) { resultado = $"Error ejecutando '{call.Name}': {ex.Message}"; }

                pasoSb.AppendLine($"{call.Name}({call.ArgumentsJson})");
                resSb.AppendLine($"{call.Name} -> {resultado}");
                msgs.Add(new AiToolMessage("tool", resultado, null, call.Id, call.Name));
            }
            debug.Add(new AiDebugPrompt($"Herramientas ejecutadas (ronda {ronda + 1})", DateTimeOffset.UtcNow, pasoSb.ToString().TrimEnd(), resSb.ToString().TrimEnd()));
        }

        await _usage.RecordAsync(agent.Id, agent.Provider, model, inTok, outTok, "test", false, ct);
        debug.Insert(0, new AiDebugPrompt("Prompt principal del agente (con herramientas MCP)", DateTimeOffset.UtcNow, systemPrompt, $"Supero el maximo de pasos ({MaxToolRounds})."));
        return new AiChatResult(false, null, $"El agente supero el maximo de pasos de herramientas ({MaxToolRounds}).", inTok, outTok, null, debug);
    }

    private static IReadOnlyDictionary<string, object?> ParseArgs(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson)) { return new Dictionary<string, object?>(); }
        try
        {
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(argumentsJson);
            return dict ?? new Dictionary<string, object?>();
        }
        catch { return new Dictionary<string, object?>(); }
    }

    /// <summary>
    /// Antepone al prompt un bloque con la identidad de la copropiedad ACTIVA (la del JWT). Asi el
    /// agente siempre sabe sobre que copropiedad responde, sin hardcodear el nombre en el prompt y
    /// reflejando automaticamente la propiedad que el usuario tenga seleccionada en el selector.
    /// </summary>
    private async Task<string> PrependContextoCopropiedadAsync(string systemPrompt, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tenantId) { return systemPrompt; }
        var t = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tenantId, ct);
        if (t is null) { return systemPrompt; }

        var sb = new StringBuilder();
        sb.AppendLine("CONTEXTO: estas atendiendo EXCLUSIVAMENTE a la copropiedad activa seleccionada por el usuario:");
        sb.AppendLine($"- Nombre: {t.Nombre}");
        if (!string.IsNullOrWhiteSpace(t.Nit)) { sb.AppendLine($"- NIT: {t.Nit}"); }
        if (!string.IsNullOrWhiteSpace(t.Direccion)) { sb.AppendLine($"- Direccion: {t.Direccion}{(string.IsNullOrWhiteSpace(t.Ciudad) ? "" : ", " + t.Ciudad)}"); }
        if (t.TipoCopropiedad.HasValue) { sb.AppendLine($"- Tipo: {t.TipoCopropiedad}"); }
        sb.AppendLine("Todas tus herramientas operan sobre esta copropiedad. Si el usuario pregunta por otra, aclara que solo puedes ver la activa.");
        sb.AppendLine("NUNCA inventes datos de la copropiedad (cifras, montos, cantidades, estados). Si no tienes una herramienta para consultar algo concreto, dilo con honestidad en vez de suponer.");
        sb.AppendLine();
        sb.Append(systemPrompt);
        return sb.ToString();
    }

    private async Task<string> BuildSystemPrompt(Guid agentId, string basePrompt, IReadOnlyList<AiChatAttachment> resources, CancellationToken ct)
    {
        var sb = new StringBuilder(ExpandResourceRefs(basePrompt, resources));

        var prompts = await _db.AiAgentPrompts.AsNoTracking()
            .Where(p => p.AgentId == agentId).OrderBy(p => p.SortOrder)
            .Select(p => new { p.Name, p.Rule, p.Body })
            .ToListAsync(ct);
        if (prompts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("Enrutador de prompts: evalua el mensaje del cliente y, si coincide alguna de estas reglas, sigue PRIMERO las instrucciones del prompt correspondiente (ademas del comportamiento base). Si ninguna aplica, responde con el comportamiento base.");
            foreach (var p in prompts)
            {
                sb.AppendLine();
                sb.AppendLine($"### Prompt \"{p.Name}\"");
                sb.AppendLine($"Regla (cuando usarlo): {(string.IsNullOrWhiteSpace(p.Rule) ? "(sin regla; usar a criterio)" : p.Rule)}");
                sb.AppendLine($"Instrucciones: {ExpandResourceRefs(p.Body, resources)}");
            }
        }

        if (resources.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("Recursos disponibles. REGLA IMPORTANTE: cuando vayas a comunicar el contenido de un recurso (precios, politicas, textos, imagenes, videos, PDF, ubicacion), NO lo reescribas ni lo resumas: entregalo EXACTO incluyendo en tu respuesta el marcador [[enviar: Nombre exacto del recurso]]. El sistema agregara el contenido o el archivo tal cual. Puedes acompanarlo con una frase breve, pero el contenido del recurso lo entrega el marcador.");
            foreach (var r in resources)
            {
                var kind = r.ResourceType == AgentResourceType.Text ? "Texto" : r.ResourceType.ToString();
                var desc = string.IsNullOrWhiteSpace(r.Detail) ? "archivo" : r.Detail;
                sb.AppendLine($"- ({kind}) {r.Name}: {desc}  -> entregar con [[enviar: {r.Name}]]");
            }
        }

        return sb.ToString();
    }

    private static string ExpandResourceRefs(string text, IReadOnlyList<AiChatAttachment> resources)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("{{")) { return text; }
        return Regex.Replace(text, @"\{\{\s*([^}]+?)\s*\}\}", m =>
        {
            var res = FindResource(resources, m.Groups[1].Value);
            if (res is null) { return m.Value; }
            return $"el recurso \"{res.Name}\" (entregalo EXACTO incluyendo el marcador [[enviar: {res.Name}]]; el sistema agrega su contenido, no lo reescribas)";
        });
    }

    private static (string, IReadOnlyList<AiChatAttachment>) ExtractAttachments(string text, IReadOnlyList<AiChatAttachment> resources)
    {
        var attachments = new List<AiChatAttachment>();
        var clean = Regex.Replace(text, @"\[\[\s*enviar\s*:\s*([^\]]+?)\s*\]\]", m =>
        {
            var res = FindResource(resources, m.Groups[1].Value);
            if (res is not null && attachments.All(a => a.Name != res.Name)) { attachments.Add(res); }
            return string.Empty;
        }, RegexOptions.IgnoreCase);

        clean = Regex.Replace(clean, @"[ \t]+\n", "\n").Trim();
        return (clean, attachments);
    }

    private static AiChatAttachment? FindResource(IReadOnlyList<AiChatAttachment> resources, string name)
    {
        var key = Normalize(name);
        return resources.FirstOrDefault(r => Normalize(r.Name) == key);
    }

    private static string Normalize(string s)
    {
        var n = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in n)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) { sb.Append(c); }
        }
        return sb.ToString();
    }

    // ===== Datos cache (capa 3) =====

    /// <summary>Inyecta en el prompt el bloque "estado de la cache" con los datos ya capturados (solo los que tienen valor).</summary>
    private static string AppendCacheState(string systemPrompt, IReadOnlyList<CacheFieldInfo> fields, IReadOnlyDictionary<string, string?> values)
    {
        var captured = fields
            .Where(f => values.TryGetValue(f.FieldKey, out var v) && !string.IsNullOrWhiteSpace(v))
            .ToList();
        if (captured.Count == 0) { return systemPrompt; }

        var sb = new StringBuilder(systemPrompt);
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("### Datos que ya conocemos del residente (estado de la cache)");
        sb.AppendLine("Estos datos ya estan capturados. Usalos para decidir tu siguiente paso: NO le pidas al residente algo que ya sabes.");
        foreach (var f in captured)
        {
            sb.AppendLine($"- {f.Label}: {values[f.FieldKey]}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Segunda llamada (extractor): lee la conversacion y devuelve un JSON plano con los datos cache que
    /// puede inferir CON CERTEZA. Persiste cada uno via IAiAgentCacheService (que respeta sticky).
    /// Best-effort: cualquier fallo se traga sin romper la respuesta. Portado de CUBOT.travels.
    /// </summary>
    private async Task ExtractAndStoreCacheUpdatesAsync(
        Guid agentId, Guid sessionId, AiProvider provider, string apiKey, string? baseUrl, string model,
        IReadOnlyList<CacheFieldInfo> fields, IReadOnlyDictionary<string, string?> currentValues,
        IReadOnlyList<AiChatTurn> turns, string botResponse, List<AiDebugPrompt> debug, CancellationToken ct)
    {
        var lastUser = turns.LastOrDefault(t => string.Equals(t.Role, "user", StringComparison.OrdinalIgnoreCase))?.Text ?? "";

        var sys = new StringBuilder();
        sys.AppendLine("Eres un extractor de datos de una copropiedad. NO debes responder al residente.");
        sys.AppendLine("Tu unico trabajo es leer la conversacion y devolver un JSON plano con los campos que puedas inferir CON CERTEZA del mensaje del residente.");
        sys.AppendLine("Reglas:");
        sys.AppendLine("- NO inventes datos. Si no esta claro, NO incluyas el campo.");
        sys.AppendLine("- Si un campo ya tiene valor y el residente no lo cambia, NO lo incluyas.");
        sys.AppendLine("- NO incluyas el valor literal \"PENDIENTE\".");
        sys.AppendLine("- Responde UNICAMENTE el JSON, sin texto antes ni despues, sin markdown.");
        sys.AppendLine();
        sys.AppendLine("### Campos a capturar");
        foreach (var f in fields)
        {
            sys.AppendLine($"- {f.FieldKey}: {(string.IsNullOrWhiteSpace(f.Description) ? f.Label : f.Description)}");
        }
        sys.AppendLine();
        sys.AppendLine("### Estado actual de la cache");
        var anyKnown = false;
        foreach (var f in fields)
        {
            if (currentValues.TryGetValue(f.FieldKey, out var v) && !string.IsNullOrWhiteSpace(v))
            {
                sys.AppendLine($"- {f.FieldKey} = {v}");
                anyKnown = true;
            }
        }
        if (!anyKnown) { sys.AppendLine("(vacio)"); }
        sys.AppendLine();
        sys.AppendLine("### Formato de respuesta");
        sys.AppendLine("JSON plano. Ejemplo: {\"numero_apartamento\":\"302\",\"tipo_solicitud\":\"Reclamo\"}");
        sys.AppendLine("Si no hay nada nuevo, responde {}");

        var transcript = new StringBuilder();
        transcript.AppendLine("### Transcripcion de la conversacion (orden cronologico)");
        foreach (var t in turns)
        {
            var who = string.Equals(t.Role, "user", StringComparison.OrdinalIgnoreCase) ? "Residente" : "Agente";
            transcript.AppendLine($"{who}: {(string.IsNullOrWhiteSpace(t.Text) ? "(turno vacio)" : t.Text.Trim())}");
        }
        transcript.AppendLine($"Agente (respuesta actual): {botResponse}");

        var userTurn = new AiChatTurn("user",
            transcript + "\n\nQue campos puedes inferir del residente a partir de TODA esta conversacion? Solo agrega campos que puedes inferir CON CERTEZA y que no tengan ya valor.");

        var extractorSystem = sys.ToString();
        var entry = new AiDebugPrompt(
            $"Extractor de datos cache (ultimo mensaje: \"{Truncate(lastUser, 60)}\")",
            DateTimeOffset.UtcNow, extractorSystem + "\n\n---\n[Turno al extractor]\n" + userTurn.Text);
        debug.Add(entry);
        var idx = debug.Count - 1;

        AiChatResult ext;
        try { ext = await _client.CompleteAsync(provider, apiKey, baseUrl, model, extractorSystem, new[] { userTurn }, ct); }
        catch (Exception ex) { debug[idx] = entry with { Response = $"[Llamada fallida] {ex.Message}" }; return; }

        debug[idx] = entry with { Response = ext.Ok ? (ext.Text ?? "(sin texto)") : $"[Sin Ok] {ext.Error}" };
        if (!ext.Ok || string.IsNullOrWhiteSpace(ext.Text)) { return; }

        await _usage.RecordAsync(agentId, provider, model, ext.InputTokens, ext.OutputTokens, "cache", true, ct);

        var json = StripJsonFromMarkdown(ext.Text!);
        Dictionary<string, JsonElement>? parsed;
        try { parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json); }
        catch { return; }
        if (parsed is null || parsed.Count == 0) { return; }

        var fieldKeys = fields.Select(f => f.FieldKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, valEl) in parsed)
        {
            if (!fieldKeys.Contains(key)) { continue; }
            string? v = valEl.ValueKind switch
            {
                JsonValueKind.String => valEl.GetString(),
                JsonValueKind.Number => valEl.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Array => string.Join(", ", valEl.EnumerateArray()
                    .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : e.GetRawText())
                    .Where(s => !string.IsNullOrWhiteSpace(s))),
                JsonValueKind.Object => valEl.GetRawText(),
                _ => null
            };
            if (string.IsNullOrWhiteSpace(v)) { continue; }
            if (string.Equals(v.Trim(), "PENDIENTE", StringComparison.OrdinalIgnoreCase)) { continue; }
            try { await _cache.SetValueAsync(new SetAgentCacheValueRequest(agentId, sessionId, key, v.Trim(), "inference"), ct); }
            catch { /* la falla de un campo no aborta el resto */ }
        }
    }

    private static string StripJsonFromMarkdown(string text)
    {
        var t = text.Trim();
        t = Regex.Replace(t, @"^```(?:json)?\s*\n?", "", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"\n?```\s*$", "");
        var i = t.IndexOf('{');
        var j = t.LastIndexOf('}');
        if (i >= 0 && j > i) { t = t.Substring(i, j - i + 1); }
        return t.Trim();
    }

    private static string Truncate(string s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s[..n] + "...");
}
