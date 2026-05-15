using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.PanelConsolidado;

namespace Propia.Api.Controllers;

[ApiController]
[Route("api/panel")]
[Authorize]
public class PanelConsolidadoController : ControllerBase
{
    private readonly IPanelConsolidadoService _svc;
    public PanelConsolidadoController(IPanelConsolidadoService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> GetPanel(CancellationToken ct) => Ok(await _svc.GetPanelAsync(ct));

    [HttpPost("recalcular")]
    public async Task<IActionResult> Recalcular(CancellationToken ct)
        => Ok(new { actualizadas = await _svc.RecalcularSnapshotsAsync(ct) });

    [HttpGet("feed")]
    public async Task<IActionResult> Feed([FromQuery] int limit = 20, CancellationToken ct = default)
        => Ok(await _svc.ListarFeedAsync(limit, ct));
}
