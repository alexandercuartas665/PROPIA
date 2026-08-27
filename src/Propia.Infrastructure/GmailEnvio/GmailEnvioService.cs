using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Propia.Application.Common;
using Propia.Application.Integraciones;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.GmailEnvio;

/// <summary>
/// Config del OAuth client de envio (Super Admin, singleton) y conexion Gmail por copropiedad.
/// El scope de envio es gmail.send; se pide openid+email para conocer la cuenta conectada, y
/// access_type=offline + prompt=consent para obtener el refresh_token.
/// </summary>
public sealed class GmailEnvioService : IGmailEnvioService
{
    private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string Scopes = "openid email https://www.googleapis.com/auth/gmail.send";

    private readonly PropiaDbContext _db;
    private readonly ISecretProtector _secret;
    private readonly ITenantContext _tenant;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<GmailEnvioService> _log;

    public GmailEnvioService(PropiaDbContext db, ISecretProtector secret, ITenantContext tenant,
        IHttpClientFactory httpFactory, IHttpContextAccessor http, ILogger<GmailEnvioService> log)
    {
        _db = db;
        _secret = secret;
        _tenant = tenant;
        _httpFactory = httpFactory;
        _http = http;
        _log = log;
    }

    // ---------- Super Admin: OAuth client ----------
    public async Task<GmailEnvioAppConfigDto?> ObtenerAppConfigAsync(CancellationToken ct)
    {
        var cfg = await _db.GmailEnvioAppConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        return cfg is null ? null : new GmailEnvioAppConfigDto(cfg.ClientId, !string.IsNullOrEmpty(cfg.ClientSecretEncrypted), cfg.IsEnabled);
    }

    public async Task GuardarAppConfigAsync(GuardarGmailEnvioAppConfigRequest req, CancellationToken ct)
    {
        var cfg = await _db.GmailEnvioAppConfigs.FirstOrDefaultAsync(ct);
        if (cfg is null)
        {
            cfg = new GmailEnvioAppConfig();
            _db.GmailEnvioAppConfigs.Add(cfg);
        }
        cfg.ClientId = req.ClientId?.Trim();
        cfg.IsEnabled = req.IsEnabled;
        if (!string.IsNullOrWhiteSpace(req.ClientSecret))   // vacio conserva el actual
            cfg.ClientSecretEncrypted = _secret.Protect(req.ClientSecret.Trim());
        await _db.SaveChangesAsync(ct);
    }

    // ---------- Tenant: conexion Gmail ----------
    public async Task<GmailEnvioEstadoDto> ObtenerEstadoAsync(CancellationToken ct)
    {
        var app = await _db.GmailEnvioAppConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        var appOk = app is not null && !string.IsNullOrWhiteSpace(app.ClientId)
                    && !string.IsNullOrEmpty(app.ClientSecretEncrypted) && app.IsEnabled;

        var tenantId = _tenant.CurrentTenantId;
        GmailEnvioConexion? con = null;
        if (tenantId is not null)
            con = await _db.GmailEnvioConexiones.AsNoTracking().FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);

        var conectada = con is not null && con.IsEnabled && !string.IsNullOrEmpty(con.RefreshTokenEncrypted);
        return new GmailEnvioEstadoDto(appOk, conectada, conectada ? con!.Email : null);
    }

    public async Task<string?> ConstruirUrlAutorizacionAsync(string redirectUri, CancellationToken ct)
    {
        var app = await _db.GmailEnvioAppConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        if (app is null || string.IsNullOrWhiteSpace(app.ClientId) || !app.IsEnabled) return null;
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return null;

        var q = new Dictionary<string, string>
        {
            ["client_id"] = app.ClientId!,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = Scopes,
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["include_granted_scopes"] = "false",
            ["state"] = tenantId.Value.ToString("N")
        };
        var qs = string.Join("&", q.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return $"{AuthEndpoint}?{qs}";
    }

    public async Task<(bool Ok, string? Error)> CompletarConexionAsync(string code, string state, string redirectUri, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return (false, "Sin copropiedad activa.");
        if (!Guid.TryParse(state, out var stateTenant) || stateTenant != tenantId.Value)
            return (false, "El state no coincide con la copropiedad activa.");

        var app = await _db.GmailEnvioAppConfigs.FirstOrDefaultAsync(ct);
        if (app is null || string.IsNullOrWhiteSpace(app.ClientId) || string.IsNullOrEmpty(app.ClientSecretEncrypted))
            return (false, "El OAuth client de envio no esta configurado.");

        string body;
        try
        {
            var http = _httpFactory.CreateClient();
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = app.ClientId!,
                ["client_secret"] = _secret.Unprotect(app.ClientSecretEncrypted!),
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            });
            var resp = await http.PostAsync(TokenEndpoint, form, ct);
            body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("Gmail token exchange {Status}: {Body}", (int)resp.StatusCode, body);
                return (false, "Google rechazo la autorizacion.");
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Gmail: error de red en token exchange");
            return (false, "Error de red al conectar con Google.");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
        if (string.IsNullOrWhiteSpace(refreshToken))
            return (false, "Google no devolvio refresh_token. Revoca el acceso de la app en tu cuenta y reconecta.");

        var email = root.TryGetProperty("id_token", out var it) ? EmailDeIdToken(it.GetString()) : null;
        if (string.IsNullOrWhiteSpace(email)) email = "(cuenta Gmail)";

        var con = await _db.GmailEnvioConexiones.FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);
        if (con is null)
        {
            con = new GmailEnvioConexion { TenantId = tenantId.Value };
            _db.GmailEnvioConexiones.Add(con);
        }
        con.Email = email!;
        con.RefreshTokenEncrypted = _secret.Protect(refreshToken!);
        con.IsEnabled = true;
        con.ConectadoAt = DateTimeOffset.UtcNow;
        con.ConectadoPorUsuarioId = ActorId();
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task DesconectarAsync(CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return;
        var con = await _db.GmailEnvioConexiones.FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);
        if (con is null) return;
        _db.GmailEnvioConexiones.Remove(con);
        await _db.SaveChangesAsync(ct);
    }

    private Guid? ActorId()
        => Guid.TryParse(_http.HttpContext?.User?.FindFirst("user_id")?.Value, out var g) ? g : null;

    private static string? EmailDeIdToken(string? idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken)) return null;
        var parts = idToken.Split('.');
        if (parts.Length < 2) return null;
        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4) { case 2: payload += "=="; break; case 3: payload += "="; break; }
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("email", out var e) ? e.GetString() : null;
        }
        catch { return null; }
    }
}
