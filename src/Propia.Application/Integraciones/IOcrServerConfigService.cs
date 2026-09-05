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
    /// <summary>Devuelve la config de un proveedor. Si provider es null, devuelve el habilitado (o el default).</summary>
    Task<OcrProviderDto> GetAsync(OcrProvider? provider = null, CancellationToken ct = default);
    Task<OcrProviderDto> SaveAsync(SaveOcrProviderRequest request, Guid actorId, string actorEmail, string? ip, CancellationToken ct = default);
}

/// <summary>Metadata estatica de cada proveedor OCR.</summary>
public static class OcrProviderCatalog
{
    public sealed record Meta(string DisplayName, string DefaultModel, IReadOnlyList<string> Models);

    /// <summary>Todos los proveedores soportados (para el selector de Super Admin).</summary>
    public static readonly IReadOnlyList<OcrProvider> Todos = new[]
    {
        OcrProvider.AzureDocumentIntelligence,
        OcrProvider.AzureComputerVision,
        OcrProvider.GeminiDocument
    };

    public static Meta For(OcrProvider p) => p switch
    {
        OcrProvider.AzureDocumentIntelligence => new("Azure AI Document Intelligence", "prebuilt-invoice",
            new[] { "prebuilt-invoice", "prebuilt-receipt", "prebuilt-document", "prebuilt-read" }),
        OcrProvider.AzureComputerVision => new("Azure AI Vision (Computer Vision)", "read",
            new[] { "read" }),
        OcrProvider.GeminiDocument => new("IA - Google Gemini (PDF nativo)", "gemini-2.0-flash",
            new[] { "gemini-2.0-flash", "gemini-2.5-flash", "gemini-2.5-pro", "gemini-1.5-pro" }),
        _ => new(p.ToString(), "", Array.Empty<string>())
    };

    /// <summary>True si el proveedor es un motor de IA (no OCR clasico): manda el documento al LLM.</summary>
    public static bool EsIa(OcrProvider p) => p == OcrProvider.GeminiDocument;
}
