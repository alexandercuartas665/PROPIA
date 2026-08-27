namespace Propia.Application.Common;

/// <summary>Adjunto de un correo (ej. el PDF de la respuesta).</summary>
public sealed record CorreoAdjunto(string NombreArchivo, string TipoMime, byte[] Contenido);

/// <summary>Resultado de un envio por Gmail.</summary>
public sealed record GmailSendResult(bool Success, string? Error);

/// <summary>
/// Envia un correo desde la cuenta Gmail conectada de un tenant (via Gmail API, refrescando el
/// access_token con el refresh_token guardado). Soporta cuerpo HTML y adjuntos.
/// </summary>
public interface IGmailSender
{
    Task<GmailSendResult> SendAsync(
        Guid tenantId,
        IEnumerable<string> destinatarios,
        string asunto,
        string cuerpoHtml,
        IReadOnlyList<CorreoAdjunto>? adjuntos,
        CancellationToken ct = default);
}
