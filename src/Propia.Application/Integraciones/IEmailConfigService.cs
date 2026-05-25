namespace Propia.Application.Integraciones;

/// <summary>Vista de la config SMTP para el Super Admin (sin exponer la clave en claro).</summary>
public sealed record EmailConfigDto(
    string? SmtpHost,
    int SmtpPort,
    string? SmtpUser,
    bool HasPassword,
    bool UseSsl,
    string? FromEmail,
    string? FromName,
    bool IsEnabled,
    DateTimeOffset? LastValidatedAt);

public sealed record SaveEmailConfigRequest(
    string? SmtpHost,
    int SmtpPort,
    string? SmtpUser,
    string? SmtpPassword,
    bool UseSsl,
    string? FromEmail,
    string? FromName,
    bool IsEnabled);

public sealed record TestEmailRequest(string ToEmail);

/// <summary>
/// Gestiona la configuracion SMTP global (singleton) del Super Admin.
/// La clave SMTP se cifra con ISecretProtector y solo se re-cifra si llega un valor nuevo.
/// </summary>
public interface IEmailConfigService
{
    Task<EmailConfigDto?> GetAsync(CancellationToken ct = default);
    Task<EmailConfigDto> SaveAsync(SaveEmailConfigRequest request, Guid actorId, string actorEmail, string? ip, CancellationToken ct = default);
}
