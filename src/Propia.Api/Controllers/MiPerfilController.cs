using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.MiPerfil;

namespace Propia.Api.Controllers;

/// <summary>
/// Self-service del usuario autenticado (modulo "Mi Perfil" / Mi cuenta): foto, firma y
/// contactos de notificacion. Todo se resuelve por el userId del JWT.
/// </summary>
[ApiController]
[Route("api/mi-perfil")]
[Authorize]
public class MiPerfilController : ControllerBase
{
    private readonly IMiPerfilService _svc;
    public MiPerfilController(IMiPerfilService svc) => _svc = svc;

    private Guid? GetUserId()
    {
        var raw = User.FindFirstValue("user_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var g) ? g : null;
    }

    private static string? ExtFor(string? contentType) => contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => null
    };

    [HttpPost("foto")]
    [RequestSizeLimit(5_500_000)]
    public Task<IActionResult> SubirFoto(IFormFile file, CancellationToken ct) => SubirMedia(file, esFirma: false, ct);

    [HttpPost("firma")]
    [RequestSizeLimit(5_500_000)]
    public Task<IActionResult> SubirFirma(IFormFile file, CancellationToken ct) => SubirMedia(file, esFirma: true, ct);

    private async Task<IActionResult> SubirMedia(IFormFile file, bool esFirma, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (file is null || file.Length == 0) return BadRequest(new { error = "Archivo vacio." });
        if (file.Length > 5_000_000) return BadRequest(new { error = "Maximo 5 MB." });
        var ext = ExtFor(file.ContentType);
        if (ext is null) return BadRequest(new { error = "Formato no soportado. Usa JPG, PNG o WEBP." });

        await using var stream = file.OpenReadStream();
        var url = esFirma
            ? await _svc.SubirFirmaAsync(userId.Value, stream, file.ContentType, ext, ct)
            : await _svc.SubirFotoAsync(userId.Value, stream, file.ContentType, ext, ct);
        return url is null ? NotFound() : Ok(new { url });
    }

    [HttpGet("notificaciones")]
    public async Task<IActionResult> Listar(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return Ok(await _svc.ListarContactosAsync(userId.Value, ct));
    }

    [HttpPost("notificaciones")]
    public async Task<IActionResult> Agregar([FromBody] CrearContactoNotificacionRequest req, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var dto = await _svc.AgregarContactoAsync(userId.Value, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("notificaciones/{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] ActualizarContactoNotificacionRequest req, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return await _svc.ActualizarContactoAsync(userId.Value, id, req, ct) ? NoContent() : NotFound();
    }

    [HttpDelete("notificaciones/{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return await _svc.EliminarContactoAsync(userId.Value, id, ct) ? NoContent() : NotFound();
    }
}
