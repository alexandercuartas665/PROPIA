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

    [HttpPost("agents")]
    public async Task<IActionResult> CreateAgent(Guid tenantId, [FromBody] CreateAiAgentRequest req, CancellationToken ct)
    {
        var (actorId, actorEmail, ip) = Actor();
        var created = await _svc.CreateAgentAsync(tenantId, req, actorId, actorEmail, ip, ct);
        return created is null
            ? Problem("No se pudo crear el agente.")
            : CreatedAtAction(nameof(GetAgent), new { tenantId, agentId = created.Id }, created);
    }

    [HttpPut("agents/{agentId:guid}")]
    public async Task<IActionResult> UpdateAgent(Guid tenantId, Guid agentId, [FromBody] UpdateAiAgentRequest req, CancellationToken ct)
    {
        var (actorId, actorEmail, ip) = Actor();
        var updated = await _svc.UpdateAgentAsync(tenantId, agentId, req, actorId, actorEmail, ip, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPut("agents/{agentId:guid}/tools")]
    public async Task<IActionResult> SetTools(Guid tenantId, Guid agentId, [FromBody] SetAgentToolsRequest req, CancellationToken ct)
    {
        var (actorId, actorEmail, ip) = Actor();
        var detail = await _svc.SetAgentToolsAsync(tenantId, agentId, req.ToolKeys ?? Array.Empty<string>(), actorId, actorEmail, ip, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpGet("lines")]
    public async Task<IActionResult> ListLines(Guid tenantId, CancellationToken ct)
        => Ok(await _svc.ListLinesAsync(tenantId, ct));

    [HttpPost("agents/{agentId:guid}/line-binding")]
    public async Task<IActionResult> BindLine(Guid tenantId, Guid agentId, [FromBody] BindLineRequest req, CancellationToken ct)
    {
        var (actorId, actorEmail, ip) = Actor();
        var ok = await _svc.BindLineAsync(tenantId, agentId, req.WhatsAppLineId, actorId, actorEmail, ip, ct);
        return ok
            ? Ok(new { ok = true })
            : Conflict(new { ok = false, error = "La linea no existe o ya esta atendida por otro agente." });
    }

    [HttpDelete("agents/{agentId:guid}/line-binding/{whatsAppLineId:guid}")]
    public async Task<IActionResult> UnbindLine(Guid tenantId, Guid agentId, Guid whatsAppLineId, CancellationToken ct)
    {
        var (actorId, actorEmail, ip) = Actor();
        var ok = await _svc.UnbindLineAsync(tenantId, agentId, whatsAppLineId, actorId, actorEmail, ip, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpGet("agent-logs")]
    public async Task<IActionResult> ListLogs(Guid tenantId, CancellationToken ct)
        => Ok(await _svc.ListLogConversationsAsync(tenantId, ct));

    [HttpGet("agent-logs/{conversationId:guid}")]
    public async Task<IActionResult> GetLog(Guid tenantId, Guid conversationId, CancellationToken ct)
        => Ok(await _svc.GetConversationLogAsync(tenantId, conversationId, ct));

    /// <summary>Actor del token de Super Admin para auditar (user_id, email, ip).</summary>
    private (Guid actorId, string actorEmail, string? ip) Actor()
    {
        Guid.TryParse(User.FindFirst("user_id")?.Value, out var actorId);
        var actorEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value ?? "superadmin";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return (actorId, actorEmail, ip);
    }
}
