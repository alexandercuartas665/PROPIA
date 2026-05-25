using Propia.Domain.Enums;

namespace Propia.Application.Integraciones;

/// <summary>Vista de la config del servidor Evolution. La API key va enmascarada.</summary>
public sealed record EvolutionMasterDto(
    string? BaseUrl,
    string? ApiKeyMasked,
    bool HasApiKey,
    EvolutionIntegrationStatus Status,
    DateTimeOffset? LastValidatedAt,
    string WebhookMode,
    string? WebhookPublicUrl,
    bool HasWebhookToken);

public sealed record SaveEvolutionMasterRequest(
    string? BaseUrl,
    string? ApiKey,
    string WebhookMode,
    string? WebhookPublicUrl,
    string? WebhookToken);

public sealed record EvolutionValidationResult(bool Ok, string Message);

/// <summary>
/// Servidor Evolution API maestro (WhatsApp) - singleton global. La API key se cifra con
/// ISecretProtector y nunca se devuelve en claro.
/// </summary>
public interface IEvolutionMasterConfigService
{
    Task<EvolutionMasterDto?> GetAsync(CancellationToken ct = default);
    Task<EvolutionMasterDto> SaveAsync(SaveEvolutionMasterRequest request, Guid actorId, string actorEmail, string? ip, CancellationToken ct = default);
    Task<EvolutionValidationResult?> ValidateAsync(Guid actorId, string actorEmail, string? ip, CancellationToken ct = default);
}
