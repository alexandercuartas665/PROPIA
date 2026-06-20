using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Integraciones;

namespace Propia.Api.Controllers;

/// <summary>
/// Marca de plataforma PUBLICA: la lee el login (sin sesion) y el sistema para pintar
/// logo, icono, nombre y textos. Solo expone la marca; el Super Admin la edita via /admin/branding.
/// </summary>
[ApiController]
[Route("branding")]
[AllowAnonymous]
public class BrandingController : ControllerBase
{
    private readonly IPlatformBrandingService _branding;

    public BrandingController(IPlatformBrandingService branding) => _branding = branding;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await _branding.GetAsync(ct));
}
