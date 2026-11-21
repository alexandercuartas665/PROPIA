namespace Propia.Application.Common;

/// <summary>Resultado de un intento de envio de correo. No expone credenciales.</summary>
public sealed record EmailSendResult(bool Success, string? Error);

/// <summary>
/// Envia correo transaccional (invitaciones 2.5, OTP onboarding 2.1, notificaciones T.2)
/// usando la configuracion SMTP global del Super Admin (EmailConfig). Portado de CUBOT.travels.
/// </summary>
public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}

/// <summary>Proveedores de correo saliente de la plataforma (EmailConfig.Proveedor).</summary>
public static class ProveedorCorreo
{
    public const string Smtp = "smtp";
    /// <summary>SendGrid Web API v3 por HTTPS (443). La API key es la misma que se usa como clave SMTP.</summary>
    public const string SendGridApi = "sendgrid_api";

    public static bool EsValido(string? p) => p is Smtp or SendGridApi;
}

/// <summary>Resultado de una prueba de envio: por donde salio, cuanto tardo y el error (sin credenciales).</summary>
public sealed record EmailProbeResult(bool Ok, string Proveedor, string? Host, int? Puerto, long Ms, string? Error);

/// <summary>Prueba el correo saliente de la plataforma con el mismo limite de tiempo que el envio real.</summary>
public interface IEmailDiagnostico
{
    Task<EmailProbeResult> ProbarAsync(string toEmail, CancellationToken cancellationToken = default);
}
