namespace Propia.Application.InfraestructuraIa;

public enum ChatIngestResult
{
    Accepted,
    Duplicate,
    Blocked,
    LineNotFound,
    InvalidPayload
}

/// <summary>Payload normalizado del webhook de Evolution para ingesta de un entrante.</summary>
public sealed record IngestMessageRequest(
    /// <summary>Id externo del mensaje (Evolution). Obligatorio para idempotencia.</summary>
    string ExternalId,
    /// <summary>Telefono del remitente (solo digitos o E.164, se normaliza).</summary>
    string ContactPhone,
    /// <summary>Nombre del remitente reportado por WhatsApp (pushName), opcional.</summary>
    string? ContactName,
    /// <summary>Texto del mensaje (vacio si solo media).</summary>
    string Body,
    /// <summary>Tipo: text, image, video, audio, document, location.</summary>
    string MessageType = "text",
    /// <summary>Marca de tiempo del mensaje (UTC). Si null usamos UtcNow.</summary>
    DateTimeOffset? SentAt = null,
    /// <summary>InstanceName de la linea Evolution. Si no se pasa, se busca por phone destinatario.</summary>
    string? InstanceName = null);

/// <summary>
/// Ingesta de mensajes entrantes desde webhook publico (Evolution).
/// Idempotente por (TenantId, ExternalId). Bypassa la query filter porque opera
/// con TenantId explicito (sin JWT). Portado de CUBOT.travels.
/// </summary>
public interface IChatIngestService
{
    Task<ChatIngestResult> IngestTrustedAsync(Guid tenantId, IngestMessageRequest payload, CancellationToken ct = default);
}
