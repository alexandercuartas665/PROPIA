using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Propia.Domain.Entities;

namespace Propia.Api.Controllers;

/// <summary>
/// Preferencias de UI del usuario autenticado. Hoy solo guarda el tema visual
/// (light/dark) en ApplicationUser.UiTheme; en Fase 2 se ampliara a otras
/// preferencias (colapsar sidebar, idioma, densidad, etc).
/// </summary>
[ApiController]
[Route("api/preferencias")]
[Authorize]
public class PreferenciasController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;

    public PreferenciasController(UserManager<ApplicationUser> users) => _users = users;

    public record UiThemeDto(string? Theme);
    public record SetUiThemeRequest(string Theme);

    [HttpGet("ui-theme")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var userId = User.FindFirstValue("user_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized();
        var u = await _users.FindByIdAsync(uid.ToString());
        if (u is null) return NotFound();
        return Ok(new UiThemeDto(u.UiTheme));
    }

    [HttpPut("ui-theme")]
    public async Task<IActionResult> Set([FromBody] SetUiThemeRequest req, CancellationToken ct)
    {
        if (req is null || (req.Theme != "light" && req.Theme != "dark"))
            return BadRequest(new { error = "Tema invalido. Valores permitidos: 'light' o 'dark'." });

        var userId = User.FindFirstValue("user_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized();
        var u = await _users.FindByIdAsync(uid.ToString());
        if (u is null) return NotFound();

        u.UiTheme = req.Theme;
        var res = await _users.UpdateAsync(u);
        return res.Succeeded ? NoContent() : BadRequest(new { error = "No se pudo guardar la preferencia." });
    }

    // ---- Estado de secciones de Mi Copropiedad (RN-24) ----
    public record SeccionesDto(string? Colapsadas);
    public record SetSeccionesRequest(string? Colapsadas);

    [HttpGet("mi-copropiedad-secciones")]
    public async Task<IActionResult> GetSecciones(CancellationToken ct)
    {
        var userId = User.FindFirstValue("user_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized();
        var u = await _users.FindByIdAsync(uid.ToString());
        if (u is null) return NotFound();
        return Ok(new SeccionesDto(u.MiCopropiedadSeccionesColapsadas));
    }

    [HttpPut("mi-copropiedad-secciones")]
    public async Task<IActionResult> SetSecciones([FromBody] SetSeccionesRequest req, CancellationToken ct)
    {
        var userId = User.FindFirstValue("user_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized();
        var u = await _users.FindByIdAsync(uid.ToString());
        if (u is null) return NotFound();
        u.MiCopropiedadSeccionesColapsadas = string.IsNullOrWhiteSpace(req?.Colapsadas) ? null : req!.Colapsadas.Trim();
        var res = await _users.UpdateAsync(u);
        return res.Succeeded ? NoContent() : BadRequest(new { error = "No se pudo guardar." });
    }

    // ---- Agente preferido del asistente Ask AI (por usuario + copropiedad) ----
    public record AskAiAgenteDto(Guid? AgenteId);
    public record SetAskAiAgenteRequest(Guid? AgenteId);

    [HttpGet("ask-ai-agente")]
    public async Task<IActionResult> GetAskAiAgente(CancellationToken ct)
    {
        var u = await CurrentUserAsync();
        if (u is null) return Unauthorized();
        var tenant = User.FindFirstValue("tenant_id");
        if (string.IsNullOrWhiteSpace(tenant)) return Ok(new AskAiAgenteDto(null));
        var map = ParseMap(u.AskAiAgentesPorTenant);
        return Ok(new AskAiAgenteDto(map.TryGetValue(tenant, out var agente) ? agente : null));
    }

    [HttpPut("ask-ai-agente")]
    public async Task<IActionResult> SetAskAiAgente([FromBody] SetAskAiAgenteRequest req, CancellationToken ct)
    {
        var u = await CurrentUserAsync();
        if (u is null) return Unauthorized();
        var tenant = User.FindFirstValue("tenant_id");
        if (string.IsNullOrWhiteSpace(tenant)) return BadRequest(new { error = "Sin copropiedad activa." });

        var map = ParseMap(u.AskAiAgentesPorTenant);
        if (req?.AgenteId is Guid agente) { map[tenant] = agente; }
        else { map.Remove(tenant); }

        u.AskAiAgentesPorTenant = map.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(map);
        var res = await _users.UpdateAsync(u);
        return res.Succeeded ? NoContent() : BadRequest(new { error = "No se pudo guardar." });
    }

    private async Task<ApplicationUser?> CurrentUserAsync()
    {
        var userId = User.FindFirstValue("user_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userId, out var uid) ? await _users.FindByIdAsync(uid.ToString()) : null;
    }

    private static Dictionary<string, Guid> ParseMap(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Guid>>(json) ?? new(); }
        catch { return new(); }
    }
}
