using System.Net.Http.Json;
using System.Text.Json;
using Propia.Application.InfraestructuraIa;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Cliente HTTP del servidor Evolution API (v2): comprobar conexion, crear/conectar instancia,
/// estado, eliminar, enviar texto y fijar webhook. La API key va en el header "apikey".
/// Portado de CUBOT.travels (read-only). La key llega descifrada; no se persiste ni se loggea.
/// </summary>
public sealed class EvolutionApiClient : IEvolutionApiClient
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);
    private readonly HttpClient _http;

    public EvolutionApiClient(HttpClient http) => _http = http;

    public async Task<EvolutionPingResult> CheckAsync(string baseUrl, string apiKey, CancellationToken ct = default)
    {
        try
        {
            using var resp = await SendAsync(HttpMethod.Get, baseUrl, "/instance/fetchInstances", apiKey, null, ct);
            var code = (int)resp.StatusCode;
            if (resp.IsSuccessStatusCode) { return new EvolutionPingResult(true, true, code, "OK"); }
            return new EvolutionPingResult(true, false, code, code is 401 or 403 ? "Unauthorized" : $"HTTP {code}");
        }
        catch (Exception ex)
        {
            return new EvolutionPingResult(false, false, null, ex.GetType().Name);
        }
    }

    public async Task<EvolutionInstanceResult> CreateInstanceAsync(string baseUrl, string apiKey, string instanceName, CancellationToken ct = default)
    {
        var body = new { instanceName, qrcode = true, integration = "WHATSAPP-BAILEYS" };
        try
        {
            using var resp = await SendAsync(HttpMethod.Post, baseUrl, "/instance/create", apiKey, JsonContent.Create(body), ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                if ((int)resp.StatusCode is 403 or 409 || json.Contains("already in use", StringComparison.OrdinalIgnoreCase))
                {
                    return await ConnectAsync(baseUrl, apiKey, instanceName, ct);
                }
                return new EvolutionInstanceResult(false, null, null, null, $"HTTP {(int)resp.StatusCode}: {Trim(json)}");
            }
            using var doc = JsonDocument.Parse(json);
            return new EvolutionInstanceResult(true, ExtractQr(doc.RootElement), ExtractState(doc.RootElement), null, null);
        }
        catch (Exception ex)
        {
            return new EvolutionInstanceResult(false, null, null, null, ex.Message);
        }
    }

    public async Task<EvolutionInstanceResult> ConnectAsync(string baseUrl, string apiKey, string instanceName, CancellationToken ct = default)
    {
        try
        {
            using var resp = await SendAsync(HttpMethod.Get, baseUrl, $"/instance/connect/{Uri.EscapeDataString(instanceName)}", apiKey, null, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                return new EvolutionInstanceResult(false, null, null, null, $"HTTP {(int)resp.StatusCode}: {Trim(json)}");
            }
            using var doc = JsonDocument.Parse(json);
            return new EvolutionInstanceResult(true, ExtractQr(doc.RootElement), ExtractState(doc.RootElement), null, null);
        }
        catch (Exception ex)
        {
            return new EvolutionInstanceResult(false, null, null, null, ex.Message);
        }
    }

    public async Task<EvolutionInstanceResult> GetStateAsync(string baseUrl, string apiKey, string instanceName, CancellationToken ct = default)
    {
        try
        {
            using var resp = await SendAsync(HttpMethod.Get, baseUrl, $"/instance/connectionState/{Uri.EscapeDataString(instanceName)}", apiKey, null, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) { return new EvolutionInstanceResult(false, null, null, null, $"HTTP {(int)resp.StatusCode}"); }
            using var doc = JsonDocument.Parse(json);
            return new EvolutionInstanceResult(true, null, ExtractState(doc.RootElement), null, null);
        }
        catch (Exception ex)
        {
            return new EvolutionInstanceResult(false, null, null, null, ex.Message);
        }
    }

    public async Task<bool> DeleteInstanceAsync(string baseUrl, string apiKey, string instanceName, CancellationToken ct = default)
    {
        try
        {
            try { (await SendAsync(HttpMethod.Delete, baseUrl, $"/instance/logout/{Uri.EscapeDataString(instanceName)}", apiKey, null, ct)).Dispose(); }
            catch { /* puede no estar conectada */ }
            using var resp = await SendAsync(HttpMethod.Delete, baseUrl, $"/instance/delete/{Uri.EscapeDataString(instanceName)}", apiKey, null, ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<EvolutionSendResult> SendTextAsync(string baseUrl, string apiKey, string instanceName, string phone, string text, CancellationToken ct = default)
    {
        var body = new { number = phone, text };
        try
        {
            using var resp = await SendAsync(HttpMethod.Post, baseUrl, $"/message/sendText/{Uri.EscapeDataString(instanceName)}", apiKey, JsonContent.Create(body), ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (resp.IsSuccessStatusCode) { return new EvolutionSendResult(true, null, ExtractMessageId(json)); }
            return new EvolutionSendResult(false, $"HTTP {(int)resp.StatusCode}: {Trim(json)}");
        }
        catch (Exception ex)
        {
            return new EvolutionSendResult(false, ex.Message);
        }
    }

    public async Task<EvolutionSendResult> SendMediaAsync(string baseUrl, string apiKey, string instanceName, string phone, string mediaUrl, string? caption, CancellationToken ct = default)
    {
        // Evolution v2 acepta 'media' como URL publica o base64. Enviamos la URL publica del logo.
        var body = new { number = phone, mediatype = "image", media = mediaUrl, caption = caption ?? string.Empty };
        try
        {
            using var resp = await SendAsync(HttpMethod.Post, baseUrl, $"/message/sendMedia/{Uri.EscapeDataString(instanceName)}", apiKey, JsonContent.Create(body), ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (resp.IsSuccessStatusCode) { return new EvolutionSendResult(true, null, ExtractMessageId(json)); }
            return new EvolutionSendResult(false, $"HTTP {(int)resp.StatusCode}: {Trim(json)}");
        }
        catch (Exception ex)
        {
            return new EvolutionSendResult(false, ex.Message);
        }
    }

    public async Task<EvolutionSendResult> SendMediaTypedAsync(string baseUrl, string apiKey, string instanceName, string phone, string mediatype, string mediaUrl, string? caption, string? fileName, CancellationToken ct = default)
    {
        // Evolution v2 /message/sendMedia acepta 'media' como URL publica o base64. mediatype: image/video/audio/document.
        // El audio por sendMedia va como nota de audio; el caption no aplica a audio.
        var body = new Dictionary<string, object?>
        {
            ["number"] = phone,
            ["mediatype"] = mediatype,
            ["media"] = mediaUrl
        };
        if (!string.IsNullOrWhiteSpace(caption) && mediatype != "audio") { body["caption"] = caption; }
        if (mediatype == "document" && !string.IsNullOrWhiteSpace(fileName)) { body["fileName"] = fileName; }
        return await PostSendAsync(baseUrl, apiKey, $"/message/sendMedia/{Uri.EscapeDataString(instanceName)}", body, ct);
    }

    public async Task<EvolutionSendResult> SendLocationAsync(string baseUrl, string apiKey, string instanceName, string phone, double latitude, double longitude, string? name, string? address, CancellationToken ct = default)
    {
        var body = new { number = phone, name = name ?? string.Empty, address = address ?? string.Empty, latitude, longitude };
        return await PostSendAsync(baseUrl, apiKey, $"/message/sendLocation/{Uri.EscapeDataString(instanceName)}", body, ct);
    }

    public async Task<EvolutionSendResult> SendReactionAsync(string baseUrl, string apiKey, string instanceName, string remoteJid, string messageId, string emoji, CancellationToken ct = default)
    {
        // Reaccionamos a un mensaje ENTRANTE del contacto: fromMe=false. reaction="" quita la reaccion.
        var body = new { key = new { remoteJid, fromMe = false, id = messageId }, reaction = emoji };
        return await PostSendAsync(baseUrl, apiKey, $"/message/sendReaction/{Uri.EscapeDataString(instanceName)}", body, ct);
    }

    public async Task<EvolutionMediaResult> GetBase64FromMediaMessageAsync(string baseUrl, string apiKey, string instanceName, string messageKeyId, CancellationToken ct = default)
    {
        // Evolution v2: POST /chat/getBase64FromMediaMessage/{instance} con la clave del mensaje. La respuesta
        // trae el contenido en base64 + el mimetype. Portado de ECOREX. Solo necesita key.id.
        var body = new { message = new { key = new { id = messageKeyId } }, convertToMp4 = false };
        try
        {
            using var resp = await SendAsync(HttpMethod.Post, baseUrl, $"/chat/getBase64FromMediaMessage/{Uri.EscapeDataString(instanceName)}", apiKey, JsonContent.Create(body), ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) { return new EvolutionMediaResult(false, null, null, $"HTTP {(int)resp.StatusCode}: {Trim(json)}"); }
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var b64 = root.TryGetProperty("base64", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString() : null;
            var mime = root.TryGetProperty("mimetype", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString()
                : (root.TryGetProperty("mimeType", out var m2) && m2.ValueKind == JsonValueKind.String ? m2.GetString() : null);
            if (string.IsNullOrWhiteSpace(b64)) { return new EvolutionMediaResult(false, null, null, "La respuesta no trajo base64."); }
            return new EvolutionMediaResult(true, b64, mime, null);
        }
        catch (Exception ex)
        {
            return new EvolutionMediaResult(false, null, null, ex.Message);
        }
    }

    // Helper comun para los POST de envio (texto/media/ubicacion/reaccion): mapea el HTTP a EvolutionSendResult.
    private async Task<EvolutionSendResult> PostSendAsync(string baseUrl, string apiKey, string path, object body, CancellationToken ct)
    {
        try
        {
            using var resp = await SendAsync(HttpMethod.Post, baseUrl, path, apiKey, JsonContent.Create(body), ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (resp.IsSuccessStatusCode) { return new EvolutionSendResult(true, null, ExtractMessageId(json)); }
            return new EvolutionSendResult(false, $"HTTP {(int)resp.StatusCode}: {Trim(json)}");
        }
        catch (Exception ex)
        {
            return new EvolutionSendResult(false, ex.Message);
        }
    }

    // key.id del mensaje que Evolution devuelve al enviar (sendText/sendMedia/...). Correlaciona el webhook de
    // estado (messages.update). Tolerante: si la respuesta no trae key.id, devuelve null (el saliente queda "sent"
    // sin poder avanzar). Ola D.
    private static string? ExtractMessageId(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) { return null; }
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.Object
                && key.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
            {
                var s = id.GetString();
                return string.IsNullOrWhiteSpace(s) ? null : s;
            }
        }
        catch { /* respuesta no-JSON o forma inesperada: sin id */ }
        return null;
    }

    public async Task<EvolutionSendResult> SetWebhookAsync(string baseUrl, string apiKey, string instanceName, string webhookUrl, string token, CancellationToken ct = default)
    {
        var body = new
        {
            webhook = new
            {
                enabled = true,
                url = webhookUrl,
                headers = new Dictionary<string, string>
                {
                    ["x-webhook-token"] = token,
                    ["Content-Type"] = "application/json"
                },
                byEvents = false,
                base64 = false,
                // MESSAGES_UPSERT = entrantes; MESSAGES_UPDATE = acks de entrega (Ola D, analitica de entregas).
                events = new[] { "MESSAGES_UPSERT", "MESSAGES_UPDATE" }
            }
        };
        try
        {
            using var resp = await SendAsync(HttpMethod.Post, baseUrl, $"/webhook/set/{Uri.EscapeDataString(instanceName)}", apiKey, JsonContent.Create(body), ct);
            if (resp.IsSuccessStatusCode) { return new EvolutionSendResult(true, null); }
            var json = await resp.Content.ReadAsStringAsync(ct);
            return new EvolutionSendResult(false, $"HTTP {(int)resp.StatusCode}: {Trim(json)}");
        }
        catch (Exception ex)
        {
            return new EvolutionSendResult(false, ex.Message);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string baseUrl, string path, string apiKey, HttpContent? content, CancellationToken ct)
    {
        var url = $"{baseUrl.TrimEnd('/')}{path}";
        using var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.TryAddWithoutValidation("apikey", apiKey);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Timeout);
        return await _http.SendAsync(request, cts.Token);
    }

    private static string? ExtractQr(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("qrcode", out var qr) && qr.ValueKind == JsonValueKind.Object)
            {
                if (qr.TryGetProperty("base64", out var b) && b.ValueKind == JsonValueKind.String) { return b.GetString(); }
                if (qr.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String) { return c.GetString(); }
            }
            if (root.TryGetProperty("base64", out var b2) && b2.ValueKind == JsonValueKind.String) { return b2.GetString(); }
        }
        return null;
    }

    private static string? ExtractState(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("instance", out var inst) && inst.ValueKind == JsonValueKind.Object
                && inst.TryGetProperty("state", out var s1) && s1.ValueKind == JsonValueKind.String)
            {
                return s1.GetString();
            }
            if (root.TryGetProperty("state", out var s2) && s2.ValueKind == JsonValueKind.String) { return s2.GetString(); }
        }
        return null;
    }

    private static string Trim(string s) => s.Length > 200 ? s[..200] : s;
}
