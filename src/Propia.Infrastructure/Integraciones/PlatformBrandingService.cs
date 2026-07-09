using Microsoft.EntityFrameworkCore;
using Propia.Application.Integraciones;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;

namespace Propia.Infrastructure.Integraciones;

/// <summary>
/// Marca de la plataforma: tabla de una sola fila. El Super Admin la edita; el login la lee.
/// Si no existe fila, se devuelven valores por defecto sin tocar la BD. Portado de CUBOT.travels.
/// </summary>
public sealed class PlatformBrandingService : IPlatformBrandingService
{
    private readonly PropiaDbContext _db;
    private readonly IBlobStorage _blob;

    public PlatformBrandingService(PropiaDbContext db, IBlobStorage blob)
    {
        _db = db;
        _blob = blob;
    }

    public async Task<PlatformBrandingDto> GetAsync(CancellationToken ct = default)
    {
        var row = await _db.PlatformBrandings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (row is null) return PlatformBrandingDto.Default;
        // Si un campo de logo quedo vacio, caemos al asset por defecto (asset estatico del app).
        // Si tiene valor (logo subido), se pasa por ResolveUrl -> ruta del mismo origen.
        return new PlatformBrandingDto(
            row.PlatformName,
            row.Tagline,
            string.IsNullOrWhiteSpace(row.LoginLogoUrl) ? PlatformBrandingDto.DefaultLoginLogoUrl : (_blob.ResolveUrl(row.LoginLogoUrl) ?? PlatformBrandingDto.DefaultLoginLogoUrl),
            string.IsNullOrWhiteSpace(row.IconUrl) ? PlatformBrandingDto.DefaultIconUrl : (_blob.ResolveUrl(row.IconUrl) ?? PlatformBrandingDto.DefaultIconUrl),
            row.LoginHeadline,
            row.LoginSubtext);
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
        row.IconUrl = string.IsNullOrWhiteSpace(request.IconUrl) ? null : request.IconUrl.Trim();
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
