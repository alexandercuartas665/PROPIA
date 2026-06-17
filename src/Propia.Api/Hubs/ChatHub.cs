using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Propia.Api.Hubs;

/// <summary>
/// Hub SignalR para Conversaciones realtime. Cliente se une a su tenant al conectar y a cada
/// conversacion abierta para recibir mensajes en vivo. Portado de CUBOT.travels (ChatHub).
/// </summary>
[Authorize]
public sealed class ChatHub : Hub
{
    /// <summary>Se llama al conectarse: une al cliente al grupo de su tenant.</summary>
    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirstValue("tenant_id");
        if (!string.IsNullOrEmpty(tenantId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant-{tenantId}");
        }
        await base.OnConnectedAsync();
    }

    /// <summary>El cliente invoca cuando abre una conversacion para recibir sus mensajes.</summary>
    public Task JoinConversation(Guid conversationId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"conv-{conversationId}");

    /// <summary>El cliente invoca al salir de la conversacion (cambio de hilo).</summary>
    public Task LeaveConversation(Guid conversationId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conv-{conversationId}");
}
