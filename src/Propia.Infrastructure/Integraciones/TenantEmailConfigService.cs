using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Propia.Application.Common;
using Propia.Application.Integraciones;
using Propia.Domain.Entities;
using Propia.Infrastructure.Email;
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
    private readonly IConfiguration? _config;

    public TenantEmailConfigService(PropiaDbContext db, ISecretProtector secret, IConfiguration? config = null)
    {
        _db = db;
        _secret = secret;
        _config = config;
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
            // Mismo limite duro que el correo de la plataforma: un puerto bloqueado no cuelga el request.
            var msg = EnvioSmtp.Mensaje(cfg.FromEmail!, cfg.FromName, toEmail, "Prueba de correo - PROPIA",
                "Este es un correo de prueba enviado desde la configuración de correo de tu copropiedad en PROPIA. Si lo recibes, la cuenta quedó bien configurada.",
                html: false);
            await EnvioSmtp.EnviarAsync(cfg.SmtpHost!, cfg.SmtpPort, cfg.UseSsl, cfg.SmtpUser, password, msg, EnvioSmtp.Limite(_config), ct);

            // Marca la ultima validacion exitosa.
            var live = await _db.TenantEmailConfigs.FirstOrDefaultAsync(ct);
            if (live is not null) { live.LastValidatedAt = DateTimeOffset.UtcNow; await _db.SaveChangesAsync(ct); }
            return (true, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            return (false, "No se pudo enviar: " + ex.Message);
        }
    }
}
