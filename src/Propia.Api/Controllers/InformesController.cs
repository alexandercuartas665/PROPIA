using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Api.Authorization;
using Propia.Domain.Enums;
using Propia.Application.Informes;

namespace Propia.Api.Controllers;

/// <summary>
/// Modulo Informes de gestion (Capa 2). Plantillas inteligentes con prompt por seccion e informes
/// generados con los agentes de IA existentes. Todo opera sobre el tenant activo del JWT.
/// </summary>
[ApiController]
[Route("api/informes")]

[Authorize]
[RequiereRol("Administrador")]  // S-06: gestion sensible, admin
public class InformesController : ControllerBase
{
    private readonly IInformesService _svc;

    public InformesController(IInformesService svc) => _svc = svc;

    // ---------- Plantillas ----------
    [HttpGet("plantillas")]
    public async Task<IActionResult> ListarPlantillas(CancellationToken ct)
    {
        await _svc.SembrarPlantillasBaseAsync(ct); // siembra una plantilla de ejemplo la primera vez
        return Ok(await _svc.ListarPlantillasAsync(ct));
    }

    [HttpGet("plantillas/{id:guid}")]
    public async Task<IActionResult> GetPlantilla(Guid id, CancellationToken ct)
    {
        var p = await _svc.GetPlantillaAsync(id, ct);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpPost("plantillas")]
    public async Task<IActionResult> CrearPlantilla([FromBody] GuardarPlantillaRequest req, CancellationToken ct)
        => Created("", await _svc.CrearPlantillaAsync(req, ct));

    [HttpPut("plantillas/{id:guid}")]
    public async Task<IActionResult> ActualizarPlantilla(Guid id, [FromBody] GuardarPlantillaRequest req, CancellationToken ct)
        => await _svc.ActualizarPlantillaAsync(id, req, ct) ? NoContent() : NotFound();

    [HttpDelete("plantillas/{id:guid}")]
    public async Task<IActionResult> EliminarPlantilla(Guid id, CancellationToken ct)
        => await _svc.EliminarPlantillaAsync(id, ct) ? NoContent() : NotFound();

    // ---------- Informes (instancias) ----------
    [HttpGet]
    public async Task<IActionResult> ListarInformes(CancellationToken ct)
        => Ok(await _svc.ListarInformesAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetInforme(Guid id, CancellationToken ct)
    {
        var i = await _svc.GetInformeAsync(id, ct);
        return i is null ? NotFound() : Ok(i);
    }

    [HttpPost]
    public async Task<IActionResult> CrearInforme([FromBody] CrearInformeRequest req, CancellationToken ct)
    {
        var i = await _svc.CrearInformeAsync(req, ct);
        return i is null ? BadRequest(new { error = "No hay copropiedad activa." }) : Created("", i);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> EliminarInforme(Guid id, CancellationToken ct)
        => await _svc.EliminarInformeAsync(id, ct) ? NoContent() : NotFound();

    [HttpPut("{id:guid}/secciones/{seccionId:guid}")]
    public async Task<IActionResult> GuardarSeccion(Guid id, Guid seccionId, [FromBody] GuardarInformeSeccionRequest req, CancellationToken ct)
        => await _svc.GuardarSeccionAsync(id, seccionId, req, ct) ? NoContent() : NotFound();

    // ---------- Generacion con IA ----------
    [HttpPost("{id:guid}/secciones/{seccionId:guid}/generar")]
    public async Task<IActionResult> GenerarSeccion(Guid id, Guid seccionId, CancellationToken ct)
    {
        var s = await _svc.GenerarSeccionAsync(id, seccionId, ct);
        return s is null ? NotFound() : Ok(s);
    }

    [HttpPost("{id:guid}/generar")]
    public async Task<IActionResult> GenerarInforme(Guid id, CancellationToken ct)
    {
        var i = await _svc.GenerarInformeAsync(id, ct);
        return i is null ? NotFound() : Ok(i);
    }
}
