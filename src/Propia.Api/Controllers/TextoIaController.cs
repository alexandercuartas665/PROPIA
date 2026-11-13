using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.InfraestructuraIa;

namespace Propia.Api.Controllers;

/// <summary>
/// Ayudas de IA para las cajas de texto de cualquier modulo (no solo administradores): "Mejorar redaccion" que se ofrece
/// al detener el dictado por voz. Solo propone: el texto lo aplica (o no) el usuario en su formulario.
/// </summary>
[ApiController]
[Route("api/texto")]
[Authorize]
public class TextoIaController : ControllerBase
{
    private readonly ICorrectorTextoService _corrector;
    public TextoIaController(ICorrectorTextoService corrector) => _corrector = corrector;

    [HttpPost("mejorar")]
    public async Task<IActionResult> Mejorar([FromBody] MejorarTextoRequest req, CancellationToken ct)
    {
        var r = await _corrector.MejorarAsync(req, ct);
        return r.Ok ? Ok(new { texto = r.Texto }) : BadRequest(new { error = r.Error });
    }
}
