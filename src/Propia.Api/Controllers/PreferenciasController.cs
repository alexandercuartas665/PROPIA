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
}
