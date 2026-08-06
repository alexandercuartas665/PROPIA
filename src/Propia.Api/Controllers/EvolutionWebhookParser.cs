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

        // remoteJid = "57300...@s.whatsapp.net" (persona), "...@g.us" (grupo) o "<lid>@lid".
        if (!key.TryGetProperty("remoteJid", out var jidEl) || jidEl.ValueKind != JsonValueKind.String) { return null; }
        var jid = jidEl.GetString()!;
        if (jid.EndsWith("@g.us", StringComparison.OrdinalIgnoreCase)) { return null; } // grupo

        string phone;
        if (jid.EndsWith("@lid", StringComparison.OrdinalIgnoreCase))
        {
            // WhatsApp/Baileys manda algunos contactos como "<lid>@lid" (privacidad): el <lid> NO es
            // telefono. El telefono real (si viene) esta en otro campo (senderPn, remoteJidAlt, etc.).
            // Si no se puede resolver, NO ingestamos: evitamos responder a un numero inexistente (400).
            var resolved = ResolveRealPhoneFromLid(key, data);
            if (resolved is null) { return null; }
            phone = resolved;
        }
        else
        {
            phone = new string(jid.Where(char.IsDigit).ToArray());
        }
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

    /// <summary>
    /// Telefono real de un contacto @lid. Baileys/Evolution suelen traerlo en un campo hermano cuyo
    /// valor termina en "@s.whatsapp.net" (senderPn, remoteJidAlt, participantPn, ...). Devuelve solo
    /// digitos o null si no hay ningun JID de telefono resoluble en el payload.
    /// </summary>
    private static string? ResolveRealPhoneFromLid(JsonElement key, JsonElement data)
    {
        foreach (var candidate in CandidateJids(key, data))
        {
            if (string.IsNullOrEmpty(candidate)) { continue; }
            if (candidate.EndsWith("@s.whatsapp.net", StringComparison.OrdinalIgnoreCase))
            {
                var d = new string(candidate.Where(char.IsDigit).ToArray());
                if (d.Length >= 7) { return d; }
            }
        }
        return null;
    }

    private static IEnumerable<string?> CandidateJids(JsonElement key, JsonElement data)
    {
        foreach (var name in new[] { "senderPn", "remoteJidAlt", "participantPn", "participant", "senderJid" })
        {
            yield return GetStr(key, name);
        }
        foreach (var name in new[] { "senderPn", "participant" })
        {
            yield return GetStr(data, name);
        }
    }

    private static string? GetStr(JsonElement el, string name)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

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
