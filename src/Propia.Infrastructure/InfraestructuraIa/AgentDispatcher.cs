using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Propia.Application.Auth;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Despachador del agente de IA (auto-respuesta a entrantes). Reutiliza IAiInferenceService.TestChatAsync
/// para reconstruir el prompt completo del agente, y envia la respuesta por IWhatsAppConnectorService.
/// Portado de CUBOT.travels (AgentDispatcher) y SIMPLIFICADO para PROPIA: sin acoplamiento CRM
/// (no crea leads ni pedidos de ventas), y sin reacciones/adjuntos binarios (el connector de PROPIA
/// hoy solo envia texto; se ampliara en una fase posterior).
///
/// Corre SIN JWT de usuario (lo dispara el webhook via la cola). Fija el tenant en contexto con
/// ITenantContext.SetTenant ANTES de consultar: asi el query filter de EF y la RLS de Postgres
/// resuelven al tenant correcto (a diferencia de CUBOT, que usa IgnoreQueryFilters; en PROPIA eso
/// NO evita la RLS de Postgres, hay que fijar el tenant).
/// </summary>
public sealed class AgentDispatcher : IAgentDispatcher
{
    /// <summary>Maximo de turnos que se pasan a la inferencia (contexto). Igual que CUBOT.</summary>
    private const int MaxTurns = 12;

    /// <summary>
    /// Usuario de sistema (sintetico) para el token de servicio del dispatcher: es el sub/user_id del
    /// JWT que habilita el runtime de tools MCP en el chat real. No es una fila real de Identity (la
    /// validacion del JWT no consulta BD); solo aporta un id estable y el claim tenant_id para la RLS.
    /// </summary>
    public static readonly Guid DispatcherServiceUserId = new("00000000-0000-0000-0000-0000a9e17d15");

    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAiInferenceService _inference;
    private readonly IWhatsAppConnectorService _connector;
    private readonly ITokenService _tokens;
    private readonly IChatBroadcaster? _broadcaster;
    private readonly ILogger<AgentDispatcher> _logger;

    public AgentDispatcher(
        PropiaDbContext db,
        ITenantContext tenant,
        IAiInferenceService inference,
        IWhatsAppConnectorService connector,
        ITokenService tokens,
        ILogger<AgentDispatcher> logger,
        IChatBroadcaster? broadcaster = null)
    {
        _db = db;
        _tenant = tenant;
        _inference = inference;
        _connector = connector;
        _tokens = tokens;
        _logger = logger;
        _broadcaster = broadcaster;
    }

    public async Task DispatchAsync(Guid tenantId, Guid conversationId, Guid? whatsAppLineId, string inboundBody, CancellationToken ct = default)
    {
        // Fija el tenant: habilita el query filter de EF + la RLS de Postgres para este tenant.
        _tenant.SetTenant(tenantId);

        if (whatsAppLineId is null)
        {
            _logger.LogInformation("Dispatcher: skip conv {ConvId} (sin whatsAppLineId)", conversationId);
            return;
        }

        // 1) Binding activo linea -> agente. Sin binding, el dispatcher no hace nada.
        var binding = await _db.AiAgentLineBindings.AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.WhatsAppLineId == whatsAppLineId && b.IsConnected)
            .Select(b => new { b.AgentId, b.AutoConfirm })
            .FirstOrDefaultAsync(ct);
        if (binding is null)
        {
            _logger.LogInformation("Dispatcher: skip conv {ConvId} linea {LineId} (sin agente vinculado)", conversationId, whatsAppLineId);
            return;
        }

        // 2) Config del agente en una sola consulta: activo (en produccion), reacciones y saludo con logo.
        // Si esta apagado, silencio total (coherente con la UI).
        var agent = await _db.AiAgents.AsNoTracking()
            .Where(a => a.Id == binding.AgentId)
            .Select(a => new { a.IsActive, a.ReactionsEnabled, a.ReactionRatioN, a.ReactionRatioM, a.ReactionEmojis, a.SaludarConLogo })
            .FirstOrDefaultAsync(ct);
        if (agent is null || !agent.IsActive)
        {
            _logger.LogInformation("Dispatcher: skip conv {ConvId} (agente {AgentId} apagado)", conversationId, binding.AgentId);
            return;
        }

