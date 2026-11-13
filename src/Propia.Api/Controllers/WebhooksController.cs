using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.InfraestructuraIa;
using Propia.Application.Integraciones;

namespace Propia.Api.Controllers;

/// <summary>
/// Receptor de webhooks de pasarelas externas. Publico (AllowAnonymous) porque la confianza viene
/// de la firma del evento, no de un JWT. El procesamiento es idempotente. Portado de CUBOT.travels.
/// </summary>
[ApiController]
[Route("webhooks")]
[AllowAnonymous]
public class WebhooksController : ControllerBase
{
    private readonly IWompiWebhookService _wompi;
    private readonly IChatIngestService _chatIngest;
    private readonly IMetaWebhookService _meta;
    private readonly IYCloudWebhookService _ycloud;
    private readonly IDeliveryStatusService _deliveryStatus;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(IWompiWebhookService wompi, IChatIngestService chatIngest, IMetaWebhookService meta, IYCloudWebhookService ycloud, IDeliveryStatusService deliveryStatus, IConfiguration config, IWebHostEnvironment env, ILogger<WebhooksController> logger)
    {
        _wompi = wompi;
        _chatIngest = chatIngest;
        _meta = meta;
        _ycloud = ycloud;
        _deliveryStatus = deliveryStatus;
        _config = config;
        _env = env;
        _logger = logger;
    }

    // S-07: comparacion en tiempo constante para tokens/firmas de webhook.
    private static bool ComparacionSegura(string a, string b)
    {
        var ba = System.Text.Encoding.UTF8.GetBytes(a);
        var bb = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    /// <summary>
    /// Webhook de Wompi. Siempre responde 200 para que Wompi no reintente indefinidamente; el
    /// detalle del resultado queda en wompi_webhook_events (cola de conciliacion para el Super Admin).
    /// </summary>
    [HttpPost("wompi")]
    public async Task<IActionResult> Wompi(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var rawJson = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(rawJson))
            return Ok(new { received = false, reason = "empty_body" });

        var result = await _wompi.ProcessAsync(rawJson, ct);
        return Ok(new { received = true, result = result.ToString() });
    }

    /// <summary>
    /// Ingesta de mensaje WhatsApp entrante desde Evolution API. Idempotente por ExternalId.
    /// Header X-Webhook-Token valida la fuente (configurado en appsettings Propia:WebhookToken).
    /// Si el numero esta en lista negra, descarta el mensaje sin persistir.
    /// Portado de CUBOT.travels (ChatIngestService).
    /// </summary>
    [HttpPost("evolution/{tenantId:guid}")]
    public async Task<IActionResult> EvolutionInbound(Guid tenantId, CancellationToken ct)
    {
        // S-07: token OBLIGATORIO. Si no esta configurado, se rechaza (en Production siempre; en Development
        // solo si viene sin token, para permitir pruebas locales configurando Propia:WebhookToken).
        var configured = _config["Propia:WebhookToken"];
        var provided = Request.Headers["X-Webhook-Token"].ToString();
        if (string.IsNullOrWhiteSpace(configured))
        {
            _logger.LogWarning("Webhook Evolution rechazado: Propia:WebhookToken no configurado (tenant {Tenant}).", tenantId);
            return Unauthorized(new { error = "webhook_not_configured" });
        }
        if (string.IsNullOrEmpty(provided) || !ComparacionSegura(provided, configured))
        {
            return Unauthorized(new { error = "invalid_token" });
        }

        // Evolution envia su envelope NATIVO (messages.upsert), no un IngestMessageRequest plano.
        // Leemos el body crudo y lo traducimos con EvolutionWebhookParser (mismo patron que Meta).
        using var reader = new StreamReader(Request.Body);
        var rawJson = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(rawJson)) { return Ok(new { status = "empty" }); }

