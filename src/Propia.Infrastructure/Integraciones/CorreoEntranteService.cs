using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeKit;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Application.Integraciones;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;

namespace Propia.Infrastructure.Integraciones;

/// <summary>
/// Lector de buzon (Ola 1). Lee por IMAP la bandeja de entrada de la copropiedad con la MISMA cuenta y contrasena de
/// aplicacion del correo saliente (TenantEmailConfig), en SOLO LECTURA (no marca leidos, no mueve, no borra), guarda
/// cada correo nuevo con sus documentos adjuntos y le pide al agente "Coordinador de correo" el tema. En Ola 1 no se
/// ejecuta ninguna accion: la bandeja "Correos recibidos" muestra lo que el agente propone.
/// </summary>
public sealed class CorreoEntranteService : ICorreoEntranteService
{
    /// <summary>Correos que se bajan por revision (los demas quedan para el siguiente ciclo).</summary>
    public const int MaxPorRevision = 25;
    private const int MaxAdjuntos = 10;
    private const long MaxBytesAdjunto = 15L * 1024 * 1024;
    private const int MaxCuerpoGuardado = 20000;

    // Una sola revision a la vez por copropiedad (job + boton "Revisar ahora" en el mismo proceso del API).
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Candados = new();

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ISecretProtector _secret;
    private readonly IAgentesSistema _agentes;
    private readonly IAiInferenceService _inference;
    private readonly IBlobStorage _blob;
    private readonly ILogger<CorreoEntranteService> _log;

    public CorreoEntranteService(PropiaDbContext db, ITenantContext tenant, ISecretProtector secret, IAgentesSistema agentes,
        IAiInferenceService inference, IBlobStorage blob, ILogger<CorreoEntranteService> log)
    {
        _db = db; _tenant = tenant; _secret = secret; _agentes = agentes; _inference = inference; _blob = blob; _log = log;
    }

    // ---------------------------------------------------------------- config

    private Task<TenantEmailConfig?> CfgAsync(bool tracking, CancellationToken ct)
    {
        var tid = _tenant.CurrentTenantId;
        var q = tracking ? _db.TenantEmailConfigs : _db.TenantEmailConfigs.AsNoTracking();
        return q.Where(c => c.TenantId == tid).OrderBy(c => c.CreatedAt).FirstOrDefaultAsync(ct);
    }

    public async Task<CorreoLecturaConfigDto> GetConfigAsync(CancellationToken ct = default)
    {
        var c = await CfgAsync(false, ct);
        if (c is null) return new CorreoLecturaConfigDto(null, false, "imap.gmail.com", 993, false, null, null, null);
        return new CorreoLecturaConfigDto(c.SmtpUser, !string.IsNullOrEmpty(c.SmtpPasswordEncrypted),
            string.IsNullOrWhiteSpace(c.ImapHost) ? "imap.gmail.com" : c.ImapHost!, c.ImapPort <= 0 ? 993 : c.ImapPort,
            c.LecturaActiva, c.LecturaDesde, c.LecturaUltimaRevision, c.LecturaUltimoError);
    }

    public async Task<string?> GuardarConfigAsync(GuardarCorreoLecturaRequest req, CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is null) return "No hay copropiedad activa.";
        var c = await CfgAsync(true, ct);
        if (c is null)
        {
            c = new TenantEmailConfig { IsEnabled = false };
            _db.TenantEmailConfigs.Add(c);
        }

