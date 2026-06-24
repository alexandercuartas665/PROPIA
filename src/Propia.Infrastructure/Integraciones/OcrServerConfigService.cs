using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Integraciones;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Integraciones;

/// <summary>
/// Config maestra del proveedor de OCR (Super Admin, Capa 0). La API key se cifra con
/// ISecretProtector y nunca se devuelve en claro ni se loggea. Mismo patron que AiServerConfigService.
/// </summary>
public sealed class OcrServerConfigService : IOcrServerConfigService
{
    private const OcrProvider DefaultProvider = OcrProvider.AzureDocumentIntelligence;

    private readonly PropiaDbContext _db;
    private readonly ISecretProtector _secretProtector;

    public OcrServerConfigService(PropiaDbContext db, ISecretProtector secretProtector)
    {
        _db = db;
        _secretProtector = secretProtector;
    }

    public async Task<OcrProviderDto> GetAsync(CancellationToken ct = default)
    {
        var config = await _db.OcrProviderConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Provider == DefaultProvider, ct);
        return Map(DefaultProvider, config);
    }

    public async Task<OcrProviderDto> SaveAsync(SaveOcrProviderRequest request, Guid actorId, string actorEmail, string? ip, CancellationToken ct = default)
    {
        var config = await _db.OcrProviderConfigs.FirstOrDefaultAsync(c => c.Provider == request.Provider, ct);
        var isNew = config is null;
        if (config is null)
        {
            config = new OcrProviderConfig { Provider = request.Provider, CreatedAt = DateTimeOffset.UtcNow, CreatedBy = actorId };
            _db.OcrProviderConfigs.Add(config);
        }
        else
        {
            config.UpdatedAt = DateTimeOffset.UtcNow;
            config.UpdatedBy = actorId;
        }

        // La API key solo se re-cifra si llega un valor nuevo; si viene vacia se conserva la actual.
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
            config.ApiKeyEncrypted = _secretProtector.Protect(request.ApiKey.Trim());

        config.Endpoint = string.IsNullOrWhiteSpace(request.Endpoint) ? null : request.Endpoint.Trim().TrimEnd('/');
        config.ModelId = string.IsNullOrWhiteSpace(request.ModelId) ? null : request.ModelId.Trim();
        config.IsEnabled = request.IsEnabled && config.ApiKeyEncrypted is not null && config.Endpoint is not null;

        _db.SuperAdminLogs.Add(new SuperAdminLog
        {
            ActorId = actorId,
            ActorEmail = actorEmail,
            Accion = isNew ? "OCR_PROVIDER_CREATE" : "OCR_PROVIDER_UPDATE",
            EntidadAfectada = $"OcrProviderConfig:{config.Provider}",
            Ip = ip,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return Map(config.Provider, config);
    }

    private OcrProviderDto Map(OcrProvider provider, OcrProviderConfig? c)
    {
        var meta = OcrProviderCatalog.For(provider);
        return new OcrProviderDto(
            provider,
            meta.DisplayName,
            c?.Endpoint,
            c?.ModelId,
            c?.ApiKeyEncrypted is null ? null : Mask(c.ApiKeyEncrypted),
            c?.ApiKeyEncrypted is not null,
            c?.IsEnabled ?? false,
            meta.DefaultModel,
            meta.Models);
    }

    private string Mask(string encrypted)
    {
        string value;
        try { value = _secretProtector.Unprotect(encrypted); }
        catch { return "(re-ingresar)"; }
        return value.Length <= 4 ? "****" : $"{new string('*', Math.Min(value.Length - 4, 8))}{value[^4..]}";
    }
}
