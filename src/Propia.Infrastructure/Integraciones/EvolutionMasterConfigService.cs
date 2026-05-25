using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Integraciones;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Integraciones;

/// <summary>
/// Servidor Evolution API maestro (WhatsApp) - singleton global. La API key se cifra con
/// ISecretProtector. Validacion estructural (URL + key + formato); el ping real al servidor se
/// añadira cuando se active el canal WhatsApp de T.2. Portado de CUBOT.travels.
/// </summary>
public sealed class EvolutionMasterConfigService : IEvolutionMasterConfigService
{
    private readonly PropiaDbContext _db;
    private readonly ISecretProtector _secretProtector;

    public EvolutionMasterConfigService(PropiaDbContext db, ISecretProtector secretProtector)
    {
        _db = db;
        _secretProtector = secretProtector;
    }

    public async Task<EvolutionMasterDto?> GetAsync(CancellationToken ct = default)
    {
        var c = await _db.EvolutionMasterConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        return c is null ? null : Map(c);
    }

    public async Task<EvolutionMasterDto> SaveAsync(SaveEvolutionMasterRequest request, Guid actorId, string actorEmail, string? ip, CancellationToken ct = default)
    {
        var c = await _db.EvolutionMasterConfigs.FirstOrDefaultAsync(ct);
        var isNew = c is null;
        if (c is null)
        {
            c = new EvolutionMasterConfig { CreatedAt = DateTimeOffset.UtcNow, CreatedBy = actorId };
            _db.EvolutionMasterConfigs.Add(c);
        }
        else
        {
            c.UpdatedAt = DateTimeOffset.UtcNow;
            c.UpdatedBy = actorId;
        }

        c.BaseUrl = NormalizeBaseUrl(request.BaseUrl);
        c.WebhookMode = string.IsNullOrWhiteSpace(request.WebhookMode) ? "Production" : request.WebhookMode.Trim();
        c.WebhookPublicUrl = string.IsNullOrWhiteSpace(request.WebhookPublicUrl) ? null : request.WebhookPublicUrl.Trim();

        if (!string.IsNullOrWhiteSpace(request.ApiKey))
            c.ApiKeyEncrypted = _secretProtector.Protect(request.ApiKey.Trim());
        if (!string.IsNullOrWhiteSpace(request.WebhookToken))
            c.WebhookToken = _secretProtector.Protect(request.WebhookToken.Trim());

        c.Status = HasCredentials(c) ? EvolutionIntegrationStatus.Configured : EvolutionIntegrationStatus.NotConfigured;
        c.LastValidatedAt = null;

        _db.SuperAdminLogs.Add(new SuperAdminLog
        {
            ActorId = actorId,
            ActorEmail = actorEmail,
            Accion = isNew ? "EVOLUTION_CONFIG_CREATE" : "EVOLUTION_CONFIG_UPDATE",
            EntidadAfectada = $"EvolutionMasterConfig:{c.Id}",
            Ip = ip,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return Map(c);
    }

    public async Task<EvolutionValidationResult?> ValidateAsync(Guid actorId, string actorEmail, string? ip, CancellationToken ct = default)
    {
        var c = await _db.EvolutionMasterConfigs.FirstOrDefaultAsync(ct);
        if (c is null) return null;

        var (ok, message) = ValidateStructure(c);
        c.Status = ok ? EvolutionIntegrationStatus.Validated : EvolutionIntegrationStatus.Error;
        c.LastValidatedAt = DateTimeOffset.UtcNow;

        _db.SuperAdminLogs.Add(new SuperAdminLog
        {
            ActorId = actorId,
            ActorEmail = actorEmail,
            Accion = "EVOLUTION_CONFIG_VALIDATE",
            EntidadAfectada = $"EvolutionMasterConfig:{c.Id}",
            Justificacion = ok ? "Validated" : message,
            Ip = ip,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return new EvolutionValidationResult(ok, message);
    }

    private (bool ok, string message) ValidateStructure(EvolutionMasterConfig c)
    {
        if (string.IsNullOrWhiteSpace(c.BaseUrl) || string.IsNullOrWhiteSpace(c.ApiKeyEncrypted))
            return (false, "Falta la URL del servidor y/o la API key.");
        if (!c.BaseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !c.BaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return (false, "La URL del servidor debe empezar con http:// o https://");
        try { _secretProtector.Unprotect(c.ApiKeyEncrypted); }
        catch { return (false, "La API key esta cifrada con una version anterior. Vuelve a guardarla."); }
        return (true, $"Configuracion coherente ({c.BaseUrl}). (Validacion estructural; el ping real al servidor llega con el canal WhatsApp T.2.)");
    }

    private static bool HasCredentials(EvolutionMasterConfig c) =>
        !string.IsNullOrWhiteSpace(c.BaseUrl) && !string.IsNullOrWhiteSpace(c.ApiKeyEncrypted);

    // Acepta que peguen la URL del manager (https://host/manager) y guarda la base (https://host).
    private static string? NormalizeBaseUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var url = raw.Trim().TrimEnd('/');
        if (url.EndsWith("/manager", StringComparison.OrdinalIgnoreCase))
            url = url[..^"/manager".Length];
        return url.TrimEnd('/');
    }

    private EvolutionMasterDto Map(EvolutionMasterConfig c) =>
        new(c.BaseUrl,
            c.ApiKeyEncrypted is null ? null : Mask(c.ApiKeyEncrypted),
            c.ApiKeyEncrypted is not null,
            c.Status,
            c.LastValidatedAt,
            c.WebhookMode,
            c.WebhookPublicUrl,
            c.WebhookToken is not null);

    private string Mask(string encrypted)
    {
        string value;
        try { value = _secretProtector.Unprotect(encrypted); }
        catch { return "(re-ingresar)"; }
        return value.Length <= 4 ? "****" : $"{new string('*', Math.Min(value.Length - 4, 8))}{value[^4..]}";
    }
}
