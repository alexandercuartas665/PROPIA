using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Gestion del ciclo de vida de las lineas WhatsApp de la copropiedad activa (tenant-scoped, RLS).
/// La conexion real con Evolution la hace IWhatsAppConnectorService. Portado de CUBOT.travels.
/// </summary>
public sealed class WhatsAppLineService : IWhatsAppLineService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ISecretProtector _secret;

    public WhatsAppLineService(PropiaDbContext db, ITenantContext tenant, ISecretProtector secret)
    {
        _db = db;
        _tenant = tenant;
        _secret = secret;
    }

    public async Task<IReadOnlyList<WhatsAppLineDto>> ListAsync(CancellationToken ct = default)
    {
        return await _db.WhatsAppLines.AsNoTracking()
            .OrderBy(l => l.InstanceName)
            .Select(l => new WhatsAppLineDto(l.Id, l.InstanceName, l.PhoneNumber, l.Status, l.AssignedToUsuarioTenantId, l.LastConnectedAt, l.LastStatusAt, l.Provider, l.CloudPhoneNumberId))
            .ToListAsync(ct);
    }

    public async Task<WhatsAppLineDto?> CreateAsync(CreateWhatsAppLineRequest request, CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is not Guid tenantId) { return null; }

        if (string.IsNullOrWhiteSpace(request.InstanceName))
        {
            throw new InvalidOperationException("El nombre de la instancia es obligatorio.");
        }

        // Limite de lineas del plan de la copropiedad (null = ilimitado).
        var limite = await GetLineLimitAsync(tenantId, ct);
        if (limite is int max)
        {
            var actuales = await _db.WhatsAppLines.CountAsync(ct);
            if (actuales >= max)
            {
                throw new InvalidOperationException($"Alcanzaste el limite de lineas WhatsApp de tu plan ({max}). Actualiza tu plan para agregar mas.");
            }
        }

        var line = new WhatsAppLine
        {
            TenantId = tenantId,
            InstanceName = request.InstanceName.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            Status = WhatsAppLineStatus.Created,
            LastStatusAt = DateTimeOffset.UtcNow,
            Provider = request.Provider
        };

        if (request.Provider == WhatsAppProvider.Cloud)
        {
            if (string.IsNullOrWhiteSpace(request.CloudPhoneNumberId) || string.IsNullOrWhiteSpace(request.CloudAccessToken))
                throw new InvalidOperationException("Para una linea Cloud (Meta) indica el Phone Number ID y el Access Token.");
            line.CloudPhoneNumberId = request.CloudPhoneNumberId.Trim();
            line.CloudBusinessAccountId = string.IsNullOrWhiteSpace(request.CloudBusinessAccountId) ? null : request.CloudBusinessAccountId.Trim();
            line.CloudAccessTokenEncrypted = _secret.Protect(request.CloudAccessToken.Trim());
            line.CloudWebhookVerifyTokenEncrypted = string.IsNullOrWhiteSpace(request.CloudWebhookVerifyToken) ? null : _secret.Protect(request.CloudWebhookVerifyToken.Trim());
        }

        _db.WhatsAppLines.Add(line);
        await _db.SaveChangesAsync(ct);
        return Map(line);
    }

    public async Task<WhatsAppLineDto?> ChangeStatusAsync(Guid lineId, WhatsAppLineStatus status, CancellationToken ct = default)
    {
        var line = await _db.WhatsAppLines.FirstOrDefaultAsync(l => l.Id == lineId, ct);
        if (line is null) { return null; }

        if (line.Status != status)
        {
            var now = DateTimeOffset.UtcNow;
            line.Status = status;
            line.LastStatusAt = now;
            if (status == WhatsAppLineStatus.Connected) { line.LastConnectedAt = now; }
            await _db.SaveChangesAsync(ct);
        }
        return Map(line);
    }

    public async Task<WhatsAppLineDto?> AssignAsync(Guid lineId, Guid? usuarioTenantId, CancellationToken ct = default)
    {
        var line = await _db.WhatsAppLines.FirstOrDefaultAsync(l => l.Id == lineId, ct);
        if (line is null) { return null; }

        if (usuarioTenantId is Guid uid)
        {
            // El filtro RLS/global garantiza que solo se valida contra usuarios de la copropiedad activa.
            var belongs = await _db.UsuariosTenant.AnyAsync(u => u.Id == uid, ct);
            if (!belongs) { return null; }
        }

        line.AssignedToUsuarioTenantId = usuarioTenantId;
        await _db.SaveChangesAsync(ct);
        return Map(line);
    }

    // Limite de lineas WhatsApp del plan vigente de la copropiedad (Suscripcion es global, filtro explicito).
    private async Task<int?> GetLineLimitAsync(Guid tenantId, CancellationToken ct)
    {
        return await _db.Suscripciones.AsNoTracking()
            .Where(s => s.CopropiedadId == tenantId && s.Estado != EstadoSuscripcion.Cancelada)
            .OrderByDescending(s => s.FechaInicio)
            .Select(s => s.Plan!.LimiteLineasWhatsapp)
            .FirstOrDefaultAsync(ct);
    }

    private static WhatsAppLineDto Map(WhatsAppLine l) =>
        new(l.Id, l.InstanceName, l.PhoneNumber, l.Status, l.AssignedToUsuarioTenantId, l.LastConnectedAt, l.LastStatusAt, l.Provider, l.CloudPhoneNumberId);
}
