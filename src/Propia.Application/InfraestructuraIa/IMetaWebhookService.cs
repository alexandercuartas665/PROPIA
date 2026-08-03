namespace Propia.Application.InfraestructuraIa;

/// <summary>
/// Webhook de WhatsApp Cloud API (Meta). Como el de Evolution, lleva el tenant en la URL
/// (/webhooks/meta/{tenantId}) para resolver la linea por RLS normal (sin cross-tenant). Portado
/// de CUBOT.travels, adaptado al patron tenant-en-URL de PROPIA.
/// </summary>
public interface IMetaWebhookService
{
    /// <summary>Handshake GET: true si el verify_token coincide con el de alguna linea Cloud del tenant.</summary>
    Task<bool> VerifyAsync(Guid tenantId, string token, CancellationToken ct = default);

    /// <summary>Ingesta POST: resuelve la linea Cloud por phone_number_id y persiste el entrante.</summary>
    Task<ChatIngestResult> IngestAsync(Guid tenantId, string phoneNumberId, IngestMessageRequest payload, CancellationToken ct = default);
}
