using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.SuperAdmin;
using Propia.Application.UsuariosAccesos;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>
/// Usuarios de CUALQUIER copropiedad desde la API del Super Admin, igual que AgentAdminApiController para los
/// agentes. Acepta el JWT de Super Admin y el token pak_ (policy SuperAdmin; NO es [SoloSesionInteractiva]).
/// El controller solo fija el actor y delega: el servicio fija el tenant (EF + RLS) y reutiliza
/// IUsuariosService / IRolesService / IDirectorioService. Nunca recibe ni devuelve contrasenas: el usuario
/// entra por el link de invitacion. Fail-closed: sin token 401; sin super admin 403; tenant inactivo o
/// usuario / persona / rol / invitacion de OTRO tenant 404; error de negocio 400 { error }.
/// </summary>
[ApiController]
[Route("admin/tenants/{tenantId:guid}")]
[Authorize(Policy = AdminController.SuperAdminPolicy)]
public class AdminTenantUsuariosController : ControllerBase
{
    private readonly IAdminTenantUsuariosService _svc;

    public AdminTenantUsuariosController(IAdminTenantUsuariosService svc) => _svc = svc;

    [HttpGet("usuarios")]
    public Task<IActionResult> Listar(Guid tenantId, [FromQuery] EstadoUsuarioTenant? estado, [FromQuery] string? q, CancellationToken ct)
        => Ejecutar(async () => Ok(await _svc.ListarUsuariosAsync(tenantId, estado, q, ct)));

    [HttpGet("usuarios/{usuarioTenantId:guid}")]
    public Task<IActionResult> Detalle(Guid tenantId, Guid usuarioTenantId, CancellationToken ct)
        => Ejecutar(async () => Ok(await _svc.ObtenerUsuarioAsync(tenantId, usuarioTenantId, ct)));

    [HttpGet("roles")]
    public Task<IActionResult> Roles(Guid tenantId, CancellationToken ct)
        => Ejecutar(async () => Ok(await _svc.ListarRolesAsync(tenantId, ct)));

    [HttpGet("personas")]
    public Task<IActionResult> Personas(Guid tenantId, [FromQuery] string? q, CancellationToken ct)
        => Ejecutar(async () => Ok(await _svc.BuscarPersonasAsync(tenantId, q, ct)));

    [HttpPost("usuarios")]
    public Task<IActionResult> Crear(Guid tenantId, [FromBody] AdminCrearUsuarioRequest req, CancellationToken ct)
        => Ejecutar(async () =>
        {
            var creado = await _svc.CrearUsuarioAsync(tenantId, req, Actor(), ct);
            return CreatedAtAction(nameof(Detalle), new { tenantId, usuarioTenantId = creado.UsuarioTenantId }, creado);
        });

    [HttpPut("usuarios/{usuarioTenantId:guid}/roles")]
    public Task<IActionResult> ActualizarRoles(Guid tenantId, Guid usuarioTenantId, [FromBody] ActualizarRolesUsuarioRequest req, CancellationToken ct)
        => Ejecutar(async () => { await _svc.ActualizarRolesAsync(tenantId, usuarioTenantId, req, Actor(), ct); return NoContent(); });

    [HttpPut("usuarios/{usuarioTenantId:guid}/cargos")]
    public Task<IActionResult> Cargos(Guid tenantId, Guid usuarioTenantId, [FromBody] ActualizarCargosUsuarioRequest req, CancellationToken ct)
        => Ejecutar(async () => { await _svc.ActualizarCargosAsync(tenantId, usuarioTenantId, req, Actor(), ct); return NoContent(); });

    [HttpPost("usuarios/{usuarioTenantId:guid}/revocar")]
    public Task<IActionResult> Revocar(Guid tenantId, Guid usuarioTenantId, [FromBody] RevocarAccesoRequest req, CancellationToken ct)
        => Ejecutar(async () => { await _svc.RevocarAsync(tenantId, usuarioTenantId, req, Actor(), ct); return NoContent(); });

    [HttpPost("usuarios/{usuarioTenantId:guid}/reactivar")]
    public Task<IActionResult> Reactivar(Guid tenantId, Guid usuarioTenantId, CancellationToken ct)
        => Ejecutar(async () => { await _svc.ReactivarAsync(tenantId, usuarioTenantId, Actor(), ct); return NoContent(); });

    [HttpPut("invitaciones/{invitacionId:guid}/reenviar")]
    public Task<IActionResult> ReenviarInvitacion(Guid tenantId, Guid invitacionId, CancellationToken ct)
        => Ejecutar(async () => { await _svc.ReenviarInvitacionAsync(tenantId, invitacionId, Actor(), ct); return NoContent(); });

    [HttpDelete("invitaciones/{invitacionId:guid}")]
    public Task<IActionResult> CancelarInvitacion(Guid tenantId, Guid invitacionId, CancellationToken ct)
        => Ejecutar(async () => { await _svc.CancelarInvitacionAsync(tenantId, invitacionId, Actor(), ct); return NoContent(); });

    /// <summary>404 para lo que no existe en la copropiedad (o es de otra); 400 { error } para negocio.</summary>
    private async Task<IActionResult> Ejecutar(Func<Task<IActionResult>> accion)
    {
        try { return await accion(); }
        catch (AdminRecursoNoEncontradoException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Super Admin dueno del JWT o del token pak_ (mismos claims user_id/email).</summary>
    private AdminActor Actor()
    {
        Guid.TryParse(User.FindFirst("user_id")?.Value, out var actorId);
        var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value ?? "superadmin";
        return new AdminActor(actorId, email, HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());
    }
}
