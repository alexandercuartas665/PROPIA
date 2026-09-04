using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Propia.Api.Authorization;
using Propia.Application.Integraciones;

namespace Propia.Api.Controllers;

/// <summary>
/// Conexion Gmail de la copropiedad para ENVIAR respuestas PQRSD (por tenant).
/// El OAuth client (client_id/secret) lo configura el Super Admin; aqui la copropiedad
/// conecta/desconecta su cuenta Gmail y consulta el estado.
/// </summary>
// S-06 (auditoria): conectar/desconectar el Gmail de la PH es accion de Administrador.
[ApiController]
[Route("api/gmail-envio")]
[Authorize]
[RequiereRol("Administrador")]
public class GmailEnvioController : ControllerBase
{
    private readonly IGmailEnvioService _svc;
    public GmailEnvioController(IGmailEnvioService svc) => _svc = svc;

    [HttpGet("estado")]
    public async Task<IActionResult> Estado(CancellationToken ct) => Ok(await _svc.ObtenerEstadoAsync(ct));

    // Devuelve la URL de consentimiento de Google (el front redirige el navegador ahi).
    [HttpGet("conectar")]
    public async Task<IActionResult> Conectar([FromQuery] string redirectUri, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(redirectUri)) return BadRequest(new { error = "redirectUri requerido." });
        var url = await _svc.ConstruirUrlAutorizacionAsync(redirectUri, ct);
        return url is null
            ? BadRequest(new { error = "El envio por Gmail no esta configurado por el operador." })
            : Ok(new { url });
    }

    // El front (pagina de callback) manda el code + state que Google devolvio.
    [HttpPost("completar")]
    public async Task<IActionResult> Completar([FromBody] CompletarGmailRequest req, CancellationToken ct)
    {
        var (ok, error) = await _svc.CompletarConexionAsync(req.Code, req.State, req.RedirectUri, ct);
        return ok ? NoContent() : BadRequest(new { error });
    }

    [HttpDelete("conexion")]
    public async Task<IActionResult> Desconectar(CancellationToken ct)
    {
        await _svc.DesconectarAsync(ct);
        return NoContent();
    }
}

public record CompletarGmailRequest(string Code, string State, string RedirectUri);
