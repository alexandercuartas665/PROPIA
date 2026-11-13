namespace Propia.Application.InfraestructuraIa;

/// <summary>
/// Webhook de entrada de YCloud (BSP de WhatsApp). Como el de Meta, lleva el tenant en la URL
/// (/webhooks/ycloud/{tenantId}) para resolver la linea con la RLS normal. La autenticidad la da la firma
/// "YCloud-Signature: t={timestamp},s={hmac}" (HMAC-SHA256 de "{t}.{cuerpo}" con el secreto del endpoint,
/// que la copropiedad pega en su linea YCloud).
/// </summary>
public interface IYCloudWebhookService
{
    /// <summary>Valida la firma contra los secretos de las lineas YCloud del tenant.</summary>
    Task<FirmaYCloud> VerificarFirmaAsync(Guid tenantId, string cuerpo, string? header, CancellationToken ct = default);

    /// <summary>Resuelve la linea YCloud por el numero destino (to) y persiste el entrante (ingesta comun).</summary>
    Task<ChatIngestResult> IngestAsync(Guid tenantId, string numeroDestino, IngestMessageRequest payload, CancellationToken ct = default);
}

public enum FirmaYCloud
{
    Valida = 1,
    /// <summary>Ninguna linea YCloud del tenant tiene secreto de webhook configurado.</summary>
    SinSecreto = 2,
    Invalida = 3
}
