using System.Text.Json;
using Propia.Application.InfraestructuraIa;

namespace Propia.Api.Controllers;

/// <summary>Mensaje entrante normalizado de Meta + el phone_number_id que identifica la linea destino.</summary>
public sealed record ParsedMetaInbound(string PhoneNumberId, IngestMessageRequest Payload);

/// <summary>
/// Traduce el payload del webhook de Meta WhatsApp Cloud API a IngestMessageRequest. Portado de
/// CUBOT.travels. Estructura de Meta: entry[].changes[].value.{metadata.phone_number_id, contacts[], messages[]}.
/// Devuelve null si no hay mensaje entrante procesable (ej. solo eventos de estado sent/delivered/read).
/// </summary>
public static class MetaWebhookParser
{
    public static ParsedMetaInbound? Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) { return null; }
        if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array) { return null; }
        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array) { continue; }
            foreach (var change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Object) { continue; }
                // Eventos de estado (sent/delivered/read) llegan en value.statuses; los ignoramos.
                if (!value.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array) { continue; }
                if (messages.GetArrayLength() == 0) { continue; }

                if (!value.TryGetProperty("metadata", out var meta) || meta.ValueKind != JsonValueKind.Object) { continue; }
                if (!meta.TryGetProperty("phone_number_id", out var pnId) || pnId.ValueKind != JsonValueKind.String) { continue; }
                var phoneNumberId = pnId.GetString()!;

                string? contactName = null;
                if (value.TryGetProperty("contacts", out var contacts) && contacts.ValueKind == JsonValueKind.Array && contacts.GetArrayLength() > 0)
                {
                    var first = contacts[0];
                    if (first.TryGetProperty("profile", out var profile) && profile.TryGetProperty("name", out var pname) && pname.ValueKind == JsonValueKind.String)
                    {
                        contactName = pname.GetString();
                    }
                }

                var msg = messages[0];
                if (!msg.TryGetProperty("from", out var fromEl) || fromEl.ValueKind != JsonValueKind.String) { continue; }
                var phone = new string(fromEl.GetString()!.Where(char.IsDigit).ToArray());
                if (string.IsNullOrEmpty(phone)) { continue; }

                var externalId = msg.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String
                    ? idEl.GetString()!
                    : Guid.NewGuid().ToString("N");

                DateTimeOffset? sentAt = null;
                if (msg.TryGetProperty("timestamp", out var ts) && ts.ValueKind == JsonValueKind.String && long.TryParse(ts.GetString(), out var secs))
                {
                    sentAt = DateTimeOffset.FromUnixTimeSeconds(secs);
                }

                var body = ExtractText(msg) ?? "(mensaje no soportado)";

                // IngestMessageRequest(ExternalId, ContactPhone, ContactName, Body, MessageType, SentAt, InstanceName).
                // InstanceName se resuelve luego por phone_number_id (por eso va null aqui).
                return new ParsedMetaInbound(phoneNumberId,
                    new IngestMessageRequest(externalId, phone, contactName, body, "text", sentAt, null));
            }
        }
        return null;
    }

    private static string? ExtractText(JsonElement msg)
    {
        if (!msg.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String) { return null; }
        switch (typeEl.GetString())
        {
            case "text":
                if (msg.TryGetProperty("text", out var t) && t.TryGetProperty("body", out var tb) && tb.ValueKind == JsonValueKind.String) { return tb.GetString(); }
                return null;
            case "image": return TryGetCaption(msg, "image") ?? "(imagen)";
            case "video": return TryGetCaption(msg, "video") ?? "(video)";
            case "audio": return "(audio)";
            case "document": return TryGetCaption(msg, "document") ?? "(documento)";
            case "location": return "(ubicacion)";
            case "interactive":
                if (msg.TryGetProperty("interactive", out var it) && it.ValueKind == JsonValueKind.Object)
                {
                    if (it.TryGetProperty("button_reply", out var br) && br.TryGetProperty("title", out var brt)) { return brt.GetString(); }
                    if (it.TryGetProperty("list_reply", out var lr) && lr.TryGetProperty("title", out var lrt)) { return lrt.GetString(); }
                }
                return null;
            default: return null;
        }
    }

    private static string? TryGetCaption(JsonElement msg, string mediaProp)
    {
        if (msg.TryGetProperty(mediaProp, out var media) && media.ValueKind == JsonValueKind.Object
            && media.TryGetProperty("caption", out var cap) && cap.ValueKind == JsonValueKind.String)
        {
            return cap.GetString();
        }
        return null;
    }
}
