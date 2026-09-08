using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Integraciones;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Integraciones;

/// <summary>
/// Cuenta de correo saliente (SMTP) por copropiedad. La fila esta protegida por RLS (TenantEmailConfig es
/// TenantEntity), asi que solo se ve/escribe la del tenant activo. La clave se cifra con ISecretProtector.
/// </summary>
public sealed class TenantEmailConfigService : ITenantEmailConfigService
{
    private readonly PropiaDbContext _db;
    private readonly ISecretProtector _secret;

    public TenantEmailConfigService(PropiaDbContext db, ISecretProtector secret)
    {
        _db = db;
        _secret = secret;
    }

    public async Task<TenantEmailConfigDto> GetAsync(CancellationToken ct)
    {
        var cfg = await _db.TenantEmailConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        if (cfg is null)
            return new TenantEmailConfigDto("smtp.gmail.com", 587, null, true, null, null, false, false, null);
        return new TenantEmailConfigDto(cfg.SmtpHost, cfg.SmtpPort, cfg.SmtpUser, cfg.UseSsl,
            cfg.FromEmail, cfg.FromName, cfg.IsEnabled, !string.IsNullOrEmpty(cfg.SmtpPasswordEncrypted),
            cfg.LastValidatedAt);
    }

    public async Task SaveAsync(GuardarTenantEmailConfigRequest req, CancellationToken ct)
    {
        var cfg = await _db.TenantEmailConfigs.FirstOrDefaultAsync(ct);
        if (cfg is null)
        {
            cfg = new TenantEmailConfig();
            _db.TenantEmailConfigs.Add(cfg);
        }
        cfg.SmtpHost = string.IsNullOrWhiteSpace(req.SmtpHost) ? "smtp.gmail.com" : req.SmtpHost.Trim();
        cfg.SmtpPort = req.SmtpPort <= 0 ? 587 : req.SmtpPort;
        cfg.SmtpUser = req.SmtpUser?.Trim();
        cfg.UseSsl = req.UseSsl;
        cfg.FromEmail = string.IsNullOrWhiteSpace(req.FromEmail) ? req.SmtpUser?.Trim() : req.FromEmail.Trim();
        cfg.FromName = req.FromName?.Trim();
        cfg.IsEnabled = req.IsEnabled;
        // La clave solo se reemplaza si viene una nueva (no vacia); asi el usuario puede editar el resto
        // sin re-escribir la contrasena de aplicacion.
        if (!string.IsNullOrWhiteSpace(req.Password))
            cfg.SmtpPasswordEncrypted = _secret.Protect(req.Password.Trim());
        await _db.SaveChangesAsync(ct);
    }

    public async Task<(bool Ok, string? Error)> ProbarAsync(string toEmail, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
            return (false, "Indica un correo de destino para la prueba.");
        var cfg = await _db.TenantEmailConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        if (cfg is null || string.IsNullOrWhiteSpace(cfg.SmtpHost) || string.IsNullOrWhiteSpace(cfg.SmtpUser))
            return (false, "Falta configurar el host SMTP y el usuario.");
        if (string.IsNullOrEmpty(cfg.SmtpPasswordEncrypted))
            return (false, "Falta la contrasena de aplicacion.");
        if (string.IsNullOrWhiteSpace(cfg.FromEmail))
            return (false, "Falta la direccion remitente (From).");

        string password;
        try { password = _secret.Unprotect(cfg.SmtpPasswordEncrypted); }
        catch { return (false, "La clave guardada no se pudo descifrar. Vuelve a guardarla."); }

        try
        {
            using var client = new SmtpClient(cfg.SmtpHost, cfg.SmtpPort)
            {
                EnableSsl = cfg.UseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Credentials = new NetworkCredential(cfg.SmtpUser, password)
            };
            using var msg = new MailMessage
            {
                From = new MailAddress(cfg.FromEmail!, cfg.FromName ?? cfg.FromEmail!),
                Subject = "Prueba de correo - PROPIA",
                Body = "Este es un correo de prueba enviado desde la configuracion de correo de tu copropiedad en PROPIA. Si lo recibes, la cuenta quedo bien configurada.",
                IsBodyHtml = false
            };
            msg.To.Add(toEmail.Trim());
            await client.SendMailAsync(msg, ct);

            // Marca la ultima validacion exitosa.
            var live = await _db.TenantEmailConfigs.FirstOrDefaultAsync(ct);
            if (live is not null) { live.LastValidatedAt = DateTimeOffset.UtcNow; await _db.SaveChangesAsync(ct); }
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, "No se pudo enviar: " + ex.Message);
        }
    }
}
