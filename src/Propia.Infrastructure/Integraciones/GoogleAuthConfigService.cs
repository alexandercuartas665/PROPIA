using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Integraciones;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Integraciones;

/// <summary>
/// Config del proveedor Google (singleton global). El ClientSecret se cifra con ISecretProtector;
/// solo se re-cifra si llega un valor nuevo (vacio conserva el actual). Cada cambio deja registro
/// en super_admin_logs (append-only). Patron portado de CUBOT.travels.
/// </summary>
public sealed class GoogleAuthConfigService : IGoogleAuthConfigService
{
    private readonly PropiaDbContext _db;
    private readonly ISecretProtector _secretProtector;

    public GoogleAuthConfigService(PropiaDbContext db, ISecretProtector secretProtector)
    {
        _db = db;
        _secretProtector = secretProtector;
    }

    public async Task<GoogleAuthConfigDto?> GetAsync(CancellationToken ct = default)
    {
        var cfg = await _db.GoogleAuthConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        return cfg is null ? null : new GoogleAuthConfigDto(cfg.ClientId, !string.IsNullOrEmpty(cfg.ClientSecretEncrypted), cfg.IsEnabled);
    }

    public async Task<GoogleAuthConfigDto> SaveAsync(SaveGoogleAuthConfigRequest request, Guid actorId, string actorEmail, string? ip, CancellationToken ct = default)
    {
        var cfg = await _db.GoogleAuthConfigs.FirstOrDefaultAsync(ct);
        var isNew = cfg is null;
        if (cfg is null)
        {
            cfg = new GoogleAuthConfig { CreatedAt = DateTimeOffset.UtcNow, CreatedBy = actorId };
            _db.GoogleAuthConfigs.Add(cfg);
        }
        else
        {
            cfg.UpdatedAt = DateTimeOffset.UtcNow;
            cfg.UpdatedBy = actorId;
        }

        cfg.ClientId = request.ClientId?.Trim();
        cfg.IsEnabled = request.IsEnabled;

        if (!string.IsNullOrWhiteSpace(request.ClientSecret))
        {
            cfg.ClientSecretEncrypted = _secretProtector.Protect(request.ClientSecret.Trim());
        }

        _db.SuperAdminLogs.Add(new SuperAdminLog
        {
            ActorId = actorId,
            ActorEmail = actorEmail,
            Accion = isNew ? "GOOGLE_AUTH_CONFIG_CREATE" : "GOOGLE_AUTH_CONFIG_UPDATE",
            EntidadAfectada = $"GoogleAuthConfig:{cfg.Id}",
            Ip = ip,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return new GoogleAuthConfigDto(cfg.ClientId, !string.IsNullOrEmpty(cfg.ClientSecretEncrypted), cfg.IsEnabled);
    }

    public async Task<GoogleAuthCredentials?> GetCredentialsAsync(CancellationToken ct = default)
    {
        var cfg = await _db.GoogleAuthConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        if (cfg is null || !cfg.IsEnabled) return null;
        if (string.IsNullOrWhiteSpace(cfg.ClientId) || string.IsNullOrEmpty(cfg.ClientSecretEncrypted)) return null;

        try
        {
            var secret = _secretProtector.Unprotect(cfg.ClientSecretEncrypted);
            return new GoogleAuthCredentials(cfg.ClientId, secret);
        }
        catch
        {
            return null;
        }
    }
}
