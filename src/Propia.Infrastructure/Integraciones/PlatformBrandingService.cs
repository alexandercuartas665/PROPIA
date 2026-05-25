using Microsoft.EntityFrameworkCore;
using Propia.Application.Integraciones;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Integraciones;

/// <summary>
/// Marca de la plataforma: tabla de una sola fila. El Super Admin la edita; el login la lee.
/// Si no existe fila, se devuelven valores por defecto sin tocar la BD. Portado de CUBOT.travels.
/// </summary>
public sealed class PlatformBrandingService : IPlatformBrandingService
{
    private readonly PropiaDbContext _db;

    public PlatformBrandingService(PropiaDbContext db) => _db = db;

    public async Task<PlatformBrandingDto> GetAsync(CancellationToken ct = default)
    {
        var row = await _db.PlatformBrandings.AsNoTracking().FirstOrDefaultAsync(ct);
        return row is null
            ? PlatformBrandingDto.Default
            : new PlatformBrandingDto(row.PlatformName, row.Tagline, row.LoginLogoUrl, row.LoginHeadline, row.LoginSubtext);
    }

    public async Task SaveAsync(SaveBrandingRequest request, Guid actorId, string actorEmail, string? ip, CancellationToken ct = default)
    {
        var name = string.IsNullOrWhiteSpace(request.PlatformName) ? "PROPIA" : request.PlatformName.Trim();

        var row = await _db.PlatformBrandings.FirstOrDefaultAsync(ct);
        var isNew = row is null;
        if (row is null)
        {
            row = new PlatformBranding { CreatedAt = DateTimeOffset.UtcNow, CreatedBy = actorId };
            _db.PlatformBrandings.Add(row);
        }
        else
        {
            row.UpdatedAt = DateTimeOffset.UtcNow;
            row.UpdatedBy = actorId;
        }

        row.PlatformName = name;
        row.Tagline = request.Tagline?.Trim();
        row.LoginLogoUrl = string.IsNullOrWhiteSpace(request.LoginLogoUrl) ? null : request.LoginLogoUrl.Trim();
        row.LoginHeadline = request.LoginHeadline?.Trim();
        row.LoginSubtext = request.LoginSubtext?.Trim();

        _db.SuperAdminLogs.Add(new SuperAdminLog
        {
            ActorId = actorId,
            ActorEmail = actorEmail,
            Accion = isNew ? "BRANDING_CREATE" : "BRANDING_UPDATE",
            EntidadAfectada = $"PlatformBranding:{row.Id}",
            Ip = ip,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }
}
