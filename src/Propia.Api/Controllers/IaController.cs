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
    private readonly IAiAgentLineBindingService _bindings;
    private readonly IAiAgentCacheService _cache;
    private readonly IAiInferenceService _inference;
    private readonly IAiUsageService _usage;
    private readonly IMcpGateway _mcp;
    private readonly IListaNegraService _listaNegra;
    private readonly IConversacionService _conversaciones;
    private readonly IAgentRunLogService _bitacora;
    private readonly IAutomationService _automations;

    public IaController(
        IWhatsAppLineService lines,
        IWhatsAppConnectorService connector,
        IAiAgentService agents,
        IAiAgentLineBindingService bindings,
        IAiAgentCacheService cache,
        IAiInferenceService inference,
        IAiUsageService usage,
        IMcpGateway mcp,
        IListaNegraService listaNegra,
        IConversacionService conversaciones,
        IAgentRunLogService bitacora,
        IAutomationService automations)
    {
        _lines = lines;
        _connector = connector;
        _agents = agents;
        _bindings = bindings;
        _cache = cache;
        _inference = inference;
        _usage = usage;
        _mcp = mcp;
        _listaNegra = listaNegra;
        _conversaciones = conversaciones;
        _bitacora = bitacora;
        _automations = automations;
    }

    // ---------- Automatizaciones (Infraestructura IA) ----------
    [HttpGet("automatizaciones")]
    public async Task<IActionResult> ListarAutomatizaciones(CancellationToken ct)
    {
        await _automations.SeedDefaultsAsync(ct); // siembra ejemplos la primera vez
        return Ok(await _automations.ListAsync(ct));
    }

    [HttpPost("automatizaciones")]
    public async Task<IActionResult> CrearAutomatizacion([FromBody] SaveAutomationRuleRequest req, CancellationToken ct)
    {
        var r = await _automations.CreateAsync(req, ct);
        return r is null ? BadRequest(new { error = "No se pudo crear la regla." }) : Created("", r);
    }

    [HttpPut("automatizaciones/{id:guid}")]
    public async Task<IActionResult> ActualizarAutomatizacion(Guid id, [FromBody] SaveAutomationRuleRequest req, CancellationToken ct)
    {
        var r = await _automations.UpdateAsync(id, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("automatizaciones/{id:guid}/activar")]
    public async Task<IActionResult> ActivarAutomatizacion(Guid id, [FromQuery] bool activo, CancellationToken ct)
        => await _automations.SetActiveAsync(id, activo, ct) ? Ok() : NotFound();

    [HttpDelete("automatizaciones/{id:guid}")]
    public async Task<IActionResult> EliminarAutomatizacion(Guid id, CancellationToken ct)
        => await _automations.DeleteAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("automatizaciones/ejecutar")]
    public async Task<IActionResult> EjecutarAutomatizaciones(CancellationToken ct)
    {
        var r = await _automations.RunNowAsync(ct);
        return Ok(new { rulesEvaluated = r.RulesEvaluated, actionsFired = r.ActionsFired });
    }

    // ---------- Bitacora del agente (Infraestructura IA - auto-atencion) ----------
    [HttpGet("bitacora/conversaciones")]
    public async Task<IActionResult> BitacoraConversaciones(CancellationToken ct)
        => Ok(await _bitacora.ListConversationsAsync(ct));

    [HttpGet("bitacora/conversaciones/{id:guid}/log")]
    public async Task<IActionResult> BitacoraLog(Guid id, CancellationToken ct)
        => Ok(await _bitacora.GetConversationLogAsync(id, ct));

    [HttpGet("bitacora/conversaciones/{id:guid}/cache")]
    public async Task<IActionResult> BitacoraCache(Guid id, CancellationToken ct)
        => Ok(await _bitacora.GetConversationCacheAsync(id, ct));

    [HttpGet("bitacora/conversaciones/{id:guid}/mensajes")]
    public async Task<IActionResult> BitacoraMensajes(Guid id, CancellationToken ct)
        => Ok(await _bitacora.GetConversationMessagesAsync(id, ct));

    [HttpPost("bitacora/limpiar")]
    public async Task<IActionResult> BitacoraLimpiar(CancellationToken ct)
    {
        var (logs, cache) = await _bitacora.ClearAllAsync(ct);
        return Ok(new { logs, cache });
    }

    [HttpPost("bitacora/conversaciones/{id:guid}/reiniciar")]
    public async Task<IActionResult> BitacoraReiniciar(Guid id, CancellationToken ct)
    {
        var (logs, cache, messages) = await _bitacora.ResetConversationMemoryAsync(id, ct);
        return Ok(new { logs, cache, messages });
    }

    // ---------- Conversaciones humanas (Oleada IA #3) ----------
    [HttpGet("conversaciones")]
    public async Task<IActionResult> ListarConversaciones([FromQuery] string? q, CancellationToken ct)
        => Ok(await _conversaciones.ListarActivasAsync(q, ct));

    [HttpGet("conversaciones/archivadas")]
    public async Task<IActionResult> ListarConversacionesArchivadas(CancellationToken ct)
        => Ok(await _conversaciones.ListarArchivadasAsync(ct));

    [HttpGet("conversaciones/{id:guid}")]
    public async Task<IActionResult> ObtenerConversacion(Guid id, CancellationToken ct)
    {
        var c = await _conversaciones.ObtenerAsync(id, ct);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpGet("conversaciones/{id:guid}/mensajes")]
    public async Task<IActionResult> ListarMensajes(Guid id, CancellationToken ct)
        => Ok(await _conversaciones.ListarMensajesAsync(id, ct));

    [HttpPost("conversaciones/{id:guid}/mensajes")]
    public async Task<IActionResult> EnviarMensaje(Guid id, [FromBody] EnviarMensajeRequest req, CancellationToken ct)
    {
        var msg = await _conversaciones.EnviarTextoAsync(id, req, ct);
        return msg is null ? NotFound() : Created("", msg);
    }

    /// <summary>Envia un adjunto (imagen, video, audio, documento) con caption opcional. multipart/form-data.</summary>
    [HttpPost("conversaciones/{id:guid}/mensajes/media")]
    [RequestSizeLimit(50_000_000)] // 50 MB
    public async Task<IActionResult> EnviarMedia(Guid id, [FromForm] IFormFile archivo, [FromForm] string? caption, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0) return BadRequest(new { error = "Archivo vacio." });
        using var stream = archivo.OpenReadStream();
        var msg = await _conversaciones.EnviarMediaAsync(id, stream, archivo.FileName,
            string.IsNullOrEmpty(archivo.ContentType) ? "application/octet-stream" : archivo.ContentType,
            caption, ct);
        return msg is null ? NotFound() : Created("", msg);
    }

    [HttpPut("conversaciones/{id:guid}/archivar")]
    public async Task<IActionResult> ArchivarConversacion(Guid id, [FromBody] ArchivarRequest req, CancellationToken ct)
        => await _conversaciones.ArchivarAsync(id, req.Archivar, ct) ? NoContent() : NotFound();

    [HttpPut("conversaciones/{id:guid}/reiniciar-contexto")]
    public async Task<IActionResult> ReiniciarContextoAgente(Guid id, CancellationToken ct)
        => await _conversaciones.ReiniciarContextoAgenteAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("conversaciones/{id:guid}/bloquear")]
    public async Task<IActionResult> BloquearContactoConversacion(Guid id, CancellationToken ct)
        => await _conversaciones.BloquearContactoAsync(id, ct) ? NoContent() : NotFound();

    public sealed record ArchivarRequest(bool Archivar);

    // ---------- Lista negra global del tenant (portado de CUBOT.travels) ----------
    [HttpGet("lista-negra")]
    public async Task<IActionResult> ListarBloqueados(CancellationToken ct)
        => Ok(await _listaNegra.ListarAsync(ct));

    [HttpPost("lista-negra")]
    public async Task<IActionResult> BloquearNumero([FromBody] AgregarNumeroBloqueadoRequest req, CancellationToken ct)
    {
        var result = await _listaNegra.AgregarAsync(req, ct);
        if (result is null) return BadRequest(new { error = "Telefono invalido (minimo 7 digitos) o sin tenant activo." });
        return Created("", result);
    }

    [HttpDelete("lista-negra/{id:guid}")]
    public async Task<IActionResult> DesbloquearNumero(Guid id, CancellationToken ct)
        => await _listaNegra.EliminarAsync(id, ct) ? NoContent() : NotFound();

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

    [HttpDelete("lineas/{id:guid}")]
    public async Task<IActionResult> EliminarLinea(Guid id, CancellationToken ct)
        => await _connector.DeleteLineAsync(id, ct) ? NoContent() : NotFound();

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

    // ---------- Duplicar + versionado (portado de CUBOT.travels) ----------
    [HttpPost("agentes/{id:guid}/duplicar")]
    public async Task<IActionResult> DuplicarAgente(Guid id, CancellationToken ct)
    {
        var r = await _agents.DuplicateAsync(id, ct);
        return r is null ? NotFound() : Created("", r);
    }

    [HttpGet("agentes/{id:guid}/versiones")]
    public async Task<IActionResult> HistorialPrompts(Guid id, CancellationToken ct)
        => Ok(await _agents.GetPromptHistoryAsync(id, ct));

    [HttpPost("agentes/{id:guid}/versiones/{indice:int}/restaurar")]
    public async Task<IActionResult> RestaurarPrompt(Guid id, int indice, CancellationToken ct)
    {
        var r = await _agents.RestorePromptVersionAsync(id, indice, ct);
        return r is null ? NotFound() : Ok(r);
    }

    // ---------- Lineas WhatsApp atendidas por el agente ----------
    [HttpGet("agentes/{id:guid}/lineas")]
    public async Task<IActionResult> LineasAtendidas(Guid id, CancellationToken ct)
        => Ok(await _bindings.ListForAgentAsync(id, ct));

    [HttpPost("agentes/{id:guid}/lineas")]
    public async Task<IActionResult> VincularLinea(Guid id, [FromBody] SetAgentLineBindingRequest req, CancellationToken ct)
        => await _bindings.SetAsync(id, req, ct) ? NoContent() : BadRequest();

    // ---------- Datos cache del agente ----------
    [HttpGet("agentes/{id:guid}/cache")]
    public async Task<IActionResult> ListarCache(Guid id, CancellationToken ct)
        => Ok(await _cache.ListFieldsAsync(id, ct));

    [HttpPost("agentes/{id:guid}/cache")]
    public async Task<IActionResult> CrearCache(Guid id, [FromBody] CreateAgentCacheFieldRequest req, CancellationToken ct)
    {
        var r = await _cache.CreateFieldAsync(id, req, ct);
        return r is null ? NotFound() : Created("", r);
    }

    [HttpPut("agentes/cache/{fieldId:guid}")]
    public async Task<IActionResult> ActualizarCache(Guid fieldId, [FromBody] UpdateAgentCacheFieldRequest req, CancellationToken ct)
    {
        var r = await _cache.UpdateFieldAsync(fieldId, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("agentes/cache/{fieldId:guid}")]
    public async Task<IActionResult> EliminarCache(Guid fieldId, CancellationToken ct)
        => await _cache.DeleteFieldAsync(fieldId, ct) ? NoContent() : NotFound();

    [HttpPost("agentes/{id:guid}/cache/sticky")]
    public async Task<IActionResult> BulkStickyCache(Guid id, [FromQuery] bool actualizable, CancellationToken ct)
        => Ok(new { afectados = await _cache.BulkSetFieldsUpdatableAsync(id, actualizable, ct) });

    [HttpGet("agentes/{id:guid}/cache/valores")]
    public async Task<IActionResult> ValoresCache(Guid id, [FromQuery] Guid? sessionId, CancellationToken ct)
        => Ok(await _cache.GetValuesAsync(id, sessionId ?? id, ct));

    [HttpDelete("agentes/{id:guid}/cache/valores")]
    public async Task<IActionResult> LimpiarValoresCache(Guid id, [FromQuery] Guid? sessionId, CancellationToken ct)
        => Ok(new { borrados = await _cache.ClearValuesAsync(id, sessionId ?? id, ct) });

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
        => Ok(await _inference.TestChatAsync(id, req.Turns, req.SystemPromptOverride, BearerToken(), req.ContactPhone, req.ConversationId, ct));

    [HttpGet("consumo")]
    public async Task<IActionResult> Consumo(CancellationToken ct) => Ok(await _usage.GetSummaryAsync(ct));

    [HttpGet("consumo/cuota")]
    public async Task<IActionResult> Cuota(CancellationToken ct) => Ok(await _usage.GetQuotaAsync(ct));

    // ---------- Request bodies locales ----------
    public sealed record AsignarLineaRequest(Guid? UsuarioTenantId);
    public sealed record ProbarEnvioRequest(string Phone, string Text);
    public sealed record SetActiveRequest(bool Active);
    public sealed record ProbarAgenteRequest(List<AiChatTurn> Turns, string? SystemPromptOverride, string? ContactPhone = null, Guid? ConversationId = null);
}
