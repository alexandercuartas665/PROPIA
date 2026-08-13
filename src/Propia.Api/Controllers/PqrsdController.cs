using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Pqrsd;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;

namespace Propia.Api.Controllers;

/// <summary>Endpoints del modulo 2.9 PQRSD y Convivencia (spec v1.0).</summary>
[ApiController]
[Route("api/pqrsd")]
[Authorize]
public class PqrsdController : ControllerBase
{
    private readonly IPqrsdService _svc;
    private readonly PropiaDbContext _db;
    private readonly IBlobStorage _storage;
    public PqrsdController(IPqrsdService svc, PropiaDbContext db, IBlobStorage storage) { _svc = svc; _db = db; _storage = storage; }

    private Guid? GetTenantId()
    {
        var raw = User.FindFirstValue("tenant_id");
        return Guid.TryParse(raw, out var g) ? g : null;
    }

    // --- Catalogo ---
    [HttpGet("categorias")]
    public async Task<IActionResult> ListarCategorias(CancellationToken ct) => Ok(await _svc.ListarCategoriasAsync(ct));

    [HttpPost("categorias")]
    public async Task<IActionResult> CrearCategoria([FromBody] CrearCategoriaRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearCategoriaAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("categorias/{id:guid}")]
    public async Task<IActionResult> ActualizarCategoria(Guid id, [FromBody] ActualizarCategoriaRequest req, CancellationToken ct)
        => await _svc.ActualizarCategoriaAsync(id, req, ct) ? NoContent() : NotFound();

