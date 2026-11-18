using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Api.Authorization;
using Propia.Application.Integraciones;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>
/// Lector de buzon: bandeja "Correos recibidos" y buzones IMAP de la copropiedad (uno o varios). GET abiertos al tenant
/// (la contrasena nunca sale); escrituras gateadas por permiso de Comunicaciones. Los temas del coordinador son la
/// lista por copropiedad "correo.tema" (api/campos/opciones).
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

    // ---------------------------------------------------------------- buzones

    [HttpGet("buzones")]
    public async Task<IActionResult> Buzones(CancellationToken ct) => Ok(await _svc.ListarBuzonesAsync(ct));

    [RequierePermiso(ModuloCodigo.Comunicaciones, AccionPermiso.Editar)]
    [HttpPost("buzones")]
    public async Task<IActionResult> CrearBuzon([FromBody] GuardarBuzonRequest req, CancellationToken ct)
    {
        var (b, error) = await _svc.CrearBuzonAsync(req, ct);
        return b is not null ? Ok(b) : BadRequest(new { error });
    }

    [RequierePermiso(ModuloCodigo.Comunicaciones, AccionPermiso.Editar)]
    [HttpPut("buzones/{id:guid}")]
    public async Task<IActionResult> GuardarBuzon(Guid id, [FromBody] GuardarBuzonRequest req, CancellationToken ct)
    {
        var (b, error) = await _svc.GuardarBuzonAsync(id, req, ct);
        if (b is not null) return Ok(b);
        return error is null ? NotFound() : BadRequest(new { error });
    }

    [RequierePermiso(ModuloCodigo.Comunicaciones, AccionPermiso.Editar)]
    [HttpDelete("buzones/{id:guid}")]
    public async Task<IActionResult> EliminarBuzon(Guid id, CancellationToken ct)
        => await _svc.EliminarBuzonAsync(id, ct) ? NoContent() : NotFound();

    /// <summary>Prueba la conexion IMAP con lo escrito en el formulario (lo que falte se toma del buzon guardado).</summary>
    [RequierePermiso(ModuloCodigo.Comunicaciones, AccionPermiso.Editar)]
    [HttpPost("buzones/probar")]
    public async Task<IActionResult> Probar([FromBody] ProbarBuzonRequest req, CancellationToken ct)
    {
        var (ok, error) = await _svc.ProbarConexionAsync(req, ct);
        return ok ? Ok(new { ok = true }) : BadRequest(new { error });
    }

    /// <summary>"Revisar ahora": lee y clasifica los correos nuevos sin esperar al job (todos los buzones activos o uno).</summary>
    [RequierePermiso(ModuloCodigo.Comunicaciones, AccionPermiso.Editar)]
    [HttpPost("revisar")]
    public async Task<IActionResult> Revisar([FromQuery] Guid? buzonId, CancellationToken ct) => Ok(await _svc.RevisarAsync(buzonId, ct));

    // ---------------------------------------------------------------- correo

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
