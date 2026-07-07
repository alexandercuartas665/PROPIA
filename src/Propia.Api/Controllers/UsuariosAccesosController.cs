using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Api.Authorization;
using Propia.Application.UsuariosAccesos;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>
/// Endpoints del modulo 2.5 Usuarios, Roles y Accesos (spec v1.0).
/// Gestion de bandeja, invitaciones (incluye endpoint publico de aceptar) y roles+permisos.
/// </summary>
[ApiController]
[Route("api/usuarios")]
[Authorize]
[RequierePermiso(ModuloCodigo.UsuariosAccesos, AccionPermiso.Ver)]  // P0: requiere permiso en la matriz de roles (Admin siempre pasa)
public class UsuariosAccesosController : ControllerBase
{
    private readonly IUsuariosService _svc;

    public UsuariosAccesosController(IUsuariosService svc) => _svc = svc;

    // -------- Bandeja --------
    [HttpGet]
    public async Task<IActionResult> ListarUsuarios([FromQuery] EstadoUsuarioTenant? estado, [FromQuery] string? q, CancellationToken ct)
        => Ok(await _svc.ListarUsuariosAsync(estado, q, ct));

    // -------- Etiquetas de usuario (2.5) --------
    [HttpGet("etiquetas")]
    public async Task<IActionResult> ListarEtiquetas(CancellationToken ct)
        => Ok(await _svc.ListarEtiquetasAsync(ct));

    [HttpPost("etiquetas")]
    public async Task<IActionResult> CrearEtiqueta([FromBody] CrearEtiquetaUsuarioRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearEtiquetaAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("etiquetas/{etiquetaId:guid}")]
    public async Task<IActionResult> ActualizarEtiqueta(Guid etiquetaId, [FromBody] ActualizarEtiquetaUsuarioRequest req, CancellationToken ct)
        => await _svc.ActualizarEtiquetaAsync(etiquetaId, req, ct) ? NoContent() : NotFound();

    [HttpDelete("etiquetas/{etiquetaId:guid}")]
    public async Task<IActionResult> EliminarEtiqueta(Guid etiquetaId, CancellationToken ct)
        => await _svc.EliminarEtiquetaAsync(etiquetaId, ct) ? NoContent() : NotFound();

    [HttpPost("{usuarioTenantId:guid}/etiquetas/{etiquetaId:guid}")]
    public async Task<IActionResult> AsignarEtiqueta(Guid usuarioTenantId, Guid etiquetaId, CancellationToken ct)
        => await _svc.AsignarEtiquetaAsync(usuarioTenantId, etiquetaId, ct) ? NoContent() : NotFound();

    [HttpDelete("{usuarioTenantId:guid}/etiquetas/{etiquetaId:guid}")]
    public async Task<IActionResult> QuitarEtiqueta(Guid usuarioTenantId, Guid etiquetaId, CancellationToken ct)
        => await _svc.QuitarEtiquetaAsync(usuarioTenantId, etiquetaId, ct) ? NoContent() : NotFound();

    // Foto de perfil del usuario (2.5.C): sube y persiste Persona.FotoUrl.
    [HttpPost("{usuarioTenantId:guid}/foto")]
    [RequestSizeLimit(5_500_000)]
    public async Task<IActionResult> SubirFoto(Guid usuarioTenantId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "Archivo vacio." });
        if (file.Length > 5_000_000) return BadRequest(new { error = "Maximo 5 MB." });
        var ext = file.ContentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => null
        };
        if (ext is null) return BadRequest(new { error = "Formato no soportado. Usa JPG, PNG o WEBP." });

        await using var stream = file.OpenReadStream();
        var url = await _svc.SubirFotoAsync(usuarioTenantId, stream, file.ContentType, ext, ct);
        return url is null ? NotFound() : Ok(new { url });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUsuario(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetUsuarioDetalleAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    // -------- Invitaciones --------
    [HttpGet("invitaciones")]
    public async Task<IActionResult> ListarPendientes(CancellationToken ct)
        => Ok(await _svc.ListarPendientesAsync(ct));

    [HttpPost("invitaciones")]
    public async Task<IActionResult> Invitar([FromBody] CrearInvitacionRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.InvitarAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("invitaciones/{id:guid}/reenviar")]
    public async Task<IActionResult> Reenviar(Guid id, CancellationToken ct)
        => await _svc.ReenviarInvitacionAsync(id, ct) ? NoContent() : NotFound();

    [HttpDelete("invitaciones/{id:guid}")]
    public async Task<IActionResult> Cancelar(Guid id, CancellationToken ct)
        => await _svc.CancelarInvitacionAsync(id, ct) ? NoContent() : NotFound();

    // -------- Cambio de rol / revocar --------
    [HttpPut("{id:guid}/rol")]
    public async Task<IActionResult> CambiarRol(Guid id, [FromBody] CambiarRolUsuarioRequest req, CancellationToken ct)
    {
        try { return await _svc.CambiarRolAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/revocar")]
    public async Task<IActionResult> Revocar(Guid id, [FromBody] RevocarAccesoRequest req, CancellationToken ct)
    {
        try { return await _svc.RevocarAccesoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/reactivar")]
    public async Task<IActionResult> Reactivar(Guid id, CancellationToken ct)
        => await _svc.ReactivarUsuarioAsync(id, ct) ? NoContent() : NotFound();

    // -------- Auditoria --------
    [HttpGet("auditoria")]
    public async Task<IActionResult> Auditoria([FromQuery] int limit = 50, CancellationToken ct = default)
        => Ok(await _svc.ListarAuditoriaAsync(limit, ct));
}

/// <summary>Endpoints publicos para aceptar invitacion (sin auth, usan token).</summary>
[ApiController]
[Route("api/invitaciones")]
[AllowAnonymous]
public class InvitacionesPublicasController : ControllerBase
{
    private readonly IUsuariosService _svc;

    public InvitacionesPublicasController(IUsuariosService svc) => _svc = svc;

    [HttpGet("{token}")]
    public async Task<IActionResult> Consultar(string token, CancellationToken ct)
    {
        var dto = await _svc.ConsultarInvitacionAsync(token, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("aceptar")]
    public async Task<IActionResult> Aceptar([FromBody] AceptarInvitacionRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.AceptarInvitacionAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}

/// <summary>Configuracion de roles y matriz de permisos.</summary>
[ApiController]
[Route("api/roles")]
[Authorize]
[RequierePermiso(ModuloCodigo.UsuariosAccesos, AccionPermiso.Ver)]  // P0: matriz de roles (Admin siempre pasa)
public class RolesController : ControllerBase
{
    private readonly IRolesService _svc;

    public RolesController(IRolesService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _svc.ListarRolesAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detalle(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetRolDetalleAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearRolRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearRolAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] ActualizarRolRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarRolAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
    {
        try { return await _svc.EliminarRolAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}/activar")]
    public async Task<IActionResult> ActivarExtendido(Guid id, CancellationToken ct)
    {
        try { return await _svc.ActivarRolExtendidoAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}/permisos")]
    public async Task<IActionResult> ActualizarPermiso(Guid id, [FromBody] ActualizarPermisoRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarPermisoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
