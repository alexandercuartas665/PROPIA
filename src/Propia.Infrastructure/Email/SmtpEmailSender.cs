using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Email;

/// <summary>
/// Envio de correo via SMTP usando la configuracion global (cifrada) del Super Admin (EmailConfig).
/// Compatible con SendGrid (SMTP), Gmail, Mailgun, etc. No persiste ni loggea la clave.
/// Portado de CUBOT.travels.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly PropiaDbContext _db;
    private readonly ISecretProtector _secretProtector;

    public SmtpEmailSender(PropiaDbContext db, ISecretProtector secretProtector)
    {
        _db = db;
        _secretProtector = secretProtector;
    }

    public async Task<EmailSendResult> SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var cfg = await _db.EmailConfigs.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (cfg is null || !cfg.IsEnabled)
        {
            return new EmailSendResult(false, "El correo saliente no esta configurado/habilitado en la plataforma.");
        }
        if (string.IsNullOrWhiteSpace(cfg.SmtpHost) || string.IsNullOrWhiteSpace(cfg.FromEmail))
        {
            return new EmailSendResult(false, "Falta configurar el host SMTP o la direccion remitente.");
        }

        string? password = null;
        if (!string.IsNullOrEmpty(cfg.SmtpPasswordEncrypted))
        {
            try { password = _secretProtector.Unprotect(cfg.SmtpPasswordEncrypted); }
            catch { return new EmailSendResult(false, "La clave SMTP esta cifrada con una version anterior. Vuelve a guardarla."); }
        }

        try
        {
            using var client = new SmtpClient(cfg.SmtpHost, cfg.SmtpPort)
            {
                EnableSsl = cfg.UseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };
            if (!string.IsNullOrWhiteSpace(cfg.SmtpUser))
            {
                client.Credentials = new NetworkCredential(cfg.SmtpUser, password ?? string.Empty);
            }

            using var message = new MailMessage
            {
                From = new MailAddress(cfg.FromEmail, cfg.FromName ?? cfg.FromEmail),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(toEmail));

            await client.SendMailAsync(message, cancellationToken);
            return new EmailSendResult(true, null);
        }
        catch (Exception ex)
        {
            return new EmailSendResult(false, $"No se pudo enviar el correo: {ex.Message}");
        }
    }
}
