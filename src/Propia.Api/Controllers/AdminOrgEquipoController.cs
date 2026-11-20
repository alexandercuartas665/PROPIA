using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.SuperAdmin;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>
/// Equipo (Capa 1) de CUALQUIER organizacion desde la API del Super Admin. Acepta el JWT de Super Admin y el
/// token pak_ (policy SuperAdmin; NO es [SoloSesionInteractiva]). Nunca recibe ni devuelve contrasenas: el
/// acceso a cada copropiedad queda como usuario (Activo si ya tiene cuenta; si no, Pendiente para
/// admin/tenants/{t}/usuarios/{id}/activar-con-clave-temporal). Organizacion / colaborador / cargo / rol /
/// copropiedad ajenos 404; negocio 400 { error }.
/// </summary>
[ApiController]
[Route("api/admin/organizaciones/{orgId:guid}")]
[Authorize(Policy = AdminController.SuperAdminPolicy)]
public class AdminOrgEquipoController : ControllerBase
{
    private readonly IAdminOrgEquipoService _svc;

    public AdminOrgEquipoController(IAdminOrgEquipoService svc) => _svc = svc;

    [HttpGet("equipo")]
    public Task<IActionResult> Equipo(Guid orgId, [FromQuery] EstadoColaborador? estado, [FromQuery] string? q, CancellationToken ct)
        => Ejecutar(async () => Ok(await _svc.ListarEquipoAsync(orgId, estado, q, ct)));

    [HttpGet("cargos")]
    public Task<IActionResult> Cargos(Guid orgId, CancellationToken ct)
        => Ejecutar(async () => Ok(await _svc.ListarCargosAsync(orgId, ct)));

    /// <summary>Agrega (o devuelve, si ya lo era: 200 + yaExistia) un colaborador y sus asignaciones a copropiedades.</summary>
    [HttpPost("equipo")]
    public Task<IActionResult> Agregar(Guid orgId, [FromBody] AdminAgregarColaboradorRequest req, CancellationToken ct)
        => Ejecutar(async () =>
        {
            var r = await _svc.AgregarAsync(orgId, req, Actor(), ct);
            return r.YaExistia ? Ok(r) : StatusCode(StatusCodes.Status201Created, r);
        });

    [HttpPut("equipo/{colaboradorId:guid}/cargo")]
    public Task<IActionResult> Cargo(Guid orgId, Guid colaboradorId, [FromBody] AdminCambiarCargoRequest req, CancellationToken ct)
        => Ejecutar(async () => { await _svc.CambiarCargoAsync(orgId, colaboradorId, req, Actor(), ct); return NoContent(); });

    [HttpPut("equipo/{colaboradorId:guid}/desactivar")]
    public Task<IActionResult> Desactivar(Guid orgId, Guid colaboradorId, [FromBody] AdminDesactivarColaboradorRequest? req, CancellationToken ct)
        => Ejecutar(async () => { await _svc.DesactivarAsync(orgId, colaboradorId, req ?? new AdminDesactivarColaboradorRequest(null), Actor(), ct); return NoContent(); });

    [HttpPut("equipo/{colaboradorId:guid}/reactivar")]
    public Task<IActionResult> Reactivar(Guid orgId, Guid colaboradorId, CancellationToken ct)
        => Ejecutar(async () => { await _svc.ReactivarAsync(orgId, colaboradorId, Actor(), ct); return NoContent(); });

    private async Task<IActionResult> Ejecutar(Func<Task<IActionResult>> accion)
    {
        try { return await accion(); }
        catch (AdminRecursoNoEncontradoException ex) { return NotFound(new { error = ex.Message }); }
        catch (AdminConflictoException ex) { return Conflict(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    private AdminActor Actor()
    {
        Guid.TryParse(User.FindFirst("user_id")?.Value, out var actorId);
        var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value ?? "superadmin";
        return new AdminActor(actorId, email, HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());
    }
}
