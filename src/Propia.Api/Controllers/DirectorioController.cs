using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Api.Authorization;
using Propia.Application.Directorio;
using Propia.Application.MiCopropiedad;
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
    private readonly IPlantillasService _plantillas;

    public DirectorioController(IDirectorioService svc, IPlantillasService plantillas)
    {
        _svc = svc;
        _plantillas = plantillas;
    }

    private const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    // ---------- Carga masiva por plantilla ----------
    [HttpGet("plantilla")]
    public async Task<IActionResult> DescargarPlantilla(CancellationToken ct)
        => File(await _plantillas.GenerarPlantillaDirectorioAsync(ct), XlsxMime, "plantilla-directorio-propia.xlsx");

    [RequierePermiso(ModuloCodigo.Directorio, AccionPermiso.Crear)]
    [HttpPost("importar")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Importar([FromForm] IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "archivo_vacio" });
        try { await using var s = file.OpenReadStream(); return Ok(await _plantillas.ImportarDirectorioAsync(s, ct)); }
        catch (Exception ex) { return BadRequest(new { error = "archivo_invalido", detalle = ex.Message }); }
    }

    // ---------- Adjuntos (documentos de la identidad: RUT, camara, certificados) ----------
    [HttpGet("adjuntos/{tipo}/{entidadId:guid}")]
    public async Task<IActionResult> ListarAdjuntos(EntidadDirectorio tipo, Guid entidadId, CancellationToken ct)
        => Ok(await _svc.ListarAdjuntosAsync(tipo, entidadId, ct));

    [RequierePermiso(ModuloCodigo.Directorio, AccionPermiso.Crear)]
    [HttpPost("adjuntos/{tipo}/{entidadId:guid}")]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<IActionResult> SubirAdjunto(EntidadDirectorio tipo, Guid entidadId, [FromForm] IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "Archivo vacio." });
        if (file.Length > 10_000_000) return BadRequest(new { error = "Maximo 10 MB por archivo." });
        await using var stream = file.OpenReadStream();
        var dto = await _svc.AgregarAdjuntoAsync(tipo, entidadId, file.FileName, file.ContentType, file.Length, stream, ct);
        return Ok(dto);
    }

    [RequierePermiso(ModuloCodigo.Directorio, AccionPermiso.Eliminar)]
    [HttpDelete("adjuntos/{adjuntoId:guid}")]
    public async Task<IActionResult> EliminarAdjunto(Guid adjuntoId, CancellationToken ct)
        => await _svc.EliminarAdjuntoAsync(adjuntoId, ct) ? NoContent() : NotFound();

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
    [RequierePermiso(ModuloCodigo.Directorio, AccionPermiso.Crear)]
    [HttpPost("personas/{id:guid}/vincular")]
    public async Task<IActionResult> VincularCandidato(Guid id, CancellationToken ct)
    {
        try { return Ok(new { vinculada = await _svc.VincularCandidatoAsync(id, ct) }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Gemelo del anterior para empresas (dueno/tercero juridico).</summary>
    [RequierePermiso(ModuloCodigo.Directorio, AccionPermiso.Crear)]
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
    [RequierePermiso(ModuloCodigo.Directorio, AccionPermiso.Editar)]
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

    [RequierePermiso(ModuloCodigo.Directorio, AccionPermiso.Crear)]
    [HttpPost("personas/buscar")]
    public async Task<IActionResult> BuscarPersona([FromBody] BuscarPorDocumentoRequest req, CancellationToken ct)
    {
        var p = await _svc.BuscarPersonaPorDocumentoAsync(req, ct);
        return p is null ? NoContent() : Ok(p);
    }

    [RequierePermiso(ModuloCodigo.Directorio, AccionPermiso.Crear)]
    [HttpPost("personas")]
    public async Task<IActionResult> CrearPersona([FromBody] CrearPersonaRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearPersonaAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Directorio, AccionPermiso.Editar)]
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

    // Foto de la persona (ficha de residente): sube y persiste Persona.FotoUrl por personaId.
    [RequierePermiso(ModuloCodigo.Directorio, AccionPermiso.Crear)]
    [HttpPost("personas/{id:guid}/foto")]
    [RequestSizeLimit(5_500_000)]
    public async Task<IActionResult> SubirFotoPersona(Guid id, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "Archivo vacio." });
        if (file.Length > 5_000_000) return BadRequest(new { error = "Maximo 5 MB." });
        var ext = file.ContentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => null
        };
        if (ext is null) return BadRequest(new { error = "Formato no soportado. Usa JPG, PNG o WEBP." });
        await using var stream = file.OpenReadStream();
        var url = await _svc.SubirFotoPersonaAsync(id, stream, file.ContentType, ext, ct);
        return url is null ? NotFound() : Ok(new { url });
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

    [RequierePermiso(ModuloCodigo.Directorio, AccionPermiso.Crear)]
    [HttpPost("empresas")]
    public async Task<IActionResult> CrearEmpresa([FromBody] CrearEmpresaRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearEmpresaAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Directorio, AccionPermiso.Editar)]
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
    [RequierePermiso(ModuloCodigo.Directorio, AccionPermiso.Crear)]
    [HttpPost("vinculos")]
    public async Task<IActionResult> CrearVinculo([FromBody] CrearVinculoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearVinculoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Directorio, AccionPermiso.Aprobar)]
    [HttpPut("vinculos/{id:guid}/inactivar")]
    public async Task<IActionResult> InactivarVinculo(Guid id, [FromQuery] string? motivo, CancellationToken ct)
        => await _svc.InactivarVinculoAsync(id, motivo, ct) ? NoContent() : NotFound();

    [RequierePermiso(ModuloCodigo.Directorio, AccionPermiso.Crear)]
    [HttpPost("vinculos/etiquetas")]
    public async Task<IActionResult> AsignarEtiqueta([FromBody] AsignarEtiquetaRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AsignarEtiquetaAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Directorio, AccionPermiso.Eliminar)]
    [HttpDelete("vinculos/etiquetas/{id:guid}")]
    public async Task<IActionResult> QuitarEtiqueta(Guid id, CancellationToken ct)
        => await _svc.QuitarEtiquetaAsync(id, ct) ? NoContent() : NotFound();

    // ---------- Contactos ----------
    [RequierePermiso(ModuloCodigo.Directorio, AccionPermiso.Crear)]
    [HttpPost("contactos")]
    public async Task<IActionResult> AgregarContacto([FromBody] AgregarContactoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarContactoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Directorio, AccionPermiso.Eliminar)]
    [HttpDelete("contactos/{id:guid}")]
    public async Task<IActionResult> EliminarContacto(Guid id, CancellationToken ct)
        => await _svc.EliminarContactoAsync(id, ct) ? NoContent() : NotFound();

    // ---------- Catalogo de etiquetas ----------
    [HttpGet("etiquetas")]
    public async Task<IActionResult> ListarEtiquetas([FromQuery] AplicaEtiqueta? aplicaA, [FromQuery] GrupoEtiqueta? grupo, CancellationToken ct)
        => Ok(await _svc.ListarEtiquetasAsync(aplicaA, grupo, ct));

    [RequierePermiso(ModuloCodigo.Directorio, AccionPermiso.Crear)]
    [HttpPost("etiquetas")]
    public async Task<IActionResult> CrearEtiquetaCustom([FromBody] CrearEtiquetaCustomRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearEtiquetaCustomAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Directorio, AccionPermiso.Editar)]
    [HttpPut("etiquetas/{id:guid}")]
    public async Task<IActionResult> ActualizarEtiqueta(Guid id, [FromBody] EditarEtiquetaRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarEtiquetaAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Directorio, AccionPermiso.Eliminar)]
    [HttpDelete("etiquetas/{id:guid}")]
    public async Task<IActionResult> EliminarEtiquetaCustom(Guid id, CancellationToken ct)
    {
        try { return await _svc.EliminarEtiquetaCustomAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
