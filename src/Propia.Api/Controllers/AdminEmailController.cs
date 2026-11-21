using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Application.Common;

namespace Propia.Api.Controllers;

/// <summary>
/// Diagnostico del correo saliente de la plataforma desde la API del Super Admin (JWT de Super Admin o token
/// pak_). Envia un correo de prueba con la configuracion guardada y el MISMO limite de tiempo que el envio
/// real, y dice por donde salio y cuanto tardo. Sirve para verificar el correo de prod sin crear usuarios.
/// </summary>
[ApiController]
[Route("api/admin/email")]
[Authorize(Policy = AdminController.SuperAdminPolicy)]
public class AdminEmailController : ControllerBase
{
    private readonly IEmailDiagnostico _diag;

    public AdminEmailController(IEmailDiagnostico diag) => _diag = diag;

    public sealed record ProbarCorreoRequest(string? To);

    /// <summary>200 { ok, proveedor, host, puerto, ms, error } (ok=false tambien es 200: el diagnostico corrio).</summary>
    [HttpPost("probar")]
    public async Task<IActionResult> Probar([FromBody] ProbarCorreoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.To) || !req.To.Contains('@'))
            return BadRequest(new { error = "Indica un correo de destino valido en \"to\"." });
        return Ok(await _diag.ProbarAsync(req.To.Trim(), ct));
    }
}
