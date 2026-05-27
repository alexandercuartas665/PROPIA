using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>
/// Infraestructura IA de la copropiedad (Capa 2): Lineas WhatsApp, Agentes de IA y consumo.
/// Todo opera sobre el tenant activo del JWT (TenantMiddleware lo setea). RLS aisla por copropiedad.
/// </summary>
[ApiController]
[Route("api/ia")]
[Authorize]
public class IaController : ControllerBase
{
    private readonly IWhatsAppLineService _lines;
    private readonly IWhatsAppConnectorService _connector;
    private readonly IAiAgentService _agents;
    private readonly IAiInferenceService _inference;
    private readonly IAiUsageService _usage;
    private readonly IMcpGateway _mcp;

    public IaController(
        IWhatsAppLineService lines,
        IWhatsAppConnectorService connector,
        IAiAgentService agents,
        IAiInferenceService inference,
        IAiUsageService usage,
        IMcpGateway mcp)
    {
        _lines = lines;
        _connector = connector;
        _agents = agents;
        _inference = inference;
        _usage = usage;
        _mcp = mcp;
    }

    // ---------- Lineas WhatsApp ----------
    [HttpGet("lineas")]
    public async Task<IActionResult> ListLineas(CancellationToken ct) => Ok(await _lines.ListAsync(ct));

    [HttpGet("lineas/servidor-listo")]
    public async Task<IActionResult> MasterReady(CancellationToken ct) => Ok(new { ready = await _connector.MasterReadyAsync(ct) });

