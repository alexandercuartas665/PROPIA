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

    public async Task<AiCompletion> CompleteWithToolsAsync(AiProvider provider, string apiKey, string? baseUrl, string model,
        string systemPrompt, IReadOnlyList<AiToolMessage> messages, IReadOnlyList<AiToolSpec> tools, CancellationToken ct = default)
    {
        try
        {
            return provider == AiProvider.Claude
                ? await ClaudeWithTools(apiKey, baseUrl, model, systemPrompt, messages, tools, ct)
                : await OpenAiCompatibleWithTools(provider, apiKey, baseUrl, model, systemPrompt, messages, tools, ct);
        }
        catch (Exception ex)
        {
            return new AiCompletion(false, null, $"No se pudo contactar al proveedor: {ex.Message}", 0, 0, Array.Empty<AiToolCall>());
        }
    }

    private static string Base(string? baseUrl, string fallback) =>
        (string.IsNullOrWhiteSpace(baseUrl) ? fallback : baseUrl).TrimEnd('/');

    // ---------- Tool-calling: formato OpenAI (OpenAI / DeepSeek / Gemini via endpoint OpenAI) ----------
    private async Task<AiCompletion> OpenAiCompatibleWithTools(AiProvider provider, string apiKey, string? baseUrl, string model,
        string systemPrompt, IReadOnlyList<AiToolMessage> messages, IReadOnlyList<AiToolSpec> tools, CancellationToken ct)
    {
        var url = provider switch
        {
            AiProvider.Gemini => $"{Base(baseUrl, "https://generativelanguage.googleapis.com")}/v1beta/openai/chat/completions",
            AiProvider.DeepSeek => $"{Base(baseUrl, "https://api.deepseek.com")}/chat/completions",
            _ => $"{Base(baseUrl, "https://api.openai.com/v1")}/chat/completions"
        };

        var msgs = new List<object>();
        if (!string.IsNullOrWhiteSpace(systemPrompt)) { msgs.Add(new { role = "system", content = systemPrompt }); }
        foreach (var m in messages)
        {
            if (m.Role == "tool")
            {
                msgs.Add(new { role = "tool", tool_call_id = m.ToolCallId ?? "", content = m.Text ?? "" });
            }
            else if (m.Role is "assistant" or "model" && m.ToolCalls is { Count: > 0 })
            {
                msgs.Add(new
                {
                    role = "assistant",
                    content = m.Text,
                    tool_calls = m.ToolCalls.Select(tc => new
                    {
                        id = tc.Id,
                        type = "function",
                        function = new { name = tc.Name, arguments = tc.ArgumentsJson }
                    }).ToArray()
                });
            }
            else
            {
                msgs.Add(new { role = m.Role == "model" ? "assistant" : m.Role, content = m.Text ?? "" });
            }
        }

        var toolDefs = tools.Select(t => new
        {
            type = "function",
            function = new { name = t.Name, description = t.Description ?? "", parameters = ParseSchema(t.ParametersJsonSchema) }
        }).ToArray();

        var body = new { model, messages = msgs, tools = toolDefs, tool_choice = "auto" };
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonBody(body) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var resp = await _http.SendAsync(req, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) { return FailTools((int)resp.StatusCode, raw); }

        using var doc = JsonDocument.Parse(raw);
        var msg = doc.RootElement.GetProperty("choices")[0].GetProperty("message");
        string? text = msg.TryGetProperty("content", out var cnt) && cnt.ValueKind == JsonValueKind.String ? cnt.GetString() : null;

        var calls = new List<AiToolCall>();
        if (msg.TryGetProperty("tool_calls", out var tcs) && tcs.ValueKind == JsonValueKind.Array)
        {
            foreach (var tc in tcs.EnumerateArray())
            {
                var id = tc.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                var fn = tc.GetProperty("function");
                var name = fn.GetProperty("name").GetString() ?? "";
                var args = fn.TryGetProperty("arguments", out var a) ? (a.ValueKind == JsonValueKind.String ? a.GetString() ?? "{}" : a.GetRawText()) : "{}";
                calls.Add(new AiToolCall(id, name, args));
            }
        }

        var (inTok, outTok) = (0, 0);
        if (doc.RootElement.TryGetProperty("usage", out var u))
        {
            inTok = u.TryGetProperty("prompt_tokens", out var p) ? p.GetInt32() : 0;
            outTok = u.TryGetProperty("completion_tokens", out var c) ? c.GetInt32() : 0;
        }
        return new AiCompletion(true, text, null, inTok, outTok, calls);
    }

    // ---------- Tool-calling: formato nativo de Claude ----------
    private async Task<AiCompletion> ClaudeWithTools(string apiKey, string? baseUrl, string model,
        string systemPrompt, IReadOnlyList<AiToolMessage> messages, IReadOnlyList<AiToolSpec> tools, CancellationToken ct)
    {
        var url = $"{Base(baseUrl, "https://api.anthropic.com")}/v1/messages";

        var msgs = new List<object>();
        foreach (var m in messages)
        {
            if (m.Role == "tool")
            {
                msgs.Add(new
                {
                    role = "user",
                    content = new object[] { new { type = "tool_result", tool_use_id = m.ToolCallId ?? "", content = m.Text ?? "" } }
                });
            }
            else if (m.Role is "assistant" or "model" && m.ToolCalls is { Count: > 0 })
            {
                var blocks = new List<object>();
                if (!string.IsNullOrWhiteSpace(m.Text)) { blocks.Add(new { type = "text", text = m.Text }); }
                foreach (var tc in m.ToolCalls)
                {
                    blocks.Add(new { type = "tool_use", id = tc.Id, name = tc.Name, input = ParseSchema(tc.ArgumentsJson) });
                }
                msgs.Add(new { role = "assistant", content = blocks.ToArray() });
            }
            else
            {
                msgs.Add(new { role = m.Role == "model" ? "assistant" : "user", content = m.Text ?? "" });
            }
        }

        var toolDefs = tools.Select(t => new
        {
            name = t.Name,
            description = t.Description ?? "",
            input_schema = ParseSchema(t.ParametersJsonSchema)
        }).ToArray();

        var body = new
        {
            model,
            max_tokens = 1024,
            system = string.IsNullOrWhiteSpace(systemPrompt) ? null : systemPrompt,
            tools = toolDefs,
            messages = msgs
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonBody(body) };
        req.Headers.Add("x-api-key", apiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");
        using var resp = await _http.SendAsync(req, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) { return FailTools((int)resp.StatusCode, raw); }

        using var doc = JsonDocument.Parse(raw);
        var sb = new StringBuilder();
        var calls = new List<AiToolCall>();
        foreach (var block in doc.RootElement.GetProperty("content").EnumerateArray())
        {
            var type = block.GetProperty("type").GetString();
            if (type == "text") { sb.Append(block.GetProperty("text").GetString()); }
            else if (type == "tool_use")
            {
                var id = block.GetProperty("id").GetString() ?? "";
                var name = block.GetProperty("name").GetString() ?? "";
                var input = block.TryGetProperty("input", out var inp) ? inp.GetRawText() : "{}";
                calls.Add(new AiToolCall(id, name, input));
            }
        }
        var (inTok, outTok) = (0, 0);
        if (doc.RootElement.TryGetProperty("usage", out var u))
        {
            inTok = u.TryGetProperty("input_tokens", out var p) ? p.GetInt32() : 0;
            outTok = u.TryGetProperty("output_tokens", out var c) ? c.GetInt32() : 0;
        }
        var txt = sb.ToString();
        return new AiCompletion(true, string.IsNullOrEmpty(txt) ? null : txt, null, inTok, outTok, calls);
    }

    private static JsonElement ParseSchema(string? json)
    {
        try
        {
            using var d = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return d.RootElement.Clone();
        }
        catch
        {
            using var d = JsonDocument.Parse("{\"type\":\"object\",\"properties\":{}}");
            return d.RootElement.Clone();
        }
    }

    private static AiCompletion FailTools(int status, string raw)
    {
        var snippet = raw.Length > 300 ? raw[..300] : raw;
        return new AiCompletion(false, null, $"El proveedor respondio HTTP {status}. {snippet}", 0, 0, Array.Empty<AiToolCall>());
    }

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
