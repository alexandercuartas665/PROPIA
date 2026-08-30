using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Bienvenida;
using Propia.Application.MiCopropiedad;
using Propia.Application.MisCopropiedades;

namespace Propia.Api.Controllers;

/// <summary>
/// Onboarding de bienvenida (/bienvenida): usuario autenticado SIN copropiedades (JWT sin
/// tenant_id) o creando una nueva desde el selector. El asistente es de PLATAFORMA (config
/// global de IA del Super Admin); crear tolera sesion sin tenant y crea la organizacion si falta.
/// </summary>
[ApiController]
[Route("api/bienvenida")]
[Authorize]
public class BienvenidaController : ControllerBase
{
    private const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IMisCopropiedadesService _mis;
    private readonly IAsistenteBienvenidaService _asistente;
    private readonly IDistribucionImportService _distribucion;
    private readonly IPlantillasService _plantillas;

    public BienvenidaController(
        IMisCopropiedadesService mis,
        IAsistenteBienvenidaService asistente,
        IDistribucionImportService distribucion,
        IPlantillasService plantillas)
    {
        _mis = mis;
        _asistente = asistente;
        _distribucion = distribucion;
        _plantillas = plantillas;
    }

    private Guid? UserId()
    {
        var raw = User.FindFirstValue("user_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private string? BearerToken()
    {
        var raw = Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        return raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? raw[prefix.Length..].Trim() : null;
    }

    /// <summary>Crea la copropiedad (y la organizacion si el usuario no tiene ninguna).</summary>
    [HttpPost("crear")]
    public async Task<IActionResult> Crear([FromBody] CrearPrimeraCopropiedadRequest req, CancellationToken ct)
    {
        if (UserId() is not Guid userId) return Unauthorized();
        try { return Ok(await _mis.CrearPrimeraAsync(req, userId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Turno de chat con el asistente de plataforma (con tools, via el bearer del usuario).</summary>
    [HttpPost("asistente")]
    public async Task<IActionResult> Asistente([FromBody] BienvenidaChatRequest req, CancellationToken ct)
        => Ok(await _asistente.ResponderAsync(req, BearerToken(), ct));

    /// <summary>Redacta la descripcion breve de la copropiedad con IA.</summary>
    [HttpPost("generar-descripcion")]
    public async Task<IActionResult> GenerarDescripcion([FromBody] BienvenidaDescripcionRequest req, CancellationToken ct)
        => Ok(await _asistente.GenerarDescripcionAsync(req, ct));

    /// <summary>Plantilla de torres y unidades (estatica: no requiere tenant activo).</summary>
    [HttpGet("plantilla-distribucion")]
    public IActionResult PlantillaDistribucion()
        => File(_distribucion.GenerarPlantilla(), XlsxMime, "plantilla-unidades-propia.xlsx");

    /// <summary>Plantilla del directorio de personas.</summary>
    [HttpGet("plantilla-directorio")]
    public async Task<IActionResult> PlantillaDirectorio(CancellationToken ct)
    {
        try
        {
            return File(await _plantillas.GenerarPlantillaDirectorioAsync(ct), XlsxMime, "plantilla-directorio-propia.xlsx");
        }
        catch (Exception)
        {
            // Sin tenant activo algunos catalogos pueden no resolverse; la UI ofrece descargarla luego.
            return BadRequest(new { error = "plantilla_no_disponible" });
        }
    }
}
