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
using Propia.Application.Catalogos;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Application.Integraciones;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;

namespace Propia.Infrastructure.Integraciones;

/// <summary>
/// Lector de buzon. Lee por IMAP la bandeja de entrada de cada buzon de la copropiedad (<see cref="BuzonCorreo"/>, con
/// contrasena de aplicacion) en SOLO LECTURA (no marca leidos, no mueve, no borra), guarda cada correo nuevo con sus
/// documentos adjuntos y le pide al agente "Coordinador de correo" el tema, escogiendo entre los temas de la
/// copropiedad (lista "correo.tema") que atiende ese buzon. Aun no se ejecuta ninguna accion: la bandeja "Correos
/// recibidos" muestra lo que el agente propone.
/// </summary>
public sealed class CorreoEntranteService : ICorreoEntranteService
{
    /// <summary>Correos que se bajan por buzon en cada revision (los demas quedan para el siguiente ciclo).</summary>
    public const int MaxPorRevision = 25;
    private const int MaxAdjuntos = 10;
    private const long MaxBytesAdjunto = 15L * 1024 * 1024;
    private const int MaxCuerpoGuardado = 20000;
    private const int RecientesPorBuzon = 12;
    private static readonly TimeSpan OffsetColombia = TimeSpan.FromHours(-5);

    // Una sola revision a la vez por copropiedad (job + boton "Revisar ahora" en el mismo proceso del API).
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Candados = new();

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ISecretProtector _secret;
    private readonly IAgentesSistema _agentes;
    private readonly IAiInferenceService _inference;
    private readonly IBlobStorage _blob;
    private readonly IOpcionesEfectivas _opciones;
    private readonly ICorreoPqrsdService? _pqrsd;
    private readonly ILogger<CorreoEntranteService> _log;

    public CorreoEntranteService(PropiaDbContext db, ITenantContext tenant, ISecretProtector secret, IAgentesSistema agentes,
        IAiInferenceService inference, IBlobStorage blob, IOpcionesEfectivas opciones, ILogger<CorreoEntranteService> log,
        ICorreoPqrsdService? pqrsd = null)
    {
        _db = db; _tenant = tenant; _secret = secret; _agentes = agentes; _inference = inference; _blob = blob;
        _opciones = opciones; _log = log; _pqrsd = pqrsd;
    }

    // ---------------------------------------------------------------- buzones

    public async Task<IReadOnlyList<BuzonCorreoDto>> ListarBuzonesAsync(CancellationToken ct = default)
    {
        var buzones = await _db.BuzonesCorreo.AsNoTracking().OrderBy(b => b.CreatedAt).ToListAsync(ct);
        var res = new List<BuzonCorreoDto>(buzones.Count);
        foreach (var b in buzones) res.Add(await ADtoAsync(b, ct));
        return res;
    }

