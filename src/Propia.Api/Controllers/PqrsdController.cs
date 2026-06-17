using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Pqrsd;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Api.Controllers;

/// <summary>Endpoints del modulo 2.9 PQRSD y Convivencia (spec v1.0).</summary>
[ApiController]
[Route("api/pqrsd")]
[Authorize]
public class PqrsdController : ControllerBase
{
    private readonly IPqrsdService _svc;
    private readonly PropiaDbContext _db;
    public PqrsdController(IPqrsdService svc, PropiaDbContext db) { _svc = svc; _db = db; }

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

    // --- Bandeja + ficha ---
    [HttpGet("bandeja")]
    public async Task<IActionResult> Bandeja(
        [FromQuery] EstadoPqrsd? estado, [FromQuery] TipoPqrsd? tipo,
        [FromQuery] Guid? categoriaId, [FromQuery] string? q, CancellationToken ct)
        => Ok(await _svc.GetBandejaAsync(estado, tipo, categoriaId, q, ct));

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
}
