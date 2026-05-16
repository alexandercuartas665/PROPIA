using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Notificaciones;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>
/// T.2 Motor de Notificaciones - endpoints publicos.
///  - GET /api/notificaciones                  -> inbox del usuario actual + scope del tenant.
///  - GET /api/notificaciones/{id}             -> detalle.
///  - POST /api/notificaciones/{id}/leido      -> marca InApp como leido.
///  - POST /api/notificaciones/{id}/reintentar -> requeue una fallida.
///  - GET /api/notificaciones/resumen          -> counters para el campanita del header.
///
/// El endpoint de envio (EnviarAsync) NO se expone como REST - solo se invoca desde
/// otros servicios via INotificacionDispatcher. Esto evita que clientes externos
/// disparen spam.
/// </summary>
[ApiController]
[Route("api/notificaciones")]
[Authorize]
public class NotificacionesController : ControllerBase
{
    private readonly INotificacionesService _svc;
    public NotificacionesController(INotificacionesService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] EstadoNotificacion? estado,
        [FromQuery] CanalNotificacion? canal,
        [FromQuery] string? modulo,
        [FromQuery] int limite = 100,
        CancellationToken ct = default)
        => Ok(await _svc.ListarAsync(new FiltroNotificacionesRequest(estado, canal, modulo, limite), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var n = await _svc.GetAsync(id, ct);
        return n is null ? NotFound() : Ok(n);
    }

    [HttpPost("{id:guid}/leido")]
    public async Task<IActionResult> MarcarLeido(Guid id, CancellationToken ct)
        => Ok(new { leido = await _svc.MarcarLeidoAsync(id, ct) });

    [HttpPost("{id:guid}/reintentar")]
    public async Task<IActionResult> Reintentar(Guid id, CancellationToken ct)
        => Ok(await _svc.ReintentarAsync(id, ct));

    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen(CancellationToken ct)
        => Ok(await _svc.GetResumenAsync(ct));
}
