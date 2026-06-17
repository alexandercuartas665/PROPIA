namespace Propia.Application.InfraestructuraIa;

/// <summary>Item de la bandeja de conversaciones.</summary>
public sealed record ConversacionDto(
    Guid Id,
    string ContactPhone,
    string? ContactName,
    Guid? WhatsAppLineId,
    string? WhatsAppLineName,
    Guid? PersonaId,
    DateTimeOffset? LastMessageAt,
    DateTimeOffset? ArchivedAt,
    string? UltimoMensajeResumen);

/// <summary>Mensaje en el hilo de una conversacion.</summary>
public sealed record MensajeDto(
    Guid Id,
    Guid ConversationId,
    string Direction,
    string Body,
    string MessageType,
    DateTimeOffset SentAt,
    string? SentByName,
    string? MediaType,
    string? MediaUrl,
    string? MediaMimeType,
    string? ExternalId);

public sealed record EnviarMensajeRequest(string Body);

/// <summary>
/// Servicio de bandeja humana de conversaciones (Oleada IA Conversaciones).
/// Permite al operador ver, responder, archivar y bloquear conversaciones que el agente IA
/// dejo escalar. Portado de CUBOT.travels (ChatService) con alcance reducido para PROPIA Oleada 3.
/// </summary>
public interface IConversacionService
{
    Task<IReadOnlyList<ConversacionDto>> ListarActivasAsync(string? buscar, CancellationToken ct = default);
    Task<IReadOnlyList<ConversacionDto>> ListarArchivadasAsync(CancellationToken ct = default);
    Task<ConversacionDto?> ObtenerAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<MensajeDto>> ListarMensajesAsync(Guid conversationId, CancellationToken ct = default);

    /// <summary>El operador envia un mensaje texto via la linea WhatsApp asociada.</summary>
    Task<MensajeDto?> EnviarTextoAsync(Guid conversationId, EnviarMensajeRequest req, CancellationToken ct = default);

    /// <summary>
    /// El operador envia un adjunto (imagen, video, audio, documento) con caption opcional.
    /// El binario va a IBlobStorage; el mensaje queda con MediaUrl/MediaType.
    /// </summary>
    Task<MensajeDto?> EnviarMediaAsync(Guid conversationId, Stream contenido, string nombreArchivo, string mimeType, string? caption, CancellationToken ct = default);

    /// <summary>Archivar / desarchivar.</summary>
    Task<bool> ArchivarAsync(Guid id, bool archivar, CancellationToken ct = default);

    /// <summary>Reinicia el contexto del agente IA para esta conversacion (siguientes mensajes inician sesion limpia).</summary>
    Task<bool> ReiniciarContextoAgenteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Bloquea al contacto (lo agrega a la lista negra global del tenant) y archiva la conversacion.</summary>
    Task<bool> BloquearContactoAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Para uso del dispatcher: devuelve los mensajes recientes de la conversacion, ya filtrados
    /// por AgentContextResetAt (si esta seteado). Limita a las ultimas N entradas para no inflar
    /// el prompt del LLM. Portado de CUBOT.travels (reconstrucion de turnos para el agente).
    /// </summary>
    Task<IReadOnlyList<MensajeDto>> ListarMensajesParaContextoAsync(Guid conversationId, int maxTurnos = 12, CancellationToken ct = default);
}
