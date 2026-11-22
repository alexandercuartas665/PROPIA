using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using Propia.Application.Common;
using Propia.Application.Integraciones;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Integraciones;

/// <summary>
/// Envia las respuestas de PQRSD desde un buzon de la copropiedad (modulo Buzones). Misma cuenta y contrasena de
/// aplicacion que la lectura IMAP; servidor SMTP derivado del IMAP (RespuestaCorreoSender.SmtpHost), puerto 587 con
/// STARTTLS. Gmail deja la respuesta en "Enviados" del buzon. Nunca registra la contrasena.
/// </summary>
public sealed class BuzonEnvioService : IBuzonEnvioService
{
    private readonly PropiaDbContext _db;
    private readonly ISecretProtector _secret;

    public BuzonEnvioService(PropiaDbContext db, ISecretProtector secret) { _db = db; _secret = secret; }

    public async Task<EnvioBuzonResultado> EnviarRespuestaPqrsdAsync(Guid? buzonId, IReadOnlyList<string> para, string asunto,
        string html, IReadOnlyList<CorreoAdjunto> adjuntos, CancellationToken ct = default)
    {
        var candidatos = await _db.BuzonesCorreo.AsNoTracking().Where(b => b.RespondePqrsd)
            .OrderByDescending(b => b.PqrsdPredeterminado).ThenBy(b => b.CreatedAt).ToListAsync(ct);
        if (candidatos.Count == 0) return new EnvioBuzonResultado(false, false, null, null, null);
        var buzon = buzonId is Guid id ? candidatos.FirstOrDefault(b => b.Id == id) : candidatos[0];
        if (buzon is null)
            return new EnvioBuzonResultado(true, false, buzonId, null, "El buzón elegido no está habilitado para responder PQRSD.");
        if (string.IsNullOrEmpty(buzon.PasswordCifrada))
            return new EnvioBuzonResultado(true, false, buzon.Id, buzon.Cuenta, $"El buzón {buzon.Nombre} no tiene contraseña de aplicación.");

        string password;
        try { password = _secret.Unprotect(buzon.PasswordCifrada); }
        catch { return new EnvioBuzonResultado(true, false, buzon.Id, buzon.Cuenta, $"La contraseña del buzón {buzon.Nombre} no se pudo leer: vuelve a guardarla en Buzones de correo."); }

        try
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(buzon.Nombre, buzon.Cuenta.Trim()));
            foreach (var p in para.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
                msg.To.Add(MailboxAddress.Parse(p.Trim()));
            msg.Subject = asunto;
            var cuerpo = new BodyBuilder { HtmlBody = html };
            foreach (var a in adjuntos)
                cuerpo.Attachments.Add(a.NombreArchivo, a.Contenido, ContentType.Parse(a.TipoMime));
            msg.Body = cuerpo.ToMessageBody();

            using var smtp = new SmtpClient { Timeout = 30000 };
            await smtp.ConnectAsync(RespuestaCorreoSender.SmtpHost(buzon.ImapHost), 587, SecureSocketOptions.StartTls, ct);
            await smtp.AuthenticateAsync(buzon.Cuenta.Trim(), password, ct);
            await smtp.SendAsync(new MailKit.SendRequest(msg), ct);
            await smtp.DisconnectAsync(true, ct);
            return new EnvioBuzonResultado(true, true, buzon.Id, buzon.Cuenta, null);
        }
        catch (OperationCanceledException) { throw; }
        catch (AuthenticationException)
        {
            return new EnvioBuzonResultado(true, false, buzon.Id, buzon.Cuenta, $"La cuenta {buzon.Cuenta} rechazó la contraseña de aplicación al enviar.");
        }
        catch (Exception ex)
        {
            return new EnvioBuzonResultado(true, false, buzon.Id, buzon.Cuenta, $"No se pudo enviar por el buzón {buzon.Nombre}: {ex.Message}");
        }
    }
}
