using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.MiCopropiedad;

namespace Propia.Api.Controllers;

/// <summary>
/// Endpoints del modulo 2.3 Mi Copropiedad - ficha viva de la PH.
/// Toda accion opera sobre el tenant activo del JWT (TenantMiddleware lo setea).
/// RLS garantiza aislamiento entre copropiedades.
/// </summary>
[ApiController]
[Route("api/mi-copropiedad")]
[Authorize]
public class MiCopropiedadController : ControllerBase
{
    private readonly IMiCopropiedadService _svc;

    public MiCopropiedadController(IMiCopropiedadService svc) => _svc = svc;

    // ---------- Resumen ----------
    [HttpGet("resumen")]
    public async Task<IActionResult> GetResumen(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId is null) return BadRequest(new { error = "no_active_tenant" });
        var r = await _svc.GetResumenAsync(tenantId.Value, ct);
        return r is null ? NotFound() : Ok(r);
    }

    // ---------- Seccion 1: Identidad ----------
    [HttpPut("identidad")]
    public async Task<IActionResult> ActualizarIdentidad([FromBody] ActualizarIdentidadRequest req, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId is null) return BadRequest(new { error = "no_active_tenant" });
        try
        {
            var r = await _svc.ActualizarIdentidadAsync(tenantId.Value, req, ct);
            return r is null ? NotFound() : Ok(r);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ---------- Seccion 2: Distribucion - Torres ----------
    [HttpGet("torres")] public async Task<IActionResult> ListTorres(CancellationToken ct) => Ok(await _svc.ListTorresAsync(ct));
    [HttpPost("torres")]
    public async Task<IActionResult> CrearTorre([FromBody] CrearTorreRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearTorreAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
    [HttpDelete("torres/{id:guid}")]
    public async Task<IActionResult> EliminarTorre(Guid id, CancellationToken ct)
        => await _svc.EliminarTorreAsync(id, ct) ? NoContent() : NotFound();

    // ---------- Seccion 2: Distribucion - Unidades ----------
    [HttpGet("unidades")] public async Task<IActionResult> ListUnidades(CancellationToken ct) => Ok(await _svc.ListUnidadesAsync(ct));
    [HttpPost("unidades")]
    public async Task<IActionResult> CrearUnidad([FromBody] CrearUnidadRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearUnidadAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
    [HttpDelete("unidades/{id:guid}")]
    public async Task<IActionResult> EliminarUnidad(Guid id, CancellationToken ct)
        => await _svc.EliminarUnidadAsync(id, ct) ? NoContent() : NotFound();

    // ---------- Seccion 4: Gobierno - Consejo ----------
    [HttpGet("consejo")] public async Task<IActionResult> ListConsejo(CancellationToken ct) => Ok(await _svc.ListMiembrosConsejoAsync(ct));
    [HttpPost("consejo")]
    public async Task<IActionResult> AgregarMiembro([FromBody] AgregarMiembroConsejoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarMiembroConsejoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
    [HttpPut("consejo/{id:guid}/desactivar")]
    public async Task<IActionResult> DesactivarMiembro(Guid id, CancellationToken ct)
        => await _svc.DesactivarMiembroConsejoAsync(id, ct) ? NoContent() : NotFound();

    // ---------- Seccion 5: Servicios ----------
    [HttpGet("contratos")] public async Task<IActionResult> ListContratos(CancellationToken ct) => Ok(await _svc.ListContratosAsync(ct));
    [HttpPost("contratos")]
    public async Task<IActionResult> CrearContrato([FromBody] CrearContratoServicioRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearContratoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
    [HttpDelete("contratos/{id:guid}")]
    public async Task<IActionResult> EliminarContrato(Guid id, CancellationToken ct)
        => await _svc.EliminarContratoAsync(id, ct) ? NoContent() : NotFound();

    // ---------- Seccion 6: Zonas Comunes ----------
    [HttpGet("zonas")] public async Task<IActionResult> ListZonas(CancellationToken ct) => Ok(await _svc.ListZonasComunesAsync(ct));
    [HttpPost("zonas")]
    public async Task<IActionResult> CrearZona([FromBody] CrearZonaComunRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearZonaComunAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
    [HttpDelete("zonas/{id:guid}")]
    public async Task<IActionResult> EliminarZona(Guid id, CancellationToken ct)
        => await _svc.EliminarZonaComunAsync(id, ct) ? NoContent() : NotFound();

    // ---------- Seccion 7: Equipos ----------
    [HttpGet("equipos")] public async Task<IActionResult> ListEquipos(CancellationToken ct) => Ok(await _svc.ListEquiposAsync(ct));
    [HttpPost("equipos")]
    public async Task<IActionResult> CrearEquipo([FromBody] CrearEquipoActivoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearEquipoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
    [HttpDelete("equipos/{id:guid}")]
    public async Task<IActionResult> EliminarEquipo(Guid id, CancellationToken ct)
        => await _svc.EliminarEquipoAsync(id, ct) ? NoContent() : NotFound();

    // ---------- Helpers ----------
    private Guid? GetTenantId()
    {
        var raw = User.FindFirstValue("tenant_id");
        return Guid.TryParse(raw, out var g) ? g : null;
    }
}
