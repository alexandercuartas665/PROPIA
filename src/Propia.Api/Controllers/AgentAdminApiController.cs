using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.InfraestructuraIa;

namespace Propia.Api.Controllers;

/// <summary>
/// API REST de administracion de agentes de IA para el Super Admin de plataforma (Capa 6). Con un JWT
/// de Super Admin (claim is_super_admin=true, obtenido en POST /connect/login) un cliente de maquina
/// (ej. una instancia de Claude) puede leer/editar los agentes de CUALQUIER copropiedad y leer su
/// bitacora, de forma cross-tenant. El tenant va en la ruta y el servicio lo fija en EF + RLS.
/// Reutiliza la policy SuperAdmin ya existente. Portado de CUBOT.travels, adaptado a la RLS de PROPIA.
/// Fail-closed: sin token 401; sin is_super_admin=true 403; agente/conversacion inexistente 404.
/// </summary>
[ApiController]
[Route("admin/tenants/{tenantId:guid}")]
[Authorize(Policy = AdminController.SuperAdminPolicy)]
public class AgentAdminApiController : ControllerBase
{
    private readonly IAdminAgentService _svc;

    public AgentAdminApiController(IAdminAgentService svc) => _svc = svc;

    [HttpGet("agents")]
    public async Task<IActionResult> ListAgents(Guid tenantId, CancellationToken ct)
        => Ok(await _svc.ListAgentsAsync(tenantId, ct));

    [HttpGet("agents/{agentId:guid}")]
    public async Task<IActionResult> GetAgent(Guid tenantId, Guid agentId, CancellationToken ct)
    {
        var a = await _svc.GetAgentAsync(tenantId, agentId, ct);
        return a is null ? NotFound() : Ok(a);
    }

    [HttpPut("agents/{agentId:guid}")]
    public async Task<IActionResult> UpdateAgent(Guid tenantId, Guid agentId, [FromBody] UpdateAiAgentRequest req, CancellationToken ct)
    {
        Guid.TryParse(User.FindFirst("user_id")?.Value, out var actorId);
        var actorEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value ?? "superadmin";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var updated = await _svc.UpdateAgentAsync(tenantId, agentId, req, actorId, actorEmail, ip, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpGet("agent-logs")]
    public async Task<IActionResult> ListLogs(Guid tenantId, CancellationToken ct)
        => Ok(await _svc.ListLogConversationsAsync(tenantId, ct));

    [HttpGet("agent-logs/{conversationId:guid}")]
    public async Task<IActionResult> GetLog(Guid tenantId, Guid conversationId, CancellationToken ct)
        => Ok(await _svc.GetConversationLogAsync(tenantId, conversationId, ct));
}
