using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Propia.Application.Pqrsd;

namespace Propia.Api.Controllers;

/// <summary>
/// Formulario PUBLICO de radicacion PQRSD (modulo 2.9). Sin login: se comparte como link/iframe/correo.
/// El tenant viaja como Guid en la URL (no expone catalogos de otras copropiedades). Seguridad:
/// no hay busqueda de unidades ni del directorio, el solicitante escribe su unidad exacta + identificacion;
/// solo REGISTRA (no valida contra la BD). Anti-abuso: limite por IP + honeypot.
/// </summary>
[ApiController]
[Route("api/publico/pqrsd")]
[AllowAnonymous]
public class PublicPqrsdController : ControllerBase
{
    private readonly IPqrsdService _svc;
    private readonly IMemoryCache _cache;
    public PublicPqrsdController(IPqrsdService svc, IMemoryCache cache) { _svc = svc; _cache = cache; }

    // Config del formulario: nombre + logo de la copropiedad + tipos/categorias activos.
    [HttpGet("{tenantId:guid}/config")]
    public async Task<IActionResult> Config(Guid tenantId, CancellationToken ct)
    {
        var cfg = await _svc.GetConfigPublicoAsync(tenantId, ct);
        return cfg is null ? NotFound(new { error = "Copropiedad no disponible." }) : Ok(cfg);
    }

    // Radicacion externa. Devuelve el numero de radicado para que el solicitante lo guarde.
    [HttpPost("{tenantId:guid}/radicar")]
    public async Task<IActionResult> Radicar(Guid tenantId, [FromBody] RadicarPublicoRequest req, CancellationToken ct)
    {
        var ip = ClienteIp();

        // Anti-abuso: maximo 5 radicaciones por IP cada 10 minutos (ventana deslizante simple).
        var key = $"pqrpub:{tenantId:N}:{ip}";
        var intentos = _cache.GetOrCreate(key, e => { e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10); return 0; });
        if (intentos >= 5)
            return StatusCode(429, new { error = "Has enviado varias solicitudes en poco tiempo. Intenta de nuevo mas tarde." });
        _cache.Set(key, intentos + 1, TimeSpan.FromMinutes(10));

        try
        {
            var res = await _svc.RadicarPublicoAsync(tenantId, req, ip, ct);
            return Ok(res);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Seguimiento publico: el radicador ve el estado + la respuesta + los adjuntos que el gestor
    // marco como compartidos. Se resuelve por (tenant, token opaco). No expone datos de otros expedientes.
    [HttpGet("{tenantId:guid}/seguimiento/{token:guid}")]
    public async Task<IActionResult> Seguimiento(Guid tenantId, Guid token, CancellationToken ct)
    {
        var dto = await _svc.GetSeguimientoPublicoAsync(tenantId, token, ct);
        return dto is null ? NotFound(new { error = "Seguimiento no disponible." }) : Ok(dto);
    }

    private string ClienteIp()
    {
        // Detras del proxy de Railway la IP real llega en X-Forwarded-For (primer valor de la lista).
        var fwd = Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(fwd))
            return fwd.Split(',')[0].Trim();
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
    }
}
