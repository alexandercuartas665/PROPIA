using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Pqrsd;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Pqrsd;

// Particion de PqrsdService por area (clase parcial: comparte _db/_tenantContext/_http/_noti/_tareas/_membrete
// y los helpers transversales del archivo principal). Mismo comportamiento.
public partial class PqrsdService
{
    // Respuestas tipo correo (borradores enriquecidos), plantillas de respuesta y config del formulario publico.
    // ===================== Respuestas tipo correo (borradores con editor enriquecido) =====================

    public async Task<IReadOnlyList<PqrsdRespuestaDto>> ListarRespuestasAsync(Guid expedienteId, CancellationToken ct)
    {
        var respuestas = await _db.PqrsdRespuestas.AsNoTracking()
            .Where(r => r.ExpedienteId == expedienteId)
            .Include(r => r.Adjuntos)
            .Include(r => r.Destinatarios)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        // Nombre del autor: resuelve por AutorUsuarioId (User -> Persona) para que salga en todas
        // las respuestas (incluidas las viejas sin AutorNombre guardado). Fallback: email o "Sistema".
        var autorNombres = await ResolverNombresUsuariosAsync(
            respuestas.Select(r => r.AutorUsuarioId).Where(g => g != Guid.Empty), ct);

        // Numero de version actual por respuesta (para el badge "vN"). Sin historial => 1.
        var respIds = respuestas.Select(r => r.Id).ToList();
        var verMax = await _db.PqrsdRespuestaVersiones.AsNoTracking()
            .Where(v => respIds.Contains(v.RespuestaId))
            .GroupBy(v => v.RespuestaId)
            .Select(g => new { RespuestaId = g.Key, Max = g.Max(x => x.Numero) })
            .ToDictionaryAsync(x => x.RespuestaId, x => x.Max, ct);

        return respuestas.Select(r => new PqrsdRespuestaDto(
            r.Id, r.Asunto, r.CuerpoHtml,
            !string.IsNullOrWhiteSpace(r.AutorNombre) ? r.AutorNombre
                : (r.AutorUsuarioId != Guid.Empty && autorNombres.TryGetValue(r.AutorUsuarioId, out var an) ? an : null),
            r.CreatedAt, r.Enviada, r.EnviadaAt,
            r.Adjuntos.OrderBy(a => a.CreatedAt).Select(a => new PqrsdAdjuntoDto(
                a.Id, a.NombreArchivo, a.TipoMime, a.TamanioBytes, a.UrlStorage, a.CreatedAt,
                null, a.SubidoPorUsuarioId == Guid.Empty ? null : a.SubidoPorUsuarioId, a.Texto, a.Compartido)).ToList(),
            r.Archivada, r.ArchivadaAt, verMax.GetValueOrDefault(r.Id, 1),
            r.Destinatarios.Select(d => new DestinatarioRespuestaDto(d.PersonaId, d.Nombre, d.Email)).ToList()))
            .ToList();
    }

