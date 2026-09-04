using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Api.Authorization;
using Propia.Domain.Enums;
using Propia.Infrastructure.Storage;

namespace Propia.Api.Controllers;

/// <summary>
/// Endpoint de upload de imagenes (logo, foto de fachada, portada) del tenant.
/// Spec 2.3 - seccion Identidad.
///
/// El storage real se delega en IBlobStorage:
///  - Development / tests: LocalBlobStorage (filesystem wwwroot/uploads)
///  - Production: R2BlobStorage (Cloudflare R2, S3-compatible)
///
/// Reglas:
///  - Solo JPG/PNG/WEBP, max 5 MB
///  - Key: tenants/{tenantId}/{tipo}.{ext}
///  - Devuelve URL publica del blob
///  - Requiere JWT con tenant activo (mismo flujo que /api/mi-copropiedad/*)
/// </summary>
[ApiController]
[Route("api/uploads")]

[Authorize]
[RequiereRol("Administrador")]  // S-06: gestion sensible, admin
public class UploadsController : ControllerBase
{
    private readonly IBlobStorage _storage;
    private readonly ILogger<UploadsController> _logger;
    private static readonly string[] TiposPermitidos = { "logo", "fachada", "portada" };
    private static readonly Dictionary<string, string> ExtPermitidas = new()
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    public UploadsController(IBlobStorage storage, ILogger<UploadsController> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    [HttpPost("imagen/{tipo}")]
    [RequestSizeLimit(5_500_000)]  // 5 MB + margen
    public async Task<IActionResult> SubirImagen(string tipo, IFormFile file, CancellationToken ct)
    {
        var tenantId = User.FindFirstValue("tenant_id");
        if (string.IsNullOrEmpty(tenantId)) return BadRequest(new { error = "no_active_tenant" });
        if (!TiposPermitidos.Contains(tipo)) return BadRequest(new { error = $"Tipo invalido. Usa: {string.Join(", ", TiposPermitidos)}." });
        if (file is null || file.Length == 0) return BadRequest(new { error = "Archivo vacio." });
        if (file.Length > 5_000_000) return BadRequest(new { error = "Maximo 5 MB." });
        if (!ExtPermitidas.TryGetValue(file.ContentType, out var ext))
            return BadRequest(new { error = "Formato no soportado. Usa JPG, PNG o WEBP." });

        // Borramos versiones previas del mismo tipo con otra extension (idempotente).
        foreach (var otraExt in ExtPermitidas.Values.Where(e => e != ext))
        {
            var prevKey = $"tenants/{tenantId}/{tipo}{otraExt}";
            await _storage.DeleteAsync(prevKey, ct);
        }

        var key = $"tenants/{tenantId}/{tipo}{ext}";
        await using var stream = file.OpenReadStream();
        // S-09: el binario debe ser una imagen real y concordar con el Content-Type declarado.
        if (!await Security.ImageValidation.EsImagenValidaAsync(stream, file.ContentType, ct))
            return BadRequest(new { error = "El archivo no es una imagen valida (JPG, PNG o WEBP)." });
        var url = await _storage.UploadAsync(key, stream, file.ContentType, ct);

        _logger.LogInformation("Imagen {Tipo} subida para tenant {Tenant}: {Url}", tipo, tenantId, url);
        return Ok(new { url });
    }
}
