using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Directorio;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>
/// Endpoints del modulo 2.4 Directorio (spec v1.0).
/// Personas y Empresas son entidades GLOBALES de la plataforma.
/// Vinculos, Contactos, Etiquetas custom y PersonaEmpresa son TENANT (RLS).
/// </summary>
[ApiController]
[Route("api/directorio")]
[Authorize]
public class DirectorioController : ControllerBase
{
    private readonly IDirectorioService _svc;

    public DirectorioController(IDirectorioService svc) => _svc = svc;

    // ---------- Personas ----------
    [HttpGet("personas")]
    public async Task<IActionResult> ListarPersonas([FromQuery] string? q, CancellationToken ct)
        => Ok(await _svc.ListarPersonasDelTenantAsync(q, ct));

    // ---------- Selector de personas (autocompletado a nivel organizacion) ----------
    // Va ANTES de personas/{id} a proposito: si se declara despues, "candidatos" intenta
    // parsearse como guid y la ruta nunca se alcanza.

    /// <summary>
    /// Candidatos del selector: personas de cualquier copropiedad de la organizacion,
    /// marcadas segun ya esten o no en la copropiedad activa. Minimo 3 caracteres.
    /// </summary>
    [HttpGet("personas/candidatos")]
    public async Task<IActionResult> BuscarCandidatos([FromQuery] string? q, CancellationToken ct)
        => Ok(await _svc.BuscarCandidatosAsync(q ?? "", ct));

    /// <summary>Trae a la copropiedad activa una persona que ya existe en la organizacion.</summary>
    [HttpPost("personas/{id:guid}/vincular")]
    public async Task<IActionResult> VincularCandidato(Guid id, CancellationToken ct)
    {
        try { return Ok(new { vinculada = await _svc.VincularCandidatoAsync(id, ct) }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Gemelo del anterior para empresas (dueno/tercero juridico).</summary>
    [HttpPost("empresas/{id:guid}/vincular")]
    public async Task<IActionResult> VincularCandidatoEmpresa(Guid id, CancellationToken ct)
    {
        try { return Ok(new { vinculada = await _svc.VincularCandidatoEmpresaAsync(id, ct) }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ---------- Contactos rapidos (globales) para precargar/guardar desde las fichas ----------

    /// <summary>Correos/telefonos/direcciones de una persona o empresa, para precargar un formulario.</summary>
    [HttpGet("contactos-rapidos")]
    public async Task<IActionResult> ObtenerContactosRapidos([FromQuery] EntidadDirectorio tipo, [FromQuery] Guid id, CancellationToken ct)
        => Ok(await _svc.ObtenerContactosRapidosAsync(tipo, id, ct));

    /// <summary>Reemplaza en bloque los contactos de una entidad (lo que captura la ficha).</summary>
    [HttpPut("contactos-rapidos")]
    public async Task<IActionResult> ReemplazarContactos([FromBody] ReemplazarContactosRequest req, CancellationToken ct)
    {
        try { await _svc.ReemplazarContactosAsync(req, ct); return Ok(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("personas/{id:guid}")]
    public async Task<IActionResult> ObtenerPersona(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetPersona360Async(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("personas/buscar")]
    public async Task<IActionResult> BuscarPersona([FromBody] BuscarPorDocumentoRequest req, CancellationToken ct)
    {
        var p = await _svc.BuscarPersonaPorDocumentoAsync(req, ct);
        return p is null ? NoContent() : Ok(p);
    }

    [HttpPost("personas")]
    public async Task<IActionResult> CrearPersona([FromBody] CrearPersonaRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearPersonaAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("personas/{id:guid}")]
    public async Task<IActionResult> ActualizarPersona(Guid id, [FromBody] ActualizarPersonaRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _svc.ActualizarPersonaAsync(id, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ---------- Empresas ----------
    [HttpGet("empresas")]
    public async Task<IActionResult> ListarEmpresas([FromQuery] string? q, CancellationToken ct)
        => Ok(await _svc.ListarEmpresasDelTenantAsync(q, ct));

    [HttpGet("empresas/{id:guid}")]
    public async Task<IActionResult> ObtenerEmpresa(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetEmpresa360Async(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("empresas/buscar")]
    public async Task<IActionResult> BuscarEmpresa([FromQuery] string nit, CancellationToken ct)
    {
        var e = await _svc.BuscarEmpresaPorNitAsync(nit, ct);
        return e is null ? NoContent() : Ok(e);
    }

    [HttpPost("empresas")]
    public async Task<IActionResult> CrearEmpresa([FromBody] CrearEmpresaRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearEmpresaAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("empresas/{id:guid}")]
    public async Task<IActionResult> ActualizarEmpresa(Guid id, [FromBody] ActualizarEmpresaRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _svc.ActualizarEmpresaAsync(id, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("nit/dv")]
    public IActionResult CalcularDv([FromQuery] string nit)
    {
        try { return Ok(new { dv = _svc.CalcularDigitoVerificacionNit(nit) }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ---------- Vinculos ----------
    [HttpPost("vinculos")]
    public async Task<IActionResult> CrearVinculo([FromBody] CrearVinculoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearVinculoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("vinculos/{id:guid}/inactivar")]
    public async Task<IActionResult> InactivarVinculo(Guid id, [FromQuery] string? motivo, CancellationToken ct)
        => await _svc.InactivarVinculoAsync(id, motivo, ct) ? NoContent() : NotFound();

    [HttpPost("vinculos/etiquetas")]
    public async Task<IActionResult> AsignarEtiqueta([FromBody] AsignarEtiquetaRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AsignarEtiquetaAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("vinculos/etiquetas/{id:guid}")]
    public async Task<IActionResult> QuitarEtiqueta(Guid id, CancellationToken ct)
        => await _svc.QuitarEtiquetaAsync(id, ct) ? NoContent() : NotFound();

    // ---------- Contactos ----------
    [HttpPost("contactos")]
    public async Task<IActionResult> AgregarContacto([FromBody] AgregarContactoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarContactoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("contactos/{id:guid}")]
    public async Task<IActionResult> EliminarContacto(Guid id, CancellationToken ct)
        => await _svc.EliminarContactoAsync(id, ct) ? NoContent() : NotFound();

    // ---------- Catalogo de etiquetas ----------
    [HttpGet("etiquetas")]
    public async Task<IActionResult> ListarEtiquetas([FromQuery] AplicaEtiqueta? aplicaA, [FromQuery] GrupoEtiqueta? grupo, CancellationToken ct)
        => Ok(await _svc.ListarEtiquetasAsync(aplicaA, grupo, ct));

    [HttpPost("etiquetas")]
    public async Task<IActionResult> CrearEtiquetaCustom([FromBody] CrearEtiquetaCustomRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearEtiquetaCustomAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("etiquetas/{id:guid}")]
    public async Task<IActionResult> EliminarEtiquetaCustom(Guid id, CancellationToken ct)
    {
        try { return await _svc.EliminarEtiquetaCustomAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
