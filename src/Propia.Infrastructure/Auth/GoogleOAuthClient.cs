using System.Text.Json;
using Microsoft.Extensions.Logging;
using Propia.Application.Auth;

namespace Propia.Infrastructure.Auth;

/// <summary>
/// Cliente HTTP que intercambia el authorization code de Google por un id_token (OIDC).
/// El id_token es un JWT firmado por Google que en el payload trae sub/email/email_verified/name/picture.
/// Para MVP confiamos en el TLS hacia Google (no validamos la firma del JWT - Google ya lo emite
/// y solo nos llega via HTTPS desde su endpoint). Si quisieramos hardening real: validar
/// firma contra https://www.googleapis.com/oauth2/v3/certs.
/// </summary>
public sealed class GoogleOAuthClient : IGoogleOAuthClient
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    private readonly HttpClient _http;
    private readonly ILogger<GoogleOAuthClient> _logger;

    public GoogleOAuthClient(HttpClient http, ILogger<GoogleOAuthClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<GoogleIdentity?> ExchangeCodeAsync(string clientId, string clientSecret, string code, string redirectUri, CancellationToken cancellationToken = default)
    {
        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            });

            var resp = await _http.PostAsync(TokenEndpoint, content, cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google /token devolvio {Status}: {Body}", (int)resp.StatusCode, body);
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("id_token", out var idTokenEl))
            {
                _logger.LogWarning("Google /token no devolvio id_token. Body: {Body}", body);
                return null;
            }
            var idToken = idTokenEl.GetString();
            if (string.IsNullOrEmpty(idToken)) return null;

            return DecodeIdToken(idToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo intercambiando code con Google");
            return null;
        }
    }

    /// <summary>
    /// Decodifica el payload del id_token (JWT). No valida firma - asumimos confianza en TLS hacia
    /// el endpoint /token de Google. El payload del id_token es base64url-encoded JSON con sub,
    /// email, email_verified, name, picture.
    /// </summary>
    private static GoogleIdentity? DecodeIdToken(string idToken)
    {
        var parts = idToken.Split('.');
        if (parts.Length < 2) return null;

        var payloadJson = Base64UrlDecode(parts[1]);
        using var doc = JsonDocument.Parse(payloadJson);
        var root = doc.RootElement;

        var sub = root.TryGetProperty("sub", out var subEl) ? subEl.GetString() : null;
        var email = root.TryGetProperty("email", out var emEl) ? emEl.GetString() : null;
        if (string.IsNullOrEmpty(sub) || string.IsNullOrEmpty(email)) return null;

        var verified = root.TryGetProperty("email_verified", out var vEl) && vEl.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(vEl.GetString(), out var b) && b,
            _ => false
        };
        var name = root.TryGetProperty("name", out var nEl) ? nEl.GetString() : null;
        var picture = root.TryGetProperty("picture", out var pEl) ? pEl.GetString() : null;
        return new GoogleIdentity(sub, email, verified, name, picture);
    }

    private static string Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        var bytes = Convert.FromBase64String(s);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
}
