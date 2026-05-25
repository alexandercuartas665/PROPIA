namespace Propia.Application.Integraciones;

public enum WompiWebhookResult
{
    Processed,
    Duplicate,
    InvalidSignature,
    NoMatchingPayment,
    Error
}

/// <summary>
/// Procesa los webhooks de Wompi de forma idempotente: valida la firma con el secret de eventos,
/// descarta reenvios, registra el evento y concilia con la Factura/Suscripcion del modulo 0.2.
/// </summary>
public interface IWompiWebhookService
{
    Task<WompiWebhookResult> ProcessAsync(string rawJson, CancellationToken ct = default);
}
