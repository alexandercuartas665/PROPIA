using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Propia.Application.Integraciones;
using Propia.Domain.Entities;

namespace Propia.Infrastructure.Integraciones;

/// <summary>
/// PQRSD por correo (Ola P2): responde desde la MISMA cuenta del buzon (la contrasena de aplicacion sirve para IMAP y
/// SMTP) y en el mismo hilo (In-Reply-To / References = Message-ID del correo recibido; asunto "Re: ..."). Gmail guarda
/// la respuesta en "Enviados" del buzon. Servidor SMTP: Gmail -> smtp.gmail.com; otro proveedor -> el host IMAP con
/// "smtp." en lugar de "imap.". Puerto 587 con STARTTLS. Nunca registra la contrasena.
/// </summary>
public sealed class RespuestaCorreoSender : IRespuestaCorreoSender
{
    public async Task<string?> ResponderAsync(BuzonCorreo buzon, string password, string para, string asunto,
        string html, string? enRespuestaA, CancellationToken ct = default)
    {
        try
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(buzon.Nombre, buzon.Cuenta.Trim()));
            msg.To.Add(MailboxAddress.Parse(para.Trim()));
            msg.Subject = AsuntoRespuesta(asunto);
            var id = enRespuestaA?.Trim().Trim('<', '>');
            if (!string.IsNullOrWhiteSpace(id))
            {
                msg.InReplyTo = id;
                msg.References.Add(id);
            }
            msg.Body = new BodyBuilder { HtmlBody = html, TextBody = TextoPlano(html) }.ToMessageBody();

            using var smtp = new SmtpClient { Timeout = 30000 };
            await smtp.ConnectAsync(SmtpHost(buzon.ImapHost), 587, SecureSocketOptions.StartTls, ct);
            await smtp.AuthenticateAsync(buzon.Cuenta.Trim(), password, ct);
            await smtp.SendAsync(new MailKit.SendRequest(msg), ct);
            await smtp.DisconnectAsync(true, ct);
            return null;
        }
        catch (OperationCanceledException) { throw; }
        catch (AuthenticationException) { return "La cuenta rechazó la contraseña de aplicación al enviar (SMTP)."; }
        catch (Exception ex) { return "No se pudo enviar la respuesta por el buzón: " + ex.Message; }
    }

    public static string SmtpHost(string? imapHost)
    {
        var h = (imapHost ?? "").Trim().ToLowerInvariant();
        if (h.Length == 0 || h.Contains("gmail") || h.Contains("googlemail")) return "smtp.gmail.com";
        return h.StartsWith("imap.") ? "smtp." + h[5..] : h;
    }

    public static string AsuntoRespuesta(string? asunto)
    {
        var a = (asunto ?? "").Trim();
        if (a.Length == 0) return "Re: Radicado de su solicitud";
        return a.StartsWith("re:", StringComparison.OrdinalIgnoreCase) ? a : "Re: " + a;
    }

    private static string TextoPlano(string html)
    {
        var t = System.Text.RegularExpressions.Regex.Replace(html, @"<(br|/p|/div|/li)\s*/?>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        t = System.Text.RegularExpressions.Regex.Replace(t, "<[^>]+>", "");
        return System.Net.WebUtility.HtmlDecode(t).Trim();
    }
}
