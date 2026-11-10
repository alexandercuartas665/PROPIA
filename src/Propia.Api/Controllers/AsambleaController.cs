using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Api.Authorization;
using Propia.Application.Asambleas;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

[ApiController]
[Route("api/asambleas")]

[Authorize]
public class AsambleaController : ControllerBase
{
    private readonly IAsambleaService _svc;
    public AsambleaController(IAsambleaService svc) => _svc = svc;

    // Bandeja + ficha
    [HttpGet]
    public async Task<IActionResult> Bandeja([FromQuery] EstadoSesion? estado, [FromQuery] TipoSesion? tipo, CancellationToken ct)
        => Ok(await _svc.GetBandejaAsync(estado, tipo, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSesion(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetSesionAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    // Creacion + edicion
    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Crear)]
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearSesionRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.CrearSesionAsync(req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPut("puntos/{id:guid}")]
    public async Task<IActionResult> ActualizarPunto(Guid id, [FromBody] ActualizarPuntoRequest req, CancellationToken ct)
    {
        try { return await _svc.ActualizarPuntoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/documentos")]
    public async Task<IActionResult> AgregarDocumento(Guid id, [FromBody] AgregarDocumentoRequest req, CancellationToken ct)
        => Created("", await _svc.AgregarDocumentoAsync(id, req, ct));

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Eliminar)]
    [HttpDelete("documentos/{id:guid}")]
    public async Task<IActionResult> EliminarDocumento(Guid id, CancellationToken ct)
    {
        try { return await _svc.EliminarDocumentoAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Citacion
    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPut("{id:guid}/citar")]
    public async Task<IActionResult> Citar(Guid id, [FromBody] EnviarCitacionRequest req, CancellationToken ct)
    {
        try { return await _svc.EnviarCitacionAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Poderes
    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/poderes")]
    public async Task<IActionResult> OtorgarPoder(Guid id, [FromBody] OtorgarPoderRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.OtorgarPoderAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPut("poderes/{id:guid}/decidir")]
    public async Task<IActionResult> DecidirPoder(Guid id, [FromBody] DecidirPoderRequest req, CancellationToken ct)
    {
        try { return await _svc.DecidirPoderAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Sala
    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPut("{id:guid}/abrir-sala")]
    public async Task<IActionResult> AbrirSala(Guid id, CancellationToken ct)
    {
        try { return await _svc.AbrirSalaAsync(id, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPut("{id:guid}/check-in")]
    public async Task<IActionResult> CheckIn(Guid id, [FromBody] CheckInParticipanteRequest req, CancellationToken ct)
    {
        try { return await _svc.CheckInParticipanteAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Enlace self-service (Ola 1) para que un copropietario/apoderado registre asistencia y vote sin
    // login. Solo se emite si la persona tiene unidades habilitadas en la sesion. Vence 2 dias despues
    // de la fecha de la sesion (o de hoy, si la sesion ya paso).
    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpGet("{id:guid}/enlace-residente/{personaId:guid}")]
    public async Task<IActionResult> EnlaceResidente(Guid id, Guid personaId,
        [FromServices] IAsambleaTokenService tokens, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("tenant_id"), out var tenantId)) return Forbid();
        var vista = await _svc.GetSesionParaResidenteAsync(id, personaId, ct);
        if (vista is null) return NotFound();
        if (vista.MisUnidades.Count == 0)
            return BadRequest(new { error = "La persona no es propietaria ni apoderada con poder aprobado en esta sesion." });

        var expira = AsambleaEnlace.Vencimiento(vista.FechaSesion, DateTimeOffset.UtcNow);
        var token = tokens.Emitir(new AsambleaEnlaceToken(tenantId, id, personaId), expira);
        return Ok(new EnlaceResidenteDto(token, AsambleaEnlace.Ruta(tenantId, token), expira));
    }

    // Envia por correo el enlace self-service a cada propietario/apoderado habilitado (o a los
    // PersonaIds indicados). Responde de inmediato; los correos salen en segundo plano. La base del
    // enlace es App:PublicBaseUrl (prod); si no esta configurada, el origen del Web que llama.
    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPost("{id:guid}/enviar-enlaces")]
    public async Task<IActionResult> EnviarEnlaces(Guid id, [FromBody] EnviarEnlacesRequest req,
        [FromServices] IAsambleaEnlacesService enlaces, [FromServices] IConfiguration config, CancellationToken ct)
    {
        var baseUrl = (config["App:PublicBaseUrl"] ?? "").Trim();
        if (string.IsNullOrEmpty(baseUrl)) baseUrl = (req.BaseUrl ?? "").Trim();
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var u) || (u.Scheme != Uri.UriSchemeHttps && u.Scheme != Uri.UriSchemeHttp))
            return BadRequest(new { error = "No se pudo determinar la direccion publica de la aplicacion." });

        try
        {
            var r = await enlaces.ProgramarEnvioAsync(id, req.PersonaIds, u.GetLeftPart(UriPartial.Authority), ct);
            return r is null ? NotFound() : Ok(r);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Votaciones
    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/votaciones")]
    public async Task<IActionResult> AbrirVotacion(Guid id, [FromBody] AbrirVotacionRequest req, CancellationToken ct)
    {
        try { return Created("", await _svc.AbrirVotacionAsync(id, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Crear)]
    [HttpPost("votaciones/{id:guid}/votar")]
    public async Task<IActionResult> Votar(Guid id, [FromBody] EmitirVotoRequest req, CancellationToken ct)
    {
        try { return await _svc.EmitirVotoAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Aprobar)]
    [HttpPut("votaciones/{id:guid}/cerrar")]
    public async Task<IActionResult> CerrarVotacion(Guid id, CancellationToken ct)
    {
        try { return Ok(await _svc.CerrarVotacionAsync(id, new CerrarVotacionRequest(), ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Cierre
    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Aprobar)]
    [HttpPut("{id:guid}/cerrar")]
    public async Task<IActionResult> CerrarSesion(Guid id, [FromBody] CerrarSesionRequest req, CancellationToken ct)
    {
        try { return await _svc.CerrarSesionAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Acta
    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Crear)]
    [HttpPost("{id:guid}/acta")]
    public async Task<IActionResult> GenerarActa(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GenerarActaAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPut("actas/{id:guid}/firmar")]
    public async Task<IActionResult> FirmarActa(Guid id, [FromBody] FirmarActaRequest req, CancellationToken ct)
    {
        try { return await _svc.FirmarActaAsync(id, req, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Aprobar)]
    [HttpPut("actas/{id:guid}/publicar")]
    public async Task<IActionResult> PublicarActa(Guid id, CancellationToken ct)
    {
        try { return await _svc.PublicarActaAsync(id, new PublicarActaRequest(), ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Config
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig(CancellationToken ct) => Ok(await _svc.GetConfigAsync(ct));

    [RequierePermiso(ModuloCodigo.Asambleas, AccionPermiso.Editar)]
    [HttpPut("config")]
    public async Task<IActionResult> ActualizarConfig([FromBody] AsambleaConfigDto req, CancellationToken ct)
        => await _svc.ActualizarConfigAsync(req, ct) ? NoContent() : NotFound();
}
