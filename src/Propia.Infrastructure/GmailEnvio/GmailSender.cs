using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Propia.Application.Common;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.GmailEnvio;

/// <summary>
/// Envia correos desde la cuenta Gmail conectada de un tenant, via Gmail API (users.messages.send).
/// Refresca el access_token con el refresh_token guardado (cifrado). Arma un MIME multipart/mixed
/// (HTML + adjuntos) y lo manda en base64url. No persiste el access_token (se pide por envio).
/// </summary>
public sealed class GmailSender : IGmailSender
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string SendEndpoint = "https://gmail.googleapis.com/gmail/v1/users/me/messages/send";

    private readonly PropiaDbContext _db;
    private readonly ISecretProtector _secret;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<GmailSender> _log;

    public GmailSender(PropiaDbContext db, ISecretProtector secret, IHttpClientFactory httpFactory, ILogger<GmailSender> log)
    {
        _db = db;
        _secret = secret;
        _httpFactory = httpFactory;
        _log = log;
    }

    public async Task<GmailSendResult> SendAsync(Guid tenantId, IEnumerable<string> destinatarios, string asunto,
        string cuerpoHtml, IReadOnlyList<CorreoAdjunto>? adjuntos, CancellationToken ct = default)
    {
        var to = destinatarios?.Where(d => !string.IsNullOrWhiteSpace(d)).Select(d => d.Trim()).Distinct().ToList() ?? new();
        if (to.Count == 0) return new GmailSendResult(false, "No hay destinatarios.");

        // IgnoreQueryFilters: el sender corre en un contexto que puede no tener el tenant seteado.
        var conexion = await _db.GmailEnvioConexiones.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);
        if (conexion is null || !conexion.IsEnabled || string.IsNullOrEmpty(conexion.RefreshTokenEncrypted))
            return new GmailSendResult(false, "La copropiedad no tiene una cuenta Gmail conectada para envio.");

        var app = await _db.GmailEnvioAppConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        if (app is null || string.IsNullOrWhiteSpace(app.ClientId) || string.IsNullOrEmpty(app.ClientSecretEncrypted))
            return new GmailSendResult(false, "El OAuth client de envio no esta configurado (Super Admin).");

        string accessToken;
        try
        {
            accessToken = await RefrescarAccessTokenAsync(
                app.ClientId!, _secret.Unprotect(app.ClientSecretEncrypted!),
                _secret.Unprotect(conexion.RefreshTokenEncrypted!), ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Gmail: fallo al refrescar access_token para tenant {Tenant}", tenantId);
            return new GmailSendResult(false, "No se pudo autenticar con Gmail (reconecta la cuenta).");
        }

        var raw = ConstruirMimeBase64Url(conexion.Email, to, asunto, cuerpoHtml, adjuntos);

        try
        {
            var http = _httpFactory.CreateClient();
            using var req = new HttpRequestMessage(HttpMethod.Post, SendEndpoint);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            req.Content = new StringContent(JsonSerializer.Serialize(new { raw }), Encoding.UTF8, "application/json");
            var resp = await http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("Gmail send {Status}: {Body}", (int)resp.StatusCode, body);
                return new GmailSendResult(false, $"Gmail rechazo el envio ({(int)resp.StatusCode}).");
            }
            return new GmailSendResult(true, null);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Gmail: error de red al enviar para tenant {Tenant}", tenantId);
            return new GmailSendResult(false, "Error de red al enviar el correo.");
        }
    }

    private async Task<string> RefrescarAccessTokenAsync(string clientId, string clientSecret, string refreshToken, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient();
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        });
        var resp = await http.PostAsync(TokenEndpoint, form, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Google no devolvio access_token.");
    }

    // Arma el mensaje RFC822 (multipart/mixed) y lo codifica en base64url para la Gmail API.
    private static string ConstruirMimeBase64Url(string from, List<string> to, string asunto, string cuerpoHtml,
        IReadOnlyList<CorreoAdjunto>? adjuntos)
    {
        var boundary = "propia_" + Guid.NewGuid().ToString("N");
        var sb = new StringBuilder();
        sb.Append("From: ").Append(from).Append("\r\n");
        sb.Append("To: ").Append(string.Join(", ", to)).Append("\r\n");
        sb.Append("Subject: ").Append(CodificarAsunto(asunto)).Append("\r\n");
        sb.Append("MIME-Version: 1.0\r\n");
        sb.Append("Content-Type: multipart/mixed; boundary=\"").Append(boundary).Append("\"\r\n\r\n");

        // Parte HTML (base64 para soportar UTF-8 sin lios de line-length).
        sb.Append("--").Append(boundary).Append("\r\n");
        sb.Append("Content-Type: text/html; charset=\"UTF-8\"\r\n");
        sb.Append("Content-Transfer-Encoding: base64\r\n\r\n");
        sb.Append(Base64Lineas(Encoding.UTF8.GetBytes(cuerpoHtml ?? ""))).Append("\r\n");

        if (adjuntos is not null)
        {
            foreach (var a in adjuntos)
            {
                sb.Append("--").Append(boundary).Append("\r\n");
                sb.Append("Content-Type: ").Append(a.TipoMime).Append("; name=\"").Append(a.NombreArchivo).Append("\"\r\n");
                sb.Append("Content-Transfer-Encoding: base64\r\n");
                sb.Append("Content-Disposition: attachment; filename=\"").Append(a.NombreArchivo).Append("\"\r\n\r\n");
                sb.Append(Base64Lineas(a.Contenido)).Append("\r\n");
            }
        }

        sb.Append("--").Append(boundary).Append("--");

        var raw = Encoding.UTF8.GetBytes(sb.ToString());
        return Convert.ToBase64String(raw).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    // RFC 2047 para asuntos con caracteres no ASCII.
    private static string CodificarAsunto(string asunto)
    {
        asunto ??= "";
        if (asunto.All(c => c < 128)) return asunto;
        return "=?UTF-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes(asunto)) + "?=";
    }

    // Base64 partido en lineas de 76 chars (MIME).
    private static string Base64Lineas(byte[] data)
    {
        var b64 = Convert.ToBase64String(data);
        var sb = new StringBuilder(b64.Length + b64.Length / 76 * 2);
        for (var i = 0; i < b64.Length; i += 76)
        {
            sb.Append(b64, i, Math.Min(76, b64.Length - i));
            sb.Append("\r\n");
        }
        return sb.ToString().TrimEnd('\r', '\n');
    }
}
