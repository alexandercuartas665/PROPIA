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
