using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Conecta las lineas WhatsApp de la copropiedad con el servidor Evolution MAESTRO de la plataforma
/// (configurado en SuperAdmin, EvolutionMasterConfig). A diferencia de CUBOT, PROPIA usa solo el
/// servidor maestro (no hay servidor propio por tenant). Crea instancias, entrega QR, refresca estado
/// y desconecta. La API key se descifra con ISecretProtector y no se loggea.
/// </summary>
public sealed class WhatsAppConnectorService : IWhatsAppConnectorService
{
    private readonly PropiaDbContext _db;
    private readonly ISecretProtector _secret;
    private readonly IEvolutionApiClient _client;
    private readonly IWhatsAppCloudClient _cloud;

    public WhatsAppConnectorService(PropiaDbContext db, ISecretProtector secret, IEvolutionApiClient client, IWhatsAppCloudClient cloud)
    {
        _db = db;
        _secret = secret;
        _client = client;
        _cloud = cloud;
    }

    public async Task<bool> MasterReadyAsync(CancellationToken ct = default)
        => await ResolveServerAsync(ct) is not null;

    public async Task<LineConnectResult> ConnectLineAsync(Guid lineId, CancellationToken ct = default)
    {
        var line = await _db.WhatsAppLines.FirstOrDefaultAsync(l => l.Id == lineId, ct);
        if (line is null) { return new LineConnectResult(false, null, "La linea no existe."); }

        // Cloud (Meta): no hay QR. "Conectar" = validar el token contra Graph y marcar conectada.
        if (line.Provider == WhatsAppProvider.Cloud)
        {
            var creds = CloudCreds(line);
            if (creds is null) { return new LineConnectResult(false, null, "Faltan credenciales Cloud (Phone Number ID / Access Token)."); }
            var chk = await _cloud.CheckAsync(creds, ct);
            var now = DateTimeOffset.UtcNow;
            line.Status = chk.Ok ? WhatsAppLineStatus.Connected : WhatsAppLineStatus.Failed;
            line.LastStatusAt = now;
            if (chk.Ok)
            {
                line.LastConnectedAt = now;
                if (string.IsNullOrWhiteSpace(line.PhoneNumber) && !string.IsNullOrWhiteSpace(chk.PhoneNumber)) line.PhoneNumber = chk.PhoneNumber;
            }
            await _db.SaveChangesAsync(ct);
            return new LineConnectResult(chk.Ok, null, chk.Ok ? null : (chk.Error ?? "No se pudo validar el token de Meta."));
        }

        var server = await ResolveServerAsync(ct);
        if (server is null) { return new LineConnectResult(false, null, "No hay servidor Evolution maestro configurado (consola SuperAdmin)."); }

        var (baseUrl, apiKey) = server.Value;
        var result = await _client.CreateInstanceAsync(baseUrl, apiKey, EvoInstance(line), ct);
        if (!result.Ok)
        {
            line.Status = WhatsAppLineStatus.Failed;
            line.LastStatusAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            return new LineConnectResult(false, null, result.Error);
        }

        line.Status = WhatsAppLineStatus.Connecting;
        line.LastStatusAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Configura el webhook entrante (si el maestro tiene URL publica + token). Habilita la bandeja (Oleada 3).
        var (webhookUrl, webhookToken) = await EffectiveWebhookAsync(line.TenantId, ct);
        if (webhookUrl is not null && webhookToken is not null)
        {
            await _client.SetWebhookAsync(baseUrl, apiKey, EvoInstance(line), webhookUrl, webhookToken, ct);
        }

        return new LineConnectResult(true, result.QrBase64, null);
    }

