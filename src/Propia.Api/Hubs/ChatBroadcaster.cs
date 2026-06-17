using Microsoft.AspNetCore.SignalR;
using Propia.Application.InfraestructuraIa;

namespace Propia.Api.Hubs;

/// <summary>
/// Implementacion de IChatBroadcaster que empuja eventos a los grupos del Hub.
/// Vive en Propia.Api porque depende de SignalR (que solo carga en host web).
/// </summary>
public sealed class ChatBroadcaster : IChatBroadcaster
{
    private readonly IHubContext<ChatHub> _hub;

    public ChatBroadcaster(IHubContext<ChatHub> hub) => _hub = hub;

    public Task NotifyMessageAddedAsync(Guid tenantId, Guid conversationId, MensajeDto mensaje, CancellationToken ct = default)
    {
        // Doble emit: a la conversacion (timeline) + al tenant (bandeja con snippet actualizado).
        var convTask = _hub.Clients.Group($"conv-{conversationId}").SendAsync("MessageAdded", mensaje, ct);
        var tenantTask = _hub.Clients.Group($"tenant-{tenantId}").SendAsync("BoardChanged", new { conversationId, mensaje.SentAt, mensaje.Body }, ct);
        return Task.WhenAll(convTask, tenantTask);
    }

    public Task NotifyBoardChangedAsync(Guid tenantId, CancellationToken ct = default)
        => _hub.Clients.Group($"tenant-{tenantId}").SendAsync("BoardChanged", new { }, ct);
}
