using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Api.Authorization;
using Propia.Application.Integraciones;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>
/// Lector de buzon (Ola 1): bandeja "Correos recibidos" + config de lectura IMAP de la copropiedad. GET abiertos al
/// tenant (la clave nunca sale); escrituras gateadas por permiso de Comunicaciones.
/// </summary>
[ApiController]
[Route("api/correos-recibidos")]
[Authorize]
public class CorreosRecibidosController : ControllerBase
{
    private readonly ICorreoEntranteService _svc;
    public CorreosRecibidosController(ICorreoEntranteService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct) => Ok(await _svc.ListarAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken ct)
        => await _svc.ObtenerAsync(id, ct) is { } d ? Ok(d) : NotFound();

    [HttpGet("config")]
    public async Task<IActionResult> Config(CancellationToken ct) => Ok(await _svc.GetConfigAsync(ct));

    [RequierePermiso(ModuloCodigo.Comunicaciones, AccionPermiso.Editar)]
    [HttpPut("config")]
    public async Task<IActionResult> GuardarConfig([FromBody] GuardarCorreoLecturaRequest req, CancellationToken ct)
    {
        var error = await _svc.GuardarConfigAsync(req, ct);
        return error is null ? Ok(await _svc.GetConfigAsync(ct)) : BadRequest(new { error });
    }

    [RequierePermiso(ModuloCodigo.Comunicaciones, AccionPermiso.Editar)]
    [HttpPost("config/probar")]
    public async Task<IActionResult> Probar(CancellationToken ct)
    {
        var (ok, error) = await _svc.ProbarConexionAsync(ct);
        return ok ? Ok(new { ok = true }) : BadRequest(new { error });
    }

    /// <summary>"Revisar ahora": lee y clasifica los correos nuevos sin esperar al job.</summary>
    [RequierePermiso(ModuloCodigo.Comunicaciones, AccionPermiso.Editar)]
    [HttpPost("revisar")]
    public async Task<IActionResult> Revisar(CancellationToken ct) => Ok(await _svc.RevisarAsync(ct));

    [RequierePermiso(ModuloCodigo.Comunicaciones, AccionPermiso.Editar)]
    [HttpPost("{id:guid}/reclasificar")]
    public async Task<IActionResult> Reclasificar(Guid id, CancellationToken ct)
        => await _svc.ReclasificarAsync(id, ct) is { } d ? Ok(d) : NotFound();

    public sealed record DescartarRequest(bool Descartar);

    [RequierePermiso(ModuloCodigo.Comunicaciones, AccionPermiso.Editar)]
    [HttpPost("{id:guid}/descartar")]
    public async Task<IActionResult> Descartar(Guid id, [FromBody] DescartarRequest req, CancellationToken ct)
        => await _svc.DescartarAsync(id, req.Descartar, ct) is { } d ? Ok(d) : NotFound();
}
