using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Common;
using Propia.Application.Integraciones;
using Propia.Domain.Enums;
using Propia.Infrastructure.Storage;

namespace Propia.Api.Controllers;

/// <summary>
/// Integraciones de plataforma del Super Admin (correo, marca, y proximamente Wompi/IA/Google/Evolution).
/// Ruta /admin/* protegida por la policy SuperAdmin. Portado y adaptado de CUBOT.travels.
/// </summary>
[ApiController]
[Route("admin")]
[Authorize(Policy = AdminController.SuperAdminPolicy)]
public class AdminIntegracionesController : ControllerBase
{
    private readonly IEmailConfigService _email;
    private readonly IPlatformBrandingService _branding;
    private readonly IEmailSender _emailSender;
    private readonly IAiServerConfigService _ai;
    private readonly IOcrServerConfigService _ocr;
    private readonly IWompiConfigService _wompi;
    private readonly IEvolutionMasterConfigService _evolution;
    private readonly IGoogleAuthConfigService _google;
    private readonly IBlobStorage _storage;

    public AdminIntegracionesController(
        IEmailConfigService email,
        IPlatformBrandingService branding,
        IEmailSender emailSender,
        IAiServerConfigService ai,
        IOcrServerConfigService ocr,
        IWompiConfigService wompi,
        IEvolutionMasterConfigService evolution,
        IGoogleAuthConfigService google,
        IBlobStorage storage)
    {
        _email = email;
        _branding = branding;
        _emailSender = emailSender;
        _ai = ai;
        _ocr = ocr;
        _wompi = wompi;
        _evolution = evolution;
        _google = google;
        _storage = storage;
    }

    // -------------------- Servidor de Correo --------------------

    [HttpGet("email-config")]
    public async Task<IActionResult> GetEmailConfig(CancellationToken ct)
        => Ok(await _email.GetAsync(ct) ?? new EmailConfigDto(null, 587, null, false, true, null, null, false, null));

    [HttpPut("email-config")]
    public async Task<IActionResult> SaveEmailConfig([FromBody] SaveEmailConfigRequest req, CancellationToken ct)
    {
        var (id, email) = Actor();
        var dto = await _email.SaveAsync(req, id, email, Ip(), ct);
        return Ok(dto);
    }

