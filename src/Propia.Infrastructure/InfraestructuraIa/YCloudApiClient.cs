using System.Net.Http.Json;
using System.Text.Json;
using Propia.Application.InfraestructuraIa;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Cliente del BSP oficial YCloud (REST api.ycloud.com/v2). Las credenciales (ApiKey + numero
/// remitente "from") se entregan por llamada; este cliente NO conoce la entidad WhatsAppLine ni
/// el ISecretProtector. La API key va en el header "X-API-Key". Errores HTTP se mapean a Error
/// string (diseño sin throw). Espeja a WhatsAppCloudClient (Meta). Ref: ECOREX YCloudApiClient.
/// </summary>
internal sealed class YCloudApiClient : IYCloudApiClient
{
    private const string ApiBase = "https://api.ycloud.com/v2";

    private readonly HttpClient _http;

    public YCloudApiClient(HttpClient http) => _http = http;

    public async Task<YCloudCheckResult> CheckAsync(YCloudCredentials credentials, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/whatsapp/phoneNumbers?limit=10");
        req.Headers.Add("X-API-Key", credentials.ApiKey);
        try
        {
            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                return new YCloudCheckResult(false, null, null, ExtractError(body) ?? $"HTTP {(int)res.StatusCode}");

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            // La respuesta trae items[]; buscamos el numero remitente de la linea (si existe) para
            // confirmar que la API key lo cubre; si no, tomamos el primero.
            var wantDigits = Digits(credentials.FromNumber);
            string? phone = null, verifiedName = null;
            if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array && items.GetArrayLength() > 0)
            {
                JsonElement chosen = items[0];
                foreach (var it in items.EnumerateArray())
                {
                    var p = StrOf(it, "phoneNumber") ?? StrOf(it, "displayPhoneNumber");
                    if (p is not null && Digits(p) == wantDigits) { chosen = it; break; }
                }
                phone = StrOf(chosen, "phoneNumber") ?? StrOf(chosen, "displayPhoneNumber");
                verifiedName = StrOf(chosen, "verifiedName") ?? StrOf(chosen, "name");
            }
            return new YCloudCheckResult(true, phone, verifiedName, null);
        }
        catch (Exception ex) { return new YCloudCheckResult(false, null, null, ex.Message); }
    }

    public Task<YCloudSendResult> SendTextAsync(YCloudCredentials credentials, string toPhone, string text, CancellationToken ct = default)
    {
        var payload = new
        {
            from = Digits(credentials.FromNumber),
            to = Digits(toPhone),
            type = "text",
            text = new { body = text }
        };
        return SendMessageAsync(credentials, payload, ct);
    }

    public Task<YCloudSendResult> SendMediaAsync(YCloudCredentials credentials, string toPhone, WhatsAppCloudMediaKind kind, string mediaUrl, string? caption, string? fileName, CancellationToken ct = default)
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
            ["from"] = Digits(credentials.FromNumber),
            ["to"] = Digits(toPhone),
            ["type"] = typeKey,
            [typeKey] = mediaBody
        };
        return SendMessageAsync(credentials, payload, ct);
    }

    public Task<YCloudSendResult> SendTemplateAsync(YCloudCredentials credentials, string toPhone, string templateName, string languageCode, IReadOnlyList<string> bodyParams, CancellationToken ct = default)
    {
        var template = new Dictionary<string, object?>
        {
            ["name"] = templateName,
            ["language"] = new { code = string.IsNullOrWhiteSpace(languageCode) ? "es" : languageCode }
        };
        if (bodyParams is { Count: > 0 })
        {
            template["components"] = new object[]
            {
                new
                {
                    type = "body",
                    parameters = bodyParams.Select(p => new { type = "text", text = p ?? "" }).ToArray()
                }
            };
        }
        var payload = new Dictionary<string, object?>
        {
            ["from"] = Digits(credentials.FromNumber),
            ["to"] = Digits(toPhone),
            ["type"] = "template",
            ["template"] = template
        };
        return SendMessageAsync(credentials, payload, ct);
    }

    public Task<YCloudSendResult> SendLocationAsync(YCloudCredentials credentials, string toPhone, double latitude, double longitude, string? name, string? address, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["from"] = Digits(credentials.FromNumber),
            ["to"] = Digits(toPhone),
            ["type"] = "location",
            ["location"] = new { latitude, longitude, name = name ?? string.Empty, address = address ?? string.Empty }
        };
        return SendMessageAsync(credentials, payload, ct);
    }

    public Task<YCloudSendResult> SendReactionAsync(YCloudCredentials credentials, string toPhone, string messageId, string emoji, CancellationToken ct = default)
    {
        // WhatsApp/YCloud v2: type=reaction con el wamid del mensaje y el emoji. Emoji vacio quita la reaccion.
        var payload = new Dictionary<string, object?>
        {
            ["from"] = Digits(credentials.FromNumber),
            ["to"] = Digits(toPhone),
            ["type"] = "reaction",
            ["reaction"] = new { message_id = messageId, emoji }
        };
        return SendMessageAsync(credentials, payload, ct);
    }

    public async Task<YCloudTemplateListResult> ListTemplatesAsync(YCloudCredentials credentials, CancellationToken ct = default)
    {
        // PROPIA no guarda la WABA en la linea: la resolvemos desde los phoneNumbers de la cuenta (como CheckAsync).
        var (wabaId, wabaErr) = await ResolveWabaAsync(credentials, ct);
        if (wabaErr is not null)
            return new YCloudTemplateListResult(false, Array.Empty<YCloudTemplateItem>(), wabaErr);
        if (string.IsNullOrWhiteSpace(wabaId))
            return new YCloudTemplateListResult(false, Array.Empty<YCloudTemplateItem>(),
                "No se detecto la WABA de la cuenta YCloud (revisa que la API key cubra un numero registrado).");

        // Paginacion offset/limit de YCloud (items + length por pagina). Tope defensivo para no ciclar.
        const int pageSize = 100;
        const int maxPages = 50;
        var list = new List<YCloudTemplateItem>();
        var offset = 0;
        for (var page = 0; page < maxPages; page++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"{ApiBase}/whatsapp/templates?wabaId={Uri.EscapeDataString(wabaId!)}&limit={pageSize}&offset={offset}");
            req.Headers.Add("X-API-Key", credentials.ApiKey);
            int pageCount;
            try
            {
                using var res = await _http.SendAsync(req, ct);
                var body = await res.Content.ReadAsStringAsync(ct);
                if (!res.IsSuccessStatusCode)
                    return new YCloudTemplateListResult(false, list, ExtractError(body) ?? $"HTTP {(int)res.StatusCode}");
                using var doc = JsonDocument.Parse(body);
                pageCount = 0;
                if (doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                {
                    pageCount = items.GetArrayLength();
                    foreach (var it in items.EnumerateArray())
                    {
                        var parsed = YCloudTemplateParser.ParseItem(it);
                        if (parsed is not null) list.Add(parsed);
                    }
                }
            }
            catch (Exception ex)
            {
                return new YCloudTemplateListResult(false, list, ex.Message);
            }
            if (pageCount < pageSize) break;
            offset += pageSize;
        }
        return new YCloudTemplateListResult(true, list, null);
    }

    // Resuelve la WABA de la cuenta desde el endpoint de phoneNumbers (casando el numero remitente si se puede).
    private async Task<(string? WabaId, string? Error)> ResolveWabaAsync(YCloudCredentials credentials, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/whatsapp/phoneNumbers?limit=50");
        req.Headers.Add("X-API-Key", credentials.ApiKey);
        try
        {
            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                return (null, ExtractError(body) ?? $"HTTP {(int)res.StatusCode}");
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() == 0)
                return (null, null);
            var wantDigits = Digits(credentials.FromNumber);
            string? waba = StrOf(items[0], "wabaId") ?? StrOf(items[0], "whatsappBusinessAccountId");
            foreach (var it in items.EnumerateArray())
            {
                var p = StrOf(it, "phoneNumber") ?? StrOf(it, "displayPhoneNumber");
                if (p is not null && Digits(p) == wantDigits)
                {
                    waba = StrOf(it, "wabaId") ?? StrOf(it, "whatsappBusinessAccountId") ?? waba;
                    break;
                }
            }
            return (waba, null);
        }
        catch (Exception ex) { return (null, ex.Message); }
    }

    private async Task<YCloudSendResult> SendMessageAsync(YCloudCredentials credentials, object payload, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}/whatsapp/messages");
        req.Headers.Add("X-API-Key", credentials.ApiKey);
        req.Content = JsonContent.Create(payload);
        try
        {
            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                return new YCloudSendResult(false, null, ExtractError(body) ?? $"HTTP {(int)res.StatusCode}");
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            // Al enviar, YCloud devuelve su id interno (el wamid puede no existir aun).
            var id = StrOf(root, "wamid") ?? StrOf(root, "id");
            return new YCloudSendResult(true, id, null);
        }
        catch (Exception ex) { return new YCloudSendResult(false, null, ex.Message); }
    }

    private static string Digits(string? s) => new(( s ?? "").Where(char.IsDigit).ToArray());

    private static string? StrOf(JsonElement el, string prop)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    private static string? ExtractError(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            // YCloud: { "error": { "message": "..." } } o { "message": "..." }.
            if (root.TryGetProperty("error", out var err))
            {
                if (err.ValueKind == JsonValueKind.Object && err.TryGetProperty("message", out var m)) return m.GetString();
                if (err.ValueKind == JsonValueKind.String) return err.GetString();
            }
            if (root.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String) return msg.GetString();
        }
        catch { /* swallow */ }
        return null;
    }
}
