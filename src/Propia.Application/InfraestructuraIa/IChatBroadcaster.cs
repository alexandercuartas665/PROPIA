namespace Propia.Application.InfraestructuraIa;

/// <summary>
/// Push de eventos del modulo Conversaciones via SignalR. La implementacion usa IHubContext.
/// Grupos: tenant-{tenantId} (cambios de bandeja) y conv-{conversationId} (mensajes del hilo).
/// Portado de CUBOT.travels (IChatBroadcaster).
/// </summary>
public interface IChatBroadcaster
{
    /// <summary>Emite que un mensaje nuevo entro/salio en una conversacion. Receptor: grupo conv-{id}.</summary>
    Task NotifyMessageAddedAsync(Guid tenantId, Guid conversationId, MensajeDto mensaje, CancellationToken ct = default);

    /// <summary>Emite que la bandeja del tenant cambio (nueva conv, archivada, etc.). Receptor: grupo tenant-{tenantId}.</summary>
    Task NotifyBoardChangedAsync(Guid tenantId, CancellationToken ct = default);
}
