using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.MisCopropiedades;

namespace Propia.Api.Controllers;

/// <summary>
/// Alta de copropiedades desde el selector, para un usuario cliente ya autenticado.
/// (El onboarding publico de /registro vive en OnboardingController y ademas crea cuenta y organizacion.)
/// </summary>
[ApiController]
[Route("api/mis-copropiedades")]
[Authorize]
public class MisCopropiedadesController : ControllerBase
{
    private readonly IMisCopropiedadesService _svc;

    public MisCopropiedadesController(IMisCopropiedadesService svc) => _svc = svc;

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearCopropiedadRequest req, CancellationToken ct)
    {
        var raw = User.FindFirstValue("user_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(raw, out var userId)) return Unauthorized();

        try { return Ok(await _svc.CrearAsync(req, userId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