    [HttpPost("lineas")]
    public async Task<IActionResult> CrearLinea([FromBody] CreateWhatsAppLineRequest req, CancellationToken ct)
    {
        try
        {
            var r = await _lines.CreateAsync(req, ct);
            return r is null ? BadRequest(new { error = "no_active_tenant" }) : Created("", r);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("lineas/{id:guid}/asignar")]
    public async Task<IActionResult> AsignarLinea(Guid id, [FromBody] AsignarLineaRequest req, CancellationToken ct)
    {
        var r = await _lines.AssignAsync(id, req.UsuarioTenantId, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("lineas/{id:guid}/conectar")]
    public async Task<IActionResult> ConectarLinea(Guid id, CancellationToken ct) => Ok(await _connector.ConnectLineAsync(id, ct));

    [HttpPost("lineas/{id:guid}/refrescar")]
    public async Task<IActionResult> RefrescarLinea(Guid id, CancellationToken ct)
    {
        var r = await _connector.RefreshAsync(id, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("lineas/{id:guid}/desconectar")]
    public async Task<IActionResult> DesconectarLinea(Guid id, CancellationToken ct)
        => await _connector.DisconnectAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("lineas/{id:guid}/probar-envio")]
    public async Task<IActionResult> ProbarEnvio(Guid id, [FromBody] ProbarEnvioRequest req, CancellationToken ct)
        => Ok(await _connector.SendTestAsync(id, req.Phone, req.Text, ct));

    // ---------- Agentes de IA ----------
    [HttpGet("agentes")]
    public async Task<IActionResult> ListAgentes(CancellationToken ct) => Ok(await _agents.ListAsync(ct));

    [HttpGet("agentes/{id:guid}")]
    public async Task<IActionResult> GetAgente(Guid id, CancellationToken ct)
    {
        var r = await _agents.GetAsync(id, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("agentes")]
    public async Task<IActionResult> CrearAgente([FromBody] CreateAiAgentRequest req, CancellationToken ct)
    {
        var r = await _agents.CreateAsync(req, ct);
        return r is null ? BadRequest(new { error = "no_active_tenant" }) : Created("", r);
    }

    [HttpPut("agentes/{id:guid}")]
    public async Task<IActionResult> ActualizarAgente(Guid id, [FromBody] UpdateAiAgentRequest req, CancellationToken ct)
    {
        var r = await _agents.UpdateAsync(id, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPut("agentes/{id:guid}/activar")]
    public async Task<IActionResult> ActivarAgente(Guid id, [FromBody] SetActiveRequest req, CancellationToken ct)
    {
        var r = await _agents.SetActiveAsync(id, req.Active, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("agentes/{id:guid}")]
    public async Task<IActionResult> EliminarAgente(Guid id, CancellationToken ct)
        => await _agents.DeleteAsync(id, ct) ? NoContent() : NotFound();

    // ---------- Recursos del agente ----------
    [HttpPost("agentes/recursos")]
    public async Task<IActionResult> AgregarRecurso([FromBody] CreateAgentResourceRequest req, CancellationToken ct)
    {
        var r = await _agents.AddResourceAsync(req, ct);
        return r is null ? NotFound() : Created("", r);
    }

    [HttpPut("agentes/recursos/{id:guid}")]
    public async Task<IActionResult> ActualizarRecurso(Guid id, [FromBody] UpdateAgentResourceRequest req, CancellationToken ct)
    {
        var r = await _agents.UpdateResourceAsync(id, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("agentes/recursos/{id:guid}")]
    public async Task<IActionResult> EliminarRecurso(Guid id, CancellationToken ct)
        => await _agents.DeleteResourceAsync(id, ct) ? NoContent() : NotFound();

    // ---------- Prompts enrutados ----------
    [HttpPost("agentes/prompts")]
    public async Task<IActionResult> AgregarPrompt([FromBody] CreateAgentPromptRequest req, CancellationToken ct)
    {
        var r = await _agents.AddPromptAsync(req, ct);
        return r is null ? NotFound() : Created("", r);
    }

    [HttpPut("agentes/prompts/{id:guid}")]
    public async Task<IActionResult> ActualizarPrompt(Guid id, [FromBody] UpdateAgentPromptRequest req, CancellationToken ct)
    {
        var r = await _agents.UpdatePromptAsync(id, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("agentes/prompts/{id:guid}")]
    public async Task<IActionResult> EliminarPrompt(Guid id, CancellationToken ct)
        => await _agents.DeletePromptAsync(id, ct) ? NoContent() : NotFound();

    // ---------- Tools MCP del agente (Capa MCP) ----------
    /// <summary>
    /// Devuelve, por cada conexion MCP del catalogo, sus tools EN VIVO (tools/list) marcando
    /// cuales tiene habilitadas este agente. Si una conexion no responde, devuelve Reachable=false.
    /// </summary>
    [HttpGet("agentes/{id:guid}/mcp")]
    public async Task<IActionResult> ListMcpTools(Guid id, CancellationToken ct)
    {
        var agente = await _agents.GetAsync(id, ct);
        if (agente is null) { return NotFound(); }

        var bearer = BearerToken();
        if (string.IsNullOrEmpty(bearer)) { return Unauthorized(); }

        var seleccion = (await _agents.GetMcpToolsAsync(id, ct))
            .Select(s => $"{s.ConnectionCode}|{s.ToolName}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var resultado = new List<AgentMcpConnectionDto>();
        foreach (var con in McpConnectionCatalog.All)
        {
            try
            {
                var tools = await _mcp.ListToolsAsync(con.Code, bearer, ct);
                var toolDtos = tools
                    .Select(t => new AgentMcpToolDto(t.Name, t.Description, seleccion.Contains($"{con.Code}|{t.Name}")))
                    .ToList();
                resultado.Add(new AgentMcpConnectionDto(con.Code, con.DisplayName, con.Description, true, null, toolDtos));
            }
            catch (Exception ex)
            {
                resultado.Add(new AgentMcpConnectionDto(con.Code, con.DisplayName, con.Description, false, ex.Message, Array.Empty<AgentMcpToolDto>()));
            }
        }
        return Ok(resultado);
    }

    /// <summary>Reemplaza la seleccion completa de tools MCP del agente.</summary>
    [HttpPut("agentes/{id:guid}/mcp")]
    public async Task<IActionResult> SaveMcpTools(Guid id, [FromBody] SaveAgentMcpToolsRequest req, CancellationToken ct)
        => await _agents.SetMcpToolsAsync(id, req.Tools ?? Array.Empty<AgentMcpToolSelection>(), ct) ? NoContent() : NotFound();

    private string? BearerToken()
    {
        var raw = Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        return raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? raw[prefix.Length..].Trim() : null;
    }

    // ---------- Probar agente + consumo ----------
    [HttpPost("agentes/{id:guid}/probar")]
    public async Task<IActionResult> ProbarAgente(Guid id, [FromBody] ProbarAgenteRequest req, CancellationToken ct)
        => Ok(await _inference.TestChatAsync(id, req.Turns, req.SystemPromptOverride, ct));

    [HttpGet("consumo")]
    public async Task<IActionResult> Consumo(CancellationToken ct) => Ok(await _usage.GetSummaryAsync(ct));

    [HttpGet("consumo/cuota")]
    public async Task<IActionResult> Cuota(CancellationToken ct) => Ok(await _usage.GetQuotaAsync(ct));

    // ---------- Request bodies locales ----------
    public sealed record AsignarLineaRequest(Guid? UsuarioTenantId);
    public sealed record ProbarEnvioRequest(string Phone, string Text);
    public sealed record SetActiveRequest(bool Active);
    public sealed record ProbarAgenteRequest(List<AiChatTurn> Turns, string? SystemPromptOverride);
}
