using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.TableroCompartido;

namespace Propia.Api.Controllers;

/// <summary>
/// Tablero compartido entre las copropiedades que el usuario administra (Capa 1). Espejo de las
/// tareas reales de cada tenant: la lista de tenants se deriva SIEMPRE en el servidor (rol
/// Administrador via get_tenants_for_persona); el cliente jamas decide que tenants ve.
/// </summary>
[ApiController]
[Route("api/tablero-compartido")]
[Authorize]
public class TableroCompartidoController : ControllerBase
{
    private readonly ITableroCompartidoService _svc;

    public TableroCompartidoController(ITableroCompartidoService svc) => _svc = svc;

    private Guid? UserId()
    {
        var raw = User.FindFirstValue("user_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    /// <summary>Board VIRTUAL con el contrato de un tablero normal (lo renderizan las vistas
    /// existentes del modulo de Tareas sin codigo nuevo).</summary>
    [HttpGet("board")]
    public async Task<IActionResult> ObtenerBoard(CancellationToken ct)
    {
        if (UserId() is not Guid userId) return Unauthorized();
        var dto = await _svc.ObtenerBoardVirtualAsync(userId, ct);
        return dto is null
            ? StatusCode(403, new { error = "Solo los administradores de copropiedades pueden ver el tablero compartido." })
            : Ok(dto);
    }

    [HttpGet]
    public async Task<IActionResult> Obtener(CancellationToken ct)
    {
        if (UserId() is not Guid userId) return Unauthorized();
        var dto = await _svc.ObtenerAsync(userId, ct);
        return dto is null
            ? StatusCode(403, new { error = "Solo los administradores de copropiedades pueden ver el tablero compartido." })
            : Ok(dto);
    }

    [HttpPost("mover")]
    public async Task<IActionResult> Mover([FromBody] MoverTarjetaCompartidaRequest req, CancellationToken ct)
    {
        if (UserId() is not Guid userId) return Unauthorized();
        var r = await _svc.MoverAsync(userId, req, ct);
        return r.Ok ? Ok(r) : BadRequest(r);
    }

    /// <summary>Personas de los directorios de TODAS mis copropiedades administradas (para
    /// invitar usuarios cross-tenant a un tablero de Tareas).</summary>
    [HttpGet("personas")]
    public async Task<IActionResult> BuscarPersonas([FromQuery] string? q, CancellationToken ct)
    {
        if (UserId() is not Guid userId) return Unauthorized();
        return Ok(await _svc.BuscarPersonasAsync(userId, q ?? "", ct));
    }
}
