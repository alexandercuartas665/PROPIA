using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;

namespace Propia.Api.Hubs;

/// <summary>
/// Hub SignalR para Conversaciones realtime. Cliente se une a su tenant al conectar y a cada
/// conversacion abierta para recibir mensajes en vivo. Portado de CUBOT.travels (ChatHub).
/// </summary>
[Authorize]
public sealed class ChatHub : Hub
{
    private readonly ITenantContext _tenant;
    private readonly IConversacionService _conversaciones;

    public ChatHub(ITenantContext tenant, IConversacionService conversaciones)
    {
        _tenant = tenant;
        _conversaciones = conversaciones;
    }

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

    /// <summary>
    /// El cliente invoca cuando abre una conversacion para recibir sus mensajes.
    /// S-10: antes de unir al grupo conv-{id} se verifica que la conversacion pertenezca al
    /// tenant del JWT (RLS + filtro de tenant); si no, se rechaza con HubException. Sin esto,
    /// cualquier usuario autenticado podia suscribirse a la conversacion de otro tenant conociendo
    /// (o adivinando) su GUID y recibir sus mensajes en vivo.
    /// </summary>
    public async Task JoinConversation(Guid conversationId)
    {
        var tenantId = Context.User?.FindFirstValue("tenant_id");
        if (string.IsNullOrEmpty(tenantId) || !Guid.TryParse(tenantId, out var tenantGuid))
            throw new HubException("No autorizado.");

        // Fija el tenant en el scope de esta invocacion para que el interceptor RLS y el filtro
        // de EF acoten la consulta al tenant correcto (el middleware HTTP no corre por invocacion).
        _tenant.SetTenant(tenantGuid);

        if (!await _conversaciones.PerteneceAlTenantActualAsync(conversationId, Context.ConnectionAborted))
            throw new HubException("No autorizado.");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"conv-{conversationId}");
    }

    /// <summary>El cliente invoca al salir de la conversacion (cambio de hilo).</summary>
    public Task LeaveConversation(Guid conversationId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conv-{conversationId}");
}
