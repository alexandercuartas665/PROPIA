using System.Text.Json;
using Propia.Application.InfraestructuraIa;

namespace Propia.Api.Controllers;

/// <summary>Mensaje entrante normalizado de YCloud + el numero destino (to) que identifica la linea.</summary>
public sealed record ParsedYCloudInbound(string NumeroDestino, IngestMessageRequest Payload);

/// <summary>
/// Traduce los eventos del webhook de YCloud (API v2). Estructura:
///   { "type": "whatsapp.inbound_message.received", "whatsappInboundMessage": { id, wamid, from, to, sendTime,
///     customerProfile.name, type, text.body | button.text | interactive.button_reply.title | image.caption ... } }
///   { "type": "whatsapp.message.updated", "whatsappMessage": { id, wamid, status, errorCode, errorMessage } }
/// </summary>
public static class YCloudWebhookParser
{
    public const string EventoEntrante = "whatsapp.inbound_message.received";
    public const string EventoEstado = "whatsapp.message.updated";

    public static string? Tipo(JsonElement root) => Str(root, "type");

    public static ParsedYCloudInbound? ParseEntrante(JsonElement root)
    {
        if (Tipo(root) != EventoEntrante) return null;
        if (!root.TryGetProperty("whatsappInboundMessage", out var m) || m.ValueKind != JsonValueKind.Object) return null;
        var from = Digitos(Str(m, "from"));
        var to = Digitos(Str(m, "to"));
        if (from.Length == 0 || to.Length == 0) return null;
        var externalId = Str(m, "wamid") ?? Str(m, "id") ?? Guid.NewGuid().ToString("N");
        string? nombre = m.TryGetProperty("customerProfile", out var cp) ? Str(cp, "name") : null;
        DateTimeOffset? enviado = DateTimeOffset.TryParse(Str(m, "sendTime"), out var st) ? st.ToUniversalTime() : null;
        var cuerpo = Texto(m) ?? "(mensaje no soportado)";
        return new ParsedYCloudInbound(to, new IngestMessageRequest(externalId, from, nombre, cuerpo, "text", enviado, null));
    }

    /// <summary>Estados de entrega de salientes. El envio guarda el wamid (o el id de YCloud si aun no hay wamid):
    /// se emiten ambos para casar con cualquiera de los dos.</summary>
    public static IReadOnlyList<DeliveryStatusUpdate> ParseEstados(JsonElement root)
    {
        var r = new List<DeliveryStatusUpdate>();
        if (Tipo(root) != EventoEstado) return r;
        if (!root.TryGetProperty("whatsappMessage", out var m) || m.ValueKind != JsonValueKind.Object) return r;
        var estado = Str(m, "status")?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(estado)) return r;
        string? code = null, msg = null;
        if (estado == "failed")
        {
            code = Str(m, "errorCode");
            msg = Str(m, "errorMessage");
        }
        foreach (var id in new[] { Str(m, "wamid"), Str(m, "id") }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
            r.Add(new DeliveryStatusUpdate(id!, estado, code, msg));
        return r;
    }

    private static string? Texto(JsonElement m)
    {
        switch (Str(m, "type"))
        {
            case "text": return m.TryGetProperty("text", out var t) ? Str(t, "body") : null;
            // Respuesta a un boton de respuesta rapida de una plantilla.
            case "button": return m.TryGetProperty("button", out var b) ? Str(b, "text") ?? Str(b, "payload") : null;
            case "interactive":
                if (m.TryGetProperty("interactive", out var it) && it.ValueKind == JsonValueKind.Object)
                {
                    if (it.TryGetProperty("button_reply", out var br)) return Str(br, "title");
                    if (it.TryGetProperty("list_reply", out var lr)) return Str(lr, "title");
                }
                return null;
            case "image": return Caption(m, "image") ?? "(imagen)";
            case "video": return Caption(m, "video") ?? "(video)";
            case "audio": return "(audio)";
            case "document": return Caption(m, "document") ?? "(documento)";
            case "location": return "(ubicacion)";
            case "sticker": return "(sticker)";
            default: return null;
        }
    }

    private static string? Caption(JsonElement m, string prop)
        => m.TryGetProperty(prop, out var media) && media.ValueKind == JsonValueKind.Object ? Str(media, "caption") : null;

    private static string Digitos(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());

    private static string? Str(JsonElement e, string p)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
