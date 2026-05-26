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

    public WhatsAppConnectorService(PropiaDbContext db, ISecretProtector secret, IEvolutionApiClient client)
    {
        _db = db;
        _secret = secret;
        _client = client;
    }

    public async Task<bool> MasterReadyAsync(CancellationToken ct = default)
        => await ResolveServerAsync(ct) is not null;

    public async Task<LineConnectResult> ConnectLineAsync(Guid lineId, CancellationToken ct = default)
    {
        var line = await _db.WhatsAppLines.FirstOrDefaultAsync(l => l.Id == lineId, ct);
        if (line is null) { return new LineConnectResult(false, null, "La linea no existe."); }

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
        var (webhookUrl, webhookToken) = await EffectiveWebhookAsync(ct);
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
        }
        return Map(line);
    }

    public async Task<bool> DisconnectAsync(Guid lineId, CancellationToken ct = default)
    {
        var line = await _db.WhatsAppLines.FirstOrDefaultAsync(l => l.Id == lineId, ct);
        if (line is null) { return false; }

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

    public async Task<LineSendResult> SendTestAsync(Guid lineId, string phone, string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(text))
        {
            return new LineSendResult(false, "Indica el numero y el mensaje.");
        }
        var line = await _db.WhatsAppLines.FirstOrDefaultAsync(l => l.Id == lineId, ct);
        if (line is null) { return new LineSendResult(false, "La linea no existe."); }
        if (line.Status != WhatsAppLineStatus.Connected) { return new LineSendResult(false, "La linea no esta conectada."); }

        var server = await ResolveServerAsync(ct);
        if (server is null) { return new LineSendResult(false, "No hay servidor Evolution maestro configurado."); }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        var (baseUrl, apiKey) = server.Value;
        var result = await _client.SendTextAsync(baseUrl, apiKey, EvoInstance(line), digits, text.Trim(), ct);
        return new LineSendResult(result.Ok, result.Error);
    }

    // URL + token del webhook segun el maestro (modo Production usa la URL publica fija).
    private async Task<(string? Url, string? Token)> EffectiveWebhookAsync(CancellationToken ct)
    {
        var master = await _db.EvolutionMasterConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        if (master is null || string.IsNullOrWhiteSpace(master.WebhookToken) || string.IsNullOrWhiteSpace(master.WebhookPublicUrl))
        {
            return (null, null);
        }
        return ($"{master.WebhookPublicUrl!.TrimEnd('/')}/webhooks/evolution", master.WebhookToken);
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

    private static WhatsAppLineDto Map(WhatsAppLine l) =>
        new(l.Id, l.InstanceName, l.PhoneNumber, l.Status, l.AssignedToUsuarioTenantId, l.LastConnectedAt, l.LastStatusAt);
}