    private async Task<BuzonCorreoDto> ADtoAsync(BuzonCorreo b, CancellationToken ct)
    {
        var ahora = DateTimeOffset.UtcNow.ToOffset(OffsetColombia);
        var inicioHoy = new DateTimeOffset(ahora.Date, OffsetColombia).ToUniversalTime();
        var q = _db.CorreosRecibidos.AsNoTracking().Where(c => c.BuzonId == b.Id);
        var total = await q.CountAsync(ct);
        var hoy = await q.CountAsync(c => c.CreatedAt >= inicioHoy, ct);
        var pendientes = await q.CountAsync(c => c.Estado == EstadoCorreoRecibido.Nuevo || c.Estado == EstadoCorreoRecibido.Error, ct);
        var recientes = await q.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.FechaCorreo).Take(RecientesPorBuzon)
            .Select(c => new CorreoRecienteDto(c.Id, c.Asunto, c.TemaClave, c.Estado, c.FechaCorreo)).ToListAsync(ct);
        return new BuzonCorreoDto(b.Id, b.Nombre, b.Cuenta, !string.IsNullOrEmpty(b.PasswordCifrada),
            string.IsNullOrWhiteSpace(b.ImapHost) ? "imap.gmail.com" : b.ImapHost, b.ImapPort <= 0 ? 993 : b.ImapPort, b.Activo,
            b.LecturaDesde, b.UltimaRevision, b.UltimoError, LeerTemas(b.TemasJson), hoy, total, pendientes, recientes,
            b.UmbralAutoPqrsd, b.ResponderRadicado, b.PlantillaAcuseId);
    }

    public async Task<(BuzonCorreoDto? Buzon, string? Error)> CrearBuzonAsync(GuardarBuzonRequest req, CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is null) return (null, "No hay copropiedad activa.");
        if (string.IsNullOrWhiteSpace(req.Cuenta)) return (null, "Escribe el correo de la cuenta (ej. copropiedad@gmail.com).");
        var b = new BuzonCorreo();
        var error = await AplicarAsync(b, req, nuevo: true, ct);
        if (error is not null) return (null, error);
        _db.BuzonesCorreo.Add(b);
        await _db.SaveChangesAsync(ct);
        return (await ADtoAsync(b, ct), null);
    }

    public async Task<(BuzonCorreoDto? Buzon, string? Error)> GuardarBuzonAsync(Guid id, GuardarBuzonRequest req, CancellationToken ct = default)
    {
        var b = await _db.BuzonesCorreo.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b is null) return (null, null);
        var error = await AplicarAsync(b, req, nuevo: false, ct);
        if (error is not null)
        {
            // Un error no deja cambios pendientes en el contexto (si no, el siguiente guardado los arrastraria).
            await _db.Entry(b).ReloadAsync(ct);
            return (null, error);
        }
        await _db.SaveChangesAsync(ct);
        return (await ADtoAsync(b, ct), null);
    }

    // Aplica la peticion sobre el buzon (merge) y valida. Devuelve un error legible o null.
    private async Task<string?> AplicarAsync(BuzonCorreo b, GuardarBuzonRequest req, bool nuevo, CancellationToken ct)
    {
        if (req.Cuenta is not null)
        {
            var cuenta = req.Cuenta.Trim();
            if (cuenta.Length == 0 || !cuenta.Contains('@') || cuenta.Length > 320)
                return "Escribe el correo de la cuenta (ej. copropiedad@gmail.com).";
            var cuentaMin = cuenta.ToLower();
            var repetida = await _db.BuzonesCorreo.AsNoTracking()
                .AnyAsync(x => x.Id != b.Id && x.Cuenta.ToLower() == cuentaMin, ct);
            if (repetida) return "Esa cuenta ya est\u00e1 configurada en otro buz\u00f3n.";
            if (!string.Equals(cuenta, b.Cuenta, StringComparison.OrdinalIgnoreCase))
            {
                // Otra cuenta = otro buzon: el avance por UID de la anterior no sirve.
                b.UltimoUid = null; b.UidValidity = null;
                if (b.Activo) b.LecturaDesde = DateTimeOffset.UtcNow;
            }
            b.Cuenta = cuenta;
        }
        if (req.Nombre is not null) b.Nombre = Corta(req.Nombre, 120) ?? "";
        if (string.IsNullOrWhiteSpace(b.Nombre)) b.Nombre = Corta(b.Cuenta.Split('@')[0], 120) ?? "Buz\u00f3n";
        if (!string.IsNullOrWhiteSpace(req.Password))
            b.PasswordCifrada = _secret.Protect(req.Password.Replace(" ", "").Trim());
        if (!string.IsNullOrWhiteSpace(req.ImapHost)) b.ImapHost = Corta(req.ImapHost, 200)!;
        if (req.ImapPort is int p)
        {
            if (p <= 0 || p > 65535) return "El puerto IMAP no es v\u00e1lido.";
            b.ImapPort = p;
        }

        // PQRSD por correo: 0 o menos = nunca radicar solo (null); 0.5 a 1 = confianza minima.
        if (req.UmbralAutoPqrsd is decimal um)
        {
            if (um <= 0) b.UmbralAutoPqrsd = null;
            else if (um < 0.5m || um > 1m) return "El umbral para radicar PQRSD solo debe estar entre 50% y 100%.";
            else b.UmbralAutoPqrsd = Math.Round(um, 3);
        }

        // Ola P2: respuesta de radicado por el buzon y su plantilla (de PQRSD; Guid.Empty = la predeterminada).
        if (req.ResponderRadicado is bool rr) b.ResponderRadicado = rr;
        if (req.PlantillaAcuseId is Guid pl)
        {
            if (pl == Guid.Empty) b.PlantillaAcuseId = null;
            else if (!await _db.PlantillasDocumento.AnyAsync(x => x.Id == pl && x.Categoria == Application.Plantillas.PlantillasCatalogo.Pqrsd, ct))
                return "La plantilla de respuesta no existe o no es de PQRSD.";
            else b.PlantillaAcuseId = pl;
        }

        if (req.TodosLosTemas == true) b.TemasJson = null;
        else if (req.Temas is not null)
        {
            var claves = req.Temas.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            b.TemasJson = claves.Count == 0 ? null : JsonSerializer.Serialize(claves, Json);
        }

        if (req.Activo is bool activo && (nuevo || activo != b.Activo))
        {
            if (activo)
            {
                if (string.IsNullOrWhiteSpace(b.Cuenta) || string.IsNullOrEmpty(b.PasswordCifrada))
                    return "Para leer el buz\u00f3n primero escribe la cuenta y la contrase\u00f1a de aplicaci\u00f3n.";
                // Se leen solo los correos que lleguen desde ahora (no todo el historico del buzon).
                b.LecturaDesde = DateTimeOffset.UtcNow;
                b.UltimoUid = null; b.UidValidity = null; b.UltimoError = null;
            }
            b.Activo = activo;
        }
        return null;
    }

    public async Task<bool> EliminarBuzonAsync(Guid id, CancellationToken ct = default)
    {
        var b = await _db.BuzonesCorreo.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b is null) return false;
        // Los correos ya leidos se quedan en la bandeja, sin buzon.
        var correos = await _db.CorreosRecibidos.Where(c => c.BuzonId == id).ToListAsync(ct);
        foreach (var c in correos) c.BuzonId = null;
        _db.BuzonesCorreo.Remove(b);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<(bool Ok, string? Error)> ProbarConexionAsync(ProbarBuzonRequest req, CancellationToken ct = default)
    {
        BuzonCorreo? guardado = null;
        if (req.BuzonId is Guid id)
            guardado = await _db.BuzonesCorreo.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

        var user = !string.IsNullOrWhiteSpace(req.Cuenta) ? req.Cuenta.Trim() : guardado?.Cuenta;
        if (string.IsNullOrWhiteSpace(user)) return (false, "Escribe el correo de la cuenta.");
        string? pass = !string.IsNullOrWhiteSpace(req.Password) ? req.Password.Replace(" ", "").Trim() : null;
        if (pass is null)
        {
            if (guardado is null || string.IsNullOrEmpty(guardado.PasswordCifrada)) return (false, "Escribe la contrase\u00f1a de aplicaci\u00f3n.");
            try { pass = _secret.Unprotect(guardado.PasswordCifrada); }
            catch { return (false, "La contrase\u00f1a guardada no se pudo descifrar. Vuelve a escribirla."); }
        }
        var host = !string.IsNullOrWhiteSpace(req.ImapHost) ? req.ImapHost.Trim() : guardado?.ImapHost;
        var port = req.ImapPort is int p && p > 0 ? p : guardado?.ImapPort ?? 993;
        try
        {
            using var client = await ConectarAsync(host, port, user, pass, ct);
            await client.Inbox.OpenAsync(FolderAccess.ReadOnly, ct);
            await client.DisconnectAsync(true, ct);
            return (true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return (false, ErrorLegible(ex)); }
    }

    // ---------------------------------------------------------------- revision

    public async Task<RevisionBuzonResult> RevisarAsync(Guid? buzonId = null, CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return new RevisionBuzonResult(false, 0, 0, 0, "No hay copropiedad activa.");
        var candado = Candados.GetOrAdd(tid, _ => new SemaphoreSlim(1, 1));
        if (!await candado.WaitAsync(0, ct))
            return new RevisionBuzonResult(false, 0, 0, 0, "Ya hay una revisi\u00f3n del buz\u00f3n en curso. Intenta en un momento.");
        try
        {
            var q = buzonId is Guid bid ? _db.BuzonesCorreo.Where(b => b.Id == bid) : _db.BuzonesCorreo.Where(b => b.Activo);
            var buzones = await q.OrderBy(b => b.CreatedAt).ToListAsync(ct);

            int nuevos = 0;
            var errores = new List<string>();
            foreach (var b in buzones)
            {
                string? errorImap;
                var (pass, errCred) = Clave(b);
                if (pass is null) errorImap = errCred;
                else
                {
                    errorImap = null;
                    try { nuevos += await DescargarNuevosAsync(b, pass, tid, ct); }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        errorImap = ErrorLegible(ex);
                        _log.LogWarning(ex, "[Buzon] Error leyendo IMAP del buzon {Buzon} (copropiedad {Tenant})", b.Id, tid);
                    }
                }
                b.UltimaRevision = DateTimeOffset.UtcNow;
                b.UltimoError = Corta(errorImap, 1000);
                await _db.SaveChangesAsync(ct);
                if (errorImap is not null) errores.Add(buzones.Count > 1 ? $"{b.Nombre}: {errorImap}" : errorImap);
            }

            // Clasifica lo pendiente (lo recien bajado y lo que haya quedado sin clasificar antes).
            var (clasificados, conError) = await ClasificarPendientesAsync(ct);
            if (buzones.Count == 0 && clasificados == 0 && conError == 0)
                return new RevisionBuzonResult(false, 0, 0, 0, "No hay buzones con la lectura activa.");
            return new RevisionBuzonResult(errores.Count == 0, nuevos, clasificados, conError,
                errores.Count == 0 ? null : string.Join(" ", errores));
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
                    var activa = await _db.BuzonesCorreo.AsNoTracking().AnyAsync(b => b.TenantId == tid && b.Activo, ct);
                    if (!activa) continue;
                    var r = await RevisarAsync(null, ct);
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

    private async Task<int> DescargarNuevosAsync(BuzonCorreo b, string pass, Guid tid, CancellationToken ct)
    {
        using var client = await ConectarAsync(b.ImapHost, b.ImapPort, b.Cuenta, pass, ct);
        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, ct);   // EXAMINE: no cambia banderas del buzon del cliente
        var validity = (long)inbox.UidValidity;

        List<UniqueId> candidatos;
        if (b.UidValidity == validity && b.UltimoUid is long ultimo)
        {
            var rango = new UniqueIdRange(new UniqueId(inbox.UidValidity, (uint)Math.Min(ultimo + 1, uint.MaxValue)), UniqueId.MaxValue);
            candidatos = (await inbox.SearchAsync(SearchQuery.Uids(rango), ct)).Where(u => u.Id > ultimo).ToList();
        }
        else
        {
            // Primera lectura (o el servidor renumero la bandeja): desde la fecha en que se activo la lectura.
            var desde = b.LecturaDesde ?? DateTimeOffset.UtcNow;
            var porFecha = await inbox.SearchAsync(SearchQuery.DeliveredAfter(desde.UtcDateTime.Date.AddDays(-1)), ct);
            candidatos = new List<UniqueId>();
            if (porFecha.Count > 0)
            {
                var resumen = await inbox.FetchAsync(porFecha, MessageSummaryItems.UniqueId | MessageSummaryItems.InternalDate, ct);
                candidatos = resumen.Where(s => s.InternalDate is null || s.InternalDate >= desde.AddMinutes(-5))
                    .Select(s => s.UniqueId).ToList();
            }
            b.UidValidity = validity;
            // Sin correos desde la activacion: la linea base es el UID mas alto actual (los siguientes se piden por UID).
            if (candidatos.Count == 0 && inbox.UidNext is UniqueId sig && sig.Id > 0) b.UltimoUid = sig.Id - 1;
            await _db.SaveChangesAsync(ct);
        }

        int nuevos = 0;
        foreach (var uid in candidatos.OrderBy(u => u.Id).Take(MaxPorRevision))
        {
            ct.ThrowIfCancellationRequested();
            var msg = await inbox.GetMessageAsync(uid, ct);
            var messageId = string.IsNullOrWhiteSpace(msg.MessageId) ? $"uid:{b.Id:N}:{validity}:{uid.Id}" : msg.MessageId.Trim();
            if (messageId.Length > 500) messageId = messageId[..500];

            // Un mismo correo que llega a dos buzones (copia) se procesa una sola vez.
            var existe = await _db.CorreosRecibidos.AnyAsync(x => x.MessageId == messageId, ct);
            if (!existe)
            {
                var correo = await ConstruirAsync(msg, uid, messageId, tid, ct);
                correo.BuzonId = b.Id;
                _db.CorreosRecibidos.Add(correo);
                nuevos++;
            }
            b.UltimoUid = uid.Id;
            b.UidValidity = validity;
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

    // Temas activos de la copropiedad (lista "correo.tema"); si la lista aun no existe, los de fabrica.
    private async Task<IReadOnlyList<TemaCorreoDef>> TemasActivosAsync(CancellationToken ct)
    {
        IReadOnlyList<OpcionEfectivaDto> ops;
        try { ops = await _opciones.OpcionesAsync(TemasCorreoBase.Lista, false, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "[Buzon] No se pudo leer la lista de temas; se usan los de fabrica.");
            ops = Array.Empty<OpcionEfectivaDto>();
        }
        var temas = ops.Where(o => !string.IsNullOrWhiteSpace(o.Clave))
            .Select(o => new TemaCorreoDef(o.Clave, o.Label, CoordinadorCorreoAgente.DescripcionDeMeta(o.Meta), o.Color)).ToList();
        return temas.Count == 0 ? CoordinadorCorreoAgente.TemasBase : temas;
    }

    private async Task<(int Ok, int Error)> ClasificarPendientesAsync(CancellationToken ct)
    {
        var pendientes = await _db.CorreosRecibidos
            .Where(x => x.Estado == EstadoCorreoRecibido.Nuevo)
            .OrderBy(x => x.FechaCorreo).Take(MaxPorRevision * 2).ToListAsync(ct);
        if (pendientes.Count == 0) return (0, 0);
        var activos = await TemasActivosAsync(ct);
        var temasBuzon = await _db.BuzonesCorreo.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.TemasJson, ct);
        var umbrales = await UmbralesAsync(ct);
        int ok = 0, err = 0;
        foreach (var c in pendientes)
        {
            if (await ClasificarAsync(c, TemasPara(c, activos, temasBuzon), ct))
            {
                ok++;
                await _db.SaveChangesAsync(ct);
                await DelegarAsync(c, umbrales, ct);
            }
            else err++;
            await _db.SaveChangesAsync(ct);
        }
        return (ok, err);
    }

    private async Task<Dictionary<Guid, decimal?>> UmbralesAsync(CancellationToken ct)
        => await _db.BuzonesCorreo.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.UmbralAutoPqrsd, ct);

    /// <summary>Tras clasificar: el tema PQRSD pasa al Radicador de PQRSD (propone y, con confianza alta, radica).
    /// Un correo sin buzon (buzon eliminado) no se radica solo.</summary>
    private async Task DelegarAsync(CorreoRecibido c, IReadOnlyDictionary<Guid, decimal?> umbrales, CancellationToken ct)
    {
        if (_pqrsd is null || c.PqrsdExpedienteId is not null) return;
        if (!string.Equals(c.TemaClave, TemasCorreoBase.Pqrsd, StringComparison.OrdinalIgnoreCase)) return;
        var umbral = c.BuzonId is Guid bid && umbrales.TryGetValue(bid, out var u) ? u : null;
        await _pqrsd.ProcesarAsync(c, umbral, ct);
    }

    private static IReadOnlyList<TemaCorreoDef> TemasPara(CorreoRecibido c, IReadOnlyList<TemaCorreoDef> activos,
        IReadOnlyDictionary<Guid, string?> temasBuzon)
    {
        var marcados = c.BuzonId is Guid bid && temasBuzon.TryGetValue(bid, out var json) ? LeerTemas(json) : null;
        return CoordinadorCorreoAgente.TemasDelBuzon(activos, marcados);
    }

    private async Task<bool> ClasificarAsync(CorreoRecibido c, IReadOnlyList<TemaCorreoDef> temas, CancellationToken ct)
    {
        var agente = await _agentes.AsegurarAsync(PlantillaAgente.ClaveCoordinadorCorreo, ct);
        if (agente is null) return Fallo(c, "No existe la plantilla del Coordinador de correo en el Super Admin.");
        if (!agente.Activo) return Fallo(c, "El agente Coordinador de correo est\u00e1 inactivo en esta copropiedad (Agentes de IA).");

        var adjuntos = LeerAdjuntos(c.AdjuntosJson).Select(a => a.Nombre);
        var prompt = CoordinadorCorreoAgente.ArmarPrompt(temas, c.RemitenteNombre, c.RemitenteCorreo, c.Asunto, c.FechaCorreo, adjuntos, c.Cuerpo);
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

        var cl = CoordinadorCorreoAgente.Parsear(res.Text, temas);
        if (cl is null)
        {
            _log.LogWarning("[Buzon] Respuesta no interpretable del coordinador para {Id}: {Texto}", c.Id, Corta(res.Text, 300));
            return Fallo(c, "El agente respondi\u00f3 con un tema que no est\u00e1 en la lista o en un formato que no se pudo leer. Intenta reclasificar.");
        }
        c.TemaClave = cl.TemaClave;
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
        string? buzon = x.BuzonId is Guid bid
            ? await _db.BuzonesCorreo.AsNoTracking().Where(b => b.Id == bid).Select(b => b.Nombre).FirstOrDefaultAsync(ct)
            : null;
        var adj = LeerAdjuntos(x.AdjuntosJson)
            .Select(a => new CorreoAdjuntoDto(a.Nombre, a.Mime, a.Tamano, _blob.ResolveUrl(a.Ruta))).ToList();
        return new CorreoRecibidoDetalleDto(x.Id, x.BuzonId, buzon, x.FechaCorreo, x.RemitenteCorreo, x.RemitenteNombre, x.Para,
            x.Asunto, x.Cuerpo, adj, x.TemaClave, x.Confianza, x.Resumen, x.AccionSugerida, x.DatosJson, x.Estado, x.Error, x.ClasificadoEn,
            CorreoPqrsdService.LeerPropuesta(x.PropuestaPqrsdJson), x.PqrsdExpedienteId, x.PqrsdRadicado, x.PqrsdAutomatico,
            x.AcuseEnviadoAt, x.AcuseDestino, x.AcuseError);
    }

    public async Task<CorreoRecibidoDto?> ReclasificarAsync(Guid id, CancellationToken ct = default)
    {
        var x = await _db.CorreosRecibidos.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (x is null) return null;
        var activos = await TemasActivosAsync(ct);
        var temasBuzon = await _db.BuzonesCorreo.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.TemasJson, ct);
        // Un correo ya radicado conserva su radicado: reclasificar solo cambia el tema que muestra la bandeja.
        if (await ClasificarAsync(x, TemasPara(x, activos, temasBuzon), ct) && x.PqrsdExpedienteId is not null)
            x.Estado = EstadoCorreoRecibido.Procesado;
        await _db.SaveChangesAsync(ct);
        if (x.PqrsdExpedienteId is null && x.Estado == EstadoCorreoRecibido.Clasificado)
        {
            if (!string.Equals(x.TemaClave, TemasCorreoBase.Pqrsd, StringComparison.OrdinalIgnoreCase)) x.PropuestaPqrsdJson = null;
            await DelegarAsync(x, await UmbralesAsync(ct), ct);
            await _db.SaveChangesAsync(ct);
        }
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

    private static CorreoRecibidoDto ADto(CorreoRecibido x) => new(x.Id, x.BuzonId, x.FechaCorreo, x.RemitenteCorreo,
        x.RemitenteNombre, x.Asunto, x.TemaClave, x.Confianza, x.Resumen, x.AccionSugerida, x.Estado, x.Error,
        LeerAdjuntos(x.AdjuntosJson).Count, x.PqrsdExpedienteId, x.PqrsdRadicado);

    // ---------------------------------------------------------------- utilidades

    private (string? Pass, string? Error) Clave(BuzonCorreo b)
    {
        if (string.IsNullOrWhiteSpace(b.Cuenta)) return (null, "Falta el correo de la cuenta.");
        if (string.IsNullOrEmpty(b.PasswordCifrada)) return (null, "Falta la contrase\u00f1a de aplicaci\u00f3n.");
        try { return (_secret.Unprotect(b.PasswordCifrada), null); }
        catch { return (null, "La contrase\u00f1a guardada no se pudo descifrar. Vuelve a guardarla."); }
    }

    private static List<string>? LeerTemas(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var l = JsonSerializer.Deserialize<List<string>>(json, Json);
            return l is { Count: > 0 } ? l : null;
        }
        catch { return null; }
    }

    private static async Task<ImapClient> ConectarAsync(string? host, int port, string user, string pass, CancellationToken ct)
    {
        host = string.IsNullOrWhiteSpace(host) ? "imap.gmail.com" : host.Trim();
        if (port <= 0) port = 993;
        var client = new ImapClient { Timeout = 30000 };
        try
        {
            await client.ConnectAsync(host, port, port == 993 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable, ct);
            await client.AuthenticateAsync(user.Trim(), pass, ct);
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
