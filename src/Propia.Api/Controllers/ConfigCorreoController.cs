using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Api.Authorization;
using Propia.Application.Integraciones;
using Propia.Domain.Enums;

namespace Propia.Api.Controllers;

/// <summary>
/// Cuenta de correo saliente (SMTP) de la copropiedad. Se configura desde PQRSD > Configurar > Correo.
/// La lectura queda abierta al tenant (no expone la clave); las escrituras se gatean por permiso.
/// </summary>
[ApiController]
[Route("api/config/correo")]
[Authorize]
public class ConfigCorreoController : ControllerBase
{
    private readonly ITenantEmailConfigService _svc;
    public ConfigCorreoController(ITenantEmailConfigService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await _svc.GetAsync(ct));

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPut]
    public async Task<IActionResult> Guardar([FromBody] GuardarTenantEmailConfigRequest req, CancellationToken ct)
    {
        await _svc.SaveAsync(req, ct);
        return NoContent();
    }

    public record ProbarCorreoRequest(string ToEmail);

    [RequierePermiso(ModuloCodigo.Pqrs, AccionPermiso.Editar)]
    [HttpPost("probar")]
    public async Task<IActionResult> Probar([FromBody] ProbarCorreoRequest req, CancellationToken ct)
    {
        var (ok, error) = await _svc.ProbarAsync(req.ToEmail, ct);
        return ok ? Ok(new { ok = true }) : BadRequest(new { error });
    }
}
