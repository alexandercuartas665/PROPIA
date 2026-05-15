using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.EquipoOrg;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>
/// Endpoints del modulo 1.3 Gestion de Equipo (Capa 1) - spec v1.0.
/// Todos los endpoints requieren JWT con tenant_id activo (la organizacion se deriva
/// del Tenant.OrganizacionId).
/// </summary>
[ApiController]
[Route("api/equipo-org")]
[Authorize]
public class EquipoOrgController : ControllerBase
{
    private readonly IEquipoOrgService _svc;

    public EquipoOrgController(IEquipoOrgService svc) => _svc = svc;

    // ---------- Cargos ----------
    [HttpGet("cargos")]
    public async Task<IActionResult> ListarCargos(CancellationToken ct)
        => Ok(await _svc.ListarCargosAsync(ct));

    [HttpGet("cargos/{id:guid}")]
    public async Task<IActionResult> GetCargo(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetCargoDetalleAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("cargos")]
    public async Task<IActionResult> CrearCargo([FromBody] CrearCargoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearCargoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("cargos/{id:guid}")]
    public async Task<IActionResult> ActualizarCargo(Guid id, [FromBody] ActualizarCargoRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarCargoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("cargos/{id:guid}")]
    public async Task<IActionResult> EliminarCargo(Guid id, CancellationToken ct)
    {
        try { return await _svc.EliminarCargoAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("cargos/{id:guid}/permisos")]
    public async Task<IActionResult> AjustarPermisoCargo(Guid id,
        [FromBody] AjustarPermisoCargoRequest req, CancellationToken ct)
        => await _svc.AjustarPermisoCargoAsync(id, req, ct) ? NoContent() : NotFound();

    // ---------- Colaboradores ----------
    [HttpGet("colaboradores")]
    public async Task<IActionResult> ListarColaboradores(
        [FromQuery] EstadoColaborador? estado, [FromQuery] string? q, CancellationToken ct)
        => Ok(await _svc.ListarColaboradoresAsync(estado, q, ct));

    [HttpGet("colaboradores/{id:guid}")]
    public async Task<IActionResult> GetColaborador(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetColaboradorAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("identidad/buscar")]
    public async Task<IActionResult> BuscarIdentidad(
        [FromQuery] string? documento, [FromQuery] TipoDocumento? tipo,
        [FromQuery] string? email, CancellationToken ct)
    {
        var dto = await _svc.BuscarIdentidadAsync(documento, tipo, email, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("colaboradores")]
    public async Task<IActionResult> AgregarColaborador(
        [FromBody] AgregarColaboradorRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarColaboradorAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("colaboradores/{id:guid}/cargo")]
    public async Task<IActionResult> CambiarCargo(Guid id, [FromBody] CambiarCargoRequest req, CancellationToken ct)
    {
        try { return await _svc.CambiarCargoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("colaboradores/{id:guid}/permisos")]
    public async Task<IActionResult> AjustarPermisoColaborador(Guid id,
        [FromBody] AjustarPermisoColaboradorRequest req, CancellationToken ct)
        => await _svc.AjustarPermisoColaboradorAsync(id, req, ct) ? NoContent() : NotFound();

    [HttpPut("colaboradores/{id:guid}/permisos/reset")]
    public async Task<IActionResult> ResetearPermisos(Guid id, CancellationToken ct)
        => await _svc.ResetearPermisosColaboradorAsync(id, ct) ? NoContent() : NotFound();

    [HttpPut("colaboradores/{id:guid}/desactivar")]
    public async Task<IActionResult> Desactivar(Guid id,
        [FromBody] DesactivarColaboradorRequest req, CancellationToken ct)
    {
        try { return await _svc.DesactivarColaboradorAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("colaboradores/{id:guid}/reactivar")]
    public async Task<IActionResult> Reactivar(Guid id, CancellationToken ct)
        => await _svc.ReactivarColaboradorAsync(id, ct) ? NoContent() : NotFound();

    // ---------- Asignaciones a PHs ----------
    [HttpPost("colaboradores/{id:guid}/asignaciones")]
    public async Task<IActionResult> AgregarAsignacion(Guid id,
        [FromBody] AgregarAsignacionRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarAsignacionAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("asignaciones/{id:guid}/rol")]
    public async Task<IActionResult> CambiarRolPh(Guid id,
        [FromBody] CambiarRolPhRequest req, CancellationToken ct)
    {
        try { return await _svc.CambiarRolPhAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("asignaciones/{id:guid}")]
    public async Task<IActionResult> QuitarAsignacion(Guid id, CancellationToken ct)
        => await _svc.QuitarAsignacionAsync(id, ct) ? NoContent() : NotFound();

    // ---------- Vista colaborador (HU-1.3-04) ----------
    [HttpGet("mi-perfil")]
    public async Task<IActionResult> MiPerfil(CancellationToken ct)
    {
        var dto = await _svc.GetMiPerfilAsync(ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("companeros")]
    public async Task<IActionResult> Companeros(CancellationToken ct)
        => Ok(await _svc.ListarCompanerosAsync(ct));
}
