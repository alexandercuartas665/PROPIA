using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Enums;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Cliente HTTP de inferencia para los proveedores de IA. La API key llega descifrada; no se persiste
/// ni se loggea. Soporta Gemini (REST), OpenAI/ChatGPT y DeepSeek (chat/completions) y Claude (messages).
/// Portado de CUBOT.travels (read-only).
/// </summary>
public sealed class AiProviderClient : IAiProviderClient
{
    private readonly HttpClient _http;

    public AiProviderClient(HttpClient http) => _http = http;

    public async Task<AiChatResult> CompleteAsync(AiProvider provider, string apiKey, string? baseUrl, string model,
        string systemPrompt, IReadOnlyList<AiChatTurn> turns, CancellationToken ct = default)
    {
        try
        {
            return provider switch
            {
                AiProvider.Gemini => await Gemini(apiKey, baseUrl, model, systemPrompt, turns, ct),
                AiProvider.Claude => await Claude(apiKey, baseUrl, model, systemPrompt, turns, ct),
                _ => await OpenAiCompatible(provider, apiKey, baseUrl, model, systemPrompt, turns, ct)
            };
        }
        catch (Exception ex)
        {
            return new AiChatResult(false, null, $"No se pudo contactar al proveedor: {ex.Message}");
        }
    }

    private static string Base(string? baseUrl, string fallback) =>
        (string.IsNullOrWhiteSpace(baseUrl) ? fallback : baseUrl).TrimEnd('/');

    private async Task<AiChatResult> Gemini(string apiKey, string? baseUrl, string model, string systemPrompt,
        IReadOnlyList<AiChatTurn> turns, CancellationToken ct)
    {
        var url = $"{Base(baseUrl, "https://generativelanguage.googleapis.com")}/v1beta/models/{model}:generateContent?key={apiKey}";
        var body = new
        {
            systemInstruction = string.IsNullOrWhiteSpace(systemPrompt) ? null : new { parts = new[] { new { text = systemPrompt } } },
            contents = turns.Select(t => new { role = t.Role == "model" ? "model" : "user", parts = new[] { new { text = t.Text } } }).ToArray()
        };
        using var resp = await _http.PostAsync(url, JsonBody(body), ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) { return Fail((int)resp.StatusCode, raw); }

        using var doc = JsonDocument.Parse(raw);
        var text = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
        var (inTok, outTok) = (0, 0);
        if (doc.RootElement.TryGetProperty("usageMetadata", out var um))
        {
            inTok = um.TryGetProperty("promptTokenCount", out var p) ? p.GetInt32() : 0;
            outTok = um.TryGetProperty("candidatesTokenCount", out var c) ? c.GetInt32() : 0;
        }
        return new AiChatResult(true, text, null, inTok, outTok);
    }

    private async Task<AiChatResult> OpenAiCompatible(AiProvider provider, string apiKey, string? baseUrl, string model,
        string systemPrompt, IReadOnlyList<AiChatTurn> turns, CancellationToken ct)
    {
        var fallback = provider == AiProvider.DeepSeek ? "https://api.deepseek.com" : "https://api.openai.com/v1";
        var url = $"{Base(baseUrl, fallback)}/chat/completions";

        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(systemPrompt)) { messages.Add(new { role = "system", content = systemPrompt }); }
        foreach (var t in turns) { messages.Add(new { role = t.Role == "model" ? "assistant" : "user", content = t.Text }); }

        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonBody(new { model, messages }) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var resp = await _http.SendAsync(req, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) { return Fail((int)resp.StatusCode, raw); }

        using var doc = JsonDocument.Parse(raw);
        var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        var (inTok, outTok) = (0, 0);
        if (doc.RootElement.TryGetProperty("usage", out var u))
        {
            inTok = u.TryGetProperty("prompt_tokens", out var p) ? p.GetInt32() : 0;
            outTok = u.TryGetProperty("completion_tokens", out var c) ? c.GetInt32() : 0;
        }
        return new AiChatResult(true, text, null, inTok, outTok);
    }

    private async Task<AiChatResult> Claude(string apiKey, string? baseUrl, string model, string systemPrompt,
        IReadOnlyList<AiChatTurn> turns, CancellationToken ct)
    {
        var url = $"{Base(baseUrl, "https://api.anthropic.com")}/v1/messages";
        var body = new
        {
            model,
            max_tokens = 1024,
            system = string.IsNullOrWhiteSpace(systemPrompt) ? null : systemPrompt,
            messages = turns.Select(t => new { role = t.Role == "model" ? "assistant" : "user", content = t.Text }).ToArray()
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonBody(body) };
        req.Headers.Add("x-api-key", apiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");
        using var resp = await _http.SendAsync(req, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) { return Fail((int)resp.StatusCode, raw); }

        using var doc = JsonDocument.Parse(raw);
        var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
        var (inTok, outTok) = (0, 0);
        if (doc.RootElement.TryGetProperty("usage", out var u))
        {
            inTok = u.TryGetProperty("input_tokens", out var p) ? p.GetInt32() : 0;
            outTok = u.TryGetProperty("output_tokens", out var c) ? c.GetInt32() : 0;
        }
        return new AiChatResult(true, text, null, inTok, outTok);
    }

    private static StringContent JsonBody(object body) =>
        new(JsonSerializer.Serialize(body, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }),
            Encoding.UTF8, "application/json");

    private static AiChatResult Fail(int status, string raw)
    {
        var snippet = raw.Length > 300 ? raw[..300] : raw;
        return new AiChatResult(false, null, $"El proveedor respondio HTTP {status}. {snippet}");
    }
}
