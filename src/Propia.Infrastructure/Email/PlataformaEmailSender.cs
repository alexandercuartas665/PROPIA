using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Propia.Application.Common;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Email;

/// <summary>
/// Correo saliente de la plataforma con la configuracion global (cifrada) del Super Admin (EmailConfig).
/// Dos proveedores: "smtp" (MailKit) y "sendgrid_api" (SendGrid Web API v3 por HTTPS 443, la salida que
/// funciona cuando el hosting bloquea SMTP). Ambos con limite de tiempo duro (Email:TimeoutSegundos, 20 s):
/// nunca cuelgan el request. Best-effort: devuelve el error, no lanza. No persiste ni loggea la clave.
/// </summary>
public sealed class PlataformaEmailSender : IEmailSender, IEmailDiagnostico
{
    public const string HttpClientSendGrid = "SendGridApi";

    private readonly PropiaDbContext _db;
    private readonly ISecretProtector _secretProtector;
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly ILogger<PlataformaEmailSender> _log;

    public PlataformaEmailSender(PropiaDbContext db, ISecretProtector secretProtector, IHttpClientFactory http,
        IConfiguration config, ILogger<PlataformaEmailSender> log)
    {
        _db = db;
        _secretProtector = secretProtector;
        _http = http;
        _config = config;
        _log = log;
    }

    public async Task<EmailSendResult> SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var r = await EnviarAsync(toEmail, subject, htmlBody, cancellationToken);
        return new EmailSendResult(r.Ok, r.Error);
    }

    public async Task<EmailProbeResult> ProbarAsync(string toEmail, CancellationToken cancellationToken = default)
    {
        var r = await EnviarAsync(toEmail, "PROPIA - Correo de prueba",
            "<p>Este es un correo de prueba enviado desde la consola de administración de PROPIA. "
            + "Si lo recibes, el correo saliente de la plataforma funciona.</p>", cancellationToken);
        if (r.Ok)
        {
            var cfg = await _db.EmailConfigs.FirstOrDefaultAsync(cancellationToken);
            if (cfg is not null) { cfg.LastValidatedAt = DateTimeOffset.UtcNow; await _db.SaveChangesAsync(cancellationToken); }
        }
        return r;
    }

    private async Task<EmailProbeResult> EnviarAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
    {
        var cfg = await _db.EmailConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        var proveedor = string.IsNullOrWhiteSpace(cfg?.Proveedor) ? ProveedorCorreo.Smtp : cfg!.Proveedor;
        if (cfg is null || !cfg.IsEnabled)
            return new EmailProbeResult(false, proveedor, null, null, 0, "El correo saliente no esta configurado/habilitado en la plataforma.");
        if (string.IsNullOrWhiteSpace(cfg.FromEmail))
            return new EmailProbeResult(false, proveedor, null, null, 0, "Falta configurar la direccion remitente.");

        string? clave = null;
        if (!string.IsNullOrEmpty(cfg.SmtpPasswordEncrypted))
        {
            try { clave = _secretProtector.Unprotect(cfg.SmtpPasswordEncrypted); }
            catch { return new EmailProbeResult(false, proveedor, null, null, 0, "La clave de correo esta cifrada con una version anterior. Vuelve a guardarla."); }
        }

        // El envio no debe correr dentro de una transaccion de BD (la dejaria abierta hasta el limite).
        if (_db.Database.CurrentTransaction is not null)
            _log.LogWarning("Envio de correo dentro de una transaccion de BD abierta (proveedor {Proveedor}); debe enviarse despues del commit.", proveedor);

        var limite = EnvioSmtp.Limite(_config);
        var sw = Stopwatch.StartNew();
        if (proveedor == ProveedorCorreo.SendGridApi)
        {
            var host = "api.sendgrid.com";
            if (string.IsNullOrWhiteSpace(clave))
                return new EmailProbeResult(false, proveedor, host, 443, 0, "Falta la API key de SendGrid.");
            var error = await EnviarSendGridAsync(cfg, clave, toEmail, subject, htmlBody, limite, ct);
            return Resultado(proveedor, host, 443, sw, error);
        }

        if (string.IsNullOrWhiteSpace(cfg.SmtpHost))
            return new EmailProbeResult(false, proveedor, null, cfg.SmtpPort, 0, "Falta configurar el host SMTP.");
        string? err = null;
        try
        {
            var msg = EnvioSmtp.Mensaje(cfg.FromEmail, cfg.FromName, toEmail, subject, htmlBody, html: true);
            await EnvioSmtp.EnviarAsync(cfg.SmtpHost, cfg.SmtpPort, cfg.UseSsl, cfg.SmtpUser, clave, msg, limite, ct);
        }
        catch (TimeoutException ex) { err = ex.Message; }
        catch (MailKit.Net.Smtp.SmtpCommandException ex) { err = $"SMTP fallo ({(int)ex.StatusCode}): {ex.Message}"; }
        catch (MailKit.Security.AuthenticationException ex) { err = "SMTP rechazo el usuario/clave: " + ex.Message; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            var inner = ex.InnerException?.Message;
            err = "No se pudo enviar el correo: " + (inner is null ? ex.Message : $"{ex.Message} - {inner}");
        }
        return Resultado(proveedor, cfg.SmtpHost, cfg.SmtpPort, sw, err);
    }

    private EmailProbeResult Resultado(string proveedor, string host, int puerto, Stopwatch sw, string? error)
    {
        sw.Stop();
        if (error is not null)
            _log.LogWarning("Correo saliente fallo via {Proveedor} {Host}:{Puerto} en {Ms} ms: {Error}", proveedor, host, puerto, sw.ElapsedMilliseconds, error);
        return new EmailProbeResult(error is null, proveedor, host, puerto, sw.ElapsedMilliseconds, error);
    }

    /// <summary>SendGrid Web API v3 (POST /v3/mail/send, 202 = aceptado). Devuelve null si salio, o el error.</summary>
    private async Task<string?> EnviarSendGridAsync(EmailConfig cfg, string apiKey, string to, string subject, string html,
        TimeSpan limite, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(limite);
        try
        {
            var http = _http.CreateClient(HttpClientSendGrid);
            var baseUrl = (_config["Email:SendGridApiBaseUrl"] ?? "https://api.sendgrid.com").TrimEnd('/');
            using var req = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/v3/mail/send");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            req.Content = JsonContent.Create(new
            {
                personalizations = new[] { new { to = new[] { new { email = to.Trim() } } } },
                from = new { email = cfg.FromEmail, name = cfg.FromName ?? cfg.FromEmail },
                subject,
                content = new[] { new { type = "text/html", value = html } }
            });
            using var resp = await http.SendAsync(req, cts.Token);
            if (resp.IsSuccessStatusCode) return null;
            var cuerpo = await resp.Content.ReadAsStringAsync(cts.Token);
            if (cuerpo.Length > 400) cuerpo = cuerpo[..400];
            return $"SendGrid API respondio {(int)resp.StatusCode}: {cuerpo}";
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return $"SendGrid API timeout (api.sendgrid.com:443) tras {limite.TotalSeconds:0} s";
        }
        catch (HttpRequestException ex)
        {
            return "SendGrid API no respondio: " + ex.Message;
        }
    }
}
