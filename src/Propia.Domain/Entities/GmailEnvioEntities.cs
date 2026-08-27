using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Credenciales del OAuth client DEDICADO para ENVIO de correo por Gmail (scope gmail.send).
/// GLOBAL y singleton, configurable por el Super Admin. Distinto del login con Google.
/// El Client Secret se guarda cifrado (ISecretProtector).
/// </summary>
public class GmailEnvioAppConfig : BaseEntity
{
    public string? ClientId { get; set; }
    public string? ClientSecretEncrypted { get; set; }
    public bool IsEnabled { get; set; }
}

/// <summary>
/// Conexion Gmail de una copropiedad (por tenant): la cuenta desde la que se envian las respuestas
/// PQRSD. Guarda el refresh_token cifrado para poder enviar sin re-consentir. Un registro por tenant.
/// </summary>
public class GmailEnvioConexion : TenantEntity
{
    /// <summary>Correo de la cuenta Gmail conectada (el remitente From).</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>refresh_token de Google cifrado (permite obtener access_token cuando se necesita enviar).</summary>
    public string? RefreshTokenEncrypted { get; set; }

    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset? ConectadoAt { get; set; }
    public Guid? ConectadoPorUsuarioId { get; set; }
}