        // 3) Conversacion: telefono, reinicio de contexto y toma de control humana.
        var conv = await _db.Conversations.AsNoTracking()
            .Where(c => c.Id == conversationId)
            .Select(c => new { c.ContactPhone, c.AgentContextResetAt, c.IaPausadaAt })
            .FirstOrDefaultAsync(ct);
        if (conv is null || string.IsNullOrWhiteSpace(conv.ContactPhone))
        {
            _logger.LogInformation("Dispatcher: skip conv {ConvId} (sin telefono)", conversationId);
            return;
        }

        // Toma de control humana: si un operador esta atendiendo (IaPausadaAt seteado), el bot calla.
        // Se reactiva desde la bandeja con "reiniciar contexto" (limpia IaPausadaAt).
        if (conv.IaPausadaAt is not null)
        {
            _logger.LogInformation("Dispatcher: skip conv {ConvId} (IA pausada, atiende un humano)", conversationId);
            await LogRunAsync(tenantId, conversationId, binding.AgentId, AiAgentRunLogKind.Info,
                "Skip: atencion humana",
                "Un operador esta atendiendo esta conversacion (IA en pausa). El bot no interfiere. " +
                "Para reactivar al bot: usar 'Reiniciar contexto IA' en la bandeja.",
                null, ct);
            return;
        }

        // Bitacora: el mensaje recibido abre la ventana de la conversacion en /ia/bitacora.
        await LogRunAsync(tenantId, conversationId, binding.AgentId, AiAgentRunLogKind.Inbound,
            "Mensaje recibido", inboundBody, null, ct);

        // Reacciones automaticas (emoji) ANTES de llamar al LLM: cero tokens. Reacciona a ~N de cada M
        // mensajes del contacto con un emoji al azar. Independiente del modo sugerencia. Portado de
        // ECOREX (AgentConversationService.MaybeReactAsync).
        if (agent.ReactionsEnabled)
        {
            await MaybeReactAsync(tenantId, conversationId, whatsAppLineId.Value, conv.ContactPhone, binding.AgentId,
                agent.ReactionRatioN, agent.ReactionRatioM, agent.ReactionEmojis, ct);
        }