        if (req.Cuenta is not null)
        {
            var cuenta = req.Cuenta.Trim();
            if (cuenta.Length == 0 || !cuenta.Contains('@')) return Rechazar("Escribe el correo de la cuenta (ej. copropiedad@gmail.com).");
            if (!string.Equals(cuenta, c.SmtpUser, StringComparison.OrdinalIgnoreCase))
            {
                // Otra cuenta = otro buzon: el avance por UID de la anterior no sirve.
                c.LecturaUltimoUid = null; c.LecturaUidValidity = null;
                if (string.IsNullOrWhiteSpace(c.FromEmail) || string.Equals(c.FromEmail, c.SmtpUser, StringComparison.OrdinalIgnoreCase))
                    c.FromEmail = cuenta;
            }
            c.SmtpUser = cuenta;
        }
        if (!string.IsNullOrWhiteSpace(req.Password))
            c.SmtpPasswordEncrypted = _secret.Protect(req.Password.Replace(" ", "").Trim());
        if (!string.IsNullOrWhiteSpace(req.ImapHost)) c.ImapHost = req.ImapHost.Trim();
        if (req.ImapPort is int p && p > 0) c.ImapPort = p;

        if (req.LecturaActiva is bool activa && activa != c.LecturaActiva)
        {
            if (activa)
            {
                if (string.IsNullOrWhiteSpace(c.SmtpUser) || string.IsNullOrEmpty(c.SmtpPasswordEncrypted))
                    return Rechazar("Para leer el buz\u00f3n primero guarda la cuenta y la contrase\u00f1a de aplicaci\u00f3n.");
                // Se leen solo los correos que lleguen desde ahora (no todo el historico del buzon).
                c.LecturaDesde = DateTimeOffset.UtcNow;
                c.LecturaUltimoUid = null; c.LecturaUidValidity = null; c.LecturaUltimoError = null;
            }
            c.LecturaActiva = activa;
        }
        await _db.SaveChangesAsync(ct);
        return null;