    // Mapea los destinatarios del request a entidades (dedup por email, descarta emails invalidos).
    private static IEnumerable<PqrsdRespuestaDestinatario> MapDestinatarios(IEnumerable<DestinatarioRespuestaDto>? dtos)
    {
        if (dtos is null) yield break;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in dtos)
        {
            var email = d.Email?.Trim();
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) continue;
            if (!seen.Add(email)) continue;
            yield return new PqrsdRespuestaDestinatario
            {
                PersonaId = d.PersonaId,
                Nombre = string.IsNullOrWhiteSpace(d.Nombre) ? null : d.Nombre.Trim(),
                Email = email
            };
        }
    }

    // Resuelve nombres legibles de usuarios (User.Id -> Persona "Nombres Apellidos", fallback email).
    private async Task<Dictionary<Guid, string>> ResolverNombresUsuariosAsync(IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToList();
        var res = new Dictionary<Guid, string>();
        if (ids.Count == 0) return res;
        var users = await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.PersonaId, u.Email })
            .ToListAsync(ct);
        var personaIds = users.Where(u => u.PersonaId != null).Select(u => u.PersonaId!.Value).Distinct().ToList();
        var personas = personaIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Personas.AsNoTracking().Where(p => personaIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => $"{p.Nombres} {p.Apellidos}".Trim(), ct);
        foreach (var u in users)
        {
            string? nombre = null;
            if (u.PersonaId is { } pid && personas.TryGetValue(pid, out var pn) && !string.IsNullOrWhiteSpace(pn))
                nombre = pn;
            nombre ??= u.Email;
            if (!string.IsNullOrWhiteSpace(nombre)) res[u.Id] = nombre!;
        }
        return res;
    }

    // Archiva/desarchiva una respuesta. Las archivadas salen de las tarjetas activas y van a la tabla de archivados.
    public async Task<bool> ArchivarRespuestaAsync(Guid expedienteId, Guid respuestaId, bool archivar, CancellationToken ct)
    {
        var r = await _db.PqrsdRespuestas.FirstOrDefaultAsync(x => x.Id == respuestaId && x.ExpedienteId == expedienteId, ct);
        if (r is null) return false;
        r.Archivada = archivar;
        r.ArchivadaAt = archivar ? DateTimeOffset.UtcNow : null;
        r.ArchivadaPorUsuarioId = archivar ? ActorActual().UsuarioId : null;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PqrsdRespuestaDto?> CrearRespuestaBorradorAsync(Guid expedienteId, CrearRespuestaBorradorRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.CuerpoHtml)) return null;
        var exp = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == expedienteId, ct);
        if (exp is null) return null;
        var (uid, _) = ActorActual();
        var nombre = await ResolverNombreActorAsync(ct);
        var r = new PqrsdRespuesta
        {
            ExpedienteId = expedienteId,
            Asunto = string.IsNullOrWhiteSpace(req.Asunto) ? null : req.Asunto.Trim(),
            CuerpoHtml = req.CuerpoHtml,
            AutorUsuarioId = uid ?? Guid.Empty,
            AutorNombre = nombre
        };
        // v1: snapshot inicial del documento.
        r.Versiones.Add(new PqrsdRespuestaVersion
        {
            Numero = 1,
            CuerpoHtml = r.CuerpoHtml,
            Asunto = r.Asunto,
            AutorUsuarioId = r.AutorUsuarioId,
            AutorNombre = nombre
        });
        foreach (var dst in MapDestinatarios(req.Destinatarios)) r.Destinatarios.Add(dst);
        _db.PqrsdRespuestas.Add(r);
        await _db.SaveChangesAsync(ct);
        return new PqrsdRespuestaDto(r.Id, r.Asunto, r.CuerpoHtml, r.AutorNombre, r.CreatedAt,
            r.Enviada, r.EnviadaAt, new List<PqrsdAdjuntoDto>(), false, null, 1,
            r.Destinatarios.Select(d => new DestinatarioRespuestaDto(d.PersonaId, d.Nombre, d.Email)).ToList());
    }

    public async Task<PqrsdRespuestaDto?> ActualizarRespuestaBorradorAsync(Guid expedienteId, Guid respuestaId, CrearRespuestaBorradorRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.CuerpoHtml)) return null;
        var r = await _db.PqrsdRespuestas
            .Include(x => x.Adjuntos)
            .Include(x => x.Destinatarios)
            .FirstOrDefaultAsync(x => x.Id == respuestaId && x.ExpedienteId == expedienteId, ct);
        if (r is null || r.Enviada) return null;   // una respuesta ya enviada no se edita

        // Snapshot del estado previo antes de sobrescribir (para el historial de versiones).
        var oldCuerpo = r.CuerpoHtml;
        var oldAsunto = r.Asunto;

        r.Asunto = string.IsNullOrWhiteSpace(req.Asunto) ? null : req.Asunto.Trim();
        r.CuerpoHtml = req.CuerpoHtml;

        var existentes = await _db.PqrsdRespuestaVersiones
            .Where(v => v.RespuestaId == r.Id).Select(v => v.Numero).ToListAsync(ct);
        int nextNum;
        if (existentes.Count == 0)
        {
            // Respuesta creada antes de existir el historial: siembra v1 con el estado previo.
            _db.PqrsdRespuestaVersiones.Add(new PqrsdRespuestaVersion
            {
                RespuestaId = r.Id,
                Numero = 1,
                CuerpoHtml = oldCuerpo,
                Asunto = oldAsunto,
                AutorUsuarioId = r.AutorUsuarioId,
                AutorNombre = r.AutorNombre
            });
            nextNum = 2;
        }
        else nextNum = existentes.Max() + 1;

        var (uid, _) = ActorActual();
        var editorNombre = await ResolverNombreActorAsync(ct);
        _db.PqrsdRespuestaVersiones.Add(new PqrsdRespuestaVersion
        {
            RespuestaId = r.Id,
            Numero = nextNum,
            CuerpoHtml = r.CuerpoHtml,
            Asunto = r.Asunto,
            AutorUsuarioId = uid ?? Guid.Empty,
            AutorNombre = editorNombre
        });

        // Reemplaza los destinatarios por los enviados (si el request los trae).
        if (req.Destinatarios is not null)
        {
            _db.PqrsdRespuestaDestinatarios.RemoveRange(r.Destinatarios);
            r.Destinatarios.Clear();
            foreach (var dst in MapDestinatarios(req.Destinatarios)) r.Destinatarios.Add(dst);
        }

        await _db.SaveChangesAsync(ct);
        return new PqrsdRespuestaDto(r.Id, r.Asunto, r.CuerpoHtml, r.AutorNombre, r.CreatedAt, r.Enviada, r.EnviadaAt,
            r.Adjuntos.OrderBy(a => a.CreatedAt).Select(a => new PqrsdAdjuntoDto(
                a.Id, a.NombreArchivo, a.TipoMime, a.TamanioBytes, a.UrlStorage, a.CreatedAt,
                null, a.SubidoPorUsuarioId == Guid.Empty ? null : a.SubidoPorUsuarioId, a.Texto, a.Compartido)).ToList(),
            r.Archivada, r.ArchivadaAt, nextNum,
            r.Destinatarios.Select(d => new DestinatarioRespuestaDto(d.PersonaId, d.Nombre, d.Email)).ToList());
    }

    // Lista el historial de versiones de una respuesta (mas reciente primero).
    public async Task<IReadOnlyList<PqrsdRespuestaVersionDto>> ListarVersionesRespuestaAsync(Guid expedienteId, Guid respuestaId, CancellationToken ct)
    {
        var existe = await _db.PqrsdRespuestas.AsNoTracking()
            .AnyAsync(x => x.Id == respuestaId && x.ExpedienteId == expedienteId, ct);
        if (!existe) return Array.Empty<PqrsdRespuestaVersionDto>();

        var vs = await _db.PqrsdRespuestaVersiones.AsNoTracking()
            .Where(v => v.RespuestaId == respuestaId)
            .OrderByDescending(v => v.Numero)
            .ToListAsync(ct);
        var nombres = await ResolverNombresUsuariosAsync(vs.Select(v => v.AutorUsuarioId).Where(g => g != Guid.Empty), ct);
        return vs.Select(v => new PqrsdRespuestaVersionDto(
            v.Numero, v.Asunto, v.CuerpoHtml,
            !string.IsNullOrWhiteSpace(v.AutorNombre) ? v.AutorNombre
                : (v.AutorUsuarioId != Guid.Empty && nombres.TryGetValue(v.AutorUsuarioId, out var n) ? n : null),
            v.CreatedAt)).ToList();
    }

    // Compone el HTML del documento oficial (membrete + cuerpo) para vista previa o generacion de PDF.
    // Usa la identidad del Tenant + su config de membrete; el cuerpoHtml es el texto de la respuesta.
    public async Task<string?> ComponerDocumentoRespuestaAsync(Guid expedienteId, string cuerpoHtml, CancellationToken ct)
    {
        var exp = await _db.PqrsdExpedientes.AsNoTracking()
            .Include(e => e.TipoConfig)
            .Include(e => e.RadicadorPersona)
            .FirstOrDefaultAsync(e => e.Id == expedienteId, ct);
        if (exp is null) return null;

        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == exp.TenantId, ct);
        if (tenant is null) return null;

        var tipoNombre = exp.TipoConfig?.Nombre ?? exp.Tipo.ToString();
        var destinatario = exp.IdentidadReservada
            ? "Identidad reservada"
            : exp.RadicadorPersona is null
                ? null
                : $"{exp.RadicadorPersona.Nombres} {exp.RadicadorPersona.Apellidos}".Trim();

        var contenido = new Propia.Application.Documents.MembreteDocContenido(
            TipoBadge: "Respuesta PQRSD",
            RadicadoLabel: "Radicado",
            Radicado: exp.NumeroRadicado,
            Fecha: exp.RespuestaAdminAt ?? DateTimeOffset.UtcNow,
            CuerpoHtml: cuerpoHtml ?? "",
            DestinatarioNombre: destinatario,
            // Referencia del expediente (tipo) como linea del destinatario; el badge dice "Respuesta PQRSD".
            DestinatarioLinea: string.IsNullOrWhiteSpace(tipoNombre) ? null : $"Ref. {tipoNombre} - {exp.NumeroRadicado}");

        return _membrete.Construir(tenant, contenido);
    }

    // ===================== Plantillas de respuesta (combinacion de correspondencia) =====================

    private static readonly (string Token, string Desc)[] _tokensPlantilla = new[]
    {
        ("copropiedad.nombre", "Nombre de la copropiedad"),
        ("copropiedad.nit", "NIT de la copropiedad"),
        ("copropiedad.direccion", "Direccion de la copropiedad"),
        ("copropiedad.ciudad", "Ciudad"),
        ("radicado.numero", "Numero de radicado"),
        ("radicado.tipo", "Tipo de PQRSD"),
        ("radicado.categoria", "Categoria"),
        ("radicado.fecha", "Fecha de radicacion"),
        ("radicado.estado", "Estado actual"),
        ("solicitante.nombre", "Nombre del solicitante"),
        ("solicitante.identificacion", "Identificacion del solicitante"),
        ("solicitante.correo", "Correo del solicitante"),
        ("solicitante.telefono", "Telefono del solicitante"),
        ("usuario.nombre", "Nombre del solicitante (alias)"),
        ("usuario.identificacion", "Identificacion del solicitante (alias)"),
        ("unidad.numero", "Numero de la unidad"),
        ("unidad.torre", "Torre/bloque de la unidad"),
        ("unidad_privada.propietario", "Propietario de la unidad"),
        ("unidad.propietario", "Propietario de la unidad (alias)"),
        ("gestor.nombre", "Nombre de quien responde (usuario actual)"),
        ("fecha.hoy", "Fecha de hoy"),
    };

    public IReadOnlyList<PqrsdTokenDto> ListarTokensPlantilla()
        => _tokensPlantilla.Select(t => new PqrsdTokenDto("{" + t.Token + "}", t.Desc)).ToList();

    public async Task<IReadOnlyList<PqrsdPlantillaDto>> ListarPlantillasAsync(CancellationToken ct)
    {
        await SembrarPlantillasDesdeSemillaSiVacioAsync(ct);
        return await _db.PqrsdPlantillasRespuesta.AsNoTracking().Where(p => p.Activa)
            .OrderBy(p => p.Orden).ThenBy(p => p.Nombre)
            .Select(p => new PqrsdPlantillaDto(p.Id, p.Nombre, p.CuerpoHtml)).ToListAsync(ct);
    }

    // "Nace con" las plantillas: si la copropiedad no tiene NINGUNA plantilla propia, copia las
    // semillas activas del catalogo global (Super Admin). Idempotente: solo actua si esta vacio,
    // asi el admin puede borrar las que no quiera sin que reaparezcan. Corre en el contexto del
    // tenant actual (RLS ok: inserta filas de su propio tenant_id).
    private async Task SembrarPlantillasDesdeSemillaSiVacioAsync(CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentTenantId;
        if (tenantId is null) return;
        if (await _db.PqrsdPlantillasRespuesta.AnyAsync(ct)) return;   // ya tiene (query filter -> del tenant)

        var semillas = await _db.PqrsdPlantillasSemilla.AsNoTracking()
            .Where(s => s.Activa).OrderBy(s => s.Orden).ThenBy(s => s.Nombre).ToListAsync(ct);
        if (semillas.Count == 0) return;

        foreach (var s in semillas)
            _db.PqrsdPlantillasRespuesta.Add(new PqrsdPlantillaRespuesta
            {
                TenantId = tenantId.Value,
                Nombre = s.Nombre,
                CuerpoHtml = s.CuerpoHtml,
                Activa = true,
                Orden = s.Orden
            });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PqrsdPlantillaDto> CrearPlantillaAsync(GuardarPlantillaRequest req, CancellationToken ct)
    {
        var count = await _db.PqrsdPlantillasRespuesta.CountAsync(ct);
        var p = new PqrsdPlantillaRespuesta { Nombre = (req.Nombre ?? "Plantilla").Trim(), CuerpoHtml = req.CuerpoHtml ?? "", Activa = true, Orden = count };
        _db.PqrsdPlantillasRespuesta.Add(p);
        await _db.SaveChangesAsync(ct);
        return new PqrsdPlantillaDto(p.Id, p.Nombre, p.CuerpoHtml);
    }

    public async Task<bool> ActualizarPlantillaAsync(Guid id, GuardarPlantillaRequest req, CancellationToken ct)
    {
        var p = await _db.PqrsdPlantillasRespuesta.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        p.Nombre = (req.Nombre ?? p.Nombre).Trim();
        p.CuerpoHtml = req.CuerpoHtml ?? "";
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarPlantillaAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.PqrsdPlantillasRespuesta.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        _db.PqrsdPlantillasRespuesta.Remove(p);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<string?> ResolverPlantillaAsync(Guid expedienteId, Guid plantillaId, CancellationToken ct)
    {
        var plantilla = await _db.PqrsdPlantillasRespuesta.AsNoTracking().FirstOrDefaultAsync(p => p.Id == plantillaId, ct);
        if (plantilla is null) return null;
        var exp = await _db.PqrsdExpedientes.AsNoTracking()
            .Include(e => e.Categoria).Include(e => e.TipoConfig).Include(e => e.RadicadorPersona)
            .FirstOrDefaultAsync(e => e.Id == expedienteId, ct);
        if (exp is null) return null;
        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == exp.TenantId, ct);
        var (_, gestorNombre) = ActorActual();
        var esCO = new System.Globalization.CultureInfo("es-CO");

        string unidadNum = "", unidadTorre = "", propietario = "";
        if (exp.UnidadPrivadaId is { } uid)
        {
            var unidad = await _db.UnidadesPrivadas.AsNoTracking().Include(u => u.Torre).FirstOrDefaultAsync(u => u.Id == uid, ct);
            if (unidad is not null) { unidadNum = unidad.Numero; unidadTorre = unidad.Torre?.Nombre ?? ""; }
            var propId = await _db.UnidadPersonas.AsNoTracking()
                .Where(up => up.UnidadId == uid && up.Rol == Domain.Enums.RolUnidadPersona.Propietario && up.PersonaId != null)
                .Select(up => up.PersonaId).FirstOrDefaultAsync(ct);
            if (propId is { } pid)
            {
                var prop = await _db.Personas.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid, ct);
                if (prop is not null) propietario = $"{prop.Nombres} {prop.Apellidos}".Trim();
            }
        }

        var rad = exp.RadicadorPersona;
        var solNombre = exp.IdentidadReservada ? "(reservada)" : (rad is null ? "" : $"{rad.Nombres} {rad.Apellidos}".Trim());
        var solDoc = exp.IdentidadReservada ? "" : (rad?.Documento ?? "");

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["copropiedad.nombre"] = tenant?.Nombre ?? "",
            ["copropiedad.nit"] = tenant?.Nit ?? "",
            ["copropiedad.direccion"] = tenant?.Direccion ?? "",
            ["copropiedad.ciudad"] = tenant?.Ciudad ?? "",
            ["radicado.numero"] = exp.NumeroRadicado,
            ["radicado.tipo"] = exp.TipoConfig?.Nombre ?? exp.Tipo.ToString(),
            ["radicado.categoria"] = exp.Categoria?.Nombre ?? "",
            ["radicado.fecha"] = exp.CreatedAt.ToLocalTime().ToString("dd 'de' MMMM 'de' yyyy", esCO),
            ["radicado.estado"] = exp.Estado.ToString(),
            ["solicitante.nombre"] = solNombre,
            ["solicitante.identificacion"] = solDoc,
            ["solicitante.correo"] = exp.IdentidadReservada ? "" : (rad?.Email ?? ""),
            ["solicitante.telefono"] = exp.IdentidadReservada ? "" : (rad?.Telefono ?? ""),
            ["usuario.nombre"] = solNombre,
            ["usuario.identificacion"] = solDoc,
            ["unidad.numero"] = unidadNum,
            ["unidad.torre"] = unidadTorre,
            ["unidad_privada.propietario"] = propietario,
            ["unidad.propietario"] = propietario,
            ["gestor.nombre"] = gestorNombre ?? "",
            ["fecha.hoy"] = DateTimeOffset.Now.ToLocalTime().ToString("dd 'de' MMMM 'de' yyyy", esCO),
        };

        return System.Text.RegularExpressions.Regex.Replace(plantilla.CuerpoHtml, @"\{([a-zA-Z_]+\.[a-zA-Z_]+)\}", m =>
        {
            var key = m.Groups[1].Value;
            return map.TryGetValue(key, out var val) ? System.Net.WebUtility.HtmlEncode(val) : m.Value;
        });
    }

    // ---- Config del formulario publico (admin): campos opcionales + textos + orden de campos fijos ----
    // Claves canonicas de los campos FIJOS del formulario publico, en su orden por defecto.
    private static readonly string[] CamposFijosDefault =
        { "tipo", "categoria", "torre", "unidad", "tipoDoc", "documento", "nombres", "apellidos", "correo", "telefono", "descripcion" };

    private static IReadOnlyList<string> ParseOrdenCamposFijos(string? json)
    {
        List<string>? guardado = null;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try { guardado = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json!); } catch { }
        }
        if (guardado is null || guardado.Count == 0) return CamposFijosDefault;
        // Solo claves conocidas, en el orden guardado; anexar las que falten (robustez ante nuevas claves).
        var res = guardado.Where(CamposFijosDefault.Contains).Distinct().ToList();
        foreach (var k in CamposFijosDefault) if (!res.Contains(k)) res.Add(k);
        return res;
    }

    public async Task<PqrsdFormularioPublicoConfigDto> GetFormularioPublicoConfigAsync(CancellationToken ct)
    {
        var c = await _db.PqrsdFormularioPublicoConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        return new PqrsdFormularioPublicoConfigDto(
            c?.MostrarTorre ?? true, c?.MostrarCorreo ?? true, c?.MostrarTelefono ?? true,
            c?.EncabezadoTexto, c?.PieTexto, ParseOrdenCamposFijos(c?.OrdenCamposFijosJson));
    }

    public async Task<bool> GuardarFormularioPublicoConfigAsync(PqrsdFormularioPublicoConfigDto req, CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentTenantId ?? throw new InvalidOperationException("No hay copropiedad activa.");
        var c = await _db.PqrsdFormularioPublicoConfigs.FirstOrDefaultAsync(ct);
        if (c is null)
        {
            c = new PqrsdFormularioPublicoConfig { TenantId = tenantId };
            _db.PqrsdFormularioPublicoConfigs.Add(c);
        }
        c.MostrarTorre = req.MostrarTorre;
        c.MostrarCorreo = req.MostrarCorreo;
        c.MostrarTelefono = req.MostrarTelefono;
        c.EncabezadoTexto = string.IsNullOrWhiteSpace(req.EncabezadoTexto) ? null : req.EncabezadoTexto.Trim();
        c.PieTexto = string.IsNullOrWhiteSpace(req.PieTexto) ? null : req.PieTexto.Trim();
        // Orden de campos fijos: guardar solo claves conocidas; null si es el orden por defecto.
        var orden = (req.OrdenCamposFijos ?? new List<string>()).Where(CamposFijosDefault.Contains).Distinct().ToList();
        c.OrdenCamposFijosJson = (orden.Count > 0 && !orden.SequenceEqual(CamposFijosDefault))
            ? System.Text.Json.JsonSerializer.Serialize(orden) : null;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // Marca/desmarca un campo dinamico para que se pida (o no) en el formulario publico.
    public async Task<bool> SetCampoPublicoAsync(Guid campoId, bool mostrar, CancellationToken ct)
    {
        var c = await _db.PqrsdCampos.FirstOrDefaultAsync(x => x.Id == campoId, ct);
        if (c is null) return false;
        c.MostrarEnPublico = mostrar;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<RadicarPublicoResultDto> RadicarPublicoAsync(Guid tenantId, RadicarPublicoRequest req, string? ipOrigen, CancellationToken ct)
    {
        // Honeypot: bots que llenan el campo oculto se descartan silenciosamente (no es error visible).
        if (!string.IsNullOrWhiteSpace(req.Website))
            throw new InvalidOperationException("No se pudo procesar la solicitud.");

        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null || tenant.Estado != EstadoCopropiedad.Activa)
            throw new InvalidOperationException("La copropiedad no esta disponible para radicar.");

        if (!req.AceptaTratamiento)
            throw new InvalidOperationException("Debes autorizar el tratamiento de tus datos personales para radicar.");
        if (string.IsNullOrWhiteSpace(req.Documento))
            throw new InvalidOperationException("El numero de identificacion es obligatorio.");
        if (string.IsNullOrWhiteSpace(req.Nombres))
            throw new InvalidOperationException("El nombre es obligatorio.");
        if (string.IsNullOrWhiteSpace(req.UnidadTexto))
            throw new InvalidOperationException("Debes indicar tu unidad (ej. 101, A-203).");
        var descr = (req.Descripcion ?? "").Trim();
        if (descr.Length < 20)
            throw new InvalidOperationException("La descripcion es obligatoria (minimo 20 caracteres).");

        await ActivarTenantPublicoAsync(tenantId);
        await AsegurarCatalogoBaseAsync(ct);

        var tipo = await _db.PqrsdTipos.AsNoTracking().FirstOrDefaultAsync(t => t.Id == req.TipoId && t.Activo, ct)
            ?? throw new InvalidOperationException("El tipo de solicitud seleccionado no es valido.");
        var categoria = await _db.PqrsdCategorias.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.CategoriaId && c.Activa, ct)
            ?? throw new InvalidOperationException("La categoria seleccionada no es valida.");

        // --- Radicador: resolver/crear la Persona GLOBAL por (tipoDocumento, documento) ---
        var doc = req.Documento.Trim();
        var persona = await _db.Personas.FirstOrDefaultAsync(p => p.TipoDocumento == req.TipoDocumento && p.Documento == doc, ct);
        var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
        var telefono = string.IsNullOrWhiteSpace(req.Telefono) ? null : req.Telefono.Trim();
        if (persona is null)
        {
            // Email es unico global (citext): si ya lo usa otra persona, no lo asignamos para no romper el indice.
            if (email is not null && await _db.Personas.AnyAsync(p => p.Email == email, ct)) email = null;
            persona = new Persona
            {
                TipoDocumento = req.TipoDocumento,
                Documento = doc,
                Nombres = req.Nombres.Trim(),
                Apellidos = (req.Apellidos ?? "").Trim(),
                Email = email,
                Telefono = telefono,
                PerfilIncompleto = true,
                EstadoDirectorio = EstadoDirectorio.Activo,
                AceptoTratamientoDatos = true,
                FechaAceptacionDatos = DateTimeOffset.UtcNow,
                CanalAceptacion = CanalAceptacionDatos.FormularioWeb,
                IpAceptacion = ipOrigen
            };
            _db.Personas.Add(persona);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            // No sobreescribir datos existentes: solo completar contacto vacio.
            var cambio = false;
            if (string.IsNullOrWhiteSpace(persona.Email) && email is not null
                && !await _db.Personas.AnyAsync(p => p.Id != persona.Id && p.Email == email, ct))
            { persona.Email = email; cambio = true; }
            if (string.IsNullOrWhiteSpace(persona.Telefono) && telefono is not null)
            { persona.Telefono = telefono; cambio = true; }
            if (cambio) await _db.SaveChangesAsync(ct);
        }

        // --- Unidad: match EXACTO por numero (+ torre opcional). Sin busqueda: el residente conoce su unidad. ---
        var unidadTxt = req.UnidadTexto.Trim();
        var torreTxt = req.TorreTexto?.Trim();
        var unidadTxtLower = unidadTxt.ToLower();
        var qUnidad = _db.UnidadesPrivadas.AsNoTracking().Where(u => u.Numero.ToLower() == unidadTxtLower);
        if (!string.IsNullOrWhiteSpace(torreTxt))
        {
            var torreTxtLower = torreTxt.ToLower();
            var torreId = await _db.Torres.AsNoTracking()
                .Where(t => t.Nombre.ToLower() == torreTxtLower).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
            if (torreId is not null) qUnidad = qUnidad.Where(u => u.TorreId == torreId);
        }
        var unidadId = await qUnidad.Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);

        // Si no se pudo enlazar la unidad, conservamos el dato escrito al inicio de la descripcion (no se pierde).
        if (unidadId is null)
        {
            var encab = string.IsNullOrWhiteSpace(torreTxt)
                ? $"[Radicado externo] Unidad indicada por el solicitante: {unidadTxt}"
                : $"[Radicado externo] Unidad indicada por el solicitante: {torreTxt} - {unidadTxt}";
            descr = (encab + "\n\n" + descr);
            if (descr.Length > 2000) descr = descr[..2000];
        }

        // Campos dinamicos del formulario publico: solo se aceptan los que realmente estan marcados
        // para el publico (seguridad: un submit externo no puede setear cualquier campo interno).
        List<PqrsdCampoValorDto>? camposVals = null;
        if (req.CamposDinamicos is { Count: > 0 })
        {
            var permitidos = (await _db.PqrsdCampos.AsNoTracking()
                .Where(c => c.Activo && c.MostrarEnPublico).Select(c => c.Id).ToListAsync(ct)).ToHashSet();
            camposVals = req.CamposDinamicos
                .Where(kv => permitidos.Contains(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
                .Select(kv => new PqrsdCampoValorDto(kv.Key, kv.Value)).ToList();
            if (camposVals.Count == 0) camposVals = null;
        }

        var radReq = new RadicarPqrsdRequest(
            Tipo: tipo.Legal,
            CategoriaId: categoria.Id,
            Descripcion: descr,
            IdentidadReservada: false,
            Adjuntos: null,
            UnidadPrivadaId: unidadId,
            RadicadorPersonaId: persona.Id,
            Campos: camposVals,
            TipoId: tipo.Id);

        var detalle = await RadicarAsync(radReq, ct);
        return new RadicarPublicoResultDto(detalle.NumeroRadicado);
    }

    private async Task<string> GenerarNumeroRadicadoAsync(CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var prefijo = $"PQRSD-{year}-";
        var prefijoLegacy = $"PQRS-{year}-";   // radicados emitidos antes del cambio de sigla; no se reescriben
        var ultimos = await _db.PqrsdExpedientes.AsNoTracking()
            .Where(x => x.NumeroRadicado.StartsWith(prefijo) || x.NumeroRadicado.StartsWith(prefijoLegacy))
            .Select(x => x.NumeroRadicado)
            .ToListAsync(ct);
        int max = 0;
        foreach (var n in ultimos)
        {
            // El consecutivo son los digitos tras el ultimo guion, valga cual valga el prefijo.
            if (int.TryParse(n[(n.LastIndexOf('-') + 1)..], out var s) && s > max) max = s;
        }
        return $"{prefijo}{(max + 1):D4}";
    }

}
