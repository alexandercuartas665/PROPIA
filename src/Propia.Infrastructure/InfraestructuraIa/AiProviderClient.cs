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
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new AiChatResult(false, null, "El proveedor de IA no respondio a tiempo (timeout). Reintenta, o revisa el modelo y la API key en Servidores de IA.");
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
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new AiCompletion(false, null, "El proveedor de IA no respondio a tiempo (timeout).", 0, 0, Array.Empty<AiToolCall>());
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
                var role = m.Role == "model" ? "assistant" : m.Role;
                // Vision: si el turno trae imagenes entrantes, el content va como array multimodal
                // (text + image_url con data URI base64). Gemini via endpoint OpenAI acepta este formato.
                if (role == "user" && m.Images is { Count: > 0 })
                {
                    var parts = new List<object>();
                    if (!string.IsNullOrWhiteSpace(m.Text)) { parts.Add(new { type = "text", text = m.Text }); }
                    foreach (var img in m.Images)
                    {
                        parts.Add(new { type = "image_url", image_url = new { url = $"data:{img.MimeType};base64,{img.Base64}" } });
                    }
                    msgs.Add(new { role, content = parts.ToArray() });
                }
                else
                {
                    msgs.Add(new { role, content = m.Text ?? "" });
                }
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
                var role = m.Role == "model" ? "assistant" : "user";
                if (role == "user" && m.Images is { Count: > 0 })
                {
                    var blocks = new List<object>();
                    foreach (var img in m.Images)
                    {
                        blocks.Add(new { type = "image", source = new { type = "base64", media_type = img.MimeType, data = img.Base64 } });
                    }
                    if (!string.IsNullOrWhiteSpace(m.Text)) { blocks.Add(new { type = "text", text = m.Text }); }
                    msgs.Add(new { role, content = blocks.ToArray() });
                }
                else
                {
                    msgs.Add(new { role, content = m.Text ?? "" });
                }
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

    // ---------- Extraccion de documento (PDF/imagen nativo -> JSON estructurado) ----------
    public async Task<AiChatResult> ExtractFromDocumentAsync(AiProvider provider, string apiKey, string? baseUrl, string model,
        string instruction, byte[] documentBytes, string mimeType, string jsonSchema, CancellationToken ct = default)
    {
        try
        {
            return provider switch
            {
                AiProvider.Gemini => await GeminiExtract(apiKey, baseUrl, model, instruction, documentBytes, mimeType, jsonSchema, ct),
                _ => new AiChatResult(false, null, $"El proveedor {provider} no soporta extraccion de documentos con IA todavia.")
            };
        }
        catch (Exception ex)
        {
            return new AiChatResult(false, null, $"No se pudo contactar al proveedor: {ex.Message}");
        }
    }

    // ---------- Descripcion/transcripcion de media -> TEXTO plano (sin JSON schema) ----------
    public async Task<AiChatResult> DescribeMediaAsync(AiProvider provider, string apiKey, string? baseUrl, string model,
        string instruction, byte[] mediaBytes, string mimeType, CancellationToken ct = default)
    {
        try
        {
            return provider switch
            {
                AiProvider.Gemini => await GeminiDescribe(apiKey, baseUrl, model, instruction, mediaBytes, mimeType, ct),
                _ => new AiChatResult(false, null, $"El proveedor {provider} no soporta transcripcion/extraccion de media todavia.")
            };
        }
        catch (Exception ex)
        {
            return new AiChatResult(false, null, $"No se pudo contactar al proveedor: {ex.Message}");
        }
    }

    private async Task<AiChatResult> GeminiDescribe(string apiKey, string? baseUrl, string model, string instruction,
        byte[] mediaBytes, string mimeType, CancellationToken ct)
    {
        var url = $"{Base(baseUrl, "https://generativelanguage.googleapis.com")}/v1beta/models/{model}:generateContent?key={apiKey}";
        // Gemini quiere el mime base (sin parametros como "; codecs=opus" de las notas de voz de WhatsApp).
        var mime = mimeType;
        var semi = mime.IndexOf(';');
        if (semi > 0) { mime = mime[..semi].Trim(); }
        if (string.IsNullOrWhiteSpace(mime)) { mime = "application/octet-stream"; }
        var b64 = Convert.ToBase64String(mediaBytes);
        var body = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { inline_data = new { mime_type = mime, data = b64 } },
                        new { text = instruction }
                    }
                }
            },
            generationConfig = new { temperature = 0.0 }
        };

        using var resp = await _http.PostAsync(url, JsonBody(body), ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            var snip = raw.Length > 400 ? raw[..400] : raw;
            return new AiChatResult(false, null, $"Gemini respondio HTTP {(int)resp.StatusCode}. {snip}");
        }

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        string? text = null;
        if (root.TryGetProperty("candidates", out var cands) && cands.ValueKind == JsonValueKind.Array && cands.GetArrayLength() > 0)
        {
            var cand = cands[0];
            if (cand.TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts)
                && parts.ValueKind == JsonValueKind.Array && parts.GetArrayLength() > 0
                && parts[0].TryGetProperty("text", out var t))
                text = t.GetString();
        }
        if (string.IsNullOrWhiteSpace(text))
            return new AiChatResult(false, null, "Gemini no devolvio contenido (posible bloqueo de seguridad o media ilegible).");

        var (inTok, outTok) = (0, 0);
        if (root.TryGetProperty("usageMetadata", out var um))
        {
            inTok = um.TryGetProperty("promptTokenCount", out var p) ? p.GetInt32() : 0;
            outTok = um.TryGetProperty("candidatesTokenCount", out var c) ? c.GetInt32() : 0;
        }
        return new AiChatResult(true, text, null, inTok, outTok);
    }

    private async Task<AiChatResult> GeminiExtract(string apiKey, string? baseUrl, string model, string instruction,
        byte[] documentBytes, string mimeType, string jsonSchema, CancellationToken ct)
    {
        var url = $"{Base(baseUrl, "https://generativelanguage.googleapis.com")}/v1beta/models/{model}:generateContent?key={apiKey}";
        var b64 = Convert.ToBase64String(documentBytes);
        var body = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { inline_data = new { mime_type = mimeType, data = b64 } },
                        new { text = instruction }
                    }
                }
            },
            generationConfig = new
            {
                response_mime_type = "application/json",
                response_schema = ParseSchema(jsonSchema),
                temperature = 0.0
            }
        };

        using var resp = await _http.PostAsync(url, JsonBody(body), ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            var snip = raw.Length > 400 ? raw[..400] : raw;
            return new AiChatResult(false, null, $"Gemini respondio HTTP {(int)resp.StatusCode}. {snip}");
        }

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        string? text = null;
        if (root.TryGetProperty("candidates", out var cands) && cands.ValueKind == JsonValueKind.Array && cands.GetArrayLength() > 0)
        {
            var cand = cands[0];
            if (cand.TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts)
                && parts.ValueKind == JsonValueKind.Array && parts.GetArrayLength() > 0
                && parts[0].TryGetProperty("text", out var t))
                text = t.GetString();
        }
        if (string.IsNullOrWhiteSpace(text))
            return new AiChatResult(false, null, "Gemini no devolvio contenido (posible bloqueo de seguridad o documento ilegible).");

        var (inTok, outTok) = (0, 0);
        if (root.TryGetProperty("usageMetadata", out var um))
        {
            inTok = um.TryGetProperty("promptTokenCount", out var p) ? p.GetInt32() : 0;
            outTok = um.TryGetProperty("candidatesTokenCount", out var c) ? c.GetInt32() : 0;
        }
        return new AiChatResult(true, text, null, inTok, outTok);
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
            contents = turns.Select(t =>
            {
                // Vision: las imagenes entrantes del turno van como inline_data (base64) ANTES del texto.
                var parts = new List<object>();
                if (t.Images is { Count: > 0 })
                {
                    foreach (var img in t.Images)
                    {
                        parts.Add(new { inline_data = new { mime_type = img.MimeType, data = img.Base64 } });
                    }
                }
                parts.Add(new { text = t.Text ?? "" });
                return new { role = t.Role == "model" ? "model" : "user", parts = parts.ToArray() };
            }).ToArray()
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
        foreach (var t in turns)
        {
            var role = t.Role == "model" ? "assistant" : "user";
            if (role == "user" && t.Images is { Count: > 0 })
            {
                var parts = new List<object>();
                if (!string.IsNullOrWhiteSpace(t.Text)) { parts.Add(new { type = "text", text = t.Text }); }
                foreach (var img in t.Images)
                {
                    parts.Add(new { type = "image_url", image_url = new { url = $"data:{img.MimeType};base64,{img.Base64}" } });
                }
                messages.Add(new { role, content = parts.ToArray() });
            }
            else { messages.Add(new { role, content = t.Text }); }
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonBody(new { model, messages, max_tokens = 8192 }) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        // Forzar HTTP/1.1: con HTTP/2 el server resetea el stream a mitad de subir payloads grandes de
        // imagenes (vision, "Unable to write data to the transport connection"). Portado de ECOREX.
        req.Version = System.Net.HttpVersion.Version11;
        req.VersionPolicy = System.Net.Http.HttpVersionPolicy.RequestVersionExact;
        using var resp = await _http.SendAsync(req, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) { return Fail((int)resp.StatusCode, raw); }

        using var doc = JsonDocument.Parse(raw);
        var (inTok, outTok) = (0, 0);
        if (doc.RootElement.TryGetProperty("usage", out var u))
        {
            inTok = u.TryGetProperty("prompt_tokens", out var p) ? p.GetInt32() : 0;
            outTok = u.TryGetProperty("completion_tokens", out var c) ? c.GetInt32() : 0;
        }
        // Lectura defensiva: DeepSeek a veces devuelve content=null (razona en reasoning_content, o corta
        // por finish_reason=length). Si no hay texto, error con el crudo para depurar (no "sin respuesta").
        string? text = null, finish = null;
        if (doc.RootElement.TryGetProperty("choices", out var chs) && chs.ValueKind == JsonValueKind.Array && chs.GetArrayLength() > 0)
        {
            var ch0 = chs[0];
            if (ch0.TryGetProperty("finish_reason", out var fr) && fr.ValueKind == JsonValueKind.String) { finish = fr.GetString(); }
            if (ch0.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.Object)
            {
                if (msg.TryGetProperty("content", out var mc) && mc.ValueKind == JsonValueKind.String) { text = mc.GetString(); }
                if (string.IsNullOrWhiteSpace(text) && msg.TryGetProperty("reasoning_content", out var rc) && rc.ValueKind == JsonValueKind.String) { text = rc.GetString(); }
            }
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            var snip = raw.Length > 300 ? raw[..300] : raw;
            return new AiChatResult(false, null, $"El proveedor respondio 200 sin contenido (finish_reason={finish}; tokens {inTok}/{outTok}). {snip}");
        }
        return new AiChatResult(true, text, null, inTok, outTok);
    }

    private async Task<AiChatResult> Claude(string apiKey, string? baseUrl, string model, string systemPrompt,
        IReadOnlyList<AiChatTurn> turns, CancellationToken ct)
    {
        var url = $"{Base(baseUrl, "https://api.anthropic.com")}/v1/messages";
        var msgsClaude = turns.Select(t =>
        {
            var role = t.Role == "model" ? "assistant" : "user";
            // Vision: las imagenes entrantes van como bloques image (source base64) ANTES del texto.
            if (role == "user" && t.Images is { Count: > 0 })
            {
                var blocks = new List<object>();
                foreach (var img in t.Images)
                {
                    blocks.Add(new { type = "image", source = new { type = "base64", media_type = img.MimeType, data = img.Base64 } });
                }
                if (!string.IsNullOrWhiteSpace(t.Text)) { blocks.Add(new { type = "text", text = t.Text }); }
                return new { role, content = (object)blocks.ToArray() };
            }
            return new { role, content = (object)(t.Text ?? "") };
        }).ToArray();
        var body = new
        {
            model,
            max_tokens = 1024,
            system = string.IsNullOrWhiteSpace(systemPrompt) ? null : systemPrompt,
            messages = msgsClaude
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