    [HttpPost("email-config/test")]
    public async Task<IActionResult> TestEmail([FromBody] TestEmailRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.ToEmail))
            return BadRequest(new { error = "destinatario_requerido" });

        var result = await _emailSender.SendAsync(
            req.ToEmail,
            "PROPIA - Correo de prueba",
            "<p>Este es un correo de prueba enviado desde la consola de administracion de PROPIA. " +
            "Si lo recibes, la configuracion SMTP es correcta.</p>",
            ct);

        return result.Success
            ? Ok(new { enviado = true })
            : BadRequest(new { enviado = false, error = result.Error });
    }

    // -------------------- Marca de plataforma --------------------

    [HttpGet("branding")]
    public async Task<IActionResult> GetBranding(CancellationToken ct)
        => Ok(await _branding.GetAsync(ct));

    [HttpPut("branding")]
    public async Task<IActionResult> SaveBranding([FromBody] SaveBrandingRequest req, CancellationToken ct)
    {
        var (id, email) = Actor();
        await _branding.SaveAsync(req, id, email, Ip(), ct);
        return Ok(await _branding.GetAsync(ct));
    }

    /// <summary>Sube un archivo de imagen (logo o icono) de la marca y devuelve su URL absoluta.</summary>
    [HttpPost("branding/logo")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> SubirLogoMarca(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "Archivo vacio." });
        if (file.Length > 5_000_000) return BadRequest(new { error = "Maximo 5 MB." });
        var ext = file.ContentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/svg+xml" => ".svg",
            _ => ""
        };
        if (ext == "") return BadRequest(new { error = "Formato no soportado. Usa PNG, JPG, WEBP o SVG." });

        var key = $"branding/{Guid.NewGuid():N}{ext}";
        await using var stream = file.OpenReadStream();
        var url = await _storage.UploadAsync(key, stream, file.ContentType, ct);

        // En Development el blob local devuelve una URL relativa (/uploads/...). El login y el
        // sidebar se sirven desde Propia.Web (otro origen), asi que la absolutizamos contra el API.
        if (url.StartsWith('/'))
            url = $"{Request.Scheme}://{Request.Host}{url}";

        return Ok(new { url });
    }

    // -------------------- Servidores de IA --------------------

    [HttpGet("ai-config")]
    public async Task<IActionResult> ListAiConfig(CancellationToken ct)
        => Ok(await _ai.ListAsync(ct));

    [HttpPut("ai-config")]
    public async Task<IActionResult> SaveAiConfig([FromBody] SaveAiProviderRequest req, CancellationToken ct)
    {
        var (id, email) = Actor();
        var dto = await _ai.SaveAsync(req, id, email, Ip(), ct);
        return Ok(dto);
    }

    // -------------------- OCR / extraccion de documentos --------------------

    [HttpGet("ocr-config")]
    public async Task<IActionResult> GetOcrConfig([FromQuery] OcrProvider? provider, CancellationToken ct)
        => Ok(await _ocr.GetAsync(provider, ct));

    [HttpPut("ocr-config")]
    public async Task<IActionResult> SaveOcrConfig([FromBody] SaveOcrProviderRequest req, CancellationToken ct)
    {
        var (id, email) = Actor();
        return Ok(await _ocr.SaveAsync(req, id, email, Ip(), ct));
    }

    // -------------------- Wompi (config maestra) --------------------

    [HttpGet("wompi-config")]
    public async Task<IActionResult> GetWompiConfig(CancellationToken ct)
        => Ok(await _wompi.GetAsync(ct) ?? new WompiConfigDto(
            Propia.Domain.Enums.WompiEnvironment.Sandbox, null, null, null, null, null, "COP", 3,
            Propia.Domain.Enums.WompiIntegrationStatus.NotConfigured, null, false, false, false));

    [HttpPut("wompi-config")]
    public async Task<IActionResult> SaveWompiConfig([FromBody] SaveWompiConfigRequest req, CancellationToken ct)
    {
        var (id, email) = Actor();
        return Ok(await _wompi.SaveAsync(req, id, email, Ip(), ct));
    }

    [HttpPost("wompi-config/validar")]
    public async Task<IActionResult> ValidarWompiConfig(CancellationToken ct)
    {
        var (id, email) = Actor();
        var result = await _wompi.ValidateAsync(id, email, Ip(), ct);
        if (result is null) return BadRequest(new { error = "wompi_no_configurado" });
        return Ok(result);
    }

    // -------------------- Evolution API (WhatsApp) --------------------

    [HttpGet("evolution-config")]
    public async Task<IActionResult> GetEvolutionConfig(CancellationToken ct)
        => Ok(await _evolution.GetAsync(ct) ?? new EvolutionMasterDto(
            null, null, false, Propia.Domain.Enums.EvolutionIntegrationStatus.NotConfigured, null, "Production", null, false));

    [HttpPut("evolution-config")]
    public async Task<IActionResult> SaveEvolutionConfig([FromBody] SaveEvolutionMasterRequest req, CancellationToken ct)
    {
        var (id, email) = Actor();
        return Ok(await _evolution.SaveAsync(req, id, email, Ip(), ct));
    }

    [HttpPost("evolution-config/validar")]
    public async Task<IActionResult> ValidarEvolutionConfig(CancellationToken ct)
    {
        var (id, email) = Actor();
        var result = await _evolution.ValidateAsync(id, email, Ip(), ct);
        if (result is null) return BadRequest(new { error = "evolution_no_configurado" });
        return Ok(result);
    }

    // -------------------- Login con Google --------------------

    [HttpGet("google-auth-config")]
    public async Task<IActionResult> GetGoogleAuthConfig(CancellationToken ct)
        => Ok(await _google.GetAsync(ct) ?? new GoogleAuthConfigDto(null, false, false));

    [HttpPut("google-auth-config")]
    public async Task<IActionResult> SaveGoogleAuthConfig([FromBody] SaveGoogleAuthConfigRequest req, CancellationToken ct)
    {
        var (id, email) = Actor();
        var dto = await _google.SaveAsync(req, id, email, Ip(), ct);
        return Ok(dto);
    }

    // -------------------- Diagnostico de Storage (R2 / Local) --------------------

    /// <summary>
    /// Salud del blob storage. Reporta el provider ACTIVO (R2 vs Local), la URL publica que se
    /// genera para una key de ejemplo, y hace un round-trip real: sube un blob de prueba, lo baja,
    /// verifica que su URL publica sea accesible por HTTP GET, y lo borra. Sirve para validar la
    /// config de R2 en produccion tras un deploy sin depender de subir imagenes a mano.
    /// Solo SuperAdmin (hereda la policy del controller).
    /// </summary>
    [HttpGet("storage/health")]
    public async Task<IActionResult> StorageHealth(CancellationToken ct)
    {
        var providerType = _storage.GetType().Name;  // "R2BlobStorage" o "LocalBlobStorage"
        var res = new Dictionary<string, object?>
        {
            ["provider"] = providerType,
            ["esR2"] = providerType.Contains("R2", StringComparison.OrdinalIgnoreCase),
            ["urlPublicaEjemplo"] = _storage.GetPublicUrl("tenants/EJEMPLO/logo.png"),
            ["resolveUrlEjemplo"] = _storage.ResolveUrl("https://uploads.propia.cubot.com.co/algun-bucket/tenants/EJEMPLO/logo.png"),
        };

        try
        {
            var probeKey = $"diagnostics/health-{Guid.NewGuid():N}.txt";
            var payload = System.Text.Encoding.UTF8.GetBytes("propia storage health check");
            string uploadedUrl;
            using (var ms = new MemoryStream(payload))
            {
                uploadedUrl = await _storage.UploadAsync(probeKey, ms, "text/plain", ct);
            }
            res["probeSubida"] = "ok";
            res["probeUrl"] = uploadedUrl;

            var bajado = await _storage.DownloadAsync(probeKey, ct);
            res["probeBajadaOk"] = bajado is not null && bajado.Length == payload.Length;

            if (uploadedUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                    var httpResp = await http.GetAsync(uploadedUrl, ct);
                    res["probePublicoHttpStatus"] = (int)httpResp.StatusCode;
                    res["probePublicoAccesible"] = httpResp.IsSuccessStatusCode;
                    if (!httpResp.IsSuccessStatusCode)
                        res["probePublicoNota"] = "La URL publica NO sirve el objeto: revisa Storage__R2__PublicUrl y el acceso publico del bucket R2.";
                }
                catch (Exception ex)
                {
                    res["probePublicoHttpStatus"] = "error";
                    res["probePublicoError"] = ex.Message;
                }
            }
            else
            {
                res["probePublicoNota"] = "URL relativa (LocalBlobStorage): el dominio del sitio NO la sirve en prod y se pierde al redeploy. Falta Storage__Provider=R2.";
            }

            await _storage.DeleteAsync(probeKey, ct);
            res["probeLimpieza"] = "ok";
        }
        catch (Exception ex)
        {
            res["probeError"] = ex.GetType().Name + ": " + ex.Message;
        }

        return Ok(res);
    }

    // -------------------- Helpers --------------------

    private (Guid Id, string Email) Actor()
    {
        var id = Guid.TryParse(User.FindFirstValue("user_id"), out var g) ? g : Guid.Empty;
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email") ?? "unknown";
        return (id, email);
    }

    private string? Ip() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