    public async Task<WhatsAppLineDto?> RefreshAsync(Guid lineId, CancellationToken ct = default)
    {
        var line = await _db.WhatsAppLines.FirstOrDefaultAsync(l => l.Id == lineId, ct);
        if (line is null) { return null; }

        // Cloud (Meta): el estado se valida revalidando el token contra Graph.
        if (line.Provider == WhatsAppProvider.Cloud)
        {
            var creds = CloudCreds(line);
            if (creds is not null)
            {
                var chk = await _cloud.CheckAsync(creds, ct);
                var mapped = chk.Ok ? WhatsAppLineStatus.Connected : WhatsAppLineStatus.Failed;
                if (mapped != line.Status)
                {
                    var now = DateTimeOffset.UtcNow;
                    line.Status = mapped;
                    line.LastStatusAt = now;
                    if (mapped == WhatsAppLineStatus.Connected) line.LastConnectedAt = now;
                    await _db.SaveChangesAsync(ct);
                }
            }
            return Map(line);
        }

        var server = await ResolveServerAsync(ct);
        if (server is null) { return Map(line); }

        var (baseUrl, apiKey) = server.Value;
        var state = await _client.GetStateAsync(baseUrl, apiKey, EvoInstance(line), ct);
        if (state.Ok)
        {
            var mapped = state.State?.ToLowerInvariant() switch
            {
                "open" => WhatsAppLineStatus.Connected,
                "connecting" => WhatsAppLineStatus.Connecting,
                "close" => WhatsAppLineStatus.Disconnected,
                _ => line.Status
            };
            if (mapped != line.Status)
            {
                var now = DateTimeOffset.UtcNow;
                line.Status = mapped;
                line.LastStatusAt = now;
                if (mapped == WhatsAppLineStatus.Connected) { line.LastConnectedAt = now; }
                await _db.SaveChangesAsync(ct);
            }

            // Re-aplica el webhook entrante (idempotente): asi una linea YA conectada corrige su
            // webhook con solo "Refrescar", sin re-escanear el QR. Best-effort.
            var (whUrl, whToken) = await EffectiveWebhookAsync(line.TenantId, ct);
            if (whUrl is not null && whToken is not null)
            {
                try { await _client.SetWebhookAsync(baseUrl, apiKey, EvoInstance(line), whUrl, whToken, ct); }
                catch { /* best-effort: no debe romper el refresco de estado */ }
            }
        }
        return Map(line);
    }

    public async Task<bool> DisconnectAsync(Guid lineId, CancellationToken ct = default)
    {
        var line = await _db.WhatsAppLines.FirstOrDefaultAsync(l => l.Id == lineId, ct);
        if (line is null) { return false; }

        if (line.Provider == WhatsAppProvider.Cloud)
        {
            line.Status = WhatsAppLineStatus.Disconnected;
            line.LastStatusAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            return true;
        }

        var server = await ResolveServerAsync(ct);
        if (server is not null)
        {
            var (baseUrl, apiKey) = server.Value;
            try { await _client.DeleteInstanceAsync(baseUrl, apiKey, EvoInstance(line), ct); }
            catch { /* la instancia puede no existir */ }
        }

        line.Status = WhatsAppLineStatus.Disconnected;
        line.LastStatusAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteLineAsync(Guid lineId, CancellationToken ct = default)
    {
        var line = await _db.WhatsAppLines.FirstOrDefaultAsync(l => l.Id == lineId, ct);
        if (line is null) { return false; }

        // Best-effort: elimina la instancia en Evolution. Un servidor caido no debe impedir borrar la linea local.
        if (line.Provider == WhatsAppProvider.Evolution)
        {
            var server = await ResolveServerAsync(ct);
            if (server is not null)
            {
                var (baseUrl, apiKey) = server.Value;
                try { await _client.DeleteInstanceAsync(baseUrl, apiKey, EvoInstance(line), ct); }
                catch { /* la instancia puede no existir o el servidor estar caido */ }
            }
        }

        // La fila local: los AiAgentLineBinding se borran en cascada (FK ON DELETE CASCADE);
        // las Conversation quedan con WhatsAppLineId = null (FK SET NULL).
        _db.WhatsAppLines.Remove(line);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<LineSendResult> SendTestAsync(Guid lineId, string phone, string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(text))
        {
            return new LineSendResult(false, "Indica el numero y el mensaje.");
        }
        var line = await _db.WhatsAppLines.FirstOrDefaultAsync(l => l.Id == lineId, ct);
        if (line is null) { return new LineSendResult(false, "La linea no existe."); }
        if (line.Status != WhatsAppLineStatus.Connected) { return new LineSendResult(false, "La linea no esta conectada."); }

        if (line.Provider == WhatsAppProvider.Cloud)
        {
            var creds = CloudCreds(line);
            if (creds is null) { return new LineSendResult(false, "Faltan credenciales Cloud."); }
            var digitsCloud = new string(phone.Where(char.IsDigit).ToArray());
            var rc = await _cloud.SendTextAsync(creds, digitsCloud, text.Trim(), ct);
            return new LineSendResult(rc.Ok, rc.Error);
        }

        var server = await ResolveServerAsync(ct);
        if (server is null) { return new LineSendResult(false, "No hay servidor Evolution maestro configurado."); }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        var (baseUrl, apiKey) = server.Value;
        var result = await _client.SendTextAsync(baseUrl, apiKey, EvoInstance(line), digits, text.Trim(), ct);
        return new LineSendResult(result.Ok, result.Error);
    }

    public async Task<LineSendResult> SendMediaAsync(Guid lineId, string phone, string imageUrl, string? caption, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(imageUrl))
        {
            return new LineSendResult(false, "Indica el numero y la imagen.");
        }
        var line = await _db.WhatsAppLines.FirstOrDefaultAsync(l => l.Id == lineId, ct);
        if (line is null) { return new LineSendResult(false, "La linea no existe."); }
        if (line.Status != WhatsAppLineStatus.Connected) { return new LineSendResult(false, "La linea no esta conectada."); }

        if (line.Provider == WhatsAppProvider.Cloud)
        {
            var creds = CloudCreds(line);
            if (creds is null) { return new LineSendResult(false, "Faltan credenciales Cloud."); }
            var digitsCloud = new string(phone.Where(char.IsDigit).ToArray());
            var rc = await _cloud.SendMediaAsync(creds, digitsCloud, WhatsAppCloudMediaKind.Image, imageUrl, caption, null, ct);
            return new LineSendResult(rc.Ok, rc.Error);
        }

        var server = await ResolveServerAsync(ct);
        if (server is null) { return new LineSendResult(false, "No hay servidor Evolution maestro configurado."); }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        var (baseUrl, apiKey) = server.Value;
        var result = await _client.SendMediaAsync(baseUrl, apiKey, EvoInstance(line), digits, imageUrl, caption, ct);
        return new LineSendResult(result.Ok, result.Error);
    }

