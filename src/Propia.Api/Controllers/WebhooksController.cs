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
    private readonly IConfiguration _config;

    public WebhooksController(IWompiWebhookService wompi, IChatIngestService chatIngest, IConfiguration config)
    {
        _wompi = wompi;
        _chatIngest = chatIngest;
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
    public async Task<IActionResult> EvolutionInbound(Guid tenantId, [FromBody] IngestMessageRequest payload, CancellationToken ct)
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
