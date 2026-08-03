using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Webhook de WhatsApp Cloud API (Meta). Lleva el tenant en la URL, asi que fija el tenant en
/// contexto (SetTenant) y resuelve la linea con la RLS normal por tenant (sin cross-tenant ni
/// funciones SECURITY DEFINER). Portado de CUBOT.travels.
/// </summary>
public sealed class MetaWebhookService : IMetaWebhookService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ISecretProtector _secret;
    private readonly IChatIngestService _ingest;

    public MetaWebhookService(PropiaDbContext db, ITenantContext tenant, ISecretProtector secret, IChatIngestService ingest)
    {
        _db = db;
        _tenant = tenant;
        _secret = secret;
        _ingest = ingest;
    }

    public async Task<bool> VerifyAsync(Guid tenantId, string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) { return false; }
        _tenant.SetTenant(tenantId);

        var encrypted = await _db.WhatsAppLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId
                     && l.Provider == WhatsAppProvider.Cloud
                     && l.CloudWebhookVerifyTokenEncrypted != null)
            .Select(l => l.CloudWebhookVerifyTokenEncrypted!)
            .ToListAsync(ct);

        foreach (var enc in encrypted)
        {
            try { if (string.Equals(_secret.Unprotect(enc), token, StringComparison.Ordinal)) { return true; } }
            catch { /* token cifrado con version anterior: se ignora esa linea */ }
        }
        return false;
    }

    public async Task<ChatIngestResult> IngestAsync(Guid tenantId, string phoneNumberId, IngestMessageRequest payload, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumberId)) { return ChatIngestResult.InvalidPayload; }
        _tenant.SetTenant(tenantId);

        // Resuelve la linea Cloud que recibio el mensaje por su phone_number_id de Meta.
        var instanceName = await _db.WhatsAppLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId
                     && l.Provider == WhatsAppProvider.Cloud
                     && l.CloudPhoneNumberId == phoneNumberId)
            .Select(l => l.InstanceName)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(instanceName)) { return ChatIngestResult.LineNotFound; }

        // La ingesta resuelve el resto por InstanceName (idempotencia + lista negra + broadcast + dispatch).
        return await _ingest.IngestTrustedAsync(tenantId, payload with { InstanceName = instanceName }, ct);
    }
}