    // URL + token del webhook segun el maestro. La ruta receptora exige el tenant en el path
    // (/webhooks/evolution/{tenantId}); sin el guid Evolution recibe 404 y los entrantes nunca llegan.
    private async Task<(string? Url, string? Token)> EffectiveWebhookAsync(Guid tenantId, CancellationToken ct)
    {
        var master = await _db.EvolutionMasterConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        if (master is null || string.IsNullOrWhiteSpace(master.WebhookToken) || string.IsNullOrWhiteSpace(master.WebhookPublicUrl))
        {
            return (null, null);
        }
        // El token se guarda cifrado (ISecretProtector); lo desciframos para enviarlo a Evolution en
        // claro, asi el header x-webhook-token que Evolution reenvia coincide con Propia:WebhookToken
        // que valida el receptor. Fallback: si venia sin cifrar (legacy), lo usamos tal cual.
        string token;
        try { token = _secret.Unprotect(master.WebhookToken!); }
        catch { token = master.WebhookToken!; }
        return ($"{master.WebhookPublicUrl!.TrimEnd('/')}/webhooks/evolution/{tenantId:D}", token);
    }

    // Servidor maestro efectivo (URL + API key descifrada). Null si no esta configurado.
    private async Task<(string baseUrl, string apiKey)?> ResolveServerAsync(CancellationToken ct)
    {
        var master = await _db.EvolutionMasterConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        if (master is null || string.IsNullOrWhiteSpace(master.BaseUrl) || string.IsNullOrWhiteSpace(master.ApiKeyEncrypted))
        {
            return null;
        }
        return (master.BaseUrl!, _secret.Unprotect(master.ApiKeyEncrypted!));
    }

    // Nombre de instancia unico en el servidor compartido: propia_<tenant>_<linea>.
    private static string EvoInstance(WhatsAppLine line) => $"propia_{line.TenantId:N}_{line.Id:N}";

    // Credenciales Cloud (Meta) de la linea, con el token descifrado. Null si faltan.
    private WhatsAppCloudCredentials? CloudCreds(WhatsAppLine line)
    {
        if (string.IsNullOrWhiteSpace(line.CloudPhoneNumberId) || string.IsNullOrWhiteSpace(line.CloudAccessTokenEncrypted)) return null;
        try { return new WhatsAppCloudCredentials(line.CloudPhoneNumberId!, _secret.Unprotect(line.CloudAccessTokenEncrypted!)); }
        catch { return null; }
    }

    private static WhatsAppLineDto Map(WhatsAppLine l) =>
        new(l.Id, l.InstanceName, l.PhoneNumber, l.Status, l.AssignedToUsuarioTenantId, l.LastConnectedAt, l.LastStatusAt, l.Provider, l.CloudPhoneNumberId);
}
