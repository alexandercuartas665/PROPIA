using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Propia.Application.Asambleas;

namespace Propia.Infrastructure.Asambleas;

/// <summary>
/// Sala de video con LiveKit (Cloud o servidor propio). Config (env en Railway):
///   LiveKit__Url        wss://xxx.livekit.cloud
///   LiveKit__ApiKey / LiveKit__ApiSecret
///   LiveKit__Grabacion__Bucket / AccessKey / Secret / Endpoint / Region / PublicUrl (opcional: S3/R2 donde LiveKit deja el MP4)
/// Los JWT (tokens de acceso, firma del webhook y llamadas Twirp de egress) se firman/validan a mano con HS256: la libreria
/// de IdentityModel exige llaves de 256 bits y el servidor de desarrollo de LiveKit usa "secret".
/// </summary>
public sealed class LiveKitVideoSalaProvider : IVideoSalaProvider
{
    private readonly IConfiguration _cfg;
    private readonly IHttpClientFactory _http;

    public LiveKitVideoSalaProvider(IConfiguration cfg, IHttpClientFactory http) { _cfg = cfg; _http = http; }

    public string Nombre => "LiveKit";
    public string Url => (_cfg["LiveKit:Url"] ?? "").Trim();
    private string ApiKey => (_cfg["LiveKit:ApiKey"] ?? "").Trim();
    private string ApiSecret => (_cfg["LiveKit:ApiSecret"] ?? "").Trim();
    public bool Configurada => Url.Length > 0 && ApiKey.Length > 0 && ApiSecret.Length > 0;
    public bool GrabacionConfigurada => Configurada
        && !string.IsNullOrWhiteSpace(_cfg["LiveKit:Grabacion:Bucket"])
        && !string.IsNullOrWhiteSpace(_cfg["LiveKit:Grabacion:AccessKey"])
        && !string.IsNullOrWhiteSpace(_cfg["LiveKit:Grabacion:Secret"]);

    public string NombreSala(Guid tenantId, Guid sesionId) => $"asm-{tenantId:N}-{sesionId:N}";

    public bool TryLeerSala(string? sala, out Guid tenantId, out Guid sesionId)
    {
        tenantId = sesionId = Guid.Empty;
        if (sala is null || sala.Length != 4 + 32 + 1 + 32 || !sala.StartsWith("asm-", StringComparison.Ordinal) || sala[36] != '-') return false;
        return Guid.TryParseExact(sala.Substring(4, 32), "N", out tenantId) && Guid.TryParseExact(sala.Substring(37, 32), "N", out sesionId);
    }

    public string CrearToken(string sala, string identidad, string nombre, bool moderador, TimeSpan vigencia)
    {
        if (!Configurada) throw new InvalidOperationException("La sala virtual no está configurada.");
        var ahora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var video = new JsonObject
        {
            ["room"] = sala,
            ["roomJoin"] = true,
            ["canPublish"] = true,
            ["canSubscribe"] = true,
            ["canPublishData"] = true,
            ["canUpdateOwnMetadata"] = false
        };
        if (moderador) { video["roomAdmin"] = true; video["roomRecord"] = true; }
        var payload = new JsonObject
        {
            ["iss"] = ApiKey,
            ["sub"] = identidad,
            ["nbf"] = ahora - 10,
            ["exp"] = ahora + (long)vigencia.TotalSeconds,
            ["name"] = nombre,
            ["metadata"] = JsonSerializer.Serialize(new { moderador }),
            ["video"] = video
        };
        return Firmar(payload);
    }

