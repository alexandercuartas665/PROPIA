using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Asambleas;

namespace Propia.Api.Controllers;

/// <summary>
/// Asambleas Ola 1: ingreso y voto self-service del copropietario SIN login, por enlace firmado
/// (sesion + persona + vencimiento, ver IAsambleaTokenService). El tenant de la URL debe coincidir con
/// el del token (no se confia en la URL sola) y se fija antes de tocar datos (RLS). El gate de
/// propiedad y las reglas del voto viven en el servicio. Sin rate limiting por IP a proposito: en una
/// asamblea presencial muchos votan desde el mismo WiFi, y el token no es adivinable.
/// </summary>
[ApiController]
[Route("api/publico/asambleas")]
[AllowAnonymous]
public class PublicAsambleaController : ControllerBase
{
    private readonly IAsambleaService _svc;
    private readonly IAsambleaTokenService _tokens;
    public PublicAsambleaController(IAsambleaService svc, IAsambleaTokenService tokens) { _svc = svc; _tokens = tokens; }

    [HttpGet("{tenantId:guid}/sesion/{token}")]
    public async Task<IActionResult> Sesion(Guid tenantId, string token, CancellationToken ct)
    {
        var (enlace, error) = await AbrirAsync(tenantId, token, ct);
        if (enlace is null) return error!;
        var dto = await _svc.GetSesionParaResidenteAsync(enlace.SesionId, enlace.PersonaId, ct);
        return dto is null ? NotFound(new { error = "Asamblea no disponible." }) : Ok(dto);
    }

    [HttpPost("{tenantId:guid}/sesion/{token}/asistencia")]
    public async Task<IActionResult> Asistencia(Guid tenantId, string token, [FromBody] MiAsistenciaRequest req, CancellationToken ct)
    {
        var (enlace, error) = await AbrirAsync(tenantId, token, ct);
        if (enlace is null) return error!;
        try
        {
            if (!await _svc.MiCheckInAsync(enlace.SesionId, enlace.PersonaId, req.UnidadPrivadaId, req.Presente, ct))
                return NotFound(new { error = "La unidad no participa en esta asamblea." });
            return Ok(await _svc.GetSesionParaResidenteAsync(enlace.SesionId, enlace.PersonaId, ct));
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{tenantId:guid}/sesion/{token}/voto")]
    public async Task<IActionResult> Voto(Guid tenantId, string token, [FromBody] MiVotoRequest req, CancellationToken ct)
    {
        var (enlace, error) = await AbrirAsync(tenantId, token, ct);
        if (enlace is null) return error!;
        if (string.IsNullOrWhiteSpace(req.Opcion)) return BadRequest(new { error = "Selecciona una opcion." });
        try
        {
            if (!await _svc.MiVotoAsync(enlace.SesionId, req.VotacionId, enlace.PersonaId, req.UnidadPrivadaId, req.Opcion, ct))
                return NotFound(new { error = "Votacion no disponible en esta asamblea." });
            return Ok(await _svc.GetSesionParaResidenteAsync(enlace.SesionId, enlace.PersonaId, ct));
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Valida el token (autentico, vigente, del mismo tenant que la URL) y fija el tenant del request.
    private async Task<(AsambleaEnlaceToken? enlace, IActionResult? error)> AbrirAsync(Guid tenantId, string token, CancellationToken ct)
    {
        var enlace = _tokens.Validar(token);
        if (enlace is null || enlace.TenantId != tenantId)
            return (null, Unauthorized(new { error = "El enlace no es valido o ya vencio. Solicita uno nuevo a la administracion." }));
        if (!await _svc.ActivarTenantPublicoAsync(tenantId, ct))
            return (null, NotFound(new { error = "Copropiedad no disponible." }));
        return (enlace, null);
    }
}
