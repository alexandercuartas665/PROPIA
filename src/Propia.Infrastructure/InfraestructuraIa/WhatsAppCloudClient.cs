using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Propia.Application.InfraestructuraIa;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Cliente de WhatsApp Cloud API (Meta Graph v21.0). Las credenciales (PhoneNumberId, AccessToken)
/// se entregan por llamada; este cliente NO conoce la entidad WhatsAppLine ni el ISecretProtector.
/// Errores HTTP de Meta se mapean a Error string. Portado de CUBOT.travels.
/// </summary>
internal sealed class WhatsAppCloudClient : IWhatsAppCloudClient
{
    private const string GraphBase = "https://graph.facebook.com/v21.0";

    private readonly HttpClient _http;

    public WhatsAppCloudClient(HttpClient http) => _http = http;

    public async Task<WhatsAppCloudCheckResult> CheckAsync(WhatsAppCloudCredentials credentials, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{GraphBase}/{credentials.PhoneNumberId}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
        try
        {
            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                return new WhatsAppCloudCheckResult(false, null, null, ExtractError(body) ?? $"HTTP {(int)res.StatusCode}");
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var verifiedName = root.TryGetProperty("verified_name", out var v) ? v.GetString() : null;
            var phone = root.TryGetProperty("display_phone_number", out var p) ? p.GetString() : null;
            return new WhatsAppCloudCheckResult(true, phone, verifiedName, null);
        }
        catch (Exception ex) { return new WhatsAppCloudCheckResult(false, null, null, ex.Message); }
    }

    public Task<WhatsAppCloudSendResult> SendTextAsync(WhatsAppCloudCredentials credentials, string toPhone, string text, CancellationToken ct = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = toPhone,
            type = "text",
            text = new { body = text, preview_url = false }
        };
        return SendMessageAsync(credentials, payload, ct);
    }

    public Task<WhatsAppCloudSendResult> SendMediaAsync(WhatsAppCloudCredentials credentials, string toPhone, WhatsAppCloudMediaKind kind, string mediaUrl, string? caption, string? fileName, CancellationToken ct = default)
    {
        var typeKey = kind switch
        {
            WhatsAppCloudMediaKind.Image => "image",
            WhatsAppCloudMediaKind.Video => "video",
            WhatsAppCloudMediaKind.Audio => "audio",
            WhatsAppCloudMediaKind.Document => "document",
            _ => "document"
        };
        var mediaBody = new Dictionary<string, object?> { ["link"] = mediaUrl };
        if (!string.IsNullOrWhiteSpace(caption) && kind != WhatsAppCloudMediaKind.Audio) mediaBody["caption"] = caption;
        if (kind == WhatsAppCloudMediaKind.Document && !string.IsNullOrWhiteSpace(fileName)) mediaBody["filename"] = fileName;
        var payload = new Dictionary<string, object?>
        {
            ["messaging_product"] = "whatsapp",
            ["recipient_type"] = "individual",
            ["to"] = toPhone,
            ["type"] = typeKey,
            [typeKey] = mediaBody
        };
        return SendMessageAsync(credentials, payload, ct);
    }

    private async Task<WhatsAppCloudSendResult> SendMessageAsync(WhatsAppCloudCredentials credentials, object payload, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{GraphBase}/{credentials.PhoneNumberId}/messages");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
        req.Content = JsonContent.Create(payload);
        try
        {
            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                return new WhatsAppCloudSendResult(false, null, ExtractError(body) ?? $"HTTP {(int)res.StatusCode}");
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var id = root.TryGetProperty("messages", out var messages) && messages.GetArrayLength() > 0
                && messages[0].TryGetProperty("id", out var idProp)
                ? idProp.GetString()
                : null;
            return new WhatsAppCloudSendResult(true, id, null);
        }
        catch (Exception ex) { return new WhatsAppCloudSendResult(false, null, ex.Message); }
    }

    private static string? ExtractError(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err) && err.TryGetProperty("message", out var msg))
                return msg.GetString();
        }
        catch { /* swallow */ }
        return null;
    }
}
