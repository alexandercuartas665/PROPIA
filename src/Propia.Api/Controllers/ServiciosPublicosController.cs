using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.ServiciosPublicos;

namespace Propia.Api.Controllers;

[ApiController]
[Route("api/servicios-publicos")]
[Authorize]
public class ServiciosPublicosController : ControllerBase
{
    private readonly IServiciosPublicosService _svc;
    public ServiciosPublicosController(IServiciosPublicosService svc) => _svc = svc;

    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen(CancellationToken ct) => Ok(await _svc.GetResumenAsync(ct));

    [HttpGet("cuentas")]
    public async Task<IActionResult> ListCuentas(CancellationToken ct) => Ok(await _svc.ListCuentasAsync(ct));

    [HttpGet("cuentas/{id:guid}")]
    public async Task<IActionResult> GetCuenta(Guid id, CancellationToken ct)
        => await _svc.GetCuentaAsync(id, ct) is { } d ? Ok(d) : NotFound();

    [HttpPost("cuentas")]
    public async Task<IActionResult> CrearCuenta([FromBody] CrearCuentaServicioRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearCuentaAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("cuentas/{id:guid}")]
    public async Task<IActionResult> EliminarCuenta(Guid id, CancellationToken ct)
        => await _svc.EliminarCuentaAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("cuentas/{id:guid}/registros")]
    public async Task<IActionResult> AgregarRegistro(Guid id, [FromBody] CrearRegistroConsumoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarRegistroAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("registros/{registroId:guid}")]
    public async Task<IActionResult> EliminarRegistro(Guid registroId, CancellationToken ct)
        => await _svc.EliminarRegistroAsync(registroId, ct) ? NoContent() : NotFound();

    [HttpPost("cuentas/{id:guid}/reclamaciones")]
    public async Task<IActionResult> AgregarReclamacion(Guid id, [FromBody] CrearReclamacionRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarReclamacionAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