    public VideoSalaEvento? LeerWebhook(string cuerpo, string? authorization)
    {
        if (!Configurada || string.IsNullOrWhiteSpace(authorization)) return null;
        var token = authorization.Trim();
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) token = token[7..].Trim();
        var claims = Validar(token);
        if (claims is null) return null;
        // El JWT del webhook trae el hash del cuerpo: sin esto cualquiera con un token viejo podria inyectar eventos.
        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(cuerpo)));
        if (claims["sha256"]?.GetValue<string>() is not { } esperado
            || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(esperado), Encoding.ASCII.GetBytes(hash)))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(cuerpo);
            var r = doc.RootElement;
            var evento = Str(r, "event") ?? "";
            var sala = r.TryGetProperty("room", out var room) ? Str(room, "name") : null;
            string? identidad = null, nombre = null;
            if (r.TryGetProperty("participant", out var part)) { identidad = Str(part, "identity"); nombre = Str(part, "name"); }
            string? egressId = null, estado = null, url = null, archivo = null, error = null; long? tam = null; double? dur = null;
            if (r.TryGetProperty("egressInfo", out var eg))
            {
                egressId = Str(eg, "egressId"); estado = Str(eg, "status"); error = Str(eg, "error");
                sala ??= Str(eg, "roomName");
                JsonElement f = default;
                var hay = eg.TryGetProperty("fileResults", out var frs) && frs.ValueKind == JsonValueKind.Array && frs.GetArrayLength() > 0
                    ? (f = frs[0]).ValueKind == JsonValueKind.Object
                    : eg.TryGetProperty("file", out f) && f.ValueKind == JsonValueKind.Object;
                if (hay)
                {
                    url = Str(f, "location"); archivo = Str(f, "filename");
                    tam = Num(f, "size") is double s ? (long)s : null;
                    dur = Num(f, "duration") is double d ? Math.Round(d / 1_000_000_000d, 1) : null;   // nanosegundos
                    // R2 no es publico: con LiveKit:Grabacion:PublicUrl (dominio del bucket) se arma la URL de descarga.
                    if (_cfg["LiveKit:Grabacion:PublicUrl"] is { Length: > 0 } pub && !string.IsNullOrEmpty(archivo))
                        url = pub.TrimEnd('/') + "/" + archivo.TrimStart('/');
                }
            }
            return new VideoSalaEvento(evento, sala, identidad, nombre, egressId, estado, url, archivo, tam, dur, error);
        }
        catch (JsonException) { return null; }
    }

    public async Task<string> IniciarGrabacionAsync(string sala, string rutaArchivo, CancellationToken ct)
    {
        if (!GrabacionConfigurada) throw new InvalidOperationException("La grabación no está configurada (LiveKit:Grabacion).");
        var s3 = new JsonObject
        {
            ["accessKey"] = _cfg["LiveKit:Grabacion:AccessKey"],
            ["secret"] = _cfg["LiveKit:Grabacion:Secret"],
            ["bucket"] = _cfg["LiveKit:Grabacion:Bucket"],
            ["region"] = _cfg["LiveKit:Grabacion:Region"] ?? "auto",
            ["forcePathStyle"] = true
        };
        if (!string.IsNullOrWhiteSpace(_cfg["LiveKit:Grabacion:Endpoint"])) s3["endpoint"] = _cfg["LiveKit:Grabacion:Endpoint"];
        var body = new JsonObject
        {
            ["roomName"] = sala,
            ["layout"] = "grid",
            ["audioOnly"] = false,
            ["fileOutputs"] = new JsonArray(new JsonObject { ["fileType"] = "MP4", ["filepath"] = rutaArchivo, ["s3"] = s3 })
        };
        using var resp = await TwirpAsync("StartRoomCompositeEgress", body, sala, ct);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return Str(doc.RootElement, "egressId") ?? throw new InvalidOperationException("LiveKit no devolvió el id de la grabación.");
    }

    public async Task DetenerGrabacionAsync(string egressId, CancellationToken ct)
    {
        using var _ = await TwirpAsync("StopEgress", new JsonObject { ["egressId"] = egressId }, null, ct);
    }

    private async Task<HttpResponseMessage> TwirpAsync(string metodo, JsonObject body, string? sala, CancellationToken ct)
    {
        var baseUrl = Url.Replace("wss://", "https://", StringComparison.OrdinalIgnoreCase).Replace("ws://", "http://", StringComparison.OrdinalIgnoreCase).TrimEnd('/');
        var ahora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var video = new JsonObject { ["roomRecord"] = true };
        if (sala is not null) video["room"] = sala;
        var token = Firmar(new JsonObject { ["iss"] = ApiKey, ["sub"] = "propia-servidor", ["nbf"] = ahora - 10, ["exp"] = ahora + 600, ["video"] = video });
        var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/twirp/livekit.Egress/{metodo}")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var http = _http.CreateClient("livekit");
        http.Timeout = TimeSpan.FromSeconds(30);
        var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            resp.Dispose();
            throw new InvalidOperationException($"LiveKit rechazó {metodo} ({(int)resp.StatusCode}): {(err.Length > 300 ? err[..300] : err)}");
        }
        return resp;
    }

    // ===================== JWT HS256 =====================

    private string Firmar(JsonObject payload)
    {
        var h = B64(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var p = B64(Encoding.UTF8.GetBytes(payload.ToJsonString()));
        var firma = B64(HMACSHA256.HashData(Encoding.UTF8.GetBytes(ApiSecret), Encoding.ASCII.GetBytes(h + "." + p)));
        return $"{h}.{p}.{firma}";
    }

    /// <summary>Valida firma, emisor (ApiKey) y vigencia; devuelve los claims.</summary>
    private JsonObject? Validar(string jwt)
    {
        var partes = jwt.Split('.');
        if (partes.Length != 3) return null;
        try
        {
            var cab = JsonNode.Parse(DeB64(partes[0])) as JsonObject;
            if (cab?["alg"]?.GetValue<string>() != "HS256") return null;
            var esperada = HMACSHA256.HashData(Encoding.UTF8.GetBytes(ApiSecret), Encoding.ASCII.GetBytes(partes[0] + "." + partes[1]));
            if (!CryptographicOperations.FixedTimeEquals(esperada, DeB64(partes[2]))) return null;
            var claims = JsonNode.Parse(DeB64(partes[1])) as JsonObject;
            if (claims is null || claims["iss"]?.GetValue<string>() != ApiKey) return null;
            var ahora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (claims["exp"] is JsonValue exp && exp.GetValue<long>() < ahora - 60) return null;
            if (claims["nbf"] is JsonValue nbf && nbf.GetValue<long>() > ahora + 60) return null;
            return claims;
        }
        catch (Exception e) when (e is FormatException or JsonException or InvalidOperationException) { return null; }
    }

    private static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] DeB64(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }

    private static string? Str(JsonElement e, string p)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(p, out var v)
            ? v.ValueKind switch { JsonValueKind.String => v.GetString(), JsonValueKind.Number => v.GetRawText(), _ => null } : null;
    // int64 de protobuf llega como texto en JSON.
    private static double? Num(JsonElement e, string p)
        => Str(e, p) is { } t && double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
}
