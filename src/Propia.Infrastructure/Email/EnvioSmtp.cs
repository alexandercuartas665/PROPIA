using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace Propia.Infrastructure.Email;

/// <summary>
/// Envio SMTP con limite de tiempo DURO (MailKit). System.Net.Mail.SmtpClient.Timeout no aplica a
/// SendMailAsync: si el puerto esta bloqueado (Railway sin plan Pro) el envio quedaba colgado ~5 min
/// esperando al TCP y el request moria con 502. Aqui todo el ciclo (conectar + autenticar + enviar)
/// corre bajo un CancellationToken que vence a los N segundos (Email:TimeoutSegundos, por defecto 20).
/// </summary>
public static class EnvioSmtp
{
    public const int TimeoutPorDefectoSegundos = 20;

    public static TimeSpan Limite(IConfiguration? config)
    {
        var s = config?.GetValue<int?>("Email:TimeoutSegundos") ?? TimeoutPorDefectoSegundos;
        return TimeSpan.FromSeconds(s is > 0 and <= 120 ? s : TimeoutPorDefectoSegundos);
    }

    /// <summary>Envia el mensaje. Lanza <see cref="TimeoutException"/> con "SMTP timeout (host:puerto)"
    /// si vence el limite; las demas fallas salen como la excepcion de MailKit.</summary>
    public static async Task EnviarAsync(string host, int puerto, bool ssl, string? usuario, string? clave,
        MimeMessage mensaje, TimeSpan limite, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(limite);
        using var smtp = new SmtpClient { Timeout = (int)limite.TotalMilliseconds };
        try
        {
            // 465 = TLS implicito; con SSL en otro puerto (587) = STARTTLS; sin SSL = texto plano.
            var seguridad = !ssl ? SecureSocketOptions.None
                : puerto == 465 ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;
            await smtp.ConnectAsync(host, puerto, seguridad, cts.Token);
            if (!string.IsNullOrWhiteSpace(usuario))
                await smtp.AuthenticateAsync(usuario, clave ?? string.Empty, cts.Token);
            await smtp.SendAsync(new MailKit.SendRequest(mensaje), cts.Token);
            await smtp.DisconnectAsync(true, cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"SMTP timeout ({host}:{puerto}) tras {limite.TotalSeconds:0} s");
        }
        catch (TimeoutException)
        {
            // El Timeout por operacion de MailKit puede vencer antes que el token: mismo mensaje con host:puerto.
            throw new TimeoutException($"SMTP timeout ({host}:{puerto}) tras {limite.TotalSeconds:0} s");
        }
        catch (IOException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException($"SMTP timeout ({host}:{puerto}) tras {limite.TotalSeconds:0} s");
        }
    }

    public static MimeMessage Mensaje(string fromEmail, string? fromName, string to, string asunto, string cuerpo, bool html)
    {
        var m = new MimeMessage();
        m.From.Add(new MailboxAddress(fromName ?? fromEmail, fromEmail));
        m.To.Add(MailboxAddress.Parse(to.Trim()));
        m.Subject = asunto;
        m.Body = new TextPart(html ? "html" : "plain") { Text = cuerpo };
        return m;
    }
}
