using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Propia.Application.Asambleas;
using Propia.Application.Common;
using Propia.Application.Comunicaciones;
using Propia.Application.Documents;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Common;
using Propia.Infrastructure.Documents;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;

namespace Propia.Infrastructure.Asambleas;

/// <summary>Acta legible de la sesion (ola 3). Ver IAsambleaActaService.</summary>
public class AsambleaActaService : IAsambleaActaService
{
    private static readonly CultureInfo Es = new("es-CO");
    private static readonly TimeSpan Co = TimeSpan.FromHours(-5);
    private const int MinutosCodigo = 15, IntentosCodigo = 5;

    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IHttpContextAccessor _http;
    private readonly IAgentesSistema _agentes;
    private readonly IAiInferenceService _ia;
    private readonly IEmailSender _email;
    private readonly IMemoryCache _cache;
    private readonly IHostEnvironment _env;
    private readonly IMembreteDocumentBuilder _builder;
    private readonly IHtmlToPdfService _pdf;
    private readonly IBlobStorage _storage;
    private readonly IComunicacionesService _comunicaciones;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AsambleaActaService> _log;

    public AsambleaActaService(PropiaDbContext db, ITenantContext tenant, IHttpContextAccessor http, IAgentesSistema agentes,
        IAiInferenceService ia, IEmailSender email, IMemoryCache cache, IHostEnvironment env, IMembreteDocumentBuilder builder,
        IHtmlToPdfService pdf, IBlobStorage storage, IComunicacionesService comunicaciones, IServiceScopeFactory scopes,
        ILogger<AsambleaActaService> log)
    {
        _db = db; _tenant = tenant; _http = http; _agentes = agentes; _ia = ia; _email = email; _cache = cache; _env = env;
        _builder = builder; _pdf = pdf; _storage = storage; _comunicaciones = comunicaciones; _scopes = scopes; _log = log;
    }

    private Guid UsuarioActual() => Guid.TryParse(_http.HttpContext?.User?.FindFirstValue("user_id"), out var id) ? id : Guid.Empty;
    private string? Ip() => _http.HttpContext?.Connection.RemoteIpAddress?.ToString();

    // ===================== Preparar: numero + borrador base =====================

    public async Task<Guid?> PrepararAsync(Guid sesionId, CancellationToken ct)
    {
        var a = await _db.Actas.FirstOrDefaultAsync(x => x.SesionId == sesionId, ct);
        if (a is null) return null;
        if (a.Estado == EstadoActa.Firmada) return a.Id;
        var ctx = await CargarAsync(a.SesionId, ct);
        if (string.IsNullOrEmpty(a.Numero))
        {
            var prefijo = $"{ctx.Sesion.FechaSesion.ToOffset(Co).Year}-";
            var nums = await _db.Actas.Where(x => x.Numero != null && x.Numero.StartsWith(prefijo)).Select(x => x.Numero!).ToListAsync(ct);
            var n = nums.Select(x => int.TryParse(x[prefijo.Length..], out var k) ? k : 0).DefaultIfEmpty(0).Max() + 1;
            a.Numero = $"{prefijo}{n:000}";
        }
        if (string.IsNullOrEmpty(a.BorradorHtml))
            a.BorradorHtml = ActaRedaccion.Base(ctx, a.Numero) + ActaRedaccion.Firmas(ctx, a);
        a.EstadoRedaccion = EstadoRedaccionActa.Redactando;
        a.ErrorRedaccion = null;
        a.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return a.Id;
    }

