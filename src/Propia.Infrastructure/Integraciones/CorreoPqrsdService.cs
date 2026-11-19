using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Application.Integraciones;
using Propia.Application.Pqrsd;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;

namespace Propia.Infrastructure.Integraciones;

/// <summary>
/// PQRSD por correo (Ola P1, 2026-11-18). Cuando el Coordinador clasifica un correo como PQRSD, el agente "Radicador
/// de PQRSD" propone tipo, categoria y unidad; si su confianza (y la del coordinador) alcanza el umbral del buzon se
/// radica solo, con medio "Correo electronico", la fecha del correo como fecha de recibido (el plazo legal cuenta desde
/// ahi), los adjuntos del correo y el correo original como constancia. Radicador: la persona del directorio con ese
/// correo o, si no existe, una persona externa (como el formulario publico). Corre dentro de la revision del buzon
/// (job: la copropiedad ya esta fijada con SetTenant) o desde el boton de la bandeja.
/// </summary>
public sealed class CorreoPqrsdService : ICorreoPqrsdService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPqrsdService _pqrsd;
    private readonly IAgentesSistema _agentes;
    private readonly IAiInferenceService _inference;
    private readonly IBlobStorage _blob;
    private readonly ILogger<CorreoPqrsdService> _log;
    private readonly ISecretProtector? _secret;
    private readonly IRespuestaCorreoSender? _respuesta;

    public CorreoPqrsdService(PropiaDbContext db, ITenantContext tenant, IPqrsdService pqrsd, IAgentesSistema agentes,
        IAiInferenceService inference, IBlobStorage blob, ILogger<CorreoPqrsdService> log,
        ISecretProtector? secret = null, IRespuestaCorreoSender? respuesta = null)
    {
        _db = db; _tenant = tenant; _pqrsd = pqrsd; _agentes = agentes; _inference = inference; _blob = blob; _log = log;
        _secret = secret; _respuesta = respuesta;
    }

    public static PropuestaPqrsdCorreo? LeerPropuesta(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<PropuestaPqrsdCorreo>(json, Json); }
        catch { return null; }
    }

    // ---------------------------------------------------------------- automatico (tras clasificar)

    public async Task ProcesarAsync(CorreoRecibido c, decimal? umbral, CancellationToken ct = default)
    {
        if (c.PqrsdExpedienteId is not null) return;
        PropuestaPqrsdCorreo? prop;
        try { prop = await ProponerAsync(c, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "[Buzon/PQRSD] Error proponiendo la PQRSD del correo {Id}", c.Id);
            prop = Vacia(c, "No se pudo consultar al agente Radicador de PQRSD; radicala desde la bandeja.");
        }
        if (prop is null) return;

        // Seguimiento de un radicado que ya existe (respuesta en el hilo del acuse, "Re: ..."): nunca se radica solo,
        // para no abrir un expediente duplicado. Adjuntarlo al expediente existente es la Ola P3.
        var motivo = await RadicadoMencionadoAsync(c, ct) is string ya
            ? $"Menciona el radicado {ya}, que ya existe: parece un seguimiento de esa PQRSD, no una nueva. Revísalo antes de radicar."
            : MotivoNoAutomatico(c, prop, umbral);
        if (motivo is not null)
        {
            c.PropuestaPqrsdJson = Serializar(prop with { Pendiente = motivo });
            return;
        }

        var r = await RadicarInternoAsync(c, prop, null, automatico: true, ct);
        if (!r.Ok) c.PropuestaPqrsdJson = Serializar(prop with { Pendiente = "No se pudo radicar solo: " + r.Error });
    }

    /// <summary>Por que no se radica solo (null = si se radica). Exige ambas confianzas sobre el umbral y la propuesta completa.</summary>
    public static string? MotivoNoAutomatico(CorreoRecibido c, PropuestaPqrsdCorreo prop, decimal? umbral)
    {
        if (umbral is not decimal u) return "Este buzón no radica PQRSD solo: confírmala.";
        if (!prop.EsPqrsd) return "El Radicador de PQRSD no cree que sea una solicitud: revisa y radica si corresponde.";
        if (prop.TipoId is null || prop.CategoriaId is null) return "Falta el tipo o la categoría: completa y radica.";
        var conf = Math.Min(c.Confianza ?? 0m, prop.Confianza ?? 0m);
        if (conf < u) return $"Confianza {conf * 100:0}% por debajo del umbral del buzón ({u * 100:0}%): confirma y radica.";
        return null;
    }

    private async Task<PropuestaPqrsdCorreo?> ProponerAsync(CorreoRecibido c, CancellationToken ct)
    {
        var tipos = (await _pqrsd.ListarTiposAsync(false, ct)).Where(t => t.Activo)
            .OrderBy(t => t.Orden).Select(t => new TipoPqrsdOpcion(t.Id, t.Nombre)).ToList();
        var cats = (await _pqrsd.ListarCategoriasAsync(ct)).Where(x => x.Activa)
            .OrderBy(x => x.Orden).Select(x => new CategoriaPqrsdOpcion(x.Id, x.Nombre)).ToList();
        if (tipos.Count == 0 || cats.Count == 0) return Vacia(c, "La copropiedad no tiene tipos o categorías de PQRSD activos.");

        var agente = await _agentes.AsegurarAsync(PlantillaAgente.ClaveRadicadorPqrsd, ct);
        if (agente is null) return Vacia(c, "No existe la plantilla del Radicador de PQRSD en el Super Admin; radicala desde la bandeja.");
        if (!agente.Activo) return Vacia(c, "El agente Radicador de PQRSD está inactivo en esta copropiedad (Agentes de IA).");

        var adjuntos = CorreoEntranteService.LeerAdjuntos(c.AdjuntosJson).Select(a => a.Nombre);
        var prompt = RadicadorPqrsdCorreoAgente.ArmarPrompt(tipos, cats, c.RemitenteNombre, c.RemitenteCorreo, c.Asunto,
            c.FechaCorreo, adjuntos, c.Cuerpo);
        var res = await _inference.TestChatAsync(agente.Id, new[] { new AiChatTurn("user", prompt) }, ct: ct);
        if (!res.Ok) return Vacia(c, "El Radicador de PQRSD no respondió (" + (res.Error ?? "sin detalle") + "); radicala desde la bandeja.");

        var prop = RadicadorPqrsdCorreoAgente.Parsear(res.Text, tipos, cats);
        if (prop is null)
        {
            _log.LogWarning("[Buzon/PQRSD] Respuesta no interpretable del radicador para {Id}: {Texto}", c.Id, res.Text?.Length > 300 ? res.Text[..300] : res.Text);
            return Vacia(c, "La respuesta del Radicador de PQRSD no se pudo leer; radicala desde la bandeja.");
        }
        // Reenvio (Ola P2): el solicitante es el remitente ORIGINAL. Primero la regla fija sobre el bloque reenviado;
        // si no alcanza y el asunto es de reenvio, lo que encontro el agente. Si no es reenvio, nada.
        var rv = RadicadorPqrsdCorreoAgente.DetectarReenvio(c.Asunto, c.Cuerpo, c.RemitenteCorreo);
        if (rv is not null)
            prop = prop with { RemitenteOriginalNombre = rv.Nombre ?? prop.RemitenteOriginalNombre, RemitenteOriginalCorreo = rv.Correo };
        else if (!RadicadorPqrsdCorreoAgente.PareceReenvio(c.Asunto) || prop.RemitenteOriginalCorreo is null
                 || string.Equals(prop.RemitenteOriginalCorreo, c.RemitenteCorreo?.Trim(), StringComparison.OrdinalIgnoreCase))
            prop = prop with { RemitenteOriginalNombre = null, RemitenteOriginalCorreo = null };

        // La unidad: primero la del vinculo del solicitante en el directorio (si es una sola); si no, la que escribio.
        var (uid, codigo) = await ResolverUnidadAsync(prop.RemitenteOriginalCorreo ?? c.RemitenteCorreo, prop.UnidadTexto, ct);
        return prop with { UnidadPrivadaId = uid, UnidadCodigo = codigo };
    }

    private static PropuestaPqrsdCorreo Vacia(CorreoRecibido c, string pendiente)
        => new(true, null, null, null, null, null, null, null, c.RemitenteNombre, null, null, null, pendiente);

    private async Task<(Guid? Id, string? Codigo)> ResolverUnidadAsync(string? correo, string? texto, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(correo))
        {
            var mail = correo.Trim();
            var personaId = await _db.Personas.AsNoTracking().Where(p => p.Email == mail).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
            if (personaId is Guid pid)
            {
                var suyas = await _db.UnidadPersonas.AsNoTracking()
                    .Where(v => v.PersonaId == pid && v.Activo).Select(v => v.UnidadId).Distinct().ToListAsync(ct);
                if (suyas.Count == 1)
                {
                    var cod = await _db.UnidadesPrivadas.AsNoTracking().Where(u => u.Id == suyas[0]).Select(u => u.Numero).FirstOrDefaultAsync(ct);
                    if (cod is not null) return (suyas[0], cod);
                }
            }
        }
        if (string.IsNullOrWhiteSpace(texto)) return (null, null);
        var unidades = (await _db.UnidadesPrivadas.AsNoTracking().Select(u => new { u.Id, u.Numero }).ToListAsync(ct))
            .Select(u => (u.Id, u.Numero)).ToList();
        var id = RadicadorPqrsdCorreoAgente.ResolverUnidad(texto, unidades);
        return id is Guid g ? (g, unidades.First(u => u.Id == g).Numero) : (null, null);
    }

    // ---------------------------------------------------------------- manual (boton de la bandeja)

    public async Task<RadicarCorreoPqrsdResult> RadicarAsync(Guid correoId, RadicarCorreoPqrsdRequest req, CancellationToken ct = default)
    {
        var c = await _db.CorreosRecibidos.FirstOrDefaultAsync(x => x.Id == correoId, ct);
        if (c is null) return new RadicarCorreoPqrsdResult(false, null, null, "El correo no existe.");
        if (c.PqrsdExpedienteId is Guid ya) return new RadicarCorreoPqrsdResult(true, c.PqrsdRadicado, ya, null);
        var prop = LeerPropuesta(c.PropuestaPqrsdJson) ?? Vacia(c, "");
        return await RadicarInternoAsync(c, prop, req, automatico: false, ct);
    }

    private async Task<RadicarCorreoPqrsdResult> RadicarInternoAsync(CorreoRecibido c, PropuestaPqrsdCorreo prop,
        RadicarCorreoPqrsdRequest? req, bool automatico, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is not Guid tid) return new RadicarCorreoPqrsdResult(false, null, null, "No hay copropiedad activa.");
        var tipoId = req?.TipoId ?? prop.TipoId;
        var categoriaId = req?.CategoriaId ?? prop.CategoriaId;
        if (tipoId is null) return new RadicarCorreoPqrsdResult(false, null, null, "Elige el tipo de PQRSD.");
        if (categoriaId is null) return new RadicarCorreoPqrsdResult(false, null, null, "Elige la categoría.");
        var tipo = (await _pqrsd.ListarTiposAsync(false, ct)).FirstOrDefault(t => t.Id == tipoId && t.Activo);
        if (tipo is null) return new RadicarCorreoPqrsdResult(false, null, null, "El tipo elegido no existe o está inactivo.");

        try
        {
            var unidadId = req is not null ? req.UnidadPrivadaId : prop.UnidadPrivadaId;
            var rv = Reenvio(c, prop);
            var lineas = new List<string>();
            if (rv is not null)
                lineas.Add($"[Reenviado por {(string.IsNullOrWhiteSpace(c.RemitenteNombre) ? "" : c.RemitenteNombre + " ")}<{c.RemitenteCorreo}>]");
            if (unidadId is null && !string.IsNullOrWhiteSpace(prop.UnidadTexto))
                lineas.Add("[Radicado por correo] Unidad indicada por el remitente: " + prop.UnidadTexto);
            var encabezado = lineas.Count == 0 ? null : string.Join("\n", lineas);
            var descripcion = !string.IsNullOrWhiteSpace(req?.Descripcion)
                ? Recortar(req!.Descripcion!.Trim())
                : RadicadorPqrsdCorreoAgente.ArmarDescripcion(c.Asunto, c.Cuerpo, c.Resumen, encabezado, rv);
            if (descripcion.Length < RadicadorPqrsdCorreoAgente.MinDescripcion)
                return new RadicarCorreoPqrsdResult(false, null, null, $"La descripción debe tener al menos {RadicadorPqrsdCorreoAgente.MinDescripcion} caracteres.");

            var personaId = rv is not null
                ? await RadicadorAsync(rv.Correo, rv.Nombre, null, ct)
                : await RadicadorAsync(c.RemitenteCorreo, c.RemitenteNombre, prop.NombreRemitente, ct);
            var adjuntos = await AdjuntosAsync(c, tid, ct);
            var fechaRecibido = DateOnly.FromDateTime(c.FechaCorreo.ToOffset(TimeSpan.FromHours(-5)).DateTime);

            // Ola P2: si el buzon responde, el acuse sale por el buzon en el hilo y la plataforma no duplica.
            var buzon = c.BuzonId is Guid bid ? await _db.BuzonesCorreo.FirstOrDefaultAsync(b => b.Id == bid, ct) : null;
            var porBuzon = RespondePorBuzon(buzon, c);

            var det = await _pqrsd.RadicarAsync(new RadicarPqrsdRequest(
                Tipo: tipo.Legal, CategoriaId: categoriaId.Value, Descripcion: descripcion, IdentidadReservada: false,
                Adjuntos: adjuntos, UnidadPrivadaId: unidadId, RadicadorPersonaId: personaId, Campos: null, TipoId: tipo.Id,
                MedioRecepcion: MedioRecepcionPqrsd.CorreoElectronico, FechaRecibido: fechaRecibido,
                OmitirConfirmacionRadicador: porBuzon), ct);

            c.PqrsdExpedienteId = det.Id;
            c.PqrsdRadicado = det.NumeroRadicado;
            c.PqrsdAutomatico = automatico;
            c.Estado = EstadoCorreoRecibido.Procesado;
            c.PropuestaPqrsdJson = Serializar(prop with { Pendiente = null });
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("[Buzon/PQRSD] Correo {Id} radicado como {Numero} ({Modo})", c.Id, det.NumeroRadicado, automatico ? "automatico" : "manual");

            if (porBuzon) await EnviarAcuseAsync(c, buzon!, det.Id, ct);
            return new RadicarCorreoPqrsdResult(true, det.NumeroRadicado, det.Id, null);
        }
        catch (InvalidOperationException ex)
        {
            return new RadicarCorreoPqrsdResult(false, null, null, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "[Buzon/PQRSD] Error radicando el correo {Id}", c.Id);
            return new RadicarCorreoPqrsdResult(false, null, null, "No se pudo radicar: " + ex.Message);
        }
    }

    private static readonly System.Text.RegularExpressions.Regex PatronRadicado =
        new(@"\b[A-Z]{2,12}-\d{4}-\d{3,6}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>Primer numero de radicado de la copropiedad que aparece en el asunto o el cuerpo (null si ninguno existe).</summary>
    private async Task<string?> RadicadoMencionadoAsync(CorreoRecibido c, CancellationToken ct)
    {
        var numeros = PatronRadicado.Matches($"{c.Asunto}\n{c.Cuerpo}").Select(m => m.Value.ToUpperInvariant())
            .Distinct().Take(20).ToList();
        if (numeros.Count == 0) return null;
        return await _db.PqrsdExpedientes.AsNoTracking().Where(e => numeros.Contains(e.NumeroRadicado))
            .Select(e => e.NumeroRadicado).FirstOrDefaultAsync(ct);
    }

    /// <summary>El reenvio del correo: el bloque reenviado (regla fija) o, si solo lo vio el agente, el correo entero
    /// con el remitente original que el agente encontro.</summary>
    private static ReenvioCorreo? Reenvio(CorreoRecibido c, PropuestaPqrsdCorreo prop)
    {
        var rv = RadicadorPqrsdCorreoAgente.DetectarReenvio(c.Asunto, c.Cuerpo, c.RemitenteCorreo);
        if (rv is not null) return rv;
        if (string.IsNullOrWhiteSpace(prop.RemitenteOriginalCorreo)) return null;
        return new ReenvioCorreo(prop.RemitenteOriginalNombre, prop.RemitenteOriginalCorreo!, null,
            (c.Cuerpo ?? "").Replace("\r\n", "\n").Trim(), null);
    }

    private bool RespondePorBuzon(BuzonCorreo? b, CorreoRecibido c)
        => b is not null && b.ResponderRadicado && _respuesta is not null && _secret is not null
           && !string.IsNullOrWhiteSpace(b.PasswordCifrada) && !string.IsNullOrWhiteSpace(c.RemitenteCorreo)
           && c.RemitenteCorreo.Contains('@');

    /// <summary>
    /// Ola P2: responde al remitente desde la cuenta del buzon, en el hilo, con la plantilla del buzon resuelta sobre el
    /// expediente. Si no sale, se anota el error y se envia la confirmacion de la plataforma (no queda sin acuse).
    /// </summary>
    private async Task EnviarAcuseAsync(CorreoRecibido c, BuzonCorreo b, Guid expedienteId, CancellationToken ct)
    {
        string? error;
        try
        {
            var plantillaId = await PlantillaAcuseAsync(b, ct);
            var cuerpo = await _pqrsd.ResolverPlantillaAsync(expedienteId, plantillaId, ct);
            string? pass = null;
            try { pass = _secret!.Unprotect(b.PasswordCifrada!); } catch { /* abajo */ }
            error = cuerpo is null ? "No se pudo armar la respuesta con la plantilla."
                : pass is null ? "La contraseña del buzón está cifrada con una versión anterior: vuelve a guardarla."
                : await _respuesta!.ResponderAsync(b, pass, c.RemitenteCorreo!.Trim(), c.Asunto ?? "", EnvolverHtml(cuerpo), c.MessageId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "[Buzon/PQRSD] Error respondiendo el radicado del correo {Id}", c.Id);
            error = "No se pudo enviar la respuesta: " + ex.Message;
        }

        if (error is null)
        {
            c.AcuseEnviadoAt = DateTimeOffset.UtcNow;
            c.AcuseDestino = c.RemitenteCorreo!.Trim();
            c.AcuseError = null;
        }
        else
        {
            c.AcuseError = error.Length > 1000 ? error[..1000] : error;
            _log.LogWarning("[Buzon/PQRSD] Respuesta por el buzon no salio para el correo {Id}: {Error}. Se envia la confirmacion de la plataforma.", c.Id, error);
            try { await _pqrsd.EnviarConfirmacionRadicadoAsync(expedienteId, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { _log.LogError(ex, "[Buzon/PQRSD] Fallo tambien la confirmacion de la plataforma ({Id})", c.Id); }
        }
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Plantilla del buzon (si sigue siendo de categoria pqrsd) o la predeterminada, que se siembra si falta.</summary>
    private async Task<Guid> PlantillaAcuseAsync(BuzonCorreo b, CancellationToken ct)
    {
        const string cat = Application.Plantillas.PlantillasCatalogo.Pqrsd;
        if (b.PlantillaAcuseId is Guid elegida
            && await _db.PlantillasDocumento.AnyAsync(p => p.Id == elegida && p.Categoria == cat, ct))
            return elegida;
        var existente = await _db.PlantillasDocumento
            .Where(p => p.Categoria == cat && p.Nombre == RadicadorPqrsdCorreoAgente.NombrePlantillaAcuse)
            .Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
        if (existente is Guid id) return id;
        var orden = (await _db.PlantillasDocumento.Select(p => (int?)p.Orden).MaxAsync(ct) ?? 0) + 1;
        var nueva = new PlantillaDocumento
        {
            Nombre = RadicadorPqrsdCorreoAgente.NombrePlantillaAcuse,
            Categoria = cat,
            CuerpoHtml = RadicadorPqrsdCorreoAgente.PlantillaAcuseHtml.Trim(),
            Orden = orden,
            // Va en el cuerpo de un correo: sin encabezado ni pie de pagina.
            EncabezadoModo = ModoMembrete.Ninguno,
            PieModo = ModoMembrete.Ninguno,
        };
        _db.PlantillasDocumento.Add(nueva);
        await _db.SaveChangesAsync(ct);
        return nueva.Id;
    }

    private static string EnvolverHtml(string cuerpo)
        => "<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;line-height:1.5;color:#222\">" + cuerpo + "</div>";

    private static string Recortar(string t)
        => t.Length <= RadicadorPqrsdCorreoAgente.MaxDescripcion ? t : t[..RadicadorPqrsdCorreoAgente.MaxDescripcion];

    /// <summary>
    /// Radicador = persona GLOBAL con ese correo (unico en el directorio). Si no existe se crea como persona externa con
    /// perfil incompleto y documento pendiente (patron de Google Sign-In/Onboarding); la administracion la completa.
    /// </summary>
    private async Task<Guid> RadicadorAsync(string? correo, string? nombreCorreo, string? nombreAgente, CancellationToken ct)
    {
        var mail = string.IsNullOrWhiteSpace(correo) ? null : correo.Trim();
        if (mail is not null)
        {
            var existente = await _db.Personas.AsNoTracking().Where(p => p.Email == mail).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
            if (existente is Guid id) return id;
        }
        var nombre = !string.IsNullOrWhiteSpace(nombreCorreo) ? nombreCorreo!.Trim()
            : !string.IsNullOrWhiteSpace(nombreAgente) ? nombreAgente!.Trim()
            : mail?.Split('@')[0] ?? "Remitente de correo";
        var (nombres, apellidos) = PartirNombre(nombre);
        var p = new Persona
        {
            TipoDocumento = TipoDocumento.CC,
            Documento = $"PENDIENTE-{Guid.NewGuid().ToString("N")[..10]}",
            Nombres = nombres,
            Apellidos = apellidos,
            Email = mail,
            PerfilIncompleto = true,
            EstadoDirectorio = EstadoDirectorio.Activo,
        };
        _db.Personas.Add(p);
        await _db.SaveChangesAsync(ct);
        return p.Id;
    }

    private static (string Nombres, string Apellidos) PartirNombre(string nombre)
    {
        var t = nombre.Replace("\"", "").Trim();
        if (t.Length > 150) t = t[..150];
        var partes = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length <= 1) return (t, "");
        if (partes.Length == 2) return (partes[0], partes[1]);
        var mitad = partes.Length / 2;
        return (string.Join(' ', partes[..mitad]), string.Join(' ', partes[mitad..]));
    }

    /// <summary>Adjuntos del expediente: los documentos del correo (ya estan en el storage) mas el correo original.</summary>
    private async Task<List<AdjuntoInicialDto>> AdjuntosAsync(CorreoRecibido c, Guid tid, CancellationToken ct)
    {
        var guardados = CorreoEntranteService.LeerAdjuntos(c.AdjuntosJson);
        var lista = guardados.Select(a => new AdjuntoInicialDto(Corta(a.Nombre, 255), a.Mime ?? "application/octet-stream",
            a.Tamano, _blob.GetPublicUrl(a.Ruta))).ToList();
        try
        {
            var texto = RadicadorPqrsdCorreoAgente.TextoCorreoOriginal(c.RemitenteNombre, c.RemitenteCorreo, c.Para, c.Asunto,
                c.FechaCorreo, guardados.Select(a => a.Nombre), c.Cuerpo);
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(texto)).ToArray();
            await using var ms = new MemoryStream(bytes);
            var url = await _blob.UploadAsync($"tenants/{tid}/correos/{c.Id}/{RadicadorPqrsdCorreoAgente.NombreAdjuntoOriginal}", ms, "text/plain; charset=utf-8", ct);
            lista.Insert(0, new AdjuntoInicialDto(RadicadorPqrsdCorreoAgente.NombreAdjuntoOriginal, "text/plain", bytes.LongLength, url));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "[Buzon/PQRSD] No se pudo guardar el correo original del correo {Id}", c.Id);
        }
        return lista;
    }

    private static string Corta(string s, int max) => s.Length <= max ? s : s[..max];

    private static string Serializar(PropuestaPqrsdCorreo p) => JsonSerializer.Serialize(p, Json);
}