    [HttpDelete("categorias/{id:guid}")]
    public async Task<IActionResult> EliminarCategoria(Guid id, CancellationToken ct)
        => await _svc.EliminarCategoriaAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("categorias/restablecer")]
    public async Task<IActionResult> RestablecerCategorias(CancellationToken ct)
        => Ok(new { restablecidas = await _svc.RestablecerCategoriasBaseAsync(ct) });

    [HttpGet("plazos")]
    public async Task<IActionResult> ListarPlazos(CancellationToken ct) => Ok(await _svc.ListarPlazosAsync(ct));

    [HttpPut("plazos/{tipo}")]
    public async Task<IActionResult> ActualizarPlazo(TipoPqrsd tipo, [FromBody] ActualizarPlazoRequest req, CancellationToken ct)
        => await _svc.ActualizarPlazoAsync(tipo, req, ct) ? NoContent() : NotFound();

    // --- Tipos configurables (catalogo editable por copropiedad) ---
    [HttpGet("tipos")]
    public async Task<IActionResult> ListarTipos([FromQuery] bool incluirInactivos, CancellationToken ct)
        => Ok(await _svc.ListarTiposAsync(incluirInactivos, ct));

    [HttpPost("tipos")]
    public async Task<IActionResult> CrearTipo([FromBody] GuardarTipoPqrsdRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearTipoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("tipos/{id:guid}")]
    public async Task<IActionResult> ActualizarTipo(Guid id, [FromBody] GuardarTipoPqrsdRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarTipoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("tipos/{id:guid}")]
    public async Task<IActionResult> EliminarTipo(Guid id, CancellationToken ct)
    {
        try { return await _svc.EliminarTipoAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Bandeja + ficha ---
    [HttpGet("bandeja")]
    public async Task<IActionResult> Bandeja(
        [FromQuery] EstadoPqrsd? estado, [FromQuery] TipoPqrsd? tipo,
        [FromQuery] Guid? categoriaId, [FromQuery] string? q,
        [FromQuery] bool archivados, CancellationToken ct)
        => Ok(await _svc.GetBandejaAsync(estado, tipo, categoriaId, q, archivados, ct));

    // --- Tablero: columnas (estados) configurables ---
    [HttpGet("estados")]
    public async Task<IActionResult> ListarEstados(CancellationToken ct) => Ok(await _svc.ListarEstadosAsync(ct));

    [HttpPost("estados")]
    public async Task<IActionResult> CrearEstado([FromBody] CrearEstadoPqrsdRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearEstadoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("estados/{id:guid}")]
    public async Task<IActionResult> ActualizarEstado(Guid id, [FromBody] ActualizarEstadoPqrsdRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarEstadoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("estados/{id:guid}")]
    public async Task<IActionResult> EliminarEstado(Guid id, CancellationToken ct)
    {
        try { return await _svc.EliminarEstadoAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("estados/{id:guid}/orden")]
    public async Task<IActionResult> ReordenarEstado(Guid id, [FromQuery] string direccion, CancellationToken ct)
        => await _svc.ReordenarEstadoAsync(id, direccion, ct) ? NoContent() : NotFound();

    [HttpPut("{id:guid}/mover")]
    public async Task<IActionResult> MoverAEstado(Guid id, [FromBody] MoverExpedienteEstadoRequest req, CancellationToken ct)
    {
        try { return await _svc.MoverAEstadoAsync(id, req.EstadoId, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Tablero: campos dinamicos ---
    [HttpGet("campos")]
    public async Task<IActionResult> ListarCampos(CancellationToken ct) => Ok(await _svc.ListarCamposAsync(ct));

    [HttpGet("campos-archivados")]
    public async Task<IActionResult> ListarCamposArchivados(CancellationToken ct) => Ok(await _svc.ListarCamposArchivadosAsync(ct));

    [HttpPost("campos")]
    public async Task<IActionResult> CrearCampo([FromBody] GuardarCampoPqrsdRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearCampoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("campos/{id:guid}")]
    public async Task<IActionResult> ActualizarCampo(Guid id, [FromBody] GuardarCampoPqrsdRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarCampoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("campos/{id:guid}")]
    public async Task<IActionResult> EliminarCampo(Guid id, CancellationToken ct)
        => await _svc.EliminarCampoAsync(id, ct) ? NoContent() : NotFound();

    [HttpPut("campos/{id:guid}/archivar")]
    public async Task<IActionResult> ArchivarCampo(Guid id, [FromQuery] bool archivar, CancellationToken ct)
    {
        try { return await _svc.SetCampoActivoAsync(id, !archivar, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("campos/{id:guid}/orden")]
    public async Task<IActionResult> ReordenarCampo(Guid id, [FromQuery] string direccion, CancellationToken ct)
        => await _svc.ReordenarCampoAsync(id, direccion, ct) ? NoContent() : NotFound();

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetExpediente(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetExpedienteAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    // --- Radicacion ---
    [HttpPost]
    public async Task<IActionResult> Radicar([FromBody] RadicarPqrsdRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.RadicarAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Vista del residente ---
    [HttpGet("mis-pqrsd")]
    public async Task<IActionResult> MisPqrsd(CancellationToken ct) => Ok(await _svc.ListarMisPqrsdAsync(ct));

    // --- Ciclo de gestion ---
    [HttpPut("{id:guid}/tomar")]
    public async Task<IActionResult> Tomar(Guid id, [FromBody] TomarExpedienteRequest req, CancellationToken ct)
        => await _svc.TomarExpedienteAsync(id, req, ct) ? NoContent() : NotFound();

    [HttpPut("{id:guid}/responder")]
    public async Task<IActionResult> Responder(Guid id, [FromBody] ResponderExpedienteRequest req, CancellationToken ct)
    {
        try { return await _svc.ResponderAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}/inconformidad")]
    public async Task<IActionResult> Inconformidad(Guid id, [FromBody] ManifestarInconformidadRequest req, CancellationToken ct)
    {
        try { return await _svc.ManifestarInconformidadAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}/cerrar")]
    public async Task<IActionResult> Cerrar(Guid id, [FromBody] CerrarDefinitivoRequest req, CancellationToken ct)
    {
        try { return await _svc.CerrarDefinitivoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Tutela ---
    [HttpPut("{id:guid}/tutela")]
    public async Task<IActionResult> ActivarTutela(Guid id, [FromBody] ActivarTutelaRequest req, CancellationToken ct)
    {
        try { return await _svc.ActivarTutelaAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Archivar / actualizar expediente ---
    [HttpPut("{id:guid}/archivar")]
    public async Task<IActionResult> Archivar(Guid id, [FromQuery] bool archivar, CancellationToken ct)
        => await _svc.ArchivarExpedienteAsync(id, archivar, ct) ? NoContent() : NotFound();

    [HttpPut("{id:guid}/actualizar")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] ActualizarExpedienteRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarExpedienteAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/actividad")]
    public async Task<IActionResult> ReportarActividad(Guid id, [FromBody] ReportarActividadPqrsdRequest req, CancellationToken ct)
    {
        try { return await _svc.ReportarActividadAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/generar-tarea")]
    public async Task<IActionResult> GenerarTarea(Guid id, CancellationToken ct)
    {
        try { var tid = await _svc.GenerarTareaAsync(id, ct); return tid is null ? NotFound() : Ok(new { tareaId = tid }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Comite ---
    [HttpPost("{id:guid}/comite")]
    public async Task<IActionResult> EscalarAComite(Guid id, [FromBody] EscalarAComiteRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.EscalarAComiteAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("comite/{sesionId:guid}/registrar")]
    public async Task<IActionResult> RegistrarSesionComite(Guid sesionId, [FromBody] RegistrarSesionComiteRequest req, CancellationToken ct)
    {
        try { return await _svc.RegistrarSesionComiteAsync(sesionId, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // --- Contexto humano del expediente: unidad asignada + propietario/residente/etc con contacto ---
    [HttpGet("{id:guid}/contexto")]
    public async Task<IActionResult> GetContexto(Guid id, CancellationToken ct)
    {
        var exp = await _db.PqrsdExpedientes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (exp is null) return NotFound();

        // Buscar UnidadPersona donde PersonaId = RadicadorPersonaId. Tomo la primera asociacion como "unidad del expediente".
        var unidadPersona = await (from up in _db.UnidadPersonas.AsNoTracking()
                                   join u in _db.UnidadesPrivadas.AsNoTracking() on up.UnidadId equals u.Id
                                   where up.PersonaId == exp.RadicadorPersonaId
                                   select new { up.UnidadId, u.Numero, u.TorreId, u.Piso, u.Tipo, u.CoeficientePropiedad })
                                  .FirstOrDefaultAsync(ct);

        PqrsdContextoUnidadDto unidadDto;
        IReadOnlyList<PqrsdContextoPersonaDto> personasDto = new List<PqrsdContextoPersonaDto>();

        if (unidadPersona is not null)
        {
            var torreNombre = unidadPersona.TorreId.HasValue
                ? await _db.Torres.AsNoTracking().Where(t => t.Id == unidadPersona.TorreId).Select(t => t.Nombre).FirstOrDefaultAsync(ct)
                : null;
            unidadDto = new PqrsdContextoUnidadDto(
                unidadPersona.UnidadId, unidadPersona.Numero, torreNombre,
                unidadPersona.Piso, unidadPersona.Tipo.ToString(), unidadPersona.CoeficientePropiedad);

            // Todas las personas de esa unidad con sus datos
            personasDto = await (from up in _db.UnidadPersonas.AsNoTracking()
                                 join p in _db.Personas.AsNoTracking() on up.PersonaId equals p.Id
                                 where up.UnidadId == unidadPersona.UnidadId
                                 select new PqrsdContextoPersonaDto(
                                     p.Id,
                                     (p.Nombres + " " + p.Apellidos).Trim(),
                                     p.Documento, p.Email, p.Telefono,
                                     up.Rol.ToString(),
                                     p.Id == exp.RadicadorPersonaId))
                                .ToListAsync(ct);
        }
        else
        {
            unidadDto = new PqrsdContextoUnidadDto(null, null, null, null, null, null);
            // Aun sin unidad asociada, devolver al menos el radicador
            var rad = await _db.Personas.AsNoTracking().FirstOrDefaultAsync(p => p.Id == exp.RadicadorPersonaId, ct);
            if (rad is not null)
            {
                personasDto = new List<PqrsdContextoPersonaDto>
                {
                    new(rad.Id, (rad.Nombres + " " + rad.Apellidos).Trim(),
                        rad.Documento, rad.Email, rad.Telefono, "Radicador", true)
                };
            }
        }

        return Ok(new PqrsdContextoDto(unidadDto, personasDto));
    }

    // --- Agregar adjunto despues de la radicacion (admin adjunta evidencias/documentos) ---
    [HttpPost("{id:guid}/adjuntos")]
    public async Task<IActionResult> AgregarAdjunto(Guid id, [FromBody] AgregarAdjuntoPqrsdRequest req, CancellationToken ct)
    {
        var exp = await _db.PqrsdExpedientes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (exp is null) return NotFound();
        if (string.IsNullOrWhiteSpace(req.NombreArchivo)) return BadRequest(new { error = "NombreArchivo requerido." });
        var adj = new PqrsdAdjunto
        {
            ExpedienteId = id,
            NombreArchivo = req.NombreArchivo.Trim(),
            TipoMime = req.TipoMime,
            TamanioBytes = req.TamanioBytes,
            UrlStorage = req.UrlStorage
        };
        _db.PqrsdAdjuntos.Add(adj);
        await _db.SaveChangesAsync(ct);
        return Created("", new PqrsdAdjuntoDto(adj.Id, adj.NombreArchivo, adj.TipoMime, adj.TamanioBytes, adj.UrlStorage, adj.CreatedAt));
    }

    // --- Subir el binario de un adjunto (documentos e imagenes) y registrarlo ---
    [HttpPost("{id:guid}/adjuntos/upload")]
    [RequestSizeLimit(52_500_000)]
    public async Task<IActionResult> SubirAdjuntoBinario(Guid id, IFormFile file, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId is null) return BadRequest(new { error = "no_active_tenant" });
        var exp = await _db.PqrsdExpedientes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (exp is null) return NotFound();
        if (file is null || file.Length == 0) return BadRequest(new { error = "Archivo vacio." });
        if (file.Length > 52_428_800) return BadRequest(new { error = "Maximo 50 MB." });

        var ext = System.IO.Path.GetExtension(file.FileName);
        var key = $"tenants/{tenantId:N}/pqrsd/{id:N}/{Guid.NewGuid():N}{ext}";
        await using var stream = file.OpenReadStream();
        // URL relativa al mismo origen (convencion host unificado: nunca absolutizar).
        var url = await _storage.UploadAsync(key, stream, file.ContentType ?? "application/octet-stream", ct);

        var adj = new PqrsdAdjunto
        {
            ExpedienteId = id,
            NombreArchivo = file.FileName,
            TipoMime = file.ContentType ?? "application/octet-stream",
            TamanioBytes = file.Length,
            UrlStorage = url,
            SubidoPorUsuarioId = Guid.TryParse(User.FindFirstValue("user_id"), out var uid) ? uid : Guid.Empty
        };
        _db.PqrsdAdjuntos.Add(adj);
        await _db.SaveChangesAsync(ct);
        return Created("", new PqrsdAdjuntoDto(adj.Id, adj.NombreArchivo, adj.TipoMime, adj.TamanioBytes, adj.UrlStorage, adj.CreatedAt));
    }

    // --- Eliminar un adjunto ---
    [HttpDelete("{id:guid}/adjuntos/{adjuntoId:guid}")]
    public async Task<IActionResult> EliminarAdjunto(Guid id, Guid adjuntoId, CancellationToken ct)
    {
        var adj = await _db.PqrsdAdjuntos.FirstOrDefaultAsync(a => a.Id == adjuntoId && a.ExpedienteId == id, ct);
        if (adj is null) return NotFound();
        _db.PqrsdAdjuntos.Remove(adj);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
