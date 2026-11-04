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
    string? ExternalId,
    /// <summary>Ola D: estado de entrega del saliente ("sent"/"delivered"/"read"/"failed"); null en entrantes.</summary>
    string? DeliveryStatus = null,
    /// <summary>Ola D: motivo del proveedor cuando DeliveryStatus es "failed".</summary>
    string? DeliveryError = null);

/// <summary>
/// Ola D (analitica de entregas): conteo de salientes por estado + los ultimos fallos con su motivo
/// + los ultimos salientes que SI salieron con su estado (para las pestanas Entregados/Fallos de la bitacora).
/// </summary>
public sealed record EntregasResumenDto(
    int Aceptados,
    int Entregados,
    int Leidos,
    int Fallidos,
    IReadOnlyList<EntregaFallidaDto> FallosRecientes,
    IReadOnlyList<EntregaDto> EntregasRecientes);

/// <summary>Un saliente que NO se entrego (DeliveryStatus "failed"), con el motivo del proveedor.</summary>
public sealed record EntregaFallidaDto(
    Guid ConversationId,
    string? ContactName,
    string ContactPhone,
    string? LineaNombre,
    DateTimeOffset SentAt,
    string? Body,
    string? DeliveryError,
    string? SentByName);

/// <summary>Un saliente que SI salio (DeliveryStatus "sent"/"delivered"/"read"), para la pestana "Entregados".</summary>
public sealed record EntregaDto(
    Guid ConversationId,
    string? ContactName,
    string ContactPhone,
    string? LineaNombre,
    DateTimeOffset SentAt,
    string? Body,
    string DeliveryStatus,
    string? SentByName);

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

    /// <summary>
    /// S-10: verifica (RLS + filtro de tenant) que la conversacion existe y pertenece al tenant
    /// activo. Lo usa ChatHub antes de unir un cliente al grupo SignalR de la conversacion, para
    /// impedir que un usuario de otro tenant se suscriba a un conv-{guid} ajeno.
    /// </summary>
    Task<bool> PerteneceAlTenantActualAsync(Guid conversationId, CancellationToken ct = default);

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

    /// <summary>
    /// Cierra y OLVIDA la conversacion: reinicia el contexto, BORRA los datos cache que el agente
    /// recordaba del contacto (nombre, unidad, etc.) y la archiva. A diferencia de "Reiniciar contexto"
    /// (que conserva el cache para que el bot retome sin arrastrar el hilo), esto hace un olvido total.
    /// Si el contacto vuelve a escribir, el agente lo atiende desde cero. Portado de ECOREX (cierre).
    /// </summary>
    Task<bool> CerrarYOlvidarAsync(Guid id, CancellationToken ct = default);

    /// <summary>Bloquea al contacto (lo agrega a la lista negra global del tenant) y archiva la conversacion.</summary>
    Task<bool> BloquearContactoAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Para uso del dispatcher: devuelve los mensajes recientes de la conversacion, ya filtrados
    /// por AgentContextResetAt (si esta seteado). Limita a las ultimas N entradas para no inflar
    /// el prompt del LLM. Portado de CUBOT.travels (reconstrucion de turnos para el agente).
    /// </summary>
    Task<IReadOnlyList<MensajeDto>> ListarMensajesParaContextoAsync(Guid conversationId, int maxTurnos = 12, CancellationToken ct = default);

    /// <summary>
    /// Ola D (analitica de entregas): resumen del estado de entrega de los salientes del tenant (conteo por
    /// estado + los ultimos <paramref name="take"/> fallos con su motivo). Tenant-scoped (RLS).
    /// </summary>
    Task<EntregasResumenDto> ObtenerResumenEntregasAsync(int take = 50, CancellationToken ct = default);
}
