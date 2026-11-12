using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Api.Authorization;
using Propia.Application.SuperAdmin;

namespace Propia.Api.Controllers;

/// <summary>
/// Tokens de API de la Consola A&amp;D (Super Admin > Tokens de API). Solo con sesion interactiva: un token no puede
/// listar, crear ni revocar tokens. El valor completo del token se devuelve UNA vez, al crearlo.
/// </summary>
[ApiController]
[Route("api/admin/api-tokens")]
[Authorize(Policy = AdminController.SuperAdminPolicy)]
[SoloSesionInteractiva]
public sealed class AdminApiTokensController : ControllerBase
{
    private readonly IApiTokensService _svc;

    public AdminApiTokensController(IApiTokensService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct) => Ok(await _svc.ListarAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearApiTokenRequest req, CancellationToken ct)
    {
        var (id, email) = Actor();
        if (id == Guid.Empty) return Unauthorized();
        try { return Ok(await _svc.CrearAsync(req, id, email, Ip(), ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revocar(Guid id, CancellationToken ct)
    {
        var (actor, email) = Actor();
        return await _svc.RevocarAsync(id, actor, email, Ip(), ct) ? NoContent() : NotFound();
    }

    private (Guid Id, string Email) Actor()
    {
        var id = Guid.TryParse(User.FindFirstValue("user_id"), out var g) ? g : Guid.Empty;
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email") ?? "unknown";
        return (id, email);
    }

    private string? Ip() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
