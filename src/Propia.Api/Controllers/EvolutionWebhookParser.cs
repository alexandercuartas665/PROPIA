using System.Text.Json;
using Propia.Application.InfraestructuraIa;

namespace Propia.Api.Controllers;

/// <summary>
/// Traduce el payload NATIVO del webhook de Evolution API (evento messages.upsert) a IngestMessageRequest.
/// Evolution envia un envelope { event, instance, data:{ key, message, pushName, messageTimestamp } }; el
/// endpoint receptor debe parsearlo (NO es un IngestMessageRequest plano; ese era el bug: llegaba envelope
/// y no mapeaba). Ignora salientes (key.fromMe), grupos (@g.us), reacciones y eventos que no sean
/// messages.upsert. El InstanceName devuelto es el nombre de instancia Evolution (propia_&lt;tenant&gt;_&lt;linea&gt;),
/// que ChatIngestService usa para resolver la linea. Espejo del MetaWebhookParser.
/// </summary>
public static class EvolutionWebhookParser
{
    public static IngestMessageRequest? Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) { return null; }

        // Solo mensajes entrantes (messages.upsert). Normaliza: minusculas y '_' -> '.'.
        if (root.TryGetProperty("event", out var evEl) && evEl.ValueKind == JsonValueKind.String)
        {
            var ev = evEl.GetString()!.ToLowerInvariant().Replace('_', '.');
            if (ev != "messages.upsert") { return null; }
        }

        var instance = root.TryGetProperty("instance", out var instEl) && instEl.ValueKind == JsonValueKind.String
            ? instEl.GetString() : null;

        if (!root.TryGetProperty("data", out var data)) { return null; }
        if (data.ValueKind == JsonValueKind.Array)
        {
            if (data.GetArrayLength() == 0) { return null; }
            data = data[0];
        }
        if (data.ValueKind != JsonValueKind.Object) { return null; }

        if (!data.TryGetProperty("key", out var key) || key.ValueKind != JsonValueKind.Object) { return null; }

        // Ignora salientes (eco de lo que envia el propio bot).
        if (key.TryGetProperty("fromMe", out var fromMe) && fromMe.ValueKind == JsonValueKind.True) { return null; }

        // remoteJid = "57300...@s.whatsapp.net" (persona) o "...@g.us" (grupo). Ignoramos grupos.
        if (!key.TryGetProperty("remoteJid", out var jidEl) || jidEl.ValueKind != JsonValueKind.String) { return null; }
        var jid = jidEl.GetString()!;
        if (jid.EndsWith("@g.us", StringComparison.OrdinalIgnoreCase)) { return null; }
        var phone = new string(jid.Where(char.IsDigit).ToArray());
        if (phone.Length < 7) { return null; }

        var externalId = key.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String
            ? idEl.GetString()! : Guid.NewGuid().ToString("N");

        string? contactName = data.TryGetProperty("pushName", out var pn) && pn.ValueKind == JsonValueKind.String
            ? pn.GetString() : null;

        DateTimeOffset? sentAt = null;
        if (data.TryGetProperty("messageTimestamp", out var tsEl))
        {
            long secs = tsEl.ValueKind == JsonValueKind.Number && tsEl.TryGetInt64(out var n) ? n
                : (tsEl.ValueKind == JsonValueKind.String && long.TryParse(tsEl.GetString(), out var s) ? s : 0);
            if (secs > 0) { sentAt = DateTimeOffset.FromUnixTimeSeconds(secs); }
        }

        // El contenido del mensaje. Ignora reacciones (no son mensajes reales).
        var message = data.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.Object ? m : default;
        if (message.ValueKind == JsonValueKind.Object && message.TryGetProperty("reactionMessage", out _)) { return null; }

        var (body, type) = ExtractBody(message);
        if (body is null) { return null; } // tipo no soportado / sin contenido de texto

        return new IngestMessageRequest(externalId, phone, contactName, body, type, sentAt, instance);
    }

    private static (string? Body, string Type) ExtractBody(JsonElement message)
    {
        if (message.ValueKind != JsonValueKind.Object) { return (null, "text"); }

        if (message.TryGetProperty("conversation", out var conv) && conv.ValueKind == JsonValueKind.String)
        {
            return (conv.GetString(), "text");
        }
        if (message.TryGetProperty("extendedTextMessage", out var ext) && ext.ValueKind == JsonValueKind.Object
            && ext.TryGetProperty("text", out var extText) && extText.ValueKind == JsonValueKind.String)
        {
            return (extText.GetString(), "text");
        }
        if (message.TryGetProperty("imageMessage", out var img) && img.ValueKind == JsonValueKind.Object)
        {
            var cap = img.TryGetProperty("caption", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
            return (string.IsNullOrWhiteSpace(cap) ? "(imagen)" : cap, "image");
        }
        if (message.TryGetProperty("videoMessage", out var vid) && vid.ValueKind == JsonValueKind.Object)
        {
            var cap = vid.TryGetProperty("caption", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
            return (string.IsNullOrWhiteSpace(cap) ? "(video)" : cap, "video");
        }
        if (message.TryGetProperty("audioMessage", out _)) { return ("(audio)", "audio"); }
        if (message.TryGetProperty("documentMessage", out var docm) && docm.ValueKind == JsonValueKind.Object)
        {
            var cap = docm.TryGetProperty("caption", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
            return (string.IsNullOrWhiteSpace(cap) ? "(documento)" : cap, "document");
        }
        if (message.TryGetProperty("locationMessage", out _)) { return ("(ubicacion)", "location"); }

        return (null, "text");
    }
}