        // Diagnostico contactos @lid (WhatsApp privacy): logueamos el body CRUDO (truncado) para
        // ver que campos trae Evolution (senderPn, remoteJidAlt, ...) y afinar la resolucion del
        // telefono real. Solo cuando aparece un LID, para no llenar el log de mensajes normales.
        if (rawJson.Contains("@lid", StringComparison.OrdinalIgnoreCase))
        {
            var trunc = rawJson.Length > 2000 ? rawJson[..2000] : rawJson;
            _logger.LogInformation("Evolution webhook @lid (tenant {Tenant}): {Raw}", tenantId, trunc);
        }

        System.Text.Json.JsonDocument doc;
        try { doc = System.Text.Json.JsonDocument.Parse(rawJson); }
        catch { return Ok(new { status = "invalid_json" }); }

        using (doc)
        {
            var payload = EvolutionWebhookParser.Parse(doc.RootElement);
            if (payload is null)
            {
                // No es un entrante: puede ser un ack de entrega (messages.update). Ola D: lo aplicamos a los
                // salientes por ExternalId. Si tampoco es un estado, se ignora (grupo, reaccion, saliente, etc.).
                var estados = EvolutionStatusParser.ParseStatuses(doc.RootElement);
                if (estados.Count > 0)
                {
                    var n = await _deliveryStatus.AplicarAsync(tenantId, estados, ct);
                    return Ok(new { status = "status_processed", updated = n });
                }
                return Ok(new { status = "ignored" }); // saliente, grupo, reaccion o evento no-mensaje
            }

            var result = await _chatIngest.IngestTrustedAsync(tenantId, payload, ct);
            return result switch
            {
                ChatIngestResult.Accepted => Accepted(new { status = "accepted" }),
                ChatIngestResult.Duplicate => Ok(new { status = "duplicate" }),
                ChatIngestResult.Blocked => Ok(new { status = "blocked" }),
                ChatIngestResult.LineNotFound => NotFound(new { error = "line_not_found" }),
                ChatIngestResult.InvalidPayload => BadRequest(new { error = "invalid_payload" }),
                _ => StatusCode(500, new { error = "unknown" })
            };
        }
    }

    /// <summary>
    /// Handshake de verificacion del webhook de Meta (WhatsApp Cloud API). Meta llama con
    /// ?hub.mode=subscribe&amp;hub.verify_token=...&amp;hub.challenge=... al configurar el callback.
    /// Devolvemos el challenge en texto plano si el verify_token coincide con el de una linea Cloud
    /// del tenant. El tenant va en la URL (mismo patron que /webhooks/evolution/{tenantId}).
    /// </summary>
    [HttpGet("meta/{tenantId:guid}")]
    public async Task<IActionResult> MetaVerify(Guid tenantId, CancellationToken ct)
    {
        var mode = Request.Query["hub.mode"].ToString();
        var token = Request.Query["hub.verify_token"].ToString();
        var challenge = Request.Query["hub.challenge"].ToString();
        if (!string.Equals(mode, "subscribe", StringComparison.Ordinal)) { return BadRequest(); }
        if (await _meta.VerifyAsync(tenantId, token, ct)) { return Content(challenge, "text/plain"); }
        return Forbid();
    }

    /// <summary>
    /// Ingesta de entrantes de Meta (WhatsApp Cloud API). Resuelve la linea por phone_number_id
    /// dentro del tenant y persiste. Siempre 200 (Meta reintenta si no recibe 2xx).
    /// </summary>
    [HttpPost("meta/{tenantId:guid}")]
    public async Task<IActionResult> MetaInbound(Guid tenantId, CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var rawJson = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(rawJson)) { return Ok(new { status = "empty" }); }

        // S-07: validar la firma HMAC-SHA256 (X-Hub-Signature-256) con el app secret de Meta. Cierra la
        // inyeccion de mensajes "desde" el telefono de un residente real. Estricto en Production; en
        // Development solo se valida si el app secret esta configurado (para permitir pruebas locales).
        var appSecret = _config["Meta:AppSecret"];
        if (string.IsNullOrWhiteSpace(appSecret))
        {
            if (!_env.IsDevelopment())
            {
                _logger.LogWarning("Webhook Meta rechazado: Meta:AppSecret no configurado (tenant {Tenant}).", tenantId);
                return Unauthorized(new { error = "webhook_not_configured" });
            }
        }
        else
        {
            var firma = Request.Headers["X-Hub-Signature-256"].ToString();
            if (!VerificarFirmaMeta(rawJson, appSecret, firma))
            {
                _logger.LogWarning("Webhook Meta rechazado: firma X-Hub-Signature-256 invalida (tenant {Tenant}).", tenantId);
                return Unauthorized(new { error = "invalid_signature" });
            }
        }

        System.Text.Json.JsonDocument doc;
        try { doc = System.Text.Json.JsonDocument.Parse(rawJson); }
        catch { return Ok(new { status = "invalid_json" }); }

        using (doc)
        {
            var parsed = MetaWebhookParser.Parse(doc.RootElement);
            if (parsed is null)
            {
                // No es un entrante: puede ser un evento de estado (value.statuses[]). Ola D: lo aplicamos a los
                // salientes por wamid. Si tampoco es estado, se ignora.
                var estados = MetaWebhookParser.ParseStatuses(doc.RootElement);
                if (estados.Count > 0)
                {
                    var n = await _deliveryStatus.AplicarAsync(tenantId, estados, ct);
                    return Ok(new { status = "status_processed", updated = n });
                }
                return Ok(new { status = "ignored" });
            }
            var result = await _meta.IngestAsync(tenantId, parsed.PhoneNumberId, parsed.Payload, ct);
            return Ok(new { status = result.ToString().ToLowerInvariant() });
        }
    }

    /// <summary>
    /// Entrantes y estados de entrega de YCloud (lineas YCloud del tenant). La firma YCloud-Signature se valida
    /// con el secreto del endpoint guardado en la linea; sin secreto configurado se rechaza en Production (en
    /// Development se acepta para probar). Responde 200 a todo lo valido (YCloud reintenta si no recibe 2xx).
    /// </summary>
    [HttpPost("ycloud/{tenantId:guid}")]
    public async Task<IActionResult> YCloudInbound(Guid tenantId, CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var rawJson = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(rawJson)) { return Ok(new { status = "empty" }); }

        var firma = await _ycloud.VerificarFirmaAsync(tenantId, rawJson, Request.Headers["YCloud-Signature"].ToString(), ct);
        if (firma == FirmaYCloud.Invalida)
        {
            _logger.LogWarning("Webhook YCloud rechazado: firma invalida (tenant {Tenant}).", tenantId);
            return Unauthorized(new { error = "invalid_signature" });
        }
        if (firma == FirmaYCloud.SinSecreto && !_env.IsDevelopment())
        {
            _logger.LogWarning("Webhook YCloud rechazado: ninguna linea YCloud tiene secreto de webhook (tenant {Tenant}).", tenantId);
            return Unauthorized(new { error = "webhook_not_configured" });
        }

        System.Text.Json.JsonDocument doc;
        try { doc = System.Text.Json.JsonDocument.Parse(rawJson); }
        catch { return Ok(new { status = "invalid_json" }); }
        using (doc)
        {
            if (YCloudWebhookParser.ParseEntrante(doc.RootElement) is { } entrante)
            {
                var result = await _ycloud.IngestAsync(tenantId, entrante.NumeroDestino, entrante.Payload, ct);
                return Ok(new { status = result.ToString().ToLowerInvariant() });
            }
            var estados = YCloudWebhookParser.ParseEstados(doc.RootElement);
            if (estados.Count > 0)
            {
                var n = await _deliveryStatus.AplicarAsync(tenantId, estados, ct);
                return Ok(new { status = "status_processed", updated = n });
            }
            return Ok(new { status = "ignored" });
        }
    }

    // S-07: HMAC-SHA256 del cuerpo crudo con el app secret; el header viene como "sha256=<hex>".
    private static bool VerificarFirmaMeta(string rawBody, string appSecret, string header)
    {
        if (string.IsNullOrWhiteSpace(header)) return false;
        var esperado = header.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase) ? header[7..] : header;
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(appSecret));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawBody));
        var calculado = Convert.ToHexString(hash).ToLowerInvariant();
        return ComparacionSegura(calculado, esperado.ToLowerInvariant());
    }
}
