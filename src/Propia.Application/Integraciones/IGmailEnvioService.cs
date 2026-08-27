namespace Propia.Application.Integraciones;

/// <summary>
/// Gestiona el OAuth client de envio (Super Admin) y la conexion Gmail por copropiedad
/// (autorizacion, callback, estado, desconexion). El envio de correos lo hace IGmailSender.
/// </summary>
public interface IGmailEnvioService
{
    // ---- Super Admin: OAuth client dedicado para envio ----
    Task<GmailEnvioAppConfigDto?> ObtenerAppConfigAsync(CancellationToken ct);
    Task GuardarAppConfigAsync(GuardarGmailEnvioAppConfigRequest req, CancellationToken ct);

    // ---- Copropiedad (tenant): conexion de la cuenta Gmail ----
    Task<GmailEnvioEstadoDto> ObtenerEstadoAsync(CancellationToken ct);

    /// <summary>URL de consentimiento de Google (scope gmail.send, offline). Null si el app no esta configurado.</summary>
    Task<string?> ConstruirUrlAutorizacionAsync(string redirectUri, CancellationToken ct);

    /// <summary>Intercambia el code por tokens y guarda la conexion del tenant. state = tenant_id.</summary>
    Task<(bool Ok, string? Error)> CompletarConexionAsync(string code, string state, string redirectUri, CancellationToken ct);

    Task DesconectarAsync(CancellationToken ct);
}
