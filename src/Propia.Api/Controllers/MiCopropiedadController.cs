using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.MiCopropiedad;
using Propia.Application.Presupuesto;
using Propia.Application.Cartera;
using Propia.Domain.Enums;

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
    private readonly IPresupuestoService _presupuesto;
    private readonly ICarteraService _cartera;

    public MiCopropiedadController(IMiCopropiedadService svc, IPresupuestoService presupuesto, ICarteraService cartera)
    {
        _svc = svc;
        _presupuesto = presupuesto;
        _cartera = cartera;
    }

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
    [HttpGet("unidades/{id:guid}")]
    public async Task<IActionResult> ObtenerUnidad(Guid id, CancellationToken ct)
    {
        var u = await _svc.ObtenerUnidadAsync(id, ct);
        return u is null ? NotFound() : Ok(u);
    }
    [HttpPost("unidades")]
    public async Task<IActionResult> CrearUnidad([FromBody] CrearUnidadRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearUnidadAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
    [HttpDelete("unidades/{id:guid}")]
    public async Task<IActionResult> EliminarUnidad(Guid id, CancellationToken ct)
        => await _svc.EliminarUnidadAsync(id, ct) ? NoContent() : NotFound();

    // ---------- Vinculos entre unidades (RN-09) ----------
    [HttpGet("unidades/{id:guid}/vinculos")]
    public async Task<IActionResult> ListVinculos(Guid id, CancellationToken ct) => Ok(await _svc.ListVinculosAsync(id, ct));
    [HttpPost("unidades/{id:guid}/vinculos")]
    public async Task<IActionResult> CrearVinculo(Guid id, [FromBody] CrearVinculoUnidadRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearVinculoAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
    [HttpDelete("vinculos/{id:guid}")]
    public async Task<IActionResult> EliminarVinculo(Guid id, CancellationToken ct)
        => await _svc.EliminarVinculoAsync(id, ct) ? NoContent() : NotFound();

    // ---------- Seccion 2: Tipos personalizados de unidad ----------
    [HttpGet("tipos-unidad")] public async Task<IActionResult> ListTiposUnidad(CancellationToken ct) => Ok(await _svc.ListTiposUnidadCustomAsync(ct));
    [HttpPost("tipos-unidad")]
    public async Task<IActionResult> CrearTipoUnidad([FromBody] CrearTipoUnidadCustomRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearTipoUnidadCustomAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
    [HttpDelete("tipos-unidad/{id:guid}")]
    public async Task<IActionResult> EliminarTipoUnidad(Guid id, CancellationToken ct)
        => await _svc.EliminarTipoUnidadCustomAsync(id, ct) ? NoContent() : NotFound();

    // ---------- Seccion 2: Tipos de coeficiente PH (RN-02) ----------
    [HttpGet("tipos-coeficiente")] public async Task<IActionResult> ListTiposCoef(CancellationToken ct) => Ok(await _svc.ListTiposCoeficienteAsync(ct));
    [HttpPost("tipos-coeficiente")]
    public async Task<IActionResult> CrearTipoCoef([FromBody] CrearTipoCoeficienteRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearTipoCoeficienteAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
    [HttpDelete("tipos-coeficiente/{id:guid}")]
    public async Task<IActionResult> EliminarTipoCoef(Guid id, CancellationToken ct)
    {
        try { return await _svc.EliminarTipoCoeficienteAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
    [HttpGet("unidades/{unidadId:guid}/coeficientes")]
    public async Task<IActionResult> ListCoefUnidad(Guid unidadId, CancellationToken ct)
        => Ok(await _svc.ListCoeficientesUnidadAsync(unidadId, ct));
    [HttpPut("unidades/{unidadId:guid}/coeficientes")]
    public async Task<IActionResult> SetCoefUnidad(Guid unidadId, [FromBody] SetCoeficienteUnidadRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.SetCoeficienteUnidadAsync(unidadId, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ---------- Seccion 2: Generador inteligente + Import CSV ----------
    [HttpPost("unidades/generar")]
    public async Task<IActionResult> GenerarUnidades([FromBody] GenerarUnidadesRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.GenerarUnidadesAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
    [HttpPost("unidades/importar-csv")]
    public async Task<IActionResult> ImportarUnidadesCsv([FromBody] ImportarUnidadesRequest req, CancellationToken ct)
    {
        var resp = await _svc.ImportarUnidadesCsvAsync(req, ct);
        return resp.Aceptado ? Created("", resp) : UnprocessableEntity(resp);
    }

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

    // ---------- Seccion 4: Comites ----------
    [HttpGet("comites")] public async Task<IActionResult> ListComites(CancellationToken ct) => Ok(await _svc.ListComitesAsync(ct));
    [HttpPost("comites")]
    public async Task<IActionResult> CrearComite([FromBody] CrearComiteRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearComiteAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
    [HttpPut("comites/{id:guid}/desactivar")]
    public async Task<IActionResult> DesactivarComite(Guid id, CancellationToken ct)
        => await _svc.DesactivarComiteAsync(id, ct) ? NoContent() : NotFound();
    [HttpGet("comites/{id:guid}/miembros")]
    public async Task<IActionResult> ListMiembrosComite(Guid id, CancellationToken ct)
        => Ok(await _svc.ListMiembrosComiteAsync(id, ct));
    [HttpPost("comites/miembros")]
    public async Task<IActionResult> AgregarMiembroComite([FromBody] AgregarComiteMiembroRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarMiembroComiteAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
    [HttpDelete("comites/miembros/{id:guid}")]
    public async Task<IActionResult> RetirarMiembroComite(Guid id, CancellationToken ct)
        => await _svc.RetirarMiembroComiteAsync(id, ct) ? NoContent() : NotFound();

    // ---------- Seccion 4: Revisor Fiscal ----------
    [HttpGet("revisor-fiscal")]
    public async Task<IActionResult> GetRevisorFiscal(CancellationToken ct)
    {
        var r = await _svc.GetRevisorFiscalActivoAsync(ct);
        return r is null ? NoContent() : Ok(r);
    }
    [HttpPost("revisor-fiscal")]
    public async Task<IActionResult> DesignarRevisorFiscal([FromBody] DesignarRevisorFiscalRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.DesignarRevisorFiscalAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
    [HttpDelete("revisor-fiscal/{id:guid}")]
    public async Task<IActionResult> RetirarRevisorFiscal(Guid id, CancellationToken ct)
        => await _svc.RetirarRevisorFiscalAsync(id, ct) ? NoContent() : NotFound();

    // ---------- Seccion 3: Equipo de trabajo ----------
    [HttpGet("equipo")] public async Task<IActionResult> ListEquipo(CancellationToken ct) => Ok(await _svc.ListEquipoAsync(ct));
    [HttpPost("equipo")]
    public async Task<IActionResult> AgregarMiembroEquipo([FromBody] AgregarMiembroEquipoRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarMiembroEquipoAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
    [HttpDelete("equipo/{id:guid}")]
    public async Task<IActionResult> DesactivarMiembroEquipo(Guid id, CancellationToken ct)
        => await _svc.DesactivarMiembroEquipoAsync(id, ct) ? NoContent() : NotFound();
    [HttpPost("vincular-persona")]
    public async Task<IActionResult> VincularPersona([FromBody] VincularPersonaPorDocumentoRequest req, CancellationToken ct)
    {
        try { return Ok(new { personaId = await _svc.VincularPersonaPorDocumentoAsync(req, ct) }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

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
    [HttpPut("zonas/{id:guid}/estado")]
    public async Task<IActionResult> CambiarEstadoZona(Guid id, [FromBody] CambiarEstadoZonaRequest req, CancellationToken ct)
        => await _svc.CambiarEstadoZonaAsync(id, req, ct) ? NoContent() : NotFound();
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
    [HttpPut("equipos/{id:guid}/estado")]
    public async Task<IActionResult> CambiarEstadoEquipo(Guid id, [FromBody] CambiarEstadoEquipoRequest req, CancellationToken ct)
        => await _svc.CambiarEstadoEquipoAsync(id, req, ct) ? NoContent() : NotFound();
    [HttpDelete("equipos/{id:guid}")]
    public async Task<IActionResult> EliminarEquipo(Guid id, CancellationToken ct)
        => await _svc.EliminarEquipoAsync(id, ct) ? NoContent() : NotFound();

    // ---------- Seccion 8: Finanzas ----------
    [HttpGet("finanzas/monedas")]
    public IActionResult ListMonedas() => Ok(_svc.ListMonedas());

    [HttpGet("finanzas")]
    public async Task<IActionResult> GetFinanzas(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId is null) return BadRequest(new { error = "no_active_tenant" });
        try
        {
            var parametros = await _svc.GetFinanzasParametrosAsync(tenantId.Value, ct);
            var resumen = await ConstruirResumenFinancieroAsync(ct);
            return Ok(new FinanzasDto(parametros, resumen));
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("finanzas")]
    public async Task<IActionResult> ActualizarFinanzas([FromBody] ActualizarFinanzasRequest req, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId is null) return BadRequest(new { error = "no_active_tenant" });
        try
        {
            var parametros = await _svc.ActualizarFinanzasAsync(tenantId.Value, req, ct);
            var resumen = await ConstruirResumenFinancieroAsync(ct);
            return Ok(new FinanzasDto(parametros, resumen));
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Resumen financiero en tiempo real (spec 2.3 seccion 8): se nutre de 2.6 Presupuesto
    /// y 2.7 Cartera. Defensivo: si un modulo aun no tiene datos, devuelve 0 en ese indicador.
    /// </summary>
    private async Task<ResumenFinancieroDto> ConstruirResumenFinancieroAsync(CancellationToken ct)
    {
        decimal cuotaVigente = 0, presupuestoAnual = 0, recaudoPct = 0, carteraMora = 0;
        bool hayVigente = false;

        try
        {
            var presupuestos = await _presupuesto.ListarPresupuestosAsync(ct);
            var vigente = presupuestos.FirstOrDefault(p => p.Estado == EstadoPresupuesto.EnEjecucion)
                          ?? presupuestos.FirstOrDefault(p => p.Estado == EstadoPresupuesto.Aprobado);
            if (vigente is not null)
            {
                hayVigente = true;
                presupuestoAnual = vigente.MontoTotal;
                var detalle = await _presupuesto.GetPresupuestoDetalleAsync(vigente.Id, ct);
                if (detalle is not null) cuotaVigente = detalle.CuotaProyectadaPromedioMes;
            }
        }
        catch { /* 2.6 sin datos */ }

        try
        {
            var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
            var recaudo = await _presupuesto.GetRecaudoResumenAsync(hoy, ct);
            recaudoPct = recaudo.PorcentajeRecaudo;
        }
        catch { /* sin liquidaciones del periodo */ }

        try
        {
            var tablero = await _cartera.GetTableroAsync(ct);
            carteraMora = tablero.Kpis.TotalMoraCop;
        }
        catch { /* 2.7 sin datos */ }

        return new ResumenFinancieroDto(cuotaVigente, recaudoPct, presupuestoAnual, carteraMora, hayVigente);
    }

    // ---------- Bitacora de cambios (RN-06) ----------
    [HttpGet("bitacora")]
    public async Task<IActionResult> ListBitacora([FromQuery] int limit = 50, CancellationToken ct = default)
        => Ok(await _svc.ListBitacoraAsync(limit, ct));

    // ---------- Helpers ----------
    private Guid? GetTenantId()
    {
        var raw = User.FindFirstValue("tenant_id");
        return Guid.TryParse(raw, out var g) ? g : null;
    }
}
