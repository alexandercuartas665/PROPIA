using System.Text.Json;
using Propia.Application.InfraestructuraIa;

namespace Propia.Api.Controllers;

/// <summary>
/// Ola D (analitica de entregas): traduce el evento messages.update de Evolution (acks de WhatsApp/Baileys)
/// a estados de entrega normalizados. Es el complemento del <see cref="EvolutionWebhookParser"/> (que maneja
/// messages.upsert = entrantes). Tolerante a las dos formas que usa Evolution v2 para el update:
///   { keyId, status }                         y
///   { key: { id, fromMe }, update: { status } }
/// y al status como string (SERVER_ACK/DELIVERY_ACK/READ/PLAYED/ERROR) o numero (2/3/4/5/0 de Baileys).
/// </summary>
public static class EvolutionStatusParser
{
    public static IReadOnlyList<DeliveryStatusUpdate> ParseStatuses(JsonElement root)
    {
        var result = new List<DeliveryStatusUpdate>();
        if (root.ValueKind != JsonValueKind.Object) { return result; }

        // Solo messages.update. Normaliza: minusculas y '_' -> '.'.
        if (root.TryGetProperty("event", out var evEl) && evEl.ValueKind == JsonValueKind.String)
        {
            var ev = evEl.GetString()!.ToLowerInvariant().Replace('_', '.');
            if (ev != "messages.update") { return result; }
        }

        if (!root.TryGetProperty("data", out var data)) { return result; }
        if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray()) { ParseOne(item, result); }
        }
        else if (data.ValueKind == JsonValueKind.Object)
        {
            ParseOne(data, result);
        }
        return result;
    }

    private static void ParseOne(JsonElement d, List<DeliveryStatusUpdate> result)
    {
        if (d.ValueKind != JsonValueKind.Object) { return; }

        // id del mensaje: key.id (forma anidada) o keyId / messageId (forma plana).
        string? id = null;
        if (d.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.Object
            && key.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
        {
            id = idEl.GetString();
        }
        id ??= Str(d, "keyId") ?? Str(d, "messageId");
        if (string.IsNullOrWhiteSpace(id)) { return; }

        // status: en update.status (forma anidada) o status (forma plana); string o numero.
        var statusEl = d.TryGetProperty("update", out var upd) && upd.ValueKind == JsonValueKind.Object
                       && upd.TryGetProperty("status", out var us) ? us
                     : d.TryGetProperty("status", out var s) ? s
                     : default;

        var normalizado = Normalizar(statusEl);
        if (normalizado is null) { return; } // PENDING u otro estado sin interes

        result.Add(new DeliveryStatusUpdate(id!, normalizado));
    }

    // Mapea el ack de Baileys (string o numero) al estado normalizado; null para los que no interesan (PENDING).
    private static string? Normalizar(JsonElement statusEl)
    {
        if (statusEl.ValueKind == JsonValueKind.Number && statusEl.TryGetInt32(out var n))
        {
            return n switch
            {
                0 => "failed",       // ERROR
                2 => "sent",         // SERVER_ACK
                3 => "delivered",    // DELIVERY_ACK
                4 => "read",         // READ
                5 => "read",         // PLAYED
                _ => null            // 1 = PENDING u otro
            };
        }
        if (statusEl.ValueKind == JsonValueKind.String)
        {
            return (statusEl.GetString() ?? "").Trim().ToUpperInvariant() switch
            {
                "ERROR" => "failed",
                "SERVER_ACK" => "sent",
                "DELIVERY_ACK" => "delivered",
                "READ" => "read",
                "PLAYED" => "read",
                _ => null
            };
        }
        return null;
    }

    private static string? Str(JsonElement el, string name)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;
}