        // 4) Reconstruye contexto: ultimos N turnos despues del reinicio de contexto, excluyendo
        // mensajes fallidos del bot y placeholders. Inbound -> user, Outbound -> model.
        var resetAt = conv.AgentContextResetAt;
        var recent = await _db.Messages.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.ConversationId == conversationId
                     && !(m.SentByName != null && m.SentByName.StartsWith("IA (ai_failed"))
                     && m.Body != "(solo adjuntos)"
                     && (resetAt == null || m.SentAt > resetAt))
            .OrderByDescending(m => m.SentAt)
            .Take(MaxTurns)
            .OrderBy(m => m.SentAt)
            .Select(m => new { m.Direction, m.Body, m.ExternalId, m.MediaType })
            .ToListAsync(ct);

        // Colapsa turnos consecutivos del mismo rol (los LLM esperan alternancia user/model; varios
        // entrantes seguidos sin respuesta del bot rompen la alternancia y dan respuesta vacia).
        var turns = new List<AiChatTurn>();
        foreach (var m in recent)
        {
            var role = m.Direction == MessageDirection.Inbound ? "user" : "model";
            if (turns.Count > 0 && turns[^1].Role == role)
            {
                turns[^1] = turns[^1] with { Text = $"{turns[^1].Text}\n{m.Body}" };
            }
            else { turns.Add(new AiChatTurn(role, m.Body)); }
        }
        if (turns.Count == 0) { turns.Add(new AiChatTurn("user", inboundBody)); }

        // Vision (Ola C): si los ultimos mensajes entrantes (el "turno" actual del residente) traen
        // imagenes, bajamos su contenido de Evolution y las adjuntamos al ultimo turno user para que el
        // agente las "vea". Solo imagenes por ahora. Best-effort: si falla la bajada, seguimos solo-texto.
        if (turns.Count > 0 && turns[^1].Role == "user")
        {
            const int MaxImagenes = 4;
            var imagenes = new List<AiInboundImage>();
            // Run final de entrantes (los mensajes despues del ultimo saliente): el mensaje actual del contacto.
            foreach (var m in recent.AsEnumerable().Reverse())
            {
                if (m.Direction != MessageDirection.Inbound) { break; }
                if (m.MediaType != MessageMediaType.Image || string.IsNullOrWhiteSpace(m.ExternalId)) { continue; }
                try
                {
                    var img = await _connector.FetchInboundImageAsync(whatsAppLineId.Value, m.ExternalId!, ct);
                    if (img is not null) { imagenes.Insert(0, new AiInboundImage(img.Value.MimeType, img.Value.Base64)); }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Dispatcher: no se pudo bajar imagen entrante {ExtId} conv {ConvId}", m.ExternalId, conversationId);
                }
                if (imagenes.Count >= MaxImagenes) { break; }
            }
            if (imagenes.Count > 0)
            {
                turns[^1] = turns[^1] with { Images = imagenes };
                await LogRunAsync(tenantId, conversationId, binding.AgentId, AiAgentRunLogKind.Info,
                    $"Imagenes adjuntas al contexto ({imagenes.Count})",
                    "El residente envio imagen(es); se adjuntaron al ultimo turno para que el agente las analice (vision).",
                    null, ct);
            }
        }

        var totalChars = turns.Sum(t => t.Text?.Length ?? 0);
        await LogRunAsync(tenantId, conversationId, binding.AgentId, AiAgentRunLogKind.Info,
            $"Contexto enviado al LLM ({turns.Count} turnos, {totalChars} chars)",
            string.Join('\n', turns.Select((t, i) => $"[{i + 1}] {t.Role}: {Preview(t.Text, 120)}")),
            null, ct);

        // 5) Inferencia (mismo motor que el chat de prueba de /ia/agentes). sessionId = conversationId
        // aisla el cache por conversacion. Para habilitar el runtime de tools MCP en el chat real
        // (ej. verificar_residencia), emitimos un token de servicio tenant-scoped: /mcp exige JWT y
        // fija la RLS desde el claim tenant_id. Best-effort: si no se puede emitir, el motor corre el
        // camino simple (sin MCP) y el agente responde de forma conversacional (como antes).
        string? bearerToken = null;
        try
        {
            var (svcToken, _) = _tokens.IssueAccessToken(
                new ApplicationUser { Id = DispatcherServiceUserId, Email = "agente-ia@dispatcher.propia" }, tenantId);
            bearerToken = svcToken;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dispatcher: no se pudo emitir token de servicio para tools MCP conv {ConvId}", conversationId);
        }

        _logger.LogInformation("Dispatcher: inferencia agente {AgentId} conv {ConvId} ({TurnCount} turnos)",
            binding.AgentId, conversationId, turns.Count);
        var result = await _inference.TestChatAsync(binding.AgentId, turns,
            systemPromptOverride: null, bearerToken: bearerToken, contactPhone: conv.ContactPhone,
            conversationId: conversationId, ct: ct);

        // Bitacora: prompts que ejecuto la inferencia (prompt principal + extractor de cache).
        if (result.DebugPrompts is not null)
        {
            foreach (var d in result.DebugPrompts)
            {
                await LogRunAsync(tenantId, conversationId, binding.AgentId,
                    AiAgentRunLogKind.Prompt, d.Title, d.Content, d.Response, ct);
            }
        }

        if (!result.Ok)
        {
            _logger.LogWarning("Dispatcher: inferencia FALLO agente {AgentId} conv {ConvId}: {Error}",
                binding.AgentId, conversationId, result.Error ?? "(sin detalle)");
            await LogRunAsync(tenantId, conversationId, binding.AgentId, AiAgentRunLogKind.Error,
                "Inferencia fallo", result.Error, null, ct);
            return;
        }

        // Texto a enviar: el texto del LLM + el detalle de los recursos de tipo Texto que el agente
        // decidio entregar ([[enviar: X]]). Los adjuntos binarios (imagen/video/audio/pdf) y la ubicacion
        // se envian aparte, DESPUES del texto (portado de ECOREX: el dispatcher ya los entrega).
        var textToSend = (result.Text ?? string.Empty).Trim();
        var binarios = new List<AiChatAttachment>();
        if (result.Attachments is { Count: > 0 })
        {
            foreach (var a in result.Attachments)
            {
                if (a.ResourceType == AgentResourceType.Text && !string.IsNullOrWhiteSpace(a.Detail))
                {
                    textToSend = string.IsNullOrWhiteSpace(textToSend) ? a.Detail!.Trim() : $"{textToSend}\n\n{a.Detail!.Trim()}";
                }
                else if (a.ResourceType != AgentResourceType.Text)
                {
                    binarios.Add(a);
                }
            }
        }

        // Nada que enviar (ni texto ni adjuntos): la respuesta del modelo vino vacia.
        if (string.IsNullOrWhiteSpace(textToSend) && binarios.Count == 0)
        {
            await LogRunAsync(tenantId, conversationId, binding.AgentId, AiAgentRunLogKind.Error,
                "Respuesta vacia del LLM",
                "El modelo devolvio una respuesta vacia (contexto largo, filtro de seguridad o prompt mal armado).",
                result.Error, ct);
            return;
        }

        // 6) Modo sugerencia (AutoConfirm = false): guarda como borrador y NO envia (ni texto ni adjuntos).
        if (!binding.AutoConfirm)
        {
            var draft = string.IsNullOrWhiteSpace(textToSend)
                ? $"(el agente propuso enviar {binarios.Count} adjunto(s))"
                : textToSend;
            await PersistOutboundAsync(tenantId, conversationId, draft, "ai_draft", ct);
            await LogRunAsync(tenantId, conversationId, binding.AgentId, AiAgentRunLogKind.Info,
                "Respuesta en borrador (sin enviar)",
                "El vinculo linea-agente esta en modo sugerencia (AutoConfirm=false): la respuesta queda como borrador para que un operador la revise.",
                draft, ct);
            return;
        }

        // 7) Envio real por la linea (router Evolution/Cloud/YCloud interno del connector).
        // 7a) Texto (si hay). Saludo con logo: si el agente lo tiene activo y este es el PRIMER saliente
        // de la conversacion, enviamos el texto como CAPTION de la imagen (logo de la copropiedad).
        if (!string.IsNullOrWhiteSpace(textToSend))
        {
            string? logoUrl = null;
            if (agent.SaludarConLogo)
            {
                var yaHayOutbound = await _db.Messages.AsNoTracking()
                    .AnyAsync(m => m.ConversationId == conversationId && m.Direction == MessageDirection.Outbound, ct);
                if (!yaHayOutbound) { logoUrl = await ResolveLogoPublicUrlAsync(tenantId, ct); }
            }

            LineSendResult send;
            try
            {
                if (logoUrl is not null)
                {
                    send = await _connector.SendMediaAsync(whatsAppLineId.Value, conv.ContactPhone, logoUrl, textToSend, ct);
                    // Si el envio de media falla, no perdemos la respuesta: caemos a texto plano.
                    if (!send.Ok)
                    {
                        await LogRunAsync(tenantId, conversationId, binding.AgentId, AiAgentRunLogKind.Info,
                            "Saludo con logo fallo; fallback a texto", send.Error, null, ct);
                        send = await _connector.SendTestAsync(whatsAppLineId.Value, conv.ContactPhone, textToSend, ct);
                    }
                }
                else
                {
                    send = await _connector.SendTestAsync(whatsAppLineId.Value, conv.ContactPhone, textToSend, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Dispatcher: envio de texto fallo conv {ConvId}", conversationId);
                send = new LineSendResult(false, ex.Message);
            }

            await PersistOutboundAsync(tenantId, conversationId, textToSend, send.Ok ? "ai_sent" : "ai_failed", ct);
            await LogRunAsync(tenantId, conversationId, binding.AgentId,
                send.Ok ? AiAgentRunLogKind.Reply : AiAgentRunLogKind.Error,
                send.Ok ? "Respuesta enviada" : "Envio fallo", textToSend, send.Ok ? "OK" : send.Error, ct);

            // Si el texto fallo (linea caida, numero invalido), no insistimos con los adjuntos.
            if (!send.Ok) { return; }
        }

        // 7b) Adjuntos binarios + ubicacion, cada uno como su propio mensaje saliente.
        foreach (var a in binarios)
        {
            await SendAgentAttachmentAsync(tenantId, conversationId, binding.AgentId, whatsAppLineId.Value, conv.ContactPhone, a, ct);
        }
    }

    /// <summary>
    /// Reaccion automatica (emoji) al ultimo mensaje entrante del contacto, SIN pasar por el LLM.
    /// Aplica la frecuencia N/M como probabilidad y elige el emoji al azar del listado configurado.
    /// Un mensaje que ya tiene reaccion no recibe otra. Portado de ECOREX (MaybeReactAsync).
    /// </summary>
    private async Task MaybeReactAsync(Guid tenantId, Guid conversationId, Guid lineId, string phone, Guid agentId,
        int ratioN, int ratioM, string? emojiCsv, CancellationToken ct)
    {
        var emojis = (emojiCsv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        if (emojis.Count == 0) { return; }            // sin emojis configurados: nada que hacer.
        if (ratioM <= 0 || ratioN <= 0) { return; }   // frecuencia invalida o cero: no reacciona.

        // Dado de N de cada M: si el numerador es menor que el denominador, tiramos el dado.
        if (ratioN < ratioM && Random.Shared.Next(ratioM) >= ratioN) { return; }

        // Ultimo entrante con id de WhatsApp y sin reaccion previa (rastreado para poder marcarlo).
        var lastInbound = await _db.Messages
            .Where(m => m.ConversationId == conversationId
                     && m.Direction == MessageDirection.Inbound
                     && m.ExternalId != null
                     && m.Reaction == null)
            .OrderByDescending(m => m.SentAt)
            .FirstOrDefaultAsync(ct);
        if (lastInbound is null || string.IsNullOrWhiteSpace(lastInbound.ExternalId)) { return; }

        var emoji = emojis[Random.Shared.Next(emojis.Count)];
        LineSendResult res;
        try { res = await _connector.SendReactionAsync(lineId, phone, lastInbound.ExternalId!, emoji, ct); }
        catch (Exception ex) { res = new LineSendResult(false, ex.Message); }

        if (res.Ok)
        {
            lastInbound.Reaction = emoji;
            await _db.SaveChangesAsync(ct);
            await LogRunAsync(tenantId, conversationId, agentId, AiAgentRunLogKind.Tool,
                $"Reaccion automatica {emoji}", "Sin consumo de IA (reaccion directa al entrante).", null, ct);
        }
        else
        {
            await LogRunAsync(tenantId, conversationId, agentId, AiAgentRunLogKind.Error,
                "No se pudo enviar la reaccion", res.Error, null, ct);
        }
    }

    /// <summary>
    /// Entrega un recurso binario (imagen/video/audio/pdf) o una ubicacion por la linea, lo persiste como
    /// saliente y lo registra en bitacora. La URL del archivo se absolutiza contra el origen publico.
    /// </summary>
    private async Task SendAgentAttachmentAsync(Guid tenantId, Guid conversationId, Guid agentId, Guid lineId,
        string phone, AiChatAttachment a, CancellationToken ct)
    {
        // Ubicacion: el Detail del recurso trae "lat,lng".
        if (a.ResourceType == AgentResourceType.Location)
        {
            if (!TryParseLatLng(a.Detail, out var lat, out var lng))
            {
                await LogRunAsync(tenantId, conversationId, agentId, AiAgentRunLogKind.Error,
                    $"Ubicacion invalida: {a.Name}", a.Detail, null, ct);
                return;
            }
            LineSendResult rl;
            try { rl = await _connector.SendLocationAsync(lineId, phone, lat, lng, a.Name, null, ct); }
            catch (Exception ex) { rl = new LineSendResult(false, ex.Message); }
            await PersistOutboundAsync(tenantId, conversationId, $"[Ubicacion] {a.Name}",
                rl.Ok ? "ai_sent" : "ai_failed", ct, MessageMediaType.Location);
            await LogRunAsync(tenantId, conversationId, agentId,
                rl.Ok ? AiAgentRunLogKind.Reply : AiAgentRunLogKind.Error,
                rl.Ok ? $"Ubicacion enviada: {a.Name}" : $"Ubicacion fallo: {a.Name}", a.Detail, rl.Ok ? "OK" : rl.Error, ct);
            return;
        }

        // Binario: absolutiza la URL del recurso (relativa al mismo origen) y envialo con su tipo.
        var url = await ResolvePublicUrlAsync(a.FileUrl, ct);
        if (string.IsNullOrWhiteSpace(url))
        {
            await LogRunAsync(tenantId, conversationId, agentId, AiAgentRunLogKind.Error,
                $"Adjunto sin URL publica: {a.Name}",
                "El recurso no tiene archivo o no hay origen publico configurado para absolutizarlo.", null, ct);
            return;
        }
        var mediaType = a.ResourceType switch
        {
            AgentResourceType.Image => MessageMediaType.Image,
            AgentResourceType.Video => MessageMediaType.Video,
            AgentResourceType.Audio => MessageMediaType.Audio,
            _ => MessageMediaType.Document
        };
        LineSendResult rs;
        try { rs = await _connector.SendAttachmentAsync(lineId, phone, a.ResourceType, url!, null, a.FileName, ct); }
        catch (Exception ex) { rs = new LineSendResult(false, ex.Message); }
        await PersistOutboundAsync(tenantId, conversationId, $"[Adjunto] {a.Name}",
            rs.Ok ? "ai_sent" : "ai_failed", ct, mediaType, url, MimeFromResource(a.ResourceType, a.FileName));
        await LogRunAsync(tenantId, conversationId, agentId,
            rs.Ok ? AiAgentRunLogKind.Reply : AiAgentRunLogKind.Error,
            rs.Ok ? $"Adjunto enviado: {a.Name}" : $"Adjunto fallo: {a.Name}", url, rs.Ok ? "OK" : rs.Error, ct);
    }

    // Parsea "lat,lng" (del Detail de un recurso Ubicacion) en coordenadas, tolerando espacios.
    private static bool TryParseLatLng(string? detail, out double lat, out double lng)
    {
        lat = 0; lng = 0;
        if (string.IsNullOrWhiteSpace(detail)) { return false; }
        var parts = detail.Split(',', 2);
        if (parts.Length != 2) { return false; }
        return double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out lat)
            && double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out lng);
    }

    // Mime orientativo para el mensaje saliente persistido (solo para la bandeja; el envio usa URL).
    private static string? MimeFromResource(AgentResourceType kind, string? fileName) => kind switch
    {
        AgentResourceType.Image => "image/*",
        AgentResourceType.Video => "video/*",
        AgentResourceType.Audio => "audio/*",
        AgentResourceType.Pdf => "application/pdf",
        _ => null
    };

    /// <summary>
    /// URL publica absoluta del logo de la copropiedad (fallback foto de fachada) para enviarla como
    /// imagen por WhatsApp. Absolutiza la ruta relativa contra el origen publico (WebhookPublicUrl del
    /// servidor Evolution maestro = https://app.propia-ad.com). Null si no hay logo o no hay origen.
    /// </summary>
    private async Task<string?> ResolveLogoPublicUrlAsync(Guid tenantId, CancellationToken ct)
    {
        var t = await _db.Tenants.AsNoTracking().Where(x => x.Id == tenantId)
            .Select(x => new { x.LogoUrl, x.FotoFachadaUrl }).FirstOrDefaultAsync(ct);
        var rel = !string.IsNullOrWhiteSpace(t?.LogoUrl) ? t!.LogoUrl : t?.FotoFachadaUrl;
        return await ResolvePublicUrlAsync(rel, ct);
    }

    /// <summary>
    /// Absolutiza una ruta de archivo (relativa al mismo origen) contra el origen publico del servidor
    /// (WebhookPublicUrl del maestro Evolution = app publico). Si ya es absoluta la devuelve tal cual;
    /// null si no hay ruta o no hay origen publico configurado.
    /// </summary>
    private async Task<string?> ResolvePublicUrlAsync(string? pathOrUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl)) { return null; }
        if (pathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) { return pathOrUrl; }
        var origin = await _db.EvolutionMasterConfigs.AsNoTracking()
            .Select(m => m.WebhookPublicUrl).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(origin)) { return null; }
        return $"{origin!.TrimEnd('/')}/{pathOrUrl!.TrimStart('/')}";
    }

    /// <summary>Persiste el saliente del agente (texto o media) y lo difunde a la bandeja en vivo (SignalR).</summary>
    private async Task PersistOutboundAsync(Guid tenantId, Guid conversationId, string body, string sourceTag, CancellationToken ct,
        MessageMediaType mediaType = MessageMediaType.None, string? mediaUrl = null, string? mediaMime = null)
    {
        var now = DateTimeOffset.UtcNow;
        var msg = new Message
        {
            TenantId = tenantId,
            ConversationId = conversationId,
            Direction = MessageDirection.Outbound,
            ExternalId = null, // el connector de PROPIA no devuelve MessageId todavia
            Body = body,
            MessageType = mediaType == MessageMediaType.None ? "text" : mediaType.ToString().ToLowerInvariant(),
            SentAt = now,
            MediaType = mediaType,
            MediaUrl = mediaUrl,
            MediaMimeType = mediaMime,
            SentByName = $"IA ({sourceTag})"
        };
        _db.Messages.Add(msg);

        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId, ct);
        if (conv is not null) { conv.LastMessageAt = now; }

        await _db.SaveChangesAsync(ct);

        if (_broadcaster is not null)
        {
            try
            {
                var dto = new MensajeDto(msg.Id, msg.ConversationId, msg.Direction.ToString(),
                    msg.Body, msg.MessageType, msg.SentAt, msg.SentByName,
                    msg.MediaType.ToString(), msg.MediaUrl, msg.MediaMimeType, msg.ExternalId);
                await _broadcaster.NotifyMessageAddedAsync(tenantId, conversationId, dto, ct);
            }
            catch { /* el despacho no debe fallar si SignalR esta caido */ }
        }
    }

    /// <summary>Escribe una entrada en la bitacora del agente. Best-effort: si falla no rompe el flujo.</summary>
    private async Task LogRunAsync(Guid tenantId, Guid conversationId, Guid agentId,
        AiAgentRunLogKind kind, string title, string? content, string? response, CancellationToken ct)
    {
        try
        {
            _db.AiAgentRunLogs.Add(new AiAgentRunLog
            {
                TenantId = tenantId,
                ConversationId = conversationId,
                AgentId = agentId,
                Kind = kind,
                OccurredAt = DateTimeOffset.UtcNow,
                Title = title.Length > 200 ? title[..200] : title,
                Content = content,
                Response = response
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo escribir bitacora del agente conv {ConvId}", conversationId);
        }
    }

    private static string Preview(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) { return string.Empty; }
        var clean = s.Replace('\r', ' ').Replace('\n', ' ');
        return clean.Length <= max ? clean : clean[..max] + "...";
    }
}
