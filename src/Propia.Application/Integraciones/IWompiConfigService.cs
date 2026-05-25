using Propia.Domain.Enums;

namespace Propia.Application.Integraciones;

/// <summary>Vista de la configuracion Wompi para la consola. Las llaves sensibles van enmascaradas.</summary>
public sealed record WompiConfigDto(
    WompiEnvironment Environment,
    string? PublicKey,
    string? PrivateKeyMasked,
    string? EventsSecretMasked,
    string? IntegritySecretMasked,
    string? WebhookEndpoint,
    string Currency,
    int MaxRetries,
    WompiIntegrationStatus Status,
    DateTimeOffset? LastValidatedAt,
    bool HasPrivateKey,
    bool HasEventsSecret,
    bool HasIntegritySecret);

/// <summary>
/// Alta/edicion de la config Wompi. Las llaves secretas son opcionales: si vienen vacias se
/// conserva el valor cifrado actual (no se re-cifra ni se borra).
/// </summary>
public sealed record SaveWompiConfigRequest(
    WompiEnvironment Environment,
    string? PublicKey,
    string? PrivateKey,
    string? EventsSecret,
    string? IntegritySecret,
    string? WebhookEndpoint,
    string Currency,
    int MaxRetries);

public sealed record WompiValidationResult(bool Ok, string Message);

/// <summary>
/// Configuracion maestra de Wompi del dueno de la plataforma (Super Admin). Singleton global.
/// Las llaves privada/eventos/integridad se cifran con ISecretProtector y nunca se devuelven en claro.
/// </summary>
public interface IWompiConfigService
{
    Task<WompiConfigDto?> GetAsync(CancellationToken ct = default);
    Task<WompiConfigDto> SaveAsync(SaveWompiConfigRequest request, Guid actorId, string actorEmail, string? ip, CancellationToken ct = default);

    /// <summary>
    /// Validacion estructural (sin cobro real): verifica que las llaves, el ambiente y la moneda
    /// sean coherentes. Punto de extension para un ping real a la API de Wompi mas adelante.
    /// </summary>
    Task<WompiValidationResult?> ValidateAsync(Guid actorId, string actorEmail, string? ip, CancellationToken ct = default);
}
