using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>Item del catalogo de tools para el form de plantillas (SuperAdmin).</summary>
public sealed record TemplateMcpToolCatalogItem(string ToolName, string? Description);
public sealed record TemplateMcpConnectionCatalogItem(string Code, string DisplayName, string? Description, bool Reachable, string? Error, IReadOnlyList<TemplateMcpToolCatalogItem> Tools);

/// <summary>
/// Endpoints del Super Admin para gestionar plantillas globales de agente IA. Estas plantillas se
/// despliegan automaticamente a cada tenant nuevo que se activa via wizard 2.1 paso 5 (si tienen
/// IsActive=true e IncludeInOnboarding=true). Sin RLS - solo SuperAdmin.
/// </summary>
[ApiController]
[Route("admin/ai-agent-templates")]
[Authorize(Policy = AdminController.SuperAdminPolicy)]
public sealed class AdminAiAgentTemplatesController : ControllerBase
{
    private readonly IAiAgentTemplateService _svc;
    private readonly IMcpGateway _mcp;

    public AdminAiAgentTemplatesController(IAiAgentTemplateService svc, IMcpGateway mcp)
    {
        _svc = svc;
        _mcp = mcp;
    }

    /// <summary>
    /// Devuelve el catalogo de conexiones MCP + sus tools EN VIVO. Lo usa el form de plantillas
    /// para mostrar checkboxes. Si una conexion no responde, devuelve Reachable=false.
    /// </summary>
    [HttpGet("mcp-catalog")]
    public async Task<IActionResult> McpCatalog(CancellationToken ct)
    {
        var bearer = BearerToken();
        if (string.IsNullOrEmpty(bearer)) return Unauthorized();

        var resultado = new List<TemplateMcpConnectionCatalogItem>();
        foreach (var con in McpConnectionCatalog.All)
        {
            try
            {
                var tools = await _mcp.ListToolsAsync(con.Code, bearer, ct);
                var items = tools.Select(t => new TemplateMcpToolCatalogItem(t.Name, t.Description)).ToList();
                resultado.Add(new TemplateMcpConnectionCatalogItem(con.Code, con.DisplayName, con.Description, true, null, items));
            }
            catch (Exception ex)
            {
                resultado.Add(new TemplateMcpConnectionCatalogItem(con.Code, con.DisplayName, con.Description, false, ex.Message, Array.Empty<TemplateMcpToolCatalogItem>()));
            }
        }
        return Ok(resultado);
    }

    private string? BearerToken()
    {
        var raw = Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        return raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? raw[prefix.Length..].Trim() : null;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _svc.GetListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveAiAgentTemplateRequest req, CancellationToken ct)
    {
        try
        {
            var (id, email) = Actor();
            var dto = await _svc.SaveAsync(null, req, id, email, Ip(), ct);
            return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            return BadRequest(new { error = "No se pudo crear la plantilla (puede haber un tool duplicado o un valor invalido)." });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveAiAgentTemplateRequest req, CancellationToken ct)
    {
        try
        {
            var (actorId, email) = Actor();
            var dto = await _svc.SaveAsync(id, req, actorId, email, Ip(), ct);
            return Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            // Devolvemos un mensaje generico para evitar exponer stack trace + headers (incluido Authorization).
            return Conflict(new { error = "Conflicto al guardar - alguien modifico la plantilla mientras editabas. Recarga la pagina e intenta de nuevo." });
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            return BadRequest(new { error = "No se pudo guardar la plantilla (puede haber un tool duplicado o un valor invalido)." });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var (actorId, email) = Actor();
        var ok = await _svc.DeleteAsync(id, actorId, email, Ip(), ct);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>Lista todos los agentes existentes (de todos los tenants) que pueden importarse como plantilla.</summary>
    [HttpGet("import-sources")]
    public async Task<IActionResult> ImportSources(CancellationToken ct)
        => Ok(await _svc.ListImportSourcesAsync(ct));

    /// <summary>Crea una plantilla nueva a partir de un agente existente en algun tenant.</summary>
    [HttpPost("import/{agentId:guid}")]
    public async Task<IActionResult> ImportFromAgent(Guid agentId, CancellationToken ct)
    {
        try
        {
            var (actorId, email) = Actor();
            var dto = await _svc.ImportFromAgentAsync(agentId, actorId, email, Ip(), ct);
            return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ---------- helpers ----------

    private (Guid Id, string Email) Actor()
    {
        var id = Guid.TryParse(User.FindFirstValue("user_id"), out var g) ? g : Guid.Empty;
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email") ?? "unknown";
        return (id, email);
    }

    private string? Ip() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
