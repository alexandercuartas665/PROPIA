using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Propia.Api.Controllers;

/// <summary>
/// Endpoint MVP de upload de imagenes (logo, foto de fachada, portada) del tenant.
/// Spec 2.3 - seccion Identidad. Para MVP usamos filesystem local en wwwroot/uploads.
/// En produccion: bucket S3/Azure Blob con CDN.
///
/// Reglas:
///  - Solo JPG/PNG/WEBP, max 5 MB
///  - Path: /uploads/tenants/{tenantId}/{tipo}.{ext}
///  - Devuelve URL relativa servida por StaticFiles
///  - Requiere JWT con tenant activo (mismo flujo que /api/mi-copropiedad/*)
/// </summary>
[ApiController]
[Route("api/uploads")]
[Authorize]
public class UploadsController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<UploadsController> _logger;
    private static readonly string[] TiposPermitidos = { "logo", "fachada", "portada" };
    private static readonly Dictionary<string, string> ExtPermitidas = new()
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    public UploadsController(IWebHostEnvironment env, ILogger<UploadsController> logger)
    {
        _env = env;
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

        var dir = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "tenants", tenantId);
        Directory.CreateDirectory(dir);
        var fileName = $"{tipo}{ext}";
        var fullPath = Path.Combine(dir, fileName);

        // Eliminamos versiones previas del mismo tipo (con otra extension)
        foreach (var ent in ExtPermitidas.Values.Where(e => e != ext))
        {
            var prev = Path.Combine(dir, $"{tipo}{ent}");
            if (System.IO.File.Exists(prev)) System.IO.File.Delete(prev);
        }

        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream, ct);
        }

        var url = $"/uploads/tenants/{tenantId}/{fileName}?v={DateTime.UtcNow.Ticks}";
        _logger.LogInformation("Imagen {Tipo} subida para tenant {Tenant}: {Url}", tipo, tenantId, url);
        return Ok(new { url });
    }
}
