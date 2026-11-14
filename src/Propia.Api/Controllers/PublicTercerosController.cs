using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Common;
using Propia.Application.Directorio;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Api.Controllers;

/// <summary>
/// Registro PUBLICO de terceros (link que comparte la copropiedad): el proveedor sube su RUT, Camara de Comercio o
/// certificacion bancaria sin iniciar sesion; la IA los lee y el tercero queda CREADO directo (decision de Alex) con
/// sus documentos. Mismo patron que el PQRSD publico: la copropiedad va en el link, debe estar activa, y hay limite
/// de envios por IP. No expone datos del Directorio: solo devuelve el nombre del tercero creado.
/// </summary>
[ApiController]
[Route("api/publico/terceros")]
[AllowAnonymous]
public class PublicTercerosController : ControllerBase
{
    /// <summary>Lecturas con IA por IP y copropiedad cada hora (cada documento = una lectura). No se usa el limitador
    /// "auth" (15/min): un proveedor que sube varios documentos a la vez lo excederia.</summary>
    public const int MaxDocumentosPorHora = 20;

    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ITercerosIaService _terceros;
    private readonly IMemoryCache _cache;

    public PublicTercerosController(PropiaDbContext db, ITenantContext tenant, ITercerosIaService terceros, IMemoryCache cache)
    {
        _db = db; _tenant = tenant; _terceros = terceros; _cache = cache;
    }

    /// <summary>Nombre y logo de la copropiedad para el encabezado del formulario (404 si no existe o no esta activa).</summary>
    [HttpGet("{tenantId:guid}/config")]
    public async Task<IActionResult> Config(Guid tenantId, CancellationToken ct)
    {
        var t = await _db.Tenants.AsNoTracking().Where(x => x.Id == tenantId && x.Estado == EstadoCopropiedad.Activa)
            .Select(x => new { x.Nombre, x.LogoUrl }).FirstOrDefaultAsync(ct);
        return t is null ? NotFound(new { error = "Copropiedad no disponible." }) : Ok(new { nombre = t.Nombre, logoUrl = t.LogoUrl });
    }

    /// <summary>Lee un documento con el Agente Documental de la copropiedad (perfil tercero). No guarda el archivo.</summary>
    [HttpPost("{tenantId:guid}/leer")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Leer(Guid tenantId, [FromBody] OcrController.ExtraerAgenteRequest req, CancellationToken ct)
    {
        if (await ValidarAsync(tenantId, contar: true, ct) is { } error) return error;
        var ocr = ActivatorUtilities.CreateInstance<OcrController>(HttpContext.RequestServices);
        ocr.ControllerContext = ControllerContext;
        return await ocr.ExtraerAgenteParaCopropiedadAsync(req with { Perfil = "tercero", PdfBase64 = null }, tenantId, ct);
    }

    /// <summary>Crea (o completa) el tercero con lo leido y le asocia el documento.</summary>
    [HttpPost("{tenantId:guid}/registrar")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Registrar(Guid tenantId, [FromForm] IFormFile? file, [FromForm] string? datos, [FromForm] string? website, CancellationToken ct)
    {
        // Honeypot: un bot que llena el campo oculto se descarta sin pistas.
        if (!string.IsNullOrWhiteSpace(website)) return BadRequest(new { error = "No se pudo procesar la solicitud." });
        if (file is null || file.Length == 0) return BadRequest(new { error = "Falta el archivo." });
        var ct0 = (file.ContentType ?? "").ToLowerInvariant();
        if (!ct0.Contains("pdf") && !ct0.StartsWith("image/")) return BadRequest(new { error = "Sube el documento en PDF o como foto (JPG o PNG)." });
        if (await ValidarAsync(tenantId, contar: false, ct) is { } error) return error;

        TerceroLeidoRequest? req;
        try { req = System.Text.Json.JsonSerializer.Deserialize<TerceroLeidoRequest>(datos ?? "{}", new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)); }
        catch { return BadRequest(new { error = "Los datos leidos no son validos." }); }
        try
        {
            await using var s = file.OpenReadStream();
            var r = await _terceros.CrearDesdeDocumentoAsync(req ?? new TerceroLeidoRequest(), file.FileName, file.ContentType, file.Length, s, ct);
            return Ok(new { nombre = r.Nombre, creado = r.Creado, documento = r.DocumentoAsociado });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Copropiedad activa + limite por IP; fija la copropiedad del link en el request (RLS) como el PQRSD publico.
    private async Task<IActionResult?> ValidarAsync(Guid tenantId, bool contar, CancellationToken ct)
    {
        var activa = await _db.Tenants.AsNoTracking().AnyAsync(x => x.Id == tenantId && x.Estado == EstadoCopropiedad.Activa, ct);
        if (!activa) return NotFound(new { error = "Copropiedad no disponible." });
        var key = $"terpub:{tenantId:N}:{ClienteIp()}";
        var usados = _cache.GetOrCreate(key, e => { e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1); return 0; });
        if (usados >= MaxDocumentosPorHora)
            return StatusCode(429, new { error = "Has enviado muchos documentos en poco tiempo. Intenta de nuevo en una hora." });
        if (contar) _cache.Set(key, usados + 1, TimeSpan.FromHours(1));
        _tenant.SetTenant(tenantId);
        await _db.Database.CloseConnectionAsync();   // el interceptor aplica app.tenant_id al reabrir
        return null;
    }

    private string ClienteIp()
    {
        var fwd = Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(fwd)) return fwd.Split(',')[0].Trim();
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
    }
}