    public void ProgramarRedaccion(Guid actaId)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return;
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopes.CreateScope();
                scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId.Value);
                await scope.ServiceProvider.GetRequiredService<IAsambleaActaService>().RedactarAsync(actaId, CancellationToken.None);
            }
            catch (Exception ex) { _log.LogError(ex, "Redaccion del acta {ActaId} fallo", actaId); }
        });
    }

    // ===================== Redaccion por IA =====================

    public async Task RedactarAsync(Guid actaId, CancellationToken ct)
    {
        var a = await _db.Actas.FirstOrDefaultAsync(x => x.Id == actaId, ct);
        if (a is null || a.Estado == EstadoActa.Firmada || TieneFirma(a)) return;
        var editadaAntes = a.Editada;
        string? error = null, html = null;
        try
        {
            var ctx = await CargarAsync(a.SesionId, ct);
            var plantilla = await PlantillaActaAsync(ctx, ct);
            var agente = await _agentes.AsegurarAsync(PlantillaAgente.ClaveActaAsamblea, ct);
            if (agente is not { Activo: true })
                error = "El agente \"Redactor de actas\" no está activo en la copropiedad (Super Admin > Plantillas de Agentes IA).";
            else
            {
                var prompt = new StringBuilder()
                    .AppendLine("PLANTILLA (estructura y estilo a seguir):").AppendLine(plantilla.Html).AppendLine()
                    .AppendLine("DATOS DE LA SESION (JSON; son hechos, no los cambies):").AppendLine(ActaRedaccion.Json(ctx, a.Numero)).AppendLine()
                    .AppendLine("TRANSCRIPCION (hora | hablante | punto: texto):").AppendLine(ActaRedaccion.TextoTranscripcion(ctx)).AppendLine()
                    .AppendLine("Redacta el acta completa.");
                var res = await _ia.TestChatAsync(agente.Id, new[] { new AiChatTurn("user", prompt.ToString()) }, ct: ct);
                var limpio = res.Ok ? ActaRedaccion.LimpiarHtmlIa(res.Text) : null;
                if (string.IsNullOrWhiteSpace(limpio)) error = res.Ok ? "La IA no devolvió el acta." : (res.Error ?? "La IA no respondió.");
                else html = limpio + ActaRedaccion.Firmas(ctx, a);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Redaccion por IA del acta {ActaId}", actaId);
            error = "No se pudo redactar con la IA: " + ex.Message;
        }

        // Releer: si mientras tanto alguien firmo o edito a mano, no se pisa su trabajo.
        _db.ChangeTracker.Clear();
        a = await _db.Actas.FirstOrDefaultAsync(x => x.Id == actaId, ct);
        if (a is null || a.Estado == EstadoActa.Firmada || TieneFirma(a) || (a.Editada && !editadaAntes)) return;
        if (html is not null)
        {
            a.BorradorHtml = html;
            a.RedactadaPorIa = true;
            a.Editada = false;
            a.EstadoRedaccion = EstadoRedaccionActa.Lista;
            a.ErrorRedaccion = null;
        }
        else
        {
            a.EstadoRedaccion = EstadoRedaccionActa.Error;
            a.ErrorRedaccion = Recortar(error ?? "Error desconocido.", 1000);
        }
        a.RedactadaEn = DateTimeOffset.UtcNow;
        a.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> RegenerarAsync(Guid actaId, CancellationToken ct)
    {
        var a = await _db.Actas.FirstOrDefaultAsync(x => x.Id == actaId, ct);
        if (a is null) return false;
        NoFirmada(a, "regenerar");
        if (string.IsNullOrEmpty(a.BorradorHtml)) await PrepararAsync(a.SesionId, ct);
        a.EstadoRedaccion = EstadoRedaccionActa.Redactando;
        a.ErrorRedaccion = null;
        a.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> GuardarBorradorAsync(Guid actaId, GuardarBorradorActaRequest req, CancellationToken ct)
    {
        var a = await _db.Actas.FirstOrDefaultAsync(x => x.Id == actaId, ct);
        if (a is null) return false;
        NoFirmada(a, "editar");
        var limpio = ActaRedaccion.Sanear(ActaRedaccion.SinFirmas(req.Html ?? ""));
        if (string.IsNullOrWhiteSpace(limpio) || limpio.Length < 50) throw new InvalidOperationException("El acta quedó vacía.");
        if (limpio.Length > 400_000) throw new InvalidOperationException("El acta es demasiado larga.");
        var ctx = await CargarAsync(a.SesionId, ct);
        a.BorradorHtml = limpio + ActaRedaccion.Firmas(ctx, a);
        a.Editada = true;
        if (a.EstadoRedaccion == EstadoRedaccionActa.Redactando) a.EstadoRedaccion = EstadoRedaccionActa.Lista;
        a.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Firmas (presidente y secretario) =====================

    public async Task<CodigoFirmaDto> SolicitarCodigoAsync(Guid actaId, SolicitarCodigoFirmaRequest req, CancellationToken ct)
    {
        var a = await _db.Actas.FirstOrDefaultAsync(x => x.Id == actaId, ct) ?? throw new InvalidOperationException("Acta no encontrada.");
        var s = await _db.Sesiones.AsNoTracking().FirstAsync(x => x.Id == a.SesionId, ct);
        var personaId = PuedeFirmar(a, s, req.Rol);
        var persona = personaId is Guid pid ? await _db.Personas.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid, ct) : null;
        if (string.IsNullOrWhiteSpace(persona?.Email))
            throw new InvalidOperationException($"El {Rol(req.Rol)} no tiene correo registrado: que firme desde su cuenta o registra su correo en el Directorio.");

        var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var expira = DateTimeOffset.UtcNow.AddMinutes(MinutosCodigo);
        _cache.Set(ClaveCodigo(actaId, req.Rol), new CodigoPendiente(HashCodigo(actaId, req.Rol, codigo), expira, 0), expira);

        // En Development no se envia correo (el SMTP de dev es real): el codigo vuelve en la respuesta, como /dev/login.
        if (_env.IsDevelopment()) return new CodigoFirmaDto(Enmascarar(persona.Email!), expira, codigo);

        var r = await _email.SendAsync(persona.Email!, $"Código para firmar el Acta No. {a.Numero}",
            $"<p>Hola {WebUtility.HtmlEncode(persona.Nombres)},</p><p>Tu código para firmar como {Rol(req.Rol)} el <b>Acta No. {WebUtility.HtmlEncode(a.Numero ?? "")}</b> " +
            $"de la sesión «{WebUtility.HtmlEncode(s.Titulo)}» es:</p><p style=\"font-size:24px;font-weight:700;letter-spacing:4px\">{codigo}</p>" +
            $"<p>Vence en {MinutosCodigo} minutos. Si no solicitaste esta firma, ignora este correo.</p>", ct);
        if (!r.Success)
        {
            _cache.Remove(ClaveCodigo(actaId, req.Rol));
            throw new InvalidOperationException("No se pudo enviar el código por correo: " + (r.Error ?? "error del servidor de correo"));
        }
        return new CodigoFirmaDto(Enmascarar(persona.Email!), expira, null);
    }

    public async Task<bool> FirmarAsync(Guid actaId, FirmarActaRolRequest req, CancellationToken ct)
    {
        var a = await _db.Actas.FirstOrDefaultAsync(x => x.Id == actaId, ct);
        if (a is null) return false;
        var s = await _db.Sesiones.AsNoTracking().FirstAsync(x => x.Id == a.SesionId, ct);
        var personaId = PuedeFirmar(a, s, req.Rol);
        if (string.IsNullOrEmpty(a.BorradorHtml)) throw new InvalidOperationException("El acta aún no tiene borrador: redáctala antes de firmar.");

        MetodoFirmaActa metodo;
        if (string.IsNullOrWhiteSpace(req.Codigo))
        {
            var usuarioPersona = await _db.Users.AsNoTracking().Where(u => u.Id == UsuarioActual()).Select(u => u.PersonaId).FirstOrDefaultAsync(ct);
            if (personaId is null || usuarioPersona != personaId)
                throw new InvalidOperationException($"Solo el {Rol(req.Rol)} designado puede firmar desde su cuenta. Envíale el código a su correo.");
            metodo = MetodoFirmaActa.Cuenta;
        }
        else
        {
            var clave = ClaveCodigo(actaId, req.Rol);
            if (!_cache.TryGetValue(clave, out CodigoPendiente? p) || p is null || p.Expira < DateTimeOffset.UtcNow)
                throw new InvalidOperationException("El código venció o no existe: solicita uno nuevo.");
            if (p.Intentos >= IntentosCodigo)
            {
                _cache.Remove(clave);
                throw new InvalidOperationException("Demasiados intentos: solicita un código nuevo.");
            }
            var ok = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(p.Hash),
                Encoding.UTF8.GetBytes(HashCodigo(actaId, req.Rol, req.Codigo.Trim())));
            if (!ok)
            {
                _cache.Set(clave, p with { Intentos = p.Intentos + 1 }, p.Expira);
                throw new InvalidOperationException("Código incorrecto.");
            }
            _cache.Remove(clave);
            metodo = MetodoFirmaActa.Codigo;
        }

        var ahora = DateTimeOffset.UtcNow;
        if (req.Rol == RolFirmaActa.Presidente)
        { a.FirmaPresidenteEn = ahora; a.FirmaPresidenteUsuarioId = UsuarioActual(); a.FirmaPresidenteMetodo = metodo; a.FirmaPresidenteIp = Ip(); }
        else
        { a.FirmaSecretarioEn = ahora; a.FirmaSecretarioUsuarioId = UsuarioActual(); a.FirmaSecretarioMetodo = metodo; a.FirmaSecretarioIp = Ip(); }

        var ctx = await CargarAsync(a.SesionId, ct);
        a.BorradorHtml = ActaRedaccion.SinFirmas(a.BorradorHtml) + ActaRedaccion.Firmas(ctx, a);
        if (a.EstadoRedaccion == EstadoRedaccionActa.Redactando) a.EstadoRedaccion = EstadoRedaccionActa.Lista;
        if (a.FirmaPresidenteEn is not null && a.FirmaSecretarioEn is not null)
        {
            // RN-10: con las dos firmas el acta queda inmutable; el hash cubre el HTML final y los datos duros.
            a.Estado = EstadoActa.Firmada;
            a.TimestampFirma = ahora;
            a.FirmadoPorUsuarioId = UsuarioActual();
            a.TipoFirma = TipoFirmaActa.ElectronicaNativa;
            a.FirmanteIp = Ip();
            a.HashDocumento = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{a.BorradorHtml}|{a.ContenidoGenerado}"))).ToLowerInvariant();
        }
        a.UpdatedAt = ahora;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Valida que el rol se pueda firmar y devuelve la persona designada (null = sin persona).</summary>
    private static Guid? PuedeFirmar(Acta a, Sesion s, RolFirmaActa rol)
    {
        if (a.Estado == EstadoActa.Firmada) throw new InvalidOperationException("El acta ya está firmada (RN-10 inmutable).");
        if (s.InstaladaEn is null) throw new InvalidOperationException("La sesión no tiene presidente ni secretario designados (no se instaló).");
        if (rol == RolFirmaActa.Presidente && a.FirmaPresidenteEn is not null) throw new InvalidOperationException("El presidente ya firmó.");
        if (rol == RolFirmaActa.Secretario && a.FirmaSecretarioEn is not null) throw new InvalidOperationException("El secretario ya firmó.");
        return rol == RolFirmaActa.Presidente ? s.PresidentePersonaId : s.SecretarioPersonaId;
    }

    private sealed record CodigoPendiente(string Hash, DateTimeOffset Expira, int Intentos);
    private static string ClaveCodigo(Guid actaId, RolFirmaActa rol) => $"asm-acta-codigo:{actaId:N}:{(int)rol}";
    private static string HashCodigo(Guid actaId, RolFirmaActa rol, string codigo)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{actaId:N}|{(int)rol}|{codigo}")));
    private static string Rol(RolFirmaActa r) => r == RolFirmaActa.Presidente ? "presidente" : "secretario";
    private static string Enmascarar(string email)
    {
        var at = email.IndexOf('@');
        return at <= 1 ? "***" + email[Math.Max(0, at)..] : email[0] + new string('*', Math.Min(6, at - 1)) + email[at..];
    }

    // ===================== Publicar =====================

    public async Task<PublicacionActaDto> PublicarAsync(Guid actaId, string? baseUrl, CancellationToken ct)
    {
        var a = await _db.Actas.FirstOrDefaultAsync(x => x.Id == actaId, ct) ?? throw new InvalidOperationException("Acta no encontrada.");
        if (a.Estado != EstadoActa.Firmada) throw new InvalidOperationException("El acta debe estar firmada por el presidente y el secretario para publicarse.");
        if (a.PublicadaEn is not null) throw new InvalidOperationException("El acta ya fue publicada.");
        var ctx = await CargarAsync(a.SesionId, ct);
        var s = ctx.Sesion;
        var plantilla = await PlantillaActaAsync(ctx, ct);
        var (enc, pie) = await new Plantillas.MembretesService(_db, _tenant).ParaDocumentoAsync(plantilla.Id, null, ct);
        var html = _builder.Construir(ctx.Tenant, new MembreteDocContenido(
            TipoBadge: "Acta", RadicadoLabel: "Acta No.", Radicado: a.Numero ?? "",
            Fecha: s.HoraCierre ?? DateTimeOffset.UtcNow, CuerpoHtml: ActaRedaccion.EstilosPdf + a.BorradorHtml), enc, pie);
        var bytes = await _pdf.RenderAsync(html, ct);
        var nombre = $"Acta No. {a.Numero} firmada.pdf";
        using (var ms = new MemoryStream(bytes))
        {
            var url = await _storage.UploadAsync($"tenants/{s.TenantId:N}/asambleas/{s.Id:N}/acta-{a.Numero}.pdf", ms, "application/pdf", ct);
            _db.SesionDocumentos.Add(new SesionDocumento
            {
                SesionId = s.Id,
                Nombre = nombre,
                Descripcion = "Acta firmada por el presidente y el secretario.",
                UrlStorage = url,
                TipoArchivo = "application/pdf",
                TamanioBytes = bytes.LongLength,
                Tipo = TipoDocumentoSesion.Acta,
                Visibilidad = VisibilidadDocumento.PostAsamblea,
                SubidoPorUsuarioId = UsuarioActual()
            });
            a.DocumentoUrl = url;
        }
        a.PublicadaEn = DateTimeOffset.UtcNow;
        a.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Comunicado en BORRADOR (decision de Alex): la administracion lo revisa y lo envia desde Comunicaciones.
        string? aviso = null;
        try
        {
            var fecha = s.FechaSesion.ToOffset(Co).ToString("d 'de' MMMM 'de' yyyy", Es);
            var asunto = Recortar($"Acta No. {a.Numero} - {s.Titulo}", 150);
            var enlace = string.IsNullOrWhiteSpace(baseUrl) ? a.DocumentoUrl! : new Uri(new Uri(baseUrl), a.DocumentoUrl).ToString();
            var cuerpo = $"<p>Se pone a disposición de los propietarios el <b>Acta No. {WebUtility.HtmlEncode(a.Numero)}</b> de la "
                + $"{WebUtility.HtmlEncode(ActaRedaccion.TipoTexto(s.Tipo).ToLowerInvariant())} «{WebUtility.HtmlEncode(s.Titulo)}», celebrada el {WebUtility.HtmlEncode(fecha)}, "
                + "firmada por el presidente y el secretario de la reunión (Ley 675 de 2001, art. 47).</p>"
                + $"<p>Puedes consultarla en el documento adjunto o en <a href=\"{WebUtility.HtmlEncode(enlace)}\">este enlace</a>.</p>";
            var c = await _comunicaciones.CrearBorradorAsync(new CrearBorradorRequest(null, TipoComunicadoBase.Circular, asunto, cuerpo, null, false), ct);
            await _comunicaciones.AgregarSegmentoAsync(c.Id, new AgregarSegmentoRequest(TipoSegmento.TipoPersona, "{\"tipoPersona\":1}"), ct);
            await _comunicaciones.AgregarAdjuntoAsync(c.Id, new CrearAdjuntoRequest(nombre, "application/pdf", bytes.LongLength, a.DocumentoUrl!), ct);
            var acta = await _db.Actas.FirstAsync(x => x.Id == actaId, ct);
            acta.ComunicadoId = c.Id;
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "No se pudo dejar el comunicado del acta {ActaId}", actaId);
            _db.ChangeTracker.Clear();
            aviso = "El acta quedó publicada en Documentos, pero no se pudo preparar el comunicado: créalo en Comunicaciones.";
        }
        return new PublicacionActaDto(a.DocumentoUrl!, (await _db.Actas.AsNoTracking().FirstAsync(x => x.Id == actaId, ct)).ComunicadoId, aviso);
    }

    // ===================== Contexto de la sesion + plantilla =====================

    private async Task<ActaContexto> CargarAsync(Guid sesionId, CancellationToken ct)
    {
        var s = await _db.Sesiones.AsNoTracking().Include(x => x.Puntos).Include(x => x.Participantes).Include(x => x.Poderes)
            .FirstAsync(x => x.Id == sesionId, ct);
        var votaciones = await _db.Votaciones.AsNoTracking().Include(v => v.Votos).Where(v => v.SesionId == sesionId).ToListAsync(ct);
        var tenant = await _db.Tenants.AsNoTracking().FirstAsync(t => t.Id == s.TenantId, ct);
        var presentes = s.Participantes.Where(p => p.Presente || p.HoraIngreso != null).ToList();
        var numeros = await _db.UnidadesPrivadas.AsNoTracking().Where(u => s.Participantes.Select(p => p.UnidadPrivadaId).Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Numero, ct);
        var lineas = await _db.SesionTranscripcionLineas.AsNoTracking().Where(x => x.SesionId == sesionId)
            .OrderBy(x => x.Hora).ThenBy(x => x.CreatedAt).Take(3000).ToListAsync(ct);
        return new ActaContexto(s, tenant, votaciones, presentes.Count, presentes.Sum(p => p.Coeficiente),
            s.Poderes.Count(p => p.Estado == EstadoPoder.Aprobado), numeros, lineas);
    }

    /// <summary>Plantilla "Acta de asamblea" (categoria Asambleas) de la copropiedad; si no existe se crea con la estructura
    /// base. Devuelve el HTML con los datos de la copropiedad y la sesion resueltos.</summary>
    private async Task<(Guid Id, string Html)> PlantillaActaAsync(ActaContexto ctx, CancellationToken ct)
    {
        var p = await _db.PlantillasDocumento.FirstOrDefaultAsync(x => x.Categoria == "asambleas" && EF.Functions.ILike(x.Nombre, "%acta%"), ct);
        if (p is null)
        {
            p = new PlantillaDocumento
            {
                Nombre = "Acta de asamblea",
                Categoria = "asambleas",
                CuerpoHtml = ActaRedaccion.PlantillaBase,
                Orden = 1
            };
            _db.PlantillasDocumento.Add(p);
            await _db.SaveChangesAsync(ct);
        }
        var s = ctx.Sesion;
        var datos = MembreteTokens.DatosCopropiedad(ctx.Tenant, null);
        var f = s.FechaSesion.ToOffset(Co);
        datos["asamblea.tipo"] = WebUtility.HtmlEncode(ActaRedaccion.TipoTexto(s.Tipo));
        datos["asamblea.fecha"] = WebUtility.HtmlEncode(f.ToString("dddd d 'de' MMMM 'de' yyyy", Es));
        datos["asamblea.hora"] = WebUtility.HtmlEncode(f.ToString("h:mm tt", Es));
        datos["asamblea.lugar"] = WebUtility.HtmlEncode(s.LugarFisico ?? "");
        datos["asamblea.enlace"] = WebUtility.HtmlEncode(s.SalaPropia ? "sala virtual de PROPIA" : s.EnlaceVideo ?? "");
        datos["asamblea.orden_del_dia"] = "<ol>" + string.Concat(s.Puntos.OrderBy(x => x.Numero).Select(x => $"<li>{WebUtility.HtmlEncode(x.Titulo)}</li>")) + "</ol>";
        return (p.Id, MembreteTokens.Resolver(p.CuerpoHtml, datos));
    }

    private static bool TieneFirma(Acta a) => a.FirmaPresidenteEn is not null || a.FirmaSecretarioEn is not null;
    private static void NoFirmada(Acta a, string accion)
    {
        if (a.Estado == EstadoActa.Firmada || TieneFirma(a))
            throw new InvalidOperationException($"El acta ya tiene una firma: no se puede {accion}.");
    }
    private static string Recortar(string t, int max) => t.Length <= max ? t : t[..max];
}

/// <summary>Datos de la sesion que usan el borrador base, el JSON para la IA y el bloque de firmas.</summary>
internal sealed record ActaContexto(Sesion Sesion, Tenant Tenant, List<Votacion> Votaciones, int Presentes, decimal CoefPresente,
    int PoderesAprobados, Dictionary<Guid, string> NumerosUnidad, List<SesionTranscripcionLinea> Lineas);

/// <summary>Redaccion determinista del acta (borrador base sin IA), JSON de hechos para la IA, firmas y limpieza.
/// Calca de A.redactarActa / conFirmas de la replica (mod-asambleas.js).</summary>
internal static class ActaRedaccion
{
    private static readonly CultureInfo Es = new("es-CO");
    private static readonly TimeSpan Co = TimeSpan.FromHours(-5);
    private static string E(string? t) => WebUtility.HtmlEncode(t ?? "");
    private static string Pct(decimal v) => v.ToString("0.00", Es) + " %";
    private static string Hora(DateTimeOffset f) => f.ToOffset(Co).ToString("h:mm tt", Es);
    private static string FechaLarga(DateTimeOffset f) => f.ToOffset(Co).ToString("dddd d 'de' MMMM 'de' yyyy", Es);
    private static bool EsAsamblea(Sesion s) => s.Tipo is TipoSesion.AsambleaOrdinaria or TipoSesion.AsambleaExtraordinaria;

    public static string TipoTexto(TipoSesion t) => t switch
    {
        TipoSesion.AsambleaOrdinaria => "Asamblea General Ordinaria",
        TipoSesion.AsambleaExtraordinaria => "Asamblea General Extraordinaria",
        TipoSesion.Consejo => "Sesión del Consejo de Administración",
        TipoSesion.Comite => "Sesión del Comité de Convivencia",
        _ => t.ToString()
    };
    private static string OpcionTexto(string o) => o switch { "Si" => "Sí", "Abstencion" => "Abstención", _ => o };

    public const string PlantillaBase = """
<p>Encabezado: nombre de la copropiedad ({copropiedad.nombre}), "ACTA No." con el número, el tipo de reunión ({asamblea.tipo}) y el título.</p>
<h4>Apertura</h4>
<p>En {copropiedad.ciudad}, el {asamblea.fecha}, siendo las {asamblea.hora}, se reunió la {asamblea.tipo} de {copropiedad.nombre}, previa convocatoria.</p>
<h4>Verificación del quórum e instalación</h4>
<p>Unidades presentes y representadas por poder, porcentaje de coeficientes, presidente y secretario designados.</p>
<h4>Orden del día</h4>
{asamblea.orden_del_dia}
<h4>Desarrollo</h4>
<p>Un apartado por punto: quién intervino y qué dijo (resumido) y la decisión con el desglose de la votación.</p>
<h4>Cierre</h4>
<p>Agotado el orden del día, se levantó la sesión a la hora de cierre.</p>
""";

    /// <summary>Estilos del documento del acta para el PDF (en la app viven en asambleas.css).</summary>
    public const string EstilosPdf = """
<style>.asm-acta-enc{text-align:center;border-bottom:1px solid #D5DCE5;padding-bottom:12px;margin-bottom:14px}.asm-acta-cop{font-size:12px;font-weight:700;color:#5B6B7D;text-transform:uppercase;letter-spacing:.05em}.asm-acta-num{font-size:18px;font-weight:800;margin-top:4px}.asm-acta-tit{font-size:12.5px;font-weight:700;color:#4B2BB0;letter-spacing:.04em}.asm-acta-sub{font-size:13px;color:#5B6B7D}h4{font-size:13px;font-weight:800;text-transform:uppercase;letter-spacing:.04em;margin:18px 0 6px}h5{font-size:13.5px;font-weight:700;margin:12px 0 4px}.asm-acta-dec{background:#F5F8FA;border-left:3px solid #6D4FE3;padding:6px 10px}.asm-acta-firmas{display:grid;grid-template-columns:1fr 1fr;gap:28px;margin-top:36px}.asm-acta-firma{text-align:center;font-size:12.5px}.asm-acta-firma-l{border-bottom:1px solid #1B2A3A;height:34px;display:flex;align-items:flex-end;justify-content:center;margin-bottom:6px}.asm-acta-firma b{display:block}.asm-acta-firma span{color:#5B6B7D}.asm-acta-firmado{font-size:11px;color:#1A8754;font-weight:700;padding-bottom:4px}</style>
""";

    public static string Base(ActaContexto c, string? numero)
    {
        var s = c.Sesion;
        var h = new StringBuilder();
        h.Append($"<div class=\"asm-acta-enc\"><div class=\"asm-acta-cop\">{E(c.Tenant.Nombre)}</div><div class=\"asm-acta-num\">ACTA No. {E(numero)}</div>")
         .Append($"<div class=\"asm-acta-tit\">{E(TipoTexto(s.Tipo).ToUpperInvariant())}{(s.SegundaConvocatoria ? " · SEGUNDA CONVOCATORIA" : "")}</div><div class=\"asm-acta-sub\">{E(s.Titulo)}</div></div>");
        var inicio = s.HoraApertura ?? s.FechaSesion;
        var donde = s.LugarFisico is { Length: > 0 } l ? $" ({E(l)}{(s.Modalidad != ModalidadSesion.Presencial && s.SalaPropia ? " y sala virtual de PROPIA" : "")})"
            : s.SalaPropia ? " (sala virtual de PROPIA)" : "";
        h.Append($"<p>El {E(FechaLarga(inicio))}, siendo las {E(Hora(inicio))}, se reunió la {E(TipoTexto(s.Tipo).ToLower(Es))} de {E(c.Tenant.Nombre)}, en modalidad {E(s.Modalidad.ToString().ToLower(Es))}{donde}")
         .Append(s.FechaCitacionEnviada is { } fc ? $", previa convocatoria enviada el {E(fc.ToOffset(Co).ToString("d 'de' MMMM 'de' yyyy", Es))}" : "").Append(".</p>");
        h.Append($"<h4>Verificación del quórum e instalación</h4><p>Se registraron {c.Presentes} de {s.Participantes.Count} unidades privadas")
         .Append(c.PoderesAprobados > 0 ? $", {c.PoderesAprobados} de ellas representada{(c.PoderesAprobados == 1 ? "" : "s")} por poder" : "").Append(". ");
        if (s.InstaladaEn is { } inst)
        {
            h.Append($"La reunión se instaló a las {E(Hora(inst))} con el {Pct(s.QuorumInstalacionPct ?? 0)} de los coeficientes")
             .Append(s.SegundaConvocatoria ? " (segunda convocatoria: decide con un número plural de propietarios, Ley 675 art. 41)"
                 : EsAsamblea(s) ? ", con lo que hubo quórum para deliberar y decidir (Ley 675 art. 45)" : ", con el quórum que exigen los estatutos")
             .Append($". Fueron designados como presidente <b>{E(s.PresidenteNombre)}</b> y como secretario <b>{E(s.SecretarioNombre)}</b>.");
        }
        else h.Append("La reunión no se instaló.");
        if (!string.IsNullOrWhiteSpace(s.JustificacionQuorum)) h.Append($" Nota sobre el quórum: {E(s.JustificacionQuorum)}");
        h.Append("</p>");
        var puntos = s.Puntos.OrderBy(p => p.Numero).ToList();
        h.Append("<h4>Orden del día</h4><ol>").Append(string.Concat(puntos.Select(p => $"<li>{E(p.Titulo)}</li>"))).Append("</ol><h4>Desarrollo</h4>");
        foreach (var p in puntos)
        {
            h.Append($"<h5>{p.Numero}. {E(p.Titulo)}</h5>");
            if (p.Estado == EstadoPunto.NoTratado) { h.Append("<p><i>Este punto no se trató y queda pendiente para una próxima reunión.</i></p>"); continue; }
            // Intervenciones del punto (transcripcion, ola 4): resumen extractivo como la replica; la IA lo redacta mejor.
            var voz = c.Lineas.Where(l => l.Tipo == TipoLineaTranscripcion.Voz && l.PuntoId == p.Id).ToList();
            if (voz.Count == 0)
                h.Append($"<p>{(string.IsNullOrWhiteSpace(p.Descripcion) ? "" : E(p.Descripcion) + " ")}Se trató sin intervenciones registradas.</p>");
            else
            {
                var autores = voz.Select(l => l.AutorNombre ?? "Participante").Distinct().Count();
                h.Append($"<p>Intervinieron {autores} {(autores == 1 ? "persona" : "personas")}. ")
                 .Append(string.Join(" ", voz.Take(4).Select(l => $"{E(l.AutorNombre ?? "Participante")}{Unidad(c, l)} señaló: «{E(Resumen(l.Texto, 200))}».")))
                 .Append(voz.Count > 4 ? $" Se registraron {voz.Count - 4} intervenciones más (ver transcripción)." : "").Append("</p>");
            }
            var v = c.Votaciones.Where(x => x.PuntoId == p.Id).OrderByDescending(x => x.HoraApertura).FirstOrDefault();
            if (v is { Estado: EstadoVotacion.Cerrada })
            {
                var opciones = Opciones(p);
                var eleccion = !opciones.Contains(OpcionesVotoBase.Si);
                var dg = AsambleaService.Desglose(v, opciones);
                h.Append("<p class=\"asm-acta-dec\">")
                 .Append(eleccion ? "Sometido a elección (gana la opción con más coeficiente, voto " : $"Sometido a votación (mayoría {p.TipoMayoria.ToString().ToLower(Es)} de más del {p.MayoriaPct:0.##} %, voto ")
                 .Append(p.ModalidadVoto == ModalidadVoto.Secreto ? "secreto" : "público").Append($"), se registraron {v.Votos.Count} votos")
                 .Append(dg is { Count: > 0 } ? ": " + string.Join(" · ", dg.Select(x => $"{E(OpcionTexto(x.Opcion))} {Pct(x.Pct)}")) : "")
                 .Append(". <b>Decisión: ")
                 .Append(v.ResultadoFinal switch
                 {
                     ResultadoVotacion.Elegido => $"ELEGIDA LA OPCIÓN «{E(v.ResultadoOpcion?.ToUpper(Es))}»",
                     ResultadoVotacion.Aprobado => "APROBADO",
                     ResultadoVotacion.Rechazado => "NO APROBADO",
                     _ => "SIN RESULTADO"
                 }).Append("</b>.</p>");
            }
        }
        var horaCierre = Hora(s.HoraCierre ?? DateTimeOffset.UtcNow);
        var nVoz = c.Lineas.Count(l => l.Tipo == TipoLineaTranscripcion.Voz);
        h.Append($"<h4>Cierre</h4><p>Agotado el orden del día, se levantó la sesión a las {E(horaCierre)}{(horaCierre.EndsWith('.') ? "" : ".")}"
            + (nVoz > 0 ? $" La transcripción ({nVoz} intervenciones) se conserva como soporte de esta acta." : "") + "</p>");
        return h.ToString();
    }

    /// <summary>Datos duros para la IA (no se cambian).</summary>
    public static string Json(ActaContexto c, string? numero)
    {
        var s = c.Sesion;
        string? L(DateTimeOffset? f) => f?.ToOffset(Co).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var datos = new
        {
            Copropiedad = c.Tenant.Nombre,
            Ciudad = c.Tenant.Ciudad,
            ActaNumero = numero,
            TipoReunion = TipoTexto(s.Tipo),
            SegundaConvocatoria = s.SegundaConvocatoria,
            Titulo = s.Titulo,
            Modalidad = s.Modalidad.ToString(),
            Lugar = s.LugarFisico,
            SalaVirtualDePropia = s.SalaPropia,
            FechaProgramada = L(s.FechaSesion),
            ConvocatoriaEnviada = L(s.FechaCitacionEnviada),
            HoraApertura = L(s.HoraApertura),
            HoraCierre = L(s.HoraCierre),
            UnidadesCitadas = s.Participantes.Count,
            UnidadesPresentes = c.Presentes,
            PoderesAprobados = c.PoderesAprobados,
            Instalacion = s.InstaladaEn is null ? null : new { Hora = L(s.InstaladaEn), QuorumPct = s.QuorumInstalacionPct, Presidente = s.PresidenteNombre, Secretario = s.SecretarioNombre },
            NotaSobreElQuorum = s.JustificacionQuorum,
            Puntos = s.Puntos.OrderBy(p => p.Numero).Select(p =>
            {
                var v = c.Votaciones.Where(x => x.PuntoId == p.Id).OrderByDescending(x => x.HoraApertura).FirstOrDefault();
                var opciones = Opciones(p);
                return new
                {
                    p.Numero,
                    p.Titulo,
                    p.Descripcion,
                    Estado = p.Estado.ToString(),
                    p.RequiereVotacion,
                    Eleccion = !opciones.Contains(OpcionesVotoBase.Si),
                    Mayoria = p.RequiereVotacion ? $"{p.TipoMayoria} de mas del {p.MayoriaPct:0.##} %" : null,
                    VotoSecreto = p.ModalidadVoto == ModalidadVoto.Secreto,
                    Votacion = v is null ? null : new
                    {
                        Estado = v.Estado.ToString(),
                        Votos = v.Votos.Count,
                        Resultado = v.ResultadoFinal?.ToString(),
                        OpcionGanadora = v.ResultadoOpcion,
                        PorcentajeGanador = v.ResultadoPct,
                        Desglose = AsambleaService.Desglose(v, opciones)?.Select(d => new { d.Opcion, d.Votos, d.Pct })
                    }
                };
            })
        };
        return JsonSerializer.Serialize(datos, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    private static string Unidad(ActaContexto c, SesionTranscripcionLinea l)
        => l.UnidadPrivadaId is Guid u && c.NumerosUnidad.TryGetValue(u, out var n) ? $" ({E(n)})" : "";
    private static string Resumen(string t, int max)
    {
        t = t.Trim().TrimEnd('.');
        return t.Length <= max ? t : t[..(max - 1)].TrimEnd() + "...";
    }

    /// <summary>Transcripcion para la IA: "HH:mm | hablante (unidad) | Pn: texto"; los eventos del sistema van como [Sistema].</summary>
    public static string TextoTranscripcion(ActaContexto c)
    {
        if (c.Lineas.Count == 0) return "(sin transcripcion)";
        var numeros = c.Sesion.Puntos.ToDictionary(p => p.Id, p => p.Numero);
        var sb = new StringBuilder();
        foreach (var l in c.Lineas)
        {
            if (sb.Length > 60_000) { sb.AppendLine("(transcripcion recortada)"); break; }
            var punto = l.PuntoId is Guid pid && numeros.TryGetValue(pid, out var n) ? $"P{n}" : "-";
            var quien = l.Tipo == TipoLineaTranscripcion.Sistema ? "[Sistema]"
                : (l.AutorNombre ?? "Participante") + (l.UnidadPrivadaId is Guid u && c.NumerosUnidad.TryGetValue(u, out var un) ? $" ({un})" : "");
            sb.Append(l.Hora.ToOffset(Co).ToString("HH:mm", CultureInfo.InvariantCulture)).Append(" | ").Append(quien).Append(" | ")
              .Append(punto).Append(": ").AppendLine(l.Texto);
        }
        return sb.ToString();
    }

    private static string[] Opciones(SesionPunto p)
        => string.IsNullOrEmpty(p.OpcionesVoto) ? OpcionesVotoBase.Default : JsonSerializer.Deserialize<string[]>(p.OpcionesVoto) ?? OpcionesVotoBase.Default;

    /// <summary>Saneador del acta: el mismo de S-19 (sin script, on*, iframe, javascript:) pero conservando el atributo
    /// class (estilos .asm-acta-* y el bloque de firmas que agrega el sistema).</summary>
    private static readonly Ganss.Xss.HtmlSanitizer Saneador = CrearSaneador();
    private static Ganss.Xss.HtmlSanitizer CrearSaneador() { var s = new Ganss.Xss.HtmlSanitizer(); s.AllowedAttributes.Add("class"); return s; }
    public static string Sanear(string html) => string.IsNullOrWhiteSpace(html) ? "" : Saneador.Sanitize(html);

    private static readonly Regex BloqueFirmas = new("<div class=\"asm-acta-firmas\"[\\s\\S]*$", RegexOptions.Compiled);
    public static string SinFirmas(string html) => BloqueFirmas.Replace(html ?? "", "");

    /// <summary>Bloque de firmas (lo agrega el sistema, nunca la IA): nombre, cargo y "Firmado digitalmente" con fecha.</summary>
    public static string Firmas(ActaContexto c, Acta a)
    {
        var s = c.Sesion;
        var de = EsAsamblea(s) ? "de la asamblea" : "de la sesión";
        string Una(string? nombre, string cargo, DateTimeOffset? en)
            => $"<div class=\"asm-acta-firma\"><div class=\"asm-acta-firma-l\">{(en is { } f ? $"<span class=\"asm-acta-firmado\">Firmado digitalmente {E(f.ToOffset(Co).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))}</span>" : "")}</div><b>{E(nombre)}</b><span>{cargo} {de}</span></div>";
        return "<div class=\"asm-acta-firmas\">" + Una(s.PresidenteNombre, "Presidente", a.FirmaPresidenteEn) + Una(s.SecretarioNombre, "Secretario", a.FirmaSecretarioEn) + "</div>";
    }

    /// <summary>Respuesta de la IA -> fragmento HTML seguro: sin cercas de codigo, sin documento completo, saneado y sin
    /// bloque de firmas (lo pone el sistema).</summary>
    public static string? LimpiarHtmlIa(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var t = Regex.Replace(texto.Trim(), "^```(?:html)?\\s*|\\s*```$", "", RegexOptions.IgnoreCase);
        var body = Regex.Match(t, "<body[^>]*>([\\s\\S]*?)</body>", RegexOptions.IgnoreCase);
        if (body.Success) t = body.Groups[1].Value;
        t = Regex.Replace(t, "<style[\\s\\S]*?</style>", "", RegexOptions.IgnoreCase);
        t = SinFirmas(Sanear(t));
        return t.Length < 50 ? null : t.Trim();
    }
}
