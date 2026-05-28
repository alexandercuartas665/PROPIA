using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Auth;

namespace Propia.Api.Controllers;

/// <summary>
/// Flujo OAuth de "Iniciar sesion con Google". Dos endpoints publicos:
/// - POST /connect/google/start: arma la URL de Google a la que el frontend debe redirigir.
/// - POST /connect/google/callback: recibe el code que Google devolvio y resuelve el login.
///   Si el correo no existe en PROPIA, hace auto-registro (crea User+Persona+OnboardingSession)
///   y devuelve OnboardingSessionId para que el frontend lleve al usuario al wizard 2.1.
/// </summary>
[ApiController]
[Route("connect/google")]
[AllowAnonymous]
public sealed class AuthGoogleController : ControllerBase
{
    private readonly IGoogleSignInService _svc;

    public AuthGoogleController(IGoogleSignInService svc) => _svc = svc;

    public sealed record StartRequest(string RedirectUri, string State);
    public sealed record StartResponse(string AuthorizeUrl);
    public sealed record CallbackRequest(string Code, string RedirectUri);

    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] StartRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.RedirectUri) || string.IsNullOrWhiteSpace(req.State))
            return BadRequest(new { error = "redirect_uri y state son obligatorios" });

        var url = await _svc.BuildAuthorizeUrlAsync(req.RedirectUri, req.State, ct);
        if (url is null) return BadRequest(new { error = "El login con Google no esta habilitado." });
        return Ok(new StartResponse(url));
    }

    [HttpPost("callback")]
    public async Task<IActionResult> Callback([FromBody] CallbackRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.RedirectUri))
            return BadRequest(new { error = "code y redirect_uri son obligatorios" });

        var result = await _svc.ResolveAsync(req.Code, req.RedirectUri, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return Ok(result);
    }
}
