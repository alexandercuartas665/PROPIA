using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Integraciones;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Integraciones;

/// <summary>
/// Cuentas maestras de IA de la plataforma (Super Admin). Un registro por proveedor; la API key se
/// cifra con ISecretProtector y nunca se devuelve en claro ni se loggea. Portado de CUBOT.travels.
/// </summary>
public sealed class AiServerConfigService : IAiServerConfigService
{
    private static readonly AiProvider[] AllProviders =
        { AiProvider.Claude, AiProvider.Gemini, AiProvider.ChatGpt, AiProvider.DeepSeek };

    private readonly PropiaDbContext _db;
    private readonly ISecretProtector _secretProtector;

    public AiServerConfigService(PropiaDbContext db, ISecretProtector secretProtector)
    {
        _db = db;
        _secretProtector = secretProtector;
    }

    public async Task<IReadOnlyList<AiProviderDto>> ListAsync(CancellationToken ct = default)
    {
        var stored = await _db.AiProviderConfigs.AsNoTracking().ToListAsync(ct);
        return AllProviders.Select(p => Map(p, stored.FirstOrDefault(c => c.Provider == p))).ToList();
    }

    public async Task<AiProviderDto> SaveAsync(SaveAiProviderRequest request, Guid actorId, string actorEmail, string? ip, CancellationToken ct = default)
    {
        var config = await _db.AiProviderConfigs.FirstOrDefaultAsync(c => c.Provider == request.Provider, ct);
        var isNew = config is null;
        if (config is null)
        {
            config = new AiProviderConfig { Provider = request.Provider, CreatedAt = DateTimeOffset.UtcNow, CreatedBy = actorId };
            _db.AiProviderConfigs.Add(config);
        }
        else
        {
            config.UpdatedAt = DateTimeOffset.UtcNow;
            config.UpdatedBy = actorId;
        }

        // La API key solo se re-cifra si llega un valor nuevo; si viene vacia se conserva la actual.
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            config.ApiKeyEncrypted = _secretProtector.Protect(request.ApiKey.Trim());
        }
        config.Model = string.IsNullOrWhiteSpace(request.Model) ? null : request.Model.Trim();
        config.BaseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? null : request.BaseUrl.Trim();
        config.IsEnabled = request.IsEnabled && config.ApiKeyEncrypted is not null;

        _db.SuperAdminLogs.Add(new SuperAdminLog
        {
            ActorId = actorId,
            ActorEmail = actorEmail,
            Accion = isNew ? "AI_PROVIDER_CREATE" : "AI_PROVIDER_UPDATE",
            EntidadAfectada = $"AiProviderConfig:{config.Provider}",
            Ip = ip,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return Map(config.Provider, config);
    }

    public async Task<IReadOnlyList<AiProviderOptionDto>> ListEnabledAsync(CancellationToken ct = default)
    {
        var enabled = await _db.AiProviderConfigs.AsNoTracking()
            .Where(c => c.IsEnabled && c.ApiKeyEncrypted != null)
            .Select(c => new { c.Provider, c.Model })
            .ToListAsync(ct);

        return enabled.Select(c =>
        {
            var meta = AiProviderCatalog.For(c.Provider);
            var model = string.IsNullOrWhiteSpace(c.Model) ? meta.DefaultModel : c.Model!;
            return new AiProviderOptionDto(c.Provider, meta.DisplayName, model);
        }).ToList();
    }

    private AiProviderDto Map(AiProvider provider, AiProviderConfig? c)
    {
        var meta = AiProviderCatalog.For(provider);
        return new AiProviderDto(
            provider,
            meta.DisplayName,
            c?.Model,
            c?.BaseUrl,
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
