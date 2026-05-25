using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public WebhooksController(IWompiWebhookService wompi) => _wompi = wompi;

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
}
