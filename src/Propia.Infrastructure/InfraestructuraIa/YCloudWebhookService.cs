using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Webhook de entrada de YCloud. Fija el tenant de la URL (SetTenant) y resuelve la linea con la RLS normal,
/// igual que MetaWebhookService. La ingesta (idempotencia, lista negra, broadcast, agentes) es la comun.
/// </summary>
public sealed class YCloudWebhookService : IYCloudWebhookService
{
    /// <summary>Ventana de validez del timestamp firmado (evita reenviar un evento capturado).</summary>
    public static readonly TimeSpan Tolerancia = TimeSpan.FromMinutes(10);

    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ISecretProtector _secret;
    private readonly IChatIngestService _ingest;

    public YCloudWebhookService(PropiaDbContext db, ITenantContext tenant, ISecretProtector secret, IChatIngestService ingest)
    {
        _db = db; _tenant = tenant; _secret = secret; _ingest = ingest;
    }

    public async Task<FirmaYCloud> VerificarFirmaAsync(Guid tenantId, string cuerpo, string? header, CancellationToken ct = default)
    {
        _tenant.SetTenant(tenantId);
        var cifrados = await _db.WhatsAppLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.Provider == WhatsAppProvider.YCloud && l.YCloudWebhookSecretEncrypted != null)
            .Select(l => l.YCloudWebhookSecretEncrypted!)
            .ToListAsync(ct);
        if (cifrados.Count == 0) return FirmaYCloud.SinSecreto;
        if (!LeerHeader(header, out var t, out var firma)) return FirmaYCloud.Invalida;
        if (!long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var segundos)
            || (DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(segundos)).Duration() > Tolerancia)
            return FirmaYCloud.Invalida;
        foreach (var enc in cifrados)
        {
            string secreto;
            try { secreto = _secret.Unprotect(enc); }
            catch { continue; }   // secreto cifrado con una llave anterior: se ignora esa linea
            if (FirmaCorrecta(secreto, t, cuerpo, firma)) return FirmaYCloud.Valida;
        }
        return FirmaYCloud.Invalida;
    }

    public async Task<ChatIngestResult> IngestAsync(Guid tenantId, string numeroDestino, IngestMessageRequest payload, CancellationToken ct = default)
    {
        var destino = new string((numeroDestino ?? "").Where(char.IsDigit).ToArray());
        if (destino.Length == 0) return ChatIngestResult.InvalidPayload;
        _tenant.SetTenant(tenantId);
        var instanceName = await _db.WhatsAppLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.Provider == WhatsAppProvider.YCloud && l.YCloudFromNumber == destino)
            .Select(l => l.InstanceName)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(instanceName)) return ChatIngestResult.LineNotFound;
        return await _ingest.IngestTrustedAsync(tenantId, payload with { InstanceName = instanceName }, ct);
    }

    /// <summary>"t=1654084800,s=abc..." -> (t, s). Tolera espacios y el orden de las partes.</summary>
    public static bool LeerHeader(string? header, out string t, out string s)
    {
        t = s = "";
        if (string.IsNullOrWhiteSpace(header)) return false;
        foreach (var parte in header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var i = parte.IndexOf('=');
            if (i <= 0) continue;
            var k = parte[..i].Trim(); var v = parte[(i + 1)..].Trim();
            if (k == "t") t = v; else if (k == "s") s = v;
        }
        return t.Length > 0 && s.Length > 0;
    }

    /// <summary>HMAC-SHA256(secreto, "{t}.{cuerpo}") en hex, comparado en tiempo constante.</summary>
    public static bool FirmaCorrecta(string secreto, string t, string cuerpo, string firma)
    {
        var calc = Firmar(secreto, t, cuerpo);
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(calc), Encoding.ASCII.GetBytes(firma.Trim().ToLowerInvariant()));
    }

    public static string Firmar(string secreto, string t, string cuerpo)
        => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secreto), Encoding.UTF8.GetBytes($"{t}.{cuerpo}"))).ToLowerInvariant();
}
