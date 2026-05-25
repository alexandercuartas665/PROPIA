using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Evento recibido del webhook de Wompi (Super Admin). Entidad GLOBAL. Garantiza idempotencia:
/// ProviderEventId es unico, asi un reenvio del mismo evento no se procesa dos veces. Guarda el
/// payload crudo y el resultado de la aplicacion para conciliacion. Portado de CUBOT.travels.
/// </summary>
public class WompiWebhookEvent : BaseEntity
{
    public string ProviderEventId { get; set; } = null!;
    public bool SignatureValid { get; set; }
    public string RawPayload { get; set; } = null!;
    public WebhookProcessingStatus ProcessingStatus { get; set; } = WebhookProcessingStatus.Received;
    public string? TransactionId { get; set; }
    public string? Reference { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
