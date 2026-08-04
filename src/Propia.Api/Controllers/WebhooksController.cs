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
    private readonly IConfiguration _config;

    public WebhooksController(IWompiWebhookService wompi, IChatIngestService chatIngest, IMetaWebhookService meta, IConfiguration config)
    {
        _wompi = wompi;
        _chatIngest = chatIngest;
        _meta = meta;
        _config = config;
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
        // Token compartido (config global por ahora; en proxima oleada se hara por-tenant).
        var configured = _config["Propia:WebhookToken"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var provided = Request.Headers["X-Webhook-Token"].ToString();
            if (!string.Equals(provided, configured, StringComparison.Ordinal))
            {
                return Unauthorized(new { error = "invalid_token" });
            }
        }

        // Evolution envia su envelope NATIVO (messages.upsert), no un IngestMessageRequest plano.
        // Leemos el body crudo y lo traducimos con EvolutionWebhookParser (mismo patron que Meta).
        using var reader = new StreamReader(Request.Body);
        var rawJson = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(rawJson)) { return Ok(new { status = "empty" }); }

        System.Text.Json.JsonDocument doc;
        try { doc = System.Text.Json.JsonDocument.Parse(rawJson); }
        catch { return Ok(new { status = "invalid_json" }); }

        using (doc)
        {
            var payload = EvolutionWebhookParser.Parse(doc.RootElement);
            if (payload is null) { return Ok(new { status = "ignored" }); } // saliente, grupo, reaccion o evento no-mensaje

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

        System.Text.Json.JsonDocument doc;
        try { doc = System.Text.Json.JsonDocument.Parse(rawJson); }
        catch { return Ok(new { status = "invalid_json" }); }

        using (doc)
        {
            var parsed = MetaWebhookParser.Parse(doc.RootElement);
            if (parsed is null) { return Ok(new { status = "ignored" }); } // eventos de estado, etc.
            var result = await _meta.IngestAsync(tenantId, parsed.PhoneNumberId, parsed.Payload, ct);
            return Ok(new { status = result.ToString().ToLowerInvariant() });
        }
    }
}
