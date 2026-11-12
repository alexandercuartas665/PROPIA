using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.SuperAdmin;

namespace Propia.Api.Controllers;

/// <summary>
/// Semillas globales de la Consola A&amp;D. GET /api/admin/semillas = indice de TODO lo que el Super Admin siembra, con
/// las rutas de su API (incluye las que viven en otros controladores: listas, agentes IA, plantillas PQRSD, menu,
/// planes). Aqui viven las que no tenian API: roles base, plantillas de comunicados, etiquetas del Directorio,
/// etiquetas de documentos y catalogo de reportes. PUT = MERGE. Acepta sesion o token de API (Bearer pak_...).
/// </summary>
[ApiController]
[Route("api/admin/semillas")]
[Authorize(Policy = AdminController.SuperAdminPolicy)]
public sealed class AdminSemillasController : ControllerBase
{
    private readonly ISemillasAdminService _svc;

    public AdminSemillasController(ISemillasAdminService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> Indice(CancellationToken ct) => Ok(await _svc.IndiceAsync(ct));

    // ---- Roles base ----
    [HttpGet("roles")]
    public async Task<IActionResult> Roles(CancellationToken ct) => Ok(await _svc.ListarRolesAsync(ct));

    [HttpPost("roles")]
    public Task<IActionResult> CrearRol([FromBody] GuardarRolSemillaRequest req, CancellationToken ct)
        => Ejecutar(async () => Ok(await _svc.CrearRolAsync(req, ct)));

    [HttpPut("roles/{id:guid}")]
    public Task<IActionResult> ActualizarRol(Guid id, [FromBody] GuardarRolSemillaRequest req, CancellationToken ct)
        => Ejecutar(async () => await _svc.ActualizarRolAsync(id, req, ct) is { } dto ? Ok(dto) : NotFound());

    [HttpPut("roles/{id:guid}/permisos")]
    public Task<IActionResult> PermisosRol(Guid id, [FromBody] GuardarPermisosRolSemillaRequest req, CancellationToken ct)
        => Ejecutar(async () => await _svc.GuardarPermisosRolAsync(id, req, ct) is { } dto ? Ok(dto) : NotFound());

    [HttpDelete("roles/{id:guid}")]
    public Task<IActionResult> EliminarRol(Guid id, CancellationToken ct)
        => Ejecutar(async () => await _svc.EliminarRolAsync(id, ct) ? NoContent() : NotFound());

    // ---- Plantillas de comunicados ----
    [HttpGet("comunicados")]
    public async Task<IActionResult> Comunicados(CancellationToken ct) => Ok(await _svc.ListarPlantillasComunicadoAsync(ct));

    [HttpPost("comunicados")]
    public Task<IActionResult> CrearComunicado([FromBody] GuardarComunicadoPlantillaSemillaRequest req, CancellationToken ct)
        => Ejecutar(async () => Ok(await _svc.CrearPlantillaComunicadoAsync(req, ct)));

    [HttpPut("comunicados/{id:guid}")]
    public Task<IActionResult> ActualizarComunicado(Guid id, [FromBody] GuardarComunicadoPlantillaSemillaRequest req, CancellationToken ct)
        => Ejecutar(async () => await _svc.ActualizarPlantillaComunicadoAsync(id, req, ct) is { } dto ? Ok(dto) : NotFound());

    // ---- Etiquetas del Directorio ----
    [HttpGet("etiquetas-directorio")]
    public async Task<IActionResult> EtiquetasDirectorio(CancellationToken ct) => Ok(await _svc.ListarEtiquetasDirectorioAsync(ct));

    [HttpPost("etiquetas-directorio")]
    public Task<IActionResult> CrearEtiquetaDirectorio([FromBody] GuardarEtiquetaDirectorioSemillaRequest req, CancellationToken ct)
        => Ejecutar(async () => Ok(await _svc.CrearEtiquetaDirectorioAsync(req, ct)));

    [HttpPut("etiquetas-directorio/{id:guid}")]
    public Task<IActionResult> ActualizarEtiquetaDirectorio(Guid id, [FromBody] GuardarEtiquetaDirectorioSemillaRequest req, CancellationToken ct)
        => Ejecutar(async () => await _svc.ActualizarEtiquetaDirectorioAsync(id, req, ct) is { } dto ? Ok(dto) : NotFound());

    // ---- Etiquetas de documentos ----
    [HttpGet("etiquetas-documentos")]
    public async Task<IActionResult> EtiquetasDocumentos(CancellationToken ct) => Ok(await _svc.ListarEtiquetasDocumentoAsync(ct));

    [HttpPost("etiquetas-documentos")]
    public Task<IActionResult> CrearEtiquetaDocumento([FromBody] GuardarEtiquetaDocumentoSemillaRequest req, CancellationToken ct)
        => Ejecutar(async () => Ok(await _svc.CrearEtiquetaDocumentoAsync(req, ct)));

    [HttpPut("etiquetas-documentos/{id:guid}")]
    public Task<IActionResult> ActualizarEtiquetaDocumento(Guid id, [FromBody] GuardarEtiquetaDocumentoSemillaRequest req, CancellationToken ct)
        => Ejecutar(async () => await _svc.ActualizarEtiquetaDocumentoAsync(id, req, ct) is { } dto ? Ok(dto) : NotFound());

    // ---- Catalogo de reportes ----
    [HttpGet("reportes")]
    public async Task<IActionResult> Reportes(CancellationToken ct) => Ok(await _svc.ListarReportesAsync(ct));

    [HttpPut("reportes/categorias/{id:guid}")]
    public Task<IActionResult> ActualizarCategoria(Guid id, [FromBody] GuardarReporteCategoriaSemillaRequest req, CancellationToken ct)
        => Ejecutar(async () => await _svc.ActualizarReporteCategoriaAsync(id, req, ct) is { } dto ? Ok(dto) : NotFound());

    [HttpPut("reportes/{id:guid}")]
    public Task<IActionResult> ActualizarReporte(Guid id, [FromBody] GuardarReporteSemillaRequest req, CancellationToken ct)
        => Ejecutar(async () => await _svc.ActualizarReporteAsync(id, req, ct) is { } dto ? Ok(dto) : NotFound());

    private async Task<IActionResult> Ejecutar(Func<Task<IActionResult>> accion)
    {
        try { return await accion(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
