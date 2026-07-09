using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.MiCopropiedad;
using Propia.Application.Presupuesto;
using Propia.Application.Cartera;
using Propia.Domain.Enums;
using Propia.Infrastructure.Storage;

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
    private readonly IBlobStorage _storage;
    private readonly IDistribucionImportService _import;

    public MiCopropiedadController(IMiCopropiedadService svc, IPresupuestoService presupuesto, ICarteraService cartera, IBlobStorage storage, IDistribucionImportService import)
    {
        _svc = svc;
        _presupuesto = presupuesto;
        _cartera = cartera;
        _storage = storage;
        _import = import;
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

    // ---------- Seccion 2: Distribucion - Carga masiva por plantilla Excel ----------
    /// <summary>Descarga la plantilla .xlsx (Instrucciones + Unidades + Torres + Catalogos) con un ejemplo cargable.</summary>
    [HttpGet("distribucion/plantilla")]
    public IActionResult DescargarPlantillaDistribucion()
    {
        var bytes = _import.GenerarPlantilla();
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "plantilla-distribucion-propia.xlsx");
    }

    /// <summary>Procesa la plantilla subida: crea torres y unidades del tenant. Devuelve el resumen + errores por fila.</summary>
    [HttpPost("distribucion/importar")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> ImportarDistribucion([FromForm] IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "archivo_vacio" });
        try
        {
            await using var s = file.OpenReadStream();
            var res = await _import.ImportarAsync(s, ct);
            return Ok(res);
        }
        catch (Exception ex) { return BadRequest(new { error = "archivo_invalido", detalle = ex.Message }); }
    }

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
    [HttpPut("unidades/{id:guid}")]
    public async Task<IActionResult> ActualizarUnidad(Guid id, [FromBody] ActualizarUnidadRequest req, CancellationToken ct)
    {
        try
        {
            var r = await _svc.ActualizarUnidadAsync(id, req, ct);
            return r is null ? NotFound() : Ok(r);
        }
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

    // ---------- Personas vinculadas a una unidad (Propietario/Residente/Familiar) ----------
    [HttpGet("unidades/{id:guid}/personas")]
    public async Task<IActionResult> ListPersonasUnidad(Guid id, CancellationToken ct)
        => Ok(await _svc.ListPersonasUnidadAsync(id, ct));

    [HttpPost("unidades/{id:guid}/personas")]
    public async Task<IActionResult> AgregarPersonaUnidad(Guid id, [FromBody] AgregarPersonaUnidadRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AgregarPersonaUnidadAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("unidades-personas/{id:guid}")]
    public async Task<IActionResult> EditarPersonaUnidad(Guid id, [FromBody] AgregarPersonaUnidadRequest req, CancellationToken ct)
    {
        try { var r = await _svc.EditarPersonaUnidadAsync(id, req, ct); return r is null ? NotFound() : Ok(r); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("unidades-personas/{id:guid}")]
    public async Task<IActionResult> EliminarPersonaUnidad(Guid id, CancellationToken ct)
        => await _svc.EliminarPersonaUnidadAsync(id, ct) ? NoContent() : NotFound();

    // ---------- Campos personalizados de unidad (definicion por copropiedad + valor por unidad) ----------
    [HttpGet("unidades-campos")]
    public async Task<IActionResult> ListCamposDefinicion(CancellationToken ct) => Ok(await _svc.ListCamposDefinicionAsync(ct));

    [HttpPost("unidades-campos")]
    public async Task<IActionResult> CrearCampoDefinicion([FromBody] CrearCampoDefinicionRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearCampoDefinicionAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("unidades-campos/{definicionId:guid}")]
    public async Task<IActionResult> EliminarCampoDefinicion(Guid definicionId, CancellationToken ct)
        => await _svc.EliminarCampoDefinicionAsync(definicionId, ct) ? NoContent() : NotFound();

    [HttpGet("unidades/{id:guid}/campos")]
    public async Task<IActionResult> ListCamposUnidad(Guid id, CancellationToken ct) => Ok(await _svc.ListCamposUnidadAsync(id, ct));

    [HttpPut("unidades/{id:guid}/campos/{definicionId:guid}")]
    public async Task<IActionResult> SetCampoValor(Guid id, Guid definicionId, [FromBody] SetCampoValorRequest req, CancellationToken ct)
    {
        try { await _svc.SetCampoValorUnidadAsync(id, definicionId, req, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ---------- Documentos / anexos de una unidad ----------
    [HttpGet("unidades/{id:guid}/documentos")]
    public async Task<IActionResult> ListDocumentosUnidad(Guid id, CancellationToken ct) => Ok(await _svc.ListDocumentosUnidadAsync(id, ct));

    [HttpPost("unidades/{id:guid}/documentos")]
    [RequestSizeLimit(11_000_000)]
    public async Task<IActionResult> SubirDocumentoUnidad(Guid id, IFormFile file, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId is null) return BadRequest(new { error = "no_active_tenant" });
        if (file is null || file.Length == 0) return BadRequest(new { error = "Archivo vacio." });
        if (file.Length > 10_000_000) return BadRequest(new { error = "Maximo 10 MB." });
        var ext = System.IO.Path.GetExtension(file.FileName);
        var key = $"tenants/{tenantId:N}/unidades/{id:N}/docs/{Guid.NewGuid():N}{ext}";
        await using var stream = file.OpenReadStream();
        var url = Absolutizar(await _storage.UploadAsync(key, stream, file.ContentType, ct));
        var dto = await _svc.AgregarDocumentoUnidadAsync(id, file.FileName, url, file.Length, ct);
        return dto is null ? BadRequest(new { error = "No se pudo registrar el documento." }) : Ok(dto);
    }

    [HttpDelete("unidades-documentos/{docId:guid}")]
    public async Task<IActionResult> EliminarDocumentoUnidad(Guid docId, CancellationToken ct)
        => await _svc.EliminarDocumentoUnidadAsync(docId, ct) ? NoContent() : NotFound();

    // ---------- Prototipo v3: bloques nuevos de la ficha de inmueble ----------
    // Placas habilitadas para ingreso
    [HttpGet("unidades/{id:guid}/placas")]
    public async Task<IActionResult> ListPlacasUnidad(Guid id, CancellationToken ct) => Ok(await _svc.ListPlacasUnidadAsync(id, ct));

    [HttpPost("unidades/{id:guid}/placas")]
    public async Task<IActionResult> AgregarPlacaUnidad(Guid id, [FromBody] CrearUnidadPlacaRequest req, CancellationToken ct)
    {
        try { var dto = await _svc.AgregarPlacaUnidadAsync(id, req, ct); return dto is null ? NotFound() : Created("", dto); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("unidades-placas/{placaId:guid}")]
    public async Task<IActionResult> EliminarPlacaUnidad(Guid placaId, CancellationToken ct)
        => await _svc.EliminarPlacaUnidadAsync(placaId, ct) ? NoContent() : NotFound();

    // Arriendos y cobros mensuales
    [HttpGet("unidades/{id:guid}/arriendos")]
    public async Task<IActionResult> ListArriendosUnidad(Guid id, CancellationToken ct) => Ok(await _svc.ListArriendosUnidadAsync(id, ct));

    [HttpPost("unidades/{id:guid}/arriendos")]
    public async Task<IActionResult> AgregarArriendoUnidad(Guid id, [FromBody] CrearUnidadArriendoRequest req, CancellationToken ct)
    {
        try { var dto = await _svc.AgregarArriendoUnidadAsync(id, req, ct); return dto is null ? NotFound() : Created("", dto); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("unidades-arriendos/{arriendoId:guid}")]
    public async Task<IActionResult> EliminarArriendoUnidad(Guid arriendoId, CancellationToken ct)
        => await _svc.EliminarArriendoUnidadAsync(arriendoId, ct) ? NoContent() : NotFound();

    // Mascotas
    [HttpGet("unidades/{id:guid}/mascotas")]
    public async Task<IActionResult> ListMascotasUnidad(Guid id, CancellationToken ct) => Ok(await _svc.ListMascotasUnidadAsync(id, ct));

    [HttpPost("unidades/{id:guid}/mascotas")]
    public async Task<IActionResult> AgregarMascotaUnidad(Guid id, [FromBody] CrearUnidadMascotaRequest req, CancellationToken ct)
    {
        try { var dto = await _svc.AgregarMascotaUnidadAsync(id, req, ct); return dto is null ? NotFound() : Created("", dto); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("unidades-mascotas/{mascotaId:guid}")]
    public async Task<IActionResult> EliminarMascotaUnidad(Guid mascotaId, CancellationToken ct)
        => await _svc.EliminarMascotaUnidadAsync(mascotaId, ct) ? NoContent() : NotFound();

    // Empleada(s) de servicio
    [HttpGet("unidades/{id:guid}/empleadas")]
    public async Task<IActionResult> ListEmpleadasUnidad(Guid id, CancellationToken ct) => Ok(await _svc.ListEmpleadasUnidadAsync(id, ct));

    [HttpPost("unidades/{id:guid}/empleadas")]
    public async Task<IActionResult> AgregarEmpleadaUnidad(Guid id, [FromBody] CrearUnidadEmpleadaRequest req, CancellationToken ct)
    {
        try { var dto = await _svc.AgregarEmpleadaUnidadAsync(id, req, ct); return dto is null ? NotFound() : Created("", dto); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("unidades-empleadas/{empleadaId:guid}")]
    public async Task<IActionResult> EliminarEmpleadaUnidad(Guid empleadaId, CancellationToken ct)
        => await _svc.EliminarEmpleadaUnidadAsync(empleadaId, ct) ? NoContent() : NotFound();

    // ---------- Oleada 2: historico de titularidad (propietarios) ----------
    [HttpGet("unidades/{id:guid}/titularidad")]
    public async Task<IActionResult> ListTitularidadUnidad(Guid id, CancellationToken ct) => Ok(await _svc.ListTitularidadUnidadAsync(id, ct));

    [HttpPost("unidades/{id:guid}/titularidad")]
    public async Task<IActionResult> AgregarTitularidadUnidad(Guid id, [FromBody] CrearUnidadTitularidadRequest req, CancellationToken ct)
    {
        try { var dto = await _svc.AgregarTitularidadUnidadAsync(id, req, ct); return dto is null ? NotFound() : Created("", dto); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("unidades-titularidad/{titularidadId:guid}")]
    public async Task<IActionResult> EliminarTitularidadUnidad(Guid titularidadId, CancellationToken ct)
        => await _svc.EliminarTitularidadUnidadAsync(titularidadId, ct) ? NoContent() : NotFound();

    // ---------- Oleada 2: campos dinamicos por persona vinculada ----------
    [HttpGet("unidades-personas/{id:guid}/campos")]
    public async Task<IActionResult> ListCamposPersona(Guid id, CancellationToken ct) => Ok(await _svc.ListCamposPersonaAsync(id, ct));

    [HttpPost("unidades-personas/{id:guid}/campos")]
    public async Task<IActionResult> AgregarCampoPersona(Guid id, [FromBody] CrearUnidadPersonaCampoRequest req, CancellationToken ct)
    {
        try { var dto = await _svc.AgregarCampoPersonaAsync(id, req, ct); return dto is null ? NotFound() : Created("", dto); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("unidades-personas-campos/{campoId:guid}")]
    public async Task<IActionResult> EliminarCampoPersona(Guid campoId, CancellationToken ct)
        => await _svc.EliminarCampoPersonaAsync(campoId, ct) ? NoContent() : NotFound();

    [HttpGet("unidades/{id:guid}/cuota-consolidada")]
    public async Task<IActionResult> GetCuotaConsolidada(Guid id, CancellationToken ct)
    {
        var c = await _svc.GetCuotaConsolidadaAsync(id, ct);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpPost("unidades/rebalancear-coeficientes")]
    public async Task<IActionResult> RebalancearCoeficientes(CancellationToken ct)
        => Ok(await _svc.RebalancearCoeficientesAsync(ct));

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

    // Gobierno + equipo consolidado por persona (para tags en Usuarios 2.5)
    [HttpGet("gobierno/persona/{personaId:guid}")]
    public async Task<IActionResult> GetGobiernoPersona(Guid personaId, CancellationToken ct)
        => Ok(await _svc.GetGobiernoPersonaAsync(personaId, ct));
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
    [HttpPut("contratos/{id:guid}")]
    public async Task<IActionResult> ActualizarContrato(Guid id, [FromBody] ActualizarContratoRequest req, CancellationToken ct)
        => await _svc.ActualizarContratoAsync(id, req, ct) ? NoContent() : NotFound();
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
    [HttpPut("equipos/{id:guid}")]
    public async Task<IActionResult> ActualizarEquipo(Guid id, [FromBody] ActualizarEquipoActivoRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _svc.ActualizarEquipoAsync(id, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
    [HttpDelete("equipos/{id:guid}")]
    public async Task<IActionResult> EliminarEquipo(Guid id, CancellationToken ct)
        => await _svc.EliminarEquipoAsync(id, ct) ? NoContent() : NotFound();

    // ---------- Ficha tecnica completa del equipo/activo ----------
    [HttpGet("equipos/{id:guid}/ficha")]
    public async Task<IActionResult> GetEquipoFicha(Guid id, CancellationToken ct)
    {
        var ficha = await _svc.GetEquipoFichaAsync(id, ct);
        return ficha is null ? NotFound() : Ok(ficha);
    }

    [HttpPost("equipos/{id:guid}/fotos")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> SubirFotoEquipo(Guid id, IFormFile file, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId is null) return BadRequest(new { error = "no_active_tenant" });
        if (file is null || file.Length == 0) return BadRequest(new { error = "Archivo vacio." });
        if (file.Length > 5_000_000) return BadRequest(new { error = "Maximo 5 MB." });
        var ext = file.ContentType switch { "image/jpeg" => ".jpg", "image/png" => ".png", "image/webp" => ".webp", _ => "" };
        if (ext == "") return BadRequest(new { error = "Formato no soportado. Usa JPG, PNG o WEBP." });
        var key = $"tenants/{tenantId:N}/equipos/{id:N}/{Guid.NewGuid():N}{ext}";
        await using var stream = file.OpenReadStream();
        var url = Absolutizar(await _storage.UploadAsync(key, stream, file.ContentType, ct));
        var dto = await _svc.AgregarFotoEquipoAsync(id, url, ct);
        return dto is null ? BadRequest(new { error = "No se pudo registrar la foto." }) : Ok(dto);
    }

    [HttpDelete("equipos/fotos/{fotoId:guid}")]
    public async Task<IActionResult> EliminarFotoEquipo(Guid fotoId, CancellationToken ct)
        => await _svc.EliminarFotoEquipoAsync(fotoId, ct) ? NoContent() : NotFound();

    [HttpPost("equipos/{id:guid}/mejoras")]
    public async Task<IActionResult> AgregarMejora(Guid id, [FromBody] AgregarMejoraRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _svc.AgregarMejoraEquipoAsync(id, req, ct);
            return dto is null ? BadRequest(new { error = "no_active_tenant" }) : Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("equipos/mejoras/{mejoraId:guid}")]
    public async Task<IActionResult> EliminarMejora(Guid mejoraId, CancellationToken ct)
        => await _svc.EliminarMejoraEquipoAsync(mejoraId, ct) ? NoContent() : NotFound();

    [HttpPut("equipos/{id:guid}/activos-vinculados")]
    public async Task<IActionResult> ToggleActivoVinculado(Guid id, [FromBody] ToggleVinculoRequest req, CancellationToken ct)
    {
        await _svc.ToggleActivoVinculadoAsync(id, req, ct);
        return NoContent();
    }

    [HttpPut("equipos/{id:guid}/contratos-vinculados")]
    public async Task<IActionResult> ToggleContratoVinculado(Guid id, [FromBody] ToggleVinculoRequest req, CancellationToken ct)
    {
        await _svc.ToggleContratoVinculadoAsync(id, req, ct);
        return NoContent();
    }

    [HttpPost("equipos/{id:guid}/campos")]
    public async Task<IActionResult> AgregarCampo(Guid id, [FromBody] AgregarCampoRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _svc.AgregarCampoEquipoAsync(id, req, ct);
            return dto is null ? BadRequest(new { error = "no_active_tenant" }) : Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("equipos/campos/{campoId:guid}")]
    public async Task<IActionResult> EliminarCampo(Guid campoId, CancellationToken ct)
        => await _svc.EliminarCampoEquipoAsync(campoId, ct) ? NoContent() : NotFound();

    // ---------- Ventanas de disponibilidad (equipos reservables y zonas comunes) ----------
    [HttpGet("disponibilidad")]
    public async Task<IActionResult> ListVentanas(
        [FromQuery] Propia.Domain.Enums.TipoEntidadDisponibilidad tipo,
        [FromQuery] Guid entidadId, CancellationToken ct)
        => Ok(await _svc.ListVentanasAsync(tipo, entidadId, ct));

    [HttpPut("disponibilidad")]
    public async Task<IActionResult> GuardarVentanas([FromBody] GuardarVentanasRequest req, CancellationToken ct)
    {
        try { await _svc.GuardarVentanasAsync(req, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ---------- Ficha completa de zona comun (seccion 4) ----------
    [HttpGet("zonas/{id:guid}/ficha")]
    public async Task<IActionResult> GetZonaFicha(Guid id, CancellationToken ct)
    {
        var ficha = await _svc.GetZonaFichaAsync(id, GetPersonaId(), ct);
        return ficha is null ? NotFound() : Ok(ficha);
    }

    [HttpPut("zonas/{id:guid}/ficha")]
    public async Task<IActionResult> GuardarZonaFicha(Guid id, [FromBody] GuardarZonaFichaRequest req, CancellationToken ct)
        => await _svc.GuardarZonaFichaAsync(id, req, ct) ? NoContent() : NotFound();

    [HttpPost("zonas/{id:guid}/imagen")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> SubirImagenZona(Guid id, IFormFile file, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId is null) return BadRequest(new { error = "no_active_tenant" });
        if (file is null || file.Length == 0) return BadRequest(new { error = "Archivo vacio." });
        if (file.Length > 5_000_000) return BadRequest(new { error = "Maximo 5 MB." });
        var ext = file.ContentType switch { "image/jpeg" => ".jpg", "image/png" => ".png", "image/webp" => ".webp", _ => "" };
        if (ext == "") return BadRequest(new { error = "Formato no soportado. Usa JPG, PNG o WEBP." });
        var key = $"tenants/{tenantId:N}/zonas/{id:N}/{Guid.NewGuid():N}{ext}";
        await using var stream = file.OpenReadStream();
        var url = Absolutizar(await _storage.UploadAsync(key, stream, file.ContentType, ct));
        var saved = await _svc.SetZonaImagenAsync(id, url, ct);
        return saved is null ? NotFound() : Ok(new { url = saved });
    }

    [HttpPost("zonas/{id:guid}/facturas")]
    public async Task<IActionResult> AgregarZonaFactura(Guid id, [FromBody] AgregarZonaFacturaRequest req, CancellationToken ct)
    {
        var dto = await _svc.AgregarZonaFacturaAsync(id, req, ct);
        return dto is null ? BadRequest(new { error = "no_active_tenant" }) : Ok(dto);
    }

    [HttpDelete("zonas/facturas/{facturaId:guid}")]
    public async Task<IActionResult> EliminarZonaFactura(Guid facturaId, CancellationToken ct)
        => await _svc.EliminarZonaFacturaAsync(facturaId, ct) ? NoContent() : NotFound();

    [HttpPost("zonas/{id:guid}/documentos")]
    [RequestSizeLimit(11_000_000)]
    public async Task<IActionResult> SubirZonaDocumento(Guid id, IFormFile file, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId is null) return BadRequest(new { error = "no_active_tenant" });
        if (file is null || file.Length == 0) return BadRequest(new { error = "Archivo vacio." });
        if (file.Length > 10_000_000) return BadRequest(new { error = "Maximo 10 MB." });
        var safe = System.IO.Path.GetExtension(file.FileName);
        var key = $"tenants/{tenantId:N}/zonas/{id:N}/docs/{Guid.NewGuid():N}{safe}";
        await using var stream = file.OpenReadStream();
        var url = Absolutizar(await _storage.UploadAsync(key, stream, file.ContentType, ct));
        var dto = await _svc.AgregarZonaDocumentoAsync(id, file.FileName, url, ct);
        return dto is null ? BadRequest(new { error = "No se pudo registrar el documento." }) : Ok(dto);
    }

    [HttpDelete("zonas/documentos/{docId:guid}")]
    public async Task<IActionResult> EliminarZonaDocumento(Guid docId, CancellationToken ct)
        => await _svc.EliminarZonaDocumentoAsync(docId, ct) ? NoContent() : NotFound();

    [HttpPost("zonas/{id:guid}/campos")]
    public async Task<IActionResult> AgregarZonaCampo(Guid id, [FromBody] AgregarZonaCampoRequest req, CancellationToken ct)
    {
        var dto = await _svc.AgregarZonaCampoAsync(id, req, ct);
        return dto is null ? BadRequest(new { error = "no_se_pudo" }) : Ok(dto);
    }

    [HttpDelete("zonas/campos/{campoId:guid}")]
    public async Task<IActionResult> EliminarZonaCampo(Guid campoId, CancellationToken ct)
        => await _svc.EliminarZonaCampoAsync(campoId, ct) ? NoContent() : NotFound();

    [HttpPost("zonas/{id:guid}/novedad-imagen")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> SubirImagenNovedadZona(Guid id, IFormFile file, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId is null) return BadRequest(new { error = "no_active_tenant" });
        if (file is null || file.Length == 0) return BadRequest(new { error = "Archivo vacio." });
        if (file.Length > 5_000_000) return BadRequest(new { error = "Maximo 5 MB." });
        var ext = file.ContentType switch { "image/jpeg" => ".jpg", "image/png" => ".png", "image/webp" => ".webp", _ => "" };
        if (ext == "") return BadRequest(new { error = "Formato no soportado. Usa JPG, PNG o WEBP." });
        var key = $"tenants/{tenantId:N}/zonas/{id:N}/novedades/{Guid.NewGuid():N}{ext}";
        await using var stream = file.OpenReadStream();
        var url = Absolutizar(await _storage.UploadAsync(key, stream, file.ContentType, ct));
        return Ok(new { url });
    }

    [HttpPost("zonas/{id:guid}/novedades")]
    public async Task<IActionResult> PublicarZonaNovedad(Guid id, [FromBody] PublicarZonaNovedadRequest req, CancellationToken ct)
    {
        var dto = await _svc.PublicarZonaNovedadAsync(id, req, GetPersonaId(), ct);
        return dto is null ? BadRequest(new { error = "no_se_pudo" }) : Ok(dto);
    }

    [HttpDelete("zonas/novedades/{novedadId:guid}")]
    public async Task<IActionResult> EliminarZonaNovedad(Guid novedadId, CancellationToken ct)
        => await _svc.EliminarZonaNovedadAsync(novedadId, ct) ? NoContent() : NotFound();

    [HttpPost("zonas/novedades/{novedadId:guid}/comentarios")]
    public async Task<IActionResult> ComentarZonaNovedad(Guid novedadId, [FromBody] ComentarZonaNovedadRequest req, CancellationToken ct)
    {
        var dto = await _svc.ComentarZonaNovedadAsync(novedadId, req, GetPersonaId(), ct);
        return dto is null ? BadRequest(new { error = "no_se_pudo" }) : Ok(dto);
    }

    [HttpPost("zonas/novedades/{novedadId:guid}/like")]
    public async Task<IActionResult> LikeZonaNovedad(Guid novedadId, CancellationToken ct)
        => Ok(new { likes = await _svc.ToggleZonaNovedadLikeAsync(novedadId, GetPersonaId(), ct) });

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

    // ---------- Configuracion avanzada de Finanzas (bloque "mas informacion") ----------

    [HttpGet("finanzas/configuracion")]
    public async Task<IActionResult> GetConfigFinanzas(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId is null) return BadRequest(new { error = "no_active_tenant" });
        try { return Ok(await _svc.GetConfiguracionFinanzasAsync(tenantId.Value, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("finanzas/configuracion")]
    public async Task<IActionResult> ActualizarConfigFinanzas([FromBody] ActualizarConfiguracionFinanzasRequest req, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId is null) return BadRequest(new { error = "no_active_tenant" });
        try { return Ok(await _svc.ActualizarConfiguracionFinanzasAsync(tenantId.Value, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ---------- Cuentas bancarias ----------

    [HttpGet("finanzas/cuentas-bancarias")]
    public async Task<IActionResult> ListCuentasBancarias(CancellationToken ct)
        => Ok(await _svc.ListCuentasBancariasAsync(ct));

    [HttpPost("finanzas/cuentas-bancarias")]
    public async Task<IActionResult> CrearCuentaBancaria([FromBody] CrearCuentaBancariaRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.CrearCuentaBancariaAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("finanzas/cuentas-bancarias/{id:guid}")]
    public async Task<IActionResult> ActualizarCuentaBancaria(Guid id, [FromBody] ActualizarCuentaBancariaRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _svc.ActualizarCuentaBancariaAsync(id, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("finanzas/cuentas-bancarias/{id:guid}")]
    public async Task<IActionResult> EliminarCuentaBancaria(Guid id, CancellationToken ct)
        => await _svc.EliminarCuentaBancariaAsync(id, ct) ? NoContent() : NotFound();

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

    private Guid? GetPersonaId()
    {
        var raw = User.FindFirstValue("persona_id");
        return Guid.TryParse(raw, out var g) ? g : null;
    }

    /// <summary>
    /// Host unificado: la Web y la API se sirven en el MISMO origen, asi que las URLs relativas
    /// (/uploads/...) resuelven bien en el navegador. NO se absolutizan: absolutizar contra el
    /// host del request grababa el loopback (localhost:8080) al subir via el HttpClient server-side,
    /// rompiendo el visor. Se guarda tal cual (relativo en Local, absoluto si el blob ya lo es).
    /// </summary>
    private string Absolutizar(string url) => url;
}
