using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Integraciones;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Integraciones;

/// <summary>
/// Servidor de correo saliente (singleton global). La clave SMTP se cifra con ISecretProtector;
/// solo se re-cifra si llega un valor nuevo. Nunca se devuelve ni se loggea en claro.
/// Cada cambio deja registro en super_admin_logs (append-only). Portado de CUBOT.travels.
/// </summary>
public sealed class EmailConfigService : IEmailConfigService
{
    private readonly PropiaDbContext _db;
    private readonly ISecretProtector _secretProtector;

    public EmailConfigService(PropiaDbContext db, ISecretProtector secretProtector)
    {
        _db = db;
        _secretProtector = secretProtector;
    }

    public async Task<EmailConfigDto?> GetAsync(CancellationToken ct = default)
    {
        var cfg = await _db.EmailConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        return cfg is null ? null : Map(cfg);
    }

    public async Task<EmailConfigDto> SaveAsync(SaveEmailConfigRequest request, Guid actorId, string actorEmail, string? ip, CancellationToken ct = default)
    {
        var cfg = await _db.EmailConfigs.FirstOrDefaultAsync(ct);
        var isNew = cfg is null;
        if (cfg is null)
        {
            cfg = new EmailConfig { CreatedAt = DateTimeOffset.UtcNow, CreatedBy = actorId };
            _db.EmailConfigs.Add(cfg);
        }
        else
        {
            cfg.UpdatedAt = DateTimeOffset.UtcNow;
            cfg.UpdatedBy = actorId;
        }

        cfg.SmtpHost = request.SmtpHost?.Trim();
        cfg.SmtpPort = request.SmtpPort <= 0 ? 587 : request.SmtpPort;
        cfg.SmtpUser = request.SmtpUser?.Trim();
        cfg.UseSsl = request.UseSsl;
        cfg.FromEmail = request.FromEmail?.Trim();
        cfg.FromName = request.FromName?.Trim();
        cfg.IsEnabled = request.IsEnabled;

        // La clave solo se re-cifra si llega un valor nuevo; vacia conserva la actual.
        if (!string.IsNullOrWhiteSpace(request.SmtpPassword))
        {
            cfg.SmtpPasswordEncrypted = _secretProtector.Protect(request.SmtpPassword.Trim());
        }

        // Auditoria SIN la clave.
        _db.SuperAdminLogs.Add(new SuperAdminLog
        {
            ActorId = actorId,
            ActorEmail = actorEmail,
            Accion = isNew ? "EMAIL_CONFIG_CREATE" : "EMAIL_CONFIG_UPDATE",
            EntidadAfectada = $"EmailConfig:{cfg.Id}",
            Ip = ip,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return Map(cfg);
    }

    private static EmailConfigDto Map(EmailConfig c) => new(
        c.SmtpHost, c.SmtpPort, c.SmtpUser,
        !string.IsNullOrEmpty(c.SmtpPasswordEncrypted),
        c.UseSsl, c.FromEmail, c.FromName, c.IsEnabled, c.LastValidatedAt);
}
