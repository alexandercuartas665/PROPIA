using Propia.Domain.Enums;

namespace Propia.Application.Integraciones;

public sealed record OcrProviderDto(
    OcrProvider Provider,
    string DisplayName,
    string? Endpoint,
    string? ModelId,
    string? ApiKeyMasked,
    bool HasApiKey,
    bool IsEnabled,
    string DefaultModel,
    IReadOnlyList<string> SuggestedModels);

public sealed record SaveOcrProviderRequest(
    OcrProvider Provider,
    string? Endpoint,
    string? ApiKey,
    string? ModelId,
    bool IsEnabled);

/// <summary>
/// Config maestra del proveedor de OCR (Super Admin, Capa 0). La API key se cifra con
/// ISecretProtector y nunca se devuelve en claro ni se loggea.
/// </summary>
public interface IOcrServerConfigService
{
    Task<OcrProviderDto> GetAsync(CancellationToken ct = default);
    Task<OcrProviderDto> SaveAsync(SaveOcrProviderRequest request, Guid actorId, string actorEmail, string? ip, CancellationToken ct = default);
}

/// <summary>Metadata estatica de cada proveedor OCR.</summary>
public static class OcrProviderCatalog
{
    public sealed record Meta(string DisplayName, string DefaultModel, IReadOnlyList<string> Models);

    public static Meta For(OcrProvider p) => p switch
    {
        OcrProvider.AzureDocumentIntelligence => new("Azure AI Document Intelligence", "prebuilt-invoice",
            new[] { "prebuilt-invoice", "prebuilt-receipt", "prebuilt-document", "prebuilt-read" }),
        _ => new(p.ToString(), "", Array.Empty<string>())
    };
}
