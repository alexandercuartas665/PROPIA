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
    private readonly IMcpGateway _mcp;

    public AgentAdminApiController(IAdminAgentService svc, IMcpGateway mcp)
    {
        _svc = svc;
        _mcp = mcp;
    }

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
        var toolKeys = (req.ToolKeys ?? Array.Empty<string>())
            .Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).ToList();

        // Gap cerrado: validar las keys contra el catalogo EN VIVO de la conexion "copropiedades" (antes
        // aceptaba cualquier string). Lista vacia = limpiar seleccion, no requiere catalogo.
        if (toolKeys.Count > 0)
        {
            var bearer = BearerToken();
            if (string.IsNullOrEmpty(bearer)) { return Unauthorized(); }

            // Intenta validar contra el catalogo en vivo. Si el servidor MCP no responde, NO se bloquea
            // (se acepta como antes de este gap): no atamos el set de tools a la disponibilidad del MCP.
            IReadOnlyList<McpToolInfo>? validas = null;
            try { validas = await _mcp.ListToolsAsync(McpConnectionCatalog.Copropiedades, bearer, ct); }
            catch { /* catalogo MCP no disponible: se omite la validacion */ }

            if (validas is not null)
            {
                var validNames = validas.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var invalidas = toolKeys.Where(k => !validNames.Contains(k)).Distinct().ToList();
                if (invalidas.Count > 0)
                    return BadRequest(new
                    {
                        error = "Hay toolKeys que no existen en la conexion 'copropiedades'.",
                        invalidKeys = invalidas,
                        validKeys = validNames.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray()
                    });
            }
        }

        var (actorId, actorEmail, ip) = Actor();
        var detail = await _svc.SetAgentToolsAsync(tenantId, agentId, toolKeys, actorId, actorEmail, ip, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>Gap cerrado: catalogo de tools MCP EN VIVO (conexion "copropiedades" y futuras). Permite
    /// descubrir por API que toolKeys son validas antes de PUT tools. Reachable=false si no responde.</summary>
    [HttpGet("mcp-tools")]
    public async Task<IActionResult> ListMcpTools(Guid tenantId, CancellationToken ct)
    {
        var bearer = BearerToken();
        if (string.IsNullOrEmpty(bearer)) { return Unauthorized(); }

        var resultado = new List<AdminMcpConnectionCatalogDto>();
        foreach (var con in McpConnectionCatalog.All)
        {
            try
            {
                var tools = await _mcp.ListToolsAsync(con.Code, bearer, ct);
                resultado.Add(new AdminMcpConnectionCatalogDto(con.Code, con.DisplayName, con.Description, true, null,
                    tools.Select(t => new AdminMcpToolCatalogDto(t.Name, t.Description)).ToList()));
            }
            catch (Exception ex)
            {
                resultado.Add(new AdminMcpConnectionCatalogDto(con.Code, con.DisplayName, con.Description, false, ex.Message,
                    Array.Empty<AdminMcpToolCatalogDto>()));
            }
        }
        return Ok(resultado);
    }

    [HttpGet("lines")]
    public async Task<IActionResult> ListLines(Guid tenantId, CancellationToken ct)
        => Ok(await _svc.ListLinesAsync(tenantId, ct));

    [HttpPost("agents/{agentId:guid}/line-binding")]
    public async Task<IActionResult> BindLine(Guid tenantId, Guid agentId, [FromBody] BindLineRequest req, CancellationToken ct)
    {
        var (actorId, actorEmail, ip) = Actor();
        var ok = await _svc.BindLineAsync(tenantId, agentId, req.WhatsAppLineId, req.Reassign, actorId, actorEmail, ip, ct);
        return ok
            ? Ok(new { ok = true })
            : Conflict(new { ok = false, error = "La linea no existe o ya esta atendida por otro agente (usa reassign:true para reasignar)." });
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

    /// <summary>El bearer del propio request, que el gateway MCP presenta a /mcp para listar las tools.</summary>
    private string? BearerToken()
    {
        var raw = Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        return raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? raw[prefix.Length..].Trim() : null;
    }
}