        // Un error no deja cambios pendientes en el contexto (si no, el siguiente guardado los arrastraria).
        string Rechazar(string error)
        {
            var entrada = _db.Entry(c);
            if (entrada.State == EntityState.Added) entrada.State = EntityState.Detached;
            else entrada.Reload();
            return error;
        }
    }

    public async Task<(bool Ok, string? Error)> ProbarConexionAsync(CancellationToken ct = default)
    {
        var c = await CfgAsync(false, ct);
        var (cred, err) = Credenciales(c);
        if (cred is null) return (false, err);
        try
        {
            using var client = await ConectarAsync(c!, cred.Value.User, cred.Value.Pass, ct);
            await client.Inbox.OpenAsync(FolderAccess.ReadOnly, ct);
            await client.DisconnectAsync(true, ct);
            return (true, null);
        }
        catch (Exception ex) { return (false, ErrorLegible(ex)); }
    }

    // ---------------------------------------------------------------- revision

    public async Task<RevisionBuzonResult> RevisarAsync(CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return new RevisionBuzonResult(false, 0, 0, 0, "No hay copropiedad activa.");
        var candado = Candados.GetOrAdd(tid, _ => new SemaphoreSlim(1, 1));
        if (!await candado.WaitAsync(0, ct))
            return new RevisionBuzonResult(false, 0, 0, 0, "Ya hay una revisi\u00f3n del buz\u00f3n en curso. Intenta en un momento.");
        try
        {
            var cfg = await CfgAsync(true, ct);
            var (cred, err) = Credenciales(cfg);
            if (cred is null) return new RevisionBuzonResult(false, 0, 0, 0, err);

            int nuevos = 0;
            string? errorImap = null;
            try
            {
                nuevos = await DescargarNuevosAsync(cfg!, cred.Value.User, cred.Value.Pass, tid, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errorImap = ErrorLegible(ex);
                _log.LogWarning(ex, "[Buzon] Error leyendo IMAP de la copropiedad {Tenant}", tid);
            }

            // Clasifica lo pendiente (lo recien bajado y lo que haya quedado sin clasificar antes).
            var (clasificados, conError) = await ClasificarPendientesAsync(ct);

            cfg!.LecturaUltimaRevision = DateTimeOffset.UtcNow;
            cfg.LecturaUltimoError = errorImap;
            await _db.SaveChangesAsync(ct);
            return new RevisionBuzonResult(errorImap is null, nuevos, clasificados, conError, errorImap);
        }
        finally { candado.Release(); }
    }

    public async Task<int> RevisarTodasAsync(CancellationToken ct = default)
    {
        // Sin request: se itera copropiedad por copropiedad con SetTenant + CloseConnection (RLS con propia_app).
        var tenantIds = await _db.Tenants.AsNoTracking().Select(t => t.Id).ToListAsync(ct);
        int total = 0;
        try
        {
            foreach (var tid in tenantIds)
            {
                try
                {
                    _tenant.SetTenant(tid);
                    await _db.Database.CloseConnectionAsync();
                    var activa = await _db.TenantEmailConfigs.AsNoTracking().AnyAsync(c => c.TenantId == tid && c.LecturaActiva, ct);
                    if (!activa) continue;
                    var r = await RevisarAsync(ct);
                    total += r.Nuevos;
                    if (r.Nuevos > 0 || !r.Ok)
                        _log.LogInformation("[Buzon] Copropiedad {Tenant}: {Nuevos} nuevo(s), {Clas} clasificado(s), {Err} con error. {Error}",
                            tid, r.Nuevos, r.Clasificados, r.ConError, r.Error);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _db.ChangeTracker.Clear();
                    _log.LogWarning(ex, "[Buzon] Fallo revisando la copropiedad {Tenant}", tid);
                }
            }
        }
        finally
        {
            _tenant.Clear();
            await _db.Database.CloseConnectionAsync();
        }
        return total;
    }

    private async Task<int> DescargarNuevosAsync(TenantEmailConfig cfg, string user, string pass, Guid tid, CancellationToken ct)
    {
        using var client = await ConectarAsync(cfg, user, pass, ct);
        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, ct);   // EXAMINE: no cambia banderas del buzon del cliente
        var validity = (long)inbox.UidValidity;

        List<UniqueId> candidatos;
        if (cfg.LecturaUidValidity == validity && cfg.LecturaUltimoUid is long ultimo)
        {
            var rango = new UniqueIdRange(new UniqueId(inbox.UidValidity, (uint)Math.Min(ultimo + 1, uint.MaxValue)), UniqueId.MaxValue);
            candidatos = (await inbox.SearchAsync(SearchQuery.Uids(rango), ct)).Where(u => u.Id > ultimo).ToList();
        }
        else
        {
            // Primera lectura (o el servidor renumero la bandeja): desde la fecha en que se activo la lectura.
            var desde = cfg.LecturaDesde ?? DateTimeOffset.UtcNow;
            var porFecha = await inbox.SearchAsync(SearchQuery.DeliveredAfter(desde.UtcDateTime.Date.AddDays(-1)), ct);
            candidatos = new List<UniqueId>();
            if (porFecha.Count > 0)
            {
                var resumen = await inbox.FetchAsync(porFecha, MessageSummaryItems.UniqueId | MessageSummaryItems.InternalDate, ct);
                candidatos = resumen.Where(s => s.InternalDate is null || s.InternalDate >= desde.AddMinutes(-5))
                    .Select(s => s.UniqueId).ToList();
            }
            cfg.LecturaUidValidity = validity;
            // Sin correos desde la activacion: la linea base es el UID mas alto actual (los siguientes se piden por UID).
            if (candidatos.Count == 0 && inbox.UidNext is UniqueId sig && sig.Id > 0) cfg.LecturaUltimoUid = sig.Id - 1;
            await _db.SaveChangesAsync(ct);
        }

        int nuevos = 0;
        foreach (var uid in candidatos.OrderBy(u => u.Id).Take(MaxPorRevision))
        {
            ct.ThrowIfCancellationRequested();
            var msg = await inbox.GetMessageAsync(uid, ct);
            var messageId = string.IsNullOrWhiteSpace(msg.MessageId) ? $"uid:{validity}:{uid.Id}" : msg.MessageId.Trim();
            if (messageId.Length > 500) messageId = messageId[..500];

            var existe = await _db.CorreosRecibidos.AnyAsync(x => x.MessageId == messageId, ct);
            if (!existe)
            {
                var correo = await ConstruirAsync(msg, uid, messageId, tid, ct);
                _db.CorreosRecibidos.Add(correo);
                nuevos++;
            }
            cfg.LecturaUltimoUid = uid.Id;
            cfg.LecturaUidValidity = validity;
            await _db.SaveChangesAsync(ct);
        }
        await client.DisconnectAsync(true, ct);
        return nuevos;
    }

    private async Task<CorreoRecibido> ConstruirAsync(MimeMessage msg, UniqueId uid, string messageId, Guid tid, CancellationToken ct)
    {
        var de = msg.From.Mailboxes.FirstOrDefault();
        var correo = new CorreoRecibido
        {
            MessageId = messageId,
            ImapUid = uid.Id,
            RemitenteCorreo = Corta(de?.Address, 320),
            RemitenteNombre = Corta(de?.Name, 200),
            Para = Corta(string.Join(", ", msg.To.Mailboxes.Select(m => m.Address)), 1000),
            Asunto = Corta(msg.Subject, 500),
            FechaCorreo = msg.Date == DateTimeOffset.MinValue ? DateTimeOffset.UtcNow : msg.Date.ToUniversalTime(),
            Cuerpo = Corta(TextoPlano(msg), MaxCuerpoGuardado),
            Estado = EstadoCorreoRecibido.Nuevo,
        };

        var guardados = new List<CorreoAdjuntoGuardado>();
        int i = 0;
        foreach (var parte in msg.Attachments.OfType<MimePart>())
        {
            if (guardados.Count >= MaxAdjuntos) break;
            i++;
            if (parte.Content is null) continue;
            var nombre = string.IsNullOrWhiteSpace(parte.FileName) ? $"adjunto-{i}" : parte.FileName.Trim();
            try
            {
                using var ms = new MemoryStream();
                await parte.Content.DecodeToAsync(ms, ct);
                if (ms.Length == 0 || ms.Length > MaxBytesAdjunto) continue;
                ms.Position = 0;
                var mime = parte.ContentType?.MimeType ?? "application/octet-stream";
                var ruta = $"tenants/{tid}/correos/{correo.Id}/{i}-{NombreSeguro(nombre)}";
                await _blob.UploadAsync(ruta, ms, mime, ct);
                guardados.Add(new CorreoAdjuntoGuardado(Corta(nombre, 255)!, mime, ms.Length, ruta));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "[Buzon] No se pudo guardar el adjunto {Nombre} del correo {MessageId}", nombre, messageId);
            }
        }
        if (guardados.Count > 0) correo.AdjuntosJson = JsonSerializer.Serialize(guardados, Json);
        return correo;
    }

    // ---------------------------------------------------------------- clasificacion

    private async Task<(int Ok, int Error)> ClasificarPendientesAsync(CancellationToken ct)
    {
        var pendientes = await _db.CorreosRecibidos
            .Where(x => x.Estado == EstadoCorreoRecibido.Nuevo)
            .OrderBy(x => x.FechaCorreo).Take(MaxPorRevision).ToListAsync(ct);
        int ok = 0, err = 0;
        foreach (var c in pendientes)
        {
            if (await ClasificarAsync(c, ct)) ok++; else err++;
            await _db.SaveChangesAsync(ct);
        }
        return (ok, err);
    }

    private async Task<bool> ClasificarAsync(CorreoRecibido c, CancellationToken ct)
    {
        var agente = await _agentes.AsegurarAsync(PlantillaAgente.ClaveCoordinadorCorreo, ct);
        if (agente is null) return Fallo(c, "No existe la plantilla del Coordinador de correo en el Super Admin.");
        if (!agente.Activo) return Fallo(c, "El agente Coordinador de correo est\u00e1 inactivo en esta copropiedad (Agentes de IA).");

        var adjuntos = LeerAdjuntos(c.AdjuntosJson).Select(a => a.Nombre);
        var prompt = CoordinadorCorreoAgente.ArmarPrompt(c.RemitenteNombre, c.RemitenteCorreo, c.Asunto, c.FechaCorreo, adjuntos, c.Cuerpo);
        AiChatResult res;
        try
        {
            res = await _inference.TestChatAsync(agente.Id, new[] { new AiChatTurn("user", prompt) }, ct: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "[Buzon] Error clasificando el correo {Id}", c.Id);
            return Fallo(c, "No se pudo consultar al agente. Intenta reclasificar.");
        }
        if (!res.Ok) return Fallo(c, Corta(res.Error, 1000) ?? "El agente no respondi\u00f3.");

        var cl = CoordinadorCorreoAgente.Parsear(res.Text);
        if (cl is null)
        {
            _log.LogWarning("[Buzon] Respuesta no interpretable del coordinador para {Id}: {Texto}", c.Id, Corta(res.Text, 300));
            return Fallo(c, "El agente respondi\u00f3 en un formato que no se pudo leer. Intenta reclasificar.");
        }
        c.Tema = cl.Tema;
        c.Confianza = cl.Confianza;
        c.Resumen = cl.Resumen;
        c.AccionSugerida = cl.AccionSugerida;
        c.DatosJson = cl.DatosJson;
        c.Estado = EstadoCorreoRecibido.Clasificado;
        c.Error = null;
        c.ClasificadoEn = DateTimeOffset.UtcNow;
        return true;
    }

    private static bool Fallo(CorreoRecibido c, string error)
    {
        c.Estado = EstadoCorreoRecibido.Error;
        c.Error = error;
        return false;
    }

    // ---------------------------------------------------------------- bandeja

    public async Task<IReadOnlyList<CorreoRecibidoDto>> ListarAsync(CancellationToken ct = default)
    {
        var filas = await _db.CorreosRecibidos.AsNoTracking()
            .OrderByDescending(x => x.FechaCorreo).Take(1000).ToListAsync(ct);
        return filas.Select(ADto).ToList();
    }

    public async Task<CorreoRecibidoDetalleDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        var x = await _db.CorreosRecibidos.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (x is null) return null;
        var adj = LeerAdjuntos(x.AdjuntosJson)
            .Select(a => new CorreoAdjuntoDto(a.Nombre, a.Mime, a.Tamano, _blob.ResolveUrl(a.Ruta))).ToList();
        return new CorreoRecibidoDetalleDto(x.Id, x.FechaCorreo, x.RemitenteCorreo, x.RemitenteNombre, x.Para, x.Asunto,
            x.Cuerpo, adj, x.Tema, x.Confianza, x.Resumen, x.AccionSugerida, x.DatosJson, x.Estado, x.Error, x.ClasificadoEn);
    }

    public async Task<CorreoRecibidoDto?> ReclasificarAsync(Guid id, CancellationToken ct = default)
    {
        var x = await _db.CorreosRecibidos.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (x is null) return null;
        await ClasificarAsync(x, ct);
        await _db.SaveChangesAsync(ct);
        return ADto(x);
    }

    public async Task<CorreoRecibidoDto?> DescartarAsync(Guid id, bool descartar, CancellationToken ct = default)
    {
        var x = await _db.CorreosRecibidos.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (x is null) return null;
        if (descartar) x.Estado = EstadoCorreoRecibido.Descartado;
        else if (x.Estado == EstadoCorreoRecibido.Descartado)
            x.Estado = x.ClasificadoEn is null ? EstadoCorreoRecibido.Nuevo : EstadoCorreoRecibido.Clasificado;
        await _db.SaveChangesAsync(ct);
        return ADto(x);
    }

    private static CorreoRecibidoDto ADto(CorreoRecibido x) => new(x.Id, x.FechaCorreo, x.RemitenteCorreo, x.RemitenteNombre,
        x.Asunto, x.Tema, x.Confianza, x.Resumen, x.AccionSugerida, x.Estado, x.Error, LeerAdjuntos(x.AdjuntosJson).Count);

    // ---------------------------------------------------------------- utilidades

    private (( string User, string Pass)? Cred, string? Error) Credenciales(TenantEmailConfig? c)
    {
        if (c is null || string.IsNullOrWhiteSpace(c.SmtpUser)) return (null, "Falta configurar la cuenta de correo.");
        if (string.IsNullOrEmpty(c.SmtpPasswordEncrypted)) return (null, "Falta la contrase\u00f1a de aplicaci\u00f3n.");
        try { return ((c.SmtpUser!.Trim(), _secret.Unprotect(c.SmtpPasswordEncrypted)), null); }
        catch { return (null, "La contrase\u00f1a guardada no se pudo descifrar. Vuelve a guardarla."); }
    }

    private static async Task<ImapClient> ConectarAsync(TenantEmailConfig c, string user, string pass, CancellationToken ct)
    {
        var host = string.IsNullOrWhiteSpace(c.ImapHost) ? "imap.gmail.com" : c.ImapHost!.Trim();
        var port = c.ImapPort <= 0 ? 993 : c.ImapPort;
        var client = new ImapClient { Timeout = 30000 };
        try
        {
            await client.ConnectAsync(host, port, port == 993 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable, ct);
            await client.AuthenticateAsync(user, pass, ct);
            return client;
        }
        catch { client.Dispose(); throw; }
    }

    private static string ErrorLegible(Exception ex) => ex switch
    {
        AuthenticationException => "El servidor de correo rechaz\u00f3 la cuenta o la contrase\u00f1a de aplicaci\u00f3n. Revisa que sea una contrase\u00f1a de aplicaci\u00f3n vigente (no la clave normal de Gmail).",
        ImapProtocolException or ImapCommandException => "El servidor de correo respondi\u00f3 con un error: " + ex.Message,
        System.Net.Sockets.SocketException or TimeoutException or IOException => "No se pudo conectar con el servidor de correo. Revisa el host y el puerto IMAP.",
        _ => "No se pudo leer el buz\u00f3n: " + ex.Message
    };

    internal static List<CorreoAdjuntoGuardado> LeerAdjuntos(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<List<CorreoAdjuntoGuardado>>(json, Json) ?? new(); }
        catch { return new(); }
    }

    private static string? TextoPlano(MimeMessage msg)
    {
        if (!string.IsNullOrWhiteSpace(msg.TextBody)) return msg.TextBody.Trim();
        if (string.IsNullOrWhiteSpace(msg.HtmlBody)) return null;
        var h = Regex.Replace(msg.HtmlBody, @"<(script|style|head)[^>]*>.*?</\1>", " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        h = Regex.Replace(h, @"<(br|/p|/div|/tr|/li|/h[1-6])[^>]*>", "\n", RegexOptions.IgnoreCase);
        h = Regex.Replace(h, "<[^>]+>", " ");
        h = WebUtility.HtmlDecode(h);
        h = Regex.Replace(h, @"[ \t\u00a0]+", " ");
        h = Regex.Replace(h, @"\s*\n\s*(\n\s*)+", "\n\n");
        return h.Trim();
    }

    private static string NombreSeguro(string nombre)
    {
        var limpio = Regex.Replace(nombre, @"[^A-Za-z0-9._-]+", "_").Trim('_');
        if (limpio.Length == 0) limpio = "adjunto";
        return limpio.Length <= 80 ? limpio : limpio[^80..];
    }

    private static string? Corta(string? s, int max)
    {
        var t = s?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        return t.Length <= max ? t : t[..max];
    }
}
