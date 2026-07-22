using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Novedades;
using Propia.Domain.Enums;
using Propia.Infrastructure.Storage;

namespace Propia.Api.Controllers;

/// <summary>
/// Muro de novedades reutilizable. Antes vivia dentro de 2.3 y solo servia a zonas comunes;
/// ahora cuelga de cualquier entidad via (tipo, entidadId), y lo consumen la ficha de zona,
/// la de equipos y las que vengan.
/// </summary>
[ApiController]
[Route("api/novedades")]
[Authorize]
public class NovedadesController : ControllerBase
{
    private readonly INovedadesService _svc;
    private readonly IBlobStorage _storage;

    public NovedadesController(INovedadesService svc, IBlobStorage storage)
    {
        _svc = svc;
        _storage = storage;
    }

    [HttpGet("{tipo}/{entidadId:guid}")]
    public async Task<IActionResult> Listar(TipoEntidadNovedad tipo, Guid entidadId, CancellationToken ct)
        => Ok(await _svc.ListarAsync(tipo, entidadId, GetPersonaId(), ct));

    [HttpPost("{tipo}/{entidadId:guid}")]
    public async Task<IActionResult> Publicar(TipoEntidadNovedad tipo, Guid entidadId, [FromBody] PublicarNovedadRequest req, CancellationToken ct)
    {
        var dto = await _svc.PublicarAsync(tipo, entidadId, req, GetPersonaId(), ct);
        return dto is null ? BadRequest(new { error = "no_se_pudo" }) : Ok(dto);
    }

    [HttpDelete("{novedadId:guid}")]
    public async Task<IActionResult> Eliminar(Guid novedadId, CancellationToken ct)
        => await _svc.EliminarAsync(novedadId, ct) ? NoContent() : NotFound();

    [HttpPost("{novedadId:guid}/comentarios")]
    public async Task<IActionResult> Comentar(Guid novedadId, [FromBody] ComentarNovedadRequest req, CancellationToken ct)
    {
        var dto = await _svc.ComentarAsync(novedadId, req, GetPersonaId(), ct);
        return dto is null ? BadRequest(new { error = "no_se_pudo" }) : Ok(dto);
    }

    [HttpPost("{novedadId:guid}/like")]
    public async Task<IActionResult> Like(Guid novedadId, CancellationToken ct)
        => Ok(new { likes = await _svc.ToggleLikeAsync(novedadId, GetPersonaId(), ct) });

    /// <summary>Imagen de una publicacion. La clave lleva tipo+entidad para no mezclar muros.</summary>
    [HttpPost("{tipo}/{entidadId:guid}/imagen")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> SubirImagen(TipoEntidadNovedad tipo, Guid entidadId, IFormFile file, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId is null) return BadRequest(new { error = "no_active_tenant" });
        if (file is null || file.Length == 0) return BadRequest(new { error = "Archivo vacio." });
        if (file.Length > 5_000_000) return BadRequest(new { error = "Maximo 5 MB." });

        var ext = file.ContentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ""
        };
        if (ext == "") return BadRequest(new { error = "Formato no soportado. Usa JPG, PNG o WEBP." });

        var key = $"tenants/{tenantId:N}/novedades/{tipo.ToString().ToLowerInvariant()}/{entidadId:N}/{Guid.NewGuid():N}{ext}";
        await using var stream = file.OpenReadStream();
        // Host unificado: la URL se deja RELATIVA al mismo origen, nunca absoluta.
        var url = await _storage.UploadAsync(key, stream, file.ContentType, ct);
        return Ok(new { url });
    }

    private Guid? GetTenantId()
        => Guid.TryParse(User.FindFirstValue("tenant_id"), out var g) ? g : null;

    private Guid? GetPersonaId()
        => Guid.TryParse(User.FindFirstValue("persona_id"), out var g) ? g : null;
}
