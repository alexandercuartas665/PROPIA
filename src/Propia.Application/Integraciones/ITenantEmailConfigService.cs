namespace Propia.Application.Integraciones;

/// <summary>
/// Cuenta de correo saliente (SMTP) de la copropiedad. Empezamos por Gmail con contrasena de aplicacion.
/// La clave nunca sale del backend: el DTO solo dice si hay una guardada (<see cref="TenantEmailConfigDto.TienePassword"/>).
/// </summary>
public interface ITenantEmailConfigService
{
    Task<TenantEmailConfigDto> GetAsync(CancellationToken ct);
    Task SaveAsync(GuardarTenantEmailConfigRequest req, CancellationToken ct);
    /// <summary>Envia un correo de prueba con la config guardada. Devuelve (ok, error legible).</summary>
    Task<(bool Ok, string? Error)> ProbarAsync(string toEmail, CancellationToken ct);
}

/// <summary>Vista de la config (sin exponer la clave).</summary>
public record TenantEmailConfigDto(
    string? SmtpHost, int SmtpPort, string? SmtpUser, bool UseSsl,
    string? FromEmail, string? FromName, bool IsEnabled, bool TienePassword,
    DateTimeOffset? LastValidatedAt);

/// <summary>Guardar/actualizar. `Password` solo se aplica si viene no vacio (deja la existente si es null).</summary>
public record GuardarTenantEmailConfigRequest(
    string? SmtpHost, int SmtpPort, string? SmtpUser, string? Password, bool UseSsl,
    string? FromEmail, string? FromName, bool IsEnabled);
