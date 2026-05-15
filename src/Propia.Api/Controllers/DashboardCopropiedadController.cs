using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.DashboardCopropiedad;

namespace Propia.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardCopropiedadController : ControllerBase
{
    private readonly IDashboardCopropiedadService _svc;
    public DashboardCopropiedadController(IDashboardCopropiedadService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> Resumen(CancellationToken ct) => Ok(await _svc.GetResumenAsync(ct));

    [HttpGet("alertas")]
    public async Task<IActionResult> Alertas(CancellationToken ct) => Ok(await _svc.ListarAlertasAsync(ct));

    [HttpPost("alertas")]
    public async Task<IActionResult> CrearAlerta([FromBody] CrearAlertaRequest req, CancellationToken ct)
        => Created("", await _svc.CrearAlertaAsync(req, ct));

    [HttpPut("alertas/{id:guid}/resolver")]
    public async Task<IActionResult> ResolverAlerta(Guid id, CancellationToken ct)
        => await _svc.ResolverAlertaAsync(id, ct) ? NoContent() : NotFound();

    [HttpGet("feed")]
    public async Task<IActionResult> Feed([FromQuery] int limit = 10, CancellationToken ct = default)
        => Ok(await _svc.ListarFeedAsync(limit, ct));

    [HttpPost("feed")]
    public async Task<IActionResult> RegistrarFeed([FromBody] CrearEventoFeedRequest req, CancellationToken ct)
        => Created("", await _svc.RegistrarEventoFeedAsync(req, ct));
}
