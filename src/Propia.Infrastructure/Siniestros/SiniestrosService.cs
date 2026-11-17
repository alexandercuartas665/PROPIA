using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Catalogos;
using Propia.Application.Siniestros;
using Propia.Application.Tareas;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Application.MiCopropiedad;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Siniestros;

/// <summary>
/// Modulo Siniestros. El aislamiento por tenant lo aplica el HasQueryFilter global (y RLS FORCE en la BD).
/// Personas es global: el responsable se valida contra DirectorioVinculos (acotado al tenant).
/// </summary>
public class SiniestrosService : ISiniestrosService
{
    /// <summary>Lista por copropiedad del tipo de siniestro (CatalogoListasRegistro).</summary>
    public const string ListaTipo = "siniestro.tipo";

    private readonly PropiaDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly IOpcionesEfectivas _opciones;
    private readonly ITareasService _tareas;

    public SiniestrosService(PropiaDbContext db, IHttpContextAccessor http, IOpcionesEfectivas opciones, ITareasService tareas)
    {
        _db = db; _http = http; _opciones = opciones; _tareas = tareas;
    }

    private (Guid? UsuarioId, string? Nombre) ActorActual()
    {
        var u = _http.HttpContext?.User;
        Guid? uid = Guid.TryParse(u?.FindFirst("user_id")?.Value, out var g) ? g : null;
        var nombre = u?.FindFirst("name")?.Value
            ?? u?.FindFirst(ClaimTypes.Name)?.Value
            ?? u?.FindFirst("email")?.Value
            ?? u?.FindFirst(ClaimTypes.Email)?.Value;
        return (uid, nombre);
    }

    // =============================== Siniestros ===============================

    public async Task<IReadOnlyList<SiniestroDto>> ListAsync(CancellationToken ct)
    {
        await AsegurarEstadosBaseAsync(ct);
        var lista = await _db.Siniestros.AsNoTracking().Include(s => s.Estado).Include(s => s.Poliza)
            .OrderByDescending(s => s.FechaOcurrencia).ThenByDescending(s => s.Numero).ToListAsync(ct);
        return await ToDtosAsync(lista, ct);
    }

    public async Task<SiniestroDto?> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var s = await _db.Siniestros.AsNoTracking().Include(x => x.Estado).Include(x => x.Poliza)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return null;
        return (await ToDtosAsync(new List<Siniestro> { s }, ct))[0];
    }

    public async Task<SiniestroDto> CrearAsync(CrearSiniestroRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Titulo))
            throw new InvalidOperationException("El titulo del siniestro es obligatorio.");
        await AsegurarEstadosBaseAsync(ct);

        var s = new Siniestro
        {
            Titulo = req.Titulo.Trim(),
            Descripcion = Limpio(req.Descripcion),
            FechaOcurrencia = req.FechaOcurrencia,
            FechaReporte = req.FechaReporte,
            NumeroReclamacion = Limpio(req.NumeroReclamacion),
            MontoEstimado = req.MontoEstimado,
            MontoReclamado = req.MontoReclamado,
            Deducible = req.Deducible,
        };
        s.TipoClave = await ValidarTipoAsync(req.TipoClave, ct);
        await AplicarElementoAsync(s, req.ElementoTipo, req.ElementoId, req.ElementoOtro, ct);
        await ValidarResponsableAsync(req.ResponsablePersonaId, ct);
        s.ResponsablePersonaId = req.ResponsablePersonaId;
        s.PolizaId = await ValidarPolizaAsync(req.PolizaId, s.FechaOcurrencia, ct);
        ValidarFechasYMontos(s);

        SiniestroEstado estado;
        if (req.EstadoId is { } eid)
        {
            estado = await _db.SiniestroEstados.FirstOrDefaultAsync(e => e.Id == eid, ct)
                ?? throw new InvalidOperationException("El estado indicado no existe.");
            if (!estado.Activo) throw new InvalidOperationException($"El estado '{estado.Nombre}' esta inactivo.");
            if (EstadoSiniestroClave.PideMontoReconocido(estado.Clave))
                throw new InvalidOperationException($"Un siniestro nuevo no puede nacer en '{estado.Nombre}': registra primero el monto reconocido.");
        }
        else
        {
            estado = await _db.SiniestroEstados.FirstAsync(e => e.Clave == EstadoSiniestroClave.Reportado, ct);
        }
        s.EstadoId = estado.Id;

        // Consecutivo SIN-AAAA-NNNN. Un choque concurrente lo frena el indice unico (tenant, numero): se reintenta.
        for (var intento = 0; ; intento++)
        {
            s.Numero = await SiguienteNumeroAsync(ct);
            _db.Siniestros.Add(s);
            try
            {
                await _db.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateException) when (intento < 2)
            {
                _db.Entry(s).State = EntityState.Detached;
            }
        }

        var (uid, nombre) = ActorActual();
        _db.SiniestroComentarios.Add(new SiniestroComentario
        {
            SiniestroId = s.Id,
            EsSistema = true,
            AutorUsuarioId = uid,
            AutorNombre = nombre,
            Texto = $"Siniestro {s.Numero} registrado en '{estado.Nombre}'.",
        });
        await _db.SaveChangesAsync(ct);
        return (await ObtenerAsync(s.Id, ct))!;
    }

    public async Task<bool> ActualizarAsync(Guid id, ActualizarSiniestroRequest req, CancellationToken ct)
    {
        var s = await _db.Siniestros.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return false;
        var limpiar = new HashSet<string>(req.Limpiar ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        if (req.Titulo is not null)
        {
            if (string.IsNullOrWhiteSpace(req.Titulo)) throw new InvalidOperationException("El titulo del siniestro es obligatorio.");
            s.Titulo = req.Titulo.Trim();
        }
        if (req.Descripcion is not null) s.Descripcion = Limpio(req.Descripcion);
        if (limpiar.Contains("descripcion")) s.Descripcion = null;

        if (req.TipoClave is not null) s.TipoClave = await ValidarTipoAsync(req.TipoClave, ct);
        if (limpiar.Contains("tipo")) s.TipoClave = null;

        if (req.FechaOcurrencia is { } fo) s.FechaOcurrencia = fo;
        if (req.FechaReporte is { } fr) s.FechaReporte = fr;
        if (limpiar.Contains("fechareporte")) s.FechaReporte = null;

        if (req.ElementoTipo is { } et) await AplicarElementoAsync(s, et, req.ElementoId, req.ElementoOtro, ct);

        if (req.NumeroReclamacion is not null) s.NumeroReclamacion = Limpio(req.NumeroReclamacion);
        if (limpiar.Contains("numeroreclamacion")) s.NumeroReclamacion = null;

        if (req.ResponsablePersonaId is { } rp)
        {
            await ValidarResponsableAsync(rp, ct);
            s.ResponsablePersonaId = rp;
        }
        if (limpiar.Contains("responsable")) s.ResponsablePersonaId = null;

        if (req.MontoEstimado is { } me) s.MontoEstimado = me;
        if (limpiar.Contains("montoestimado")) s.MontoEstimado = null;
        if (req.MontoReclamado is { } mr) s.MontoReclamado = mr;
        if (limpiar.Contains("montoreclamado")) s.MontoReclamado = null;
        if (req.Deducible is { } de) s.Deducible = de;
        if (limpiar.Contains("deducible")) s.Deducible = null;
        if (req.MontoReconocido is { } mrc) s.MontoReconocido = mrc;
        if (req.FechaPago is { } fp) s.FechaPago = fp;
        if (limpiar.Contains("fechapago")) s.FechaPago = null;

        // La poliza se revalida si cambia la poliza O la fecha de ocurrencia (vigencia).
        if (req.PolizaId is { } pid) s.PolizaId = pid;
        if (limpiar.Contains("poliza")) s.PolizaId = null;
        if (req.PolizaId is not null || req.FechaOcurrencia is not null)
            s.PolizaId = await ValidarPolizaAsync(s.PolizaId, s.FechaOcurrencia, ct);

        // Un siniestro pagado o cerrado no puede quedar sin monto reconocido.
        if (limpiar.Contains("montoreconocido"))
        {
            var clave = await _db.SiniestroEstados.Where(e => e.Id == s.EstadoId).Select(e => e.Clave).FirstOrDefaultAsync(ct);
            if (EstadoSiniestroClave.PideMontoReconocido(clave))
                throw new InvalidOperationException("Un siniestro pagado o cerrado debe conservar el monto reconocido.");
            s.MontoReconocido = null;
        }

        ValidarFechasYMontos(s);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CambiarEstadoAsync(Guid id, CambiarEstadoSiniestroRequest req, CancellationToken ct)
    {
        var s = await _db.Siniestros.Include(x => x.Estado).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return false;
        var destino = await _db.SiniestroEstados.FirstOrDefaultAsync(e => e.Id == req.EstadoId, ct)
            ?? throw new InvalidOperationException("El estado indicado no existe.");
        if (destino.Id == s.EstadoId) return true;
        if (!destino.Activo) throw new InvalidOperationException($"El estado '{destino.Nombre}' esta inactivo.");

        if (req.MontoReconocido is { } mrc)
        {
            if (mrc < 0) throw new InvalidOperationException("El monto reconocido no puede ser negativo.");
            s.MontoReconocido = mrc;
        }
        if (EstadoSiniestroClave.PideMontoReconocido(destino.Clave) && s.MontoReconocido is null)
            throw new InvalidOperationException($"Para pasar a '{destino.Nombre}' indica el monto reconocido por la aseguradora.");

        if (destino.Clave == EstadoSiniestroClave.Pagado)
            s.FechaPago = req.FechaPago ?? s.FechaPago ?? HoyColombia();
        else if (req.FechaPago is { } fp)
            s.FechaPago = fp;

        s.FechaCierre = destino.Clave == EstadoSiniestroClave.Cerrado ? DateTimeOffset.UtcNow : null;

        var anterior = s.Estado?.Nombre ?? "(sin estado)";
        s.EstadoId = destino.Id;
        ValidarFechasYMontos(s);

        var (uid, nombre) = ActorActual();
        _db.SiniestroComentarios.Add(new SiniestroComentario
        {
            SiniestroId = s.Id,
            EsSistema = true,
            AutorUsuarioId = uid,
            AutorNombre = nombre,
            Texto = $"Estado: {anterior} -> {destino.Nombre}.",
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarAsync(Guid id, CancellationToken ct)
    {
        var s = await _db.Siniestros.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return false;
        // Valores EAV sin FK: se borran a mano. Documentos y comentarios caen en cascada.
        await _db.SiniestroCampoValores.Where(v => v.SiniestroId == id).ExecuteDeleteAsync(ct);
        _db.Siniestros.Remove(s);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<SiniestroDto?> CrearTareaReparacionAsync(Guid id, CancellationToken ct)
    {
        var s = await _db.Siniestros.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return null;
        if (s.TareaId is { } previa)
        {
            var vigente = await _db.Tareas.AsNoTracking().Where(t => t.Id == previa && !t.Eliminada)
                .Select(t => t.NumeroTarea).FirstOrDefaultAsync(ct);
            if (vigente is not null)
                throw new InvalidOperationException($"El siniestro ya tiene la tarea de reparacion {vigente}.");
        }

        // Elemento afectado: la tarea queda ligada a el (OrigenEntidadId) para que salga en su historial.
        string? origenTipo = s.ElementoTipo switch
        {
            ElementoSiniestro.Equipo => "equipo",
            ElementoSiniestro.Zona => "zona",
            ElementoSiniestro.Unidad => "inmueble",
            _ => null
        };
        string? origenRef = s.ElementoTipo switch
        {
            ElementoSiniestro.Equipo => await _db.EquiposActivos.AsNoTracking().Where(e => e.Id == s.ElementoId).Select(e => (string?)e.Nombre).FirstOrDefaultAsync(ct),
            ElementoSiniestro.Zona => await _db.ZonasComunes.AsNoTracking().Where(z => z.Id == s.ElementoId).Select(z => (string?)z.Nombre).FirstOrDefaultAsync(ct),
            ElementoSiniestro.Unidad => await _db.UnidadesPrivadas.AsNoTracking().Where(u => u.Id == s.ElementoId).Select(u => (string?)u.Numero).FirstOrDefaultAsync(ct),
            ElementoSiniestro.Otro => s.ElementoOtro,
            _ => null
        };

        var descripcion = $"Reparaci\u00f3n del siniestro {s.Numero}."
            + (string.IsNullOrWhiteSpace(origenRef) ? "" : $" Elemento afectado: {origenRef}.")
            + (string.IsNullOrWhiteSpace(s.Descripcion) ? "" : "\n\n" + s.Descripcion.Trim());
        var titulo = $"Reparaci\u00f3n: {s.Titulo}";
        if (titulo.Length > 200) titulo = titulo[..200];

        var tarea = await _tareas.CrearTareaAsync(new CrearTareaRequest(
            Titulo: titulo,
            Descripcion: descripcion,
            Prioridad: PrioridadTarea.Normal,
            EstadoId: null,
            AsignadoPersonaId: s.ResponsablePersonaId,
            FechaInicio: null,
            FechaVencimiento: null,
            PadreId: null,
            EtiquetaIds: null,
            OrigenTipo: origenTipo,
            OrigenReferencia: origenTipo is null ? null : origenRef,
            OrigenEntidadId: origenTipo is null ? null : s.ElementoId,
            ModuloOrigenCodigo: OrigenModulo.Siniestro,
            ModuloOrigenEntidadId: s.Id), ct);

        s.TareaId = tarea.Id;
        var (uid, nombre) = ActorActual();
        _db.SiniestroComentarios.Add(new SiniestroComentario
        {
            SiniestroId = s.Id,
            EsSistema = true,
            AutorUsuarioId = uid,
            AutorNombre = nombre,
            Texto = $"Tarea de reparaci\u00f3n {tarea.NumeroTarea} creada.",
        });
        await _db.SaveChangesAsync(ct);
        return await ObtenerAsync(id, ct);
    }

    // ---------------------------- reglas y apoyo ----------------------------

    private static DateOnly HoyColombia() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-5)).DateTime);

    private async Task<string> SiguienteNumeroAsync(CancellationToken ct)
    {
        var anio = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-5)).Year;
        var pref = $"SIN-{anio}-";
        var usados = await _db.Siniestros.AsNoTracking().Where(x => x.Numero.StartsWith(pref)).Select(x => x.Numero).ToListAsync(ct);
        var max = usados.Select(r => int.TryParse(r.AsSpan(pref.Length), out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"{pref}{max + 1:D4}";
    }

    private static void ValidarFechasYMontos(Siniestro s)
    {
        if (s.FechaOcurrencia > HoyColombia())
            throw new InvalidOperationException($"La fecha de ocurrencia ({s.FechaOcurrencia:dd/MM/yyyy}) no puede ser futura.");
        if (s.FechaReporte is { } fr && fr < s.FechaOcurrencia)
            throw new InvalidOperationException(
                $"La fecha de reporte ({fr:dd/MM/yyyy}) no puede ser anterior a la de ocurrencia ({s.FechaOcurrencia:dd/MM/yyyy}).");
        if (s.FechaPago is { } fp && fp < s.FechaOcurrencia)
            throw new InvalidOperationException("La fecha de pago no puede ser anterior a la de ocurrencia.");
        if (s.MontoEstimado < 0 || s.MontoReclamado < 0 || s.Deducible < 0 || s.MontoReconocido < 0)
            throw new InvalidOperationException("Los montos no pueden ser negativos.");
    }

    private async Task<string?> ValidarTipoAsync(string? clave, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(clave)) return null;
        var c = clave.Trim();
        var opciones = await _opciones.OpcionesAsync(ListaTipo, incluirOcultas: true, ct);
        if (!opciones.Any(o => o.Clave == c))
            throw new InvalidOperationException("El tipo de siniestro indicado no existe en esta copropiedad.");
        return c;
    }

    /// <summary>Fija el elemento afectado validando que pertenezca a la copropiedad (vinculo logico, sin FK).</summary>
    private async Task AplicarElementoAsync(Siniestro s, ElementoSiniestro tipo, Guid? elementoId, string? otro, CancellationToken ct)
    {
        switch (tipo)
        {
            case ElementoSiniestro.Ninguno:
                s.ElementoId = null; s.ElementoOtro = null;
                break;
            case ElementoSiniestro.Otro:
                if (string.IsNullOrWhiteSpace(otro))
                    throw new InvalidOperationException("Describe el elemento afectado.");
                s.ElementoId = null; s.ElementoOtro = otro.Trim();
                break;
            case ElementoSiniestro.Equipo:
            case ElementoSiniestro.Zona:
            case ElementoSiniestro.Unidad:
                if (elementoId is not { } eid || eid == Guid.Empty)
                    throw new InvalidOperationException("Elige el elemento afectado.");
                var existe = tipo switch
                {
                    ElementoSiniestro.Equipo => await _db.EquiposActivos.AnyAsync(e => e.Id == eid, ct),
                    ElementoSiniestro.Zona => await _db.ZonasComunes.AnyAsync(z => z.Id == eid, ct),
                    _ => await _db.UnidadesPrivadas.AnyAsync(u => u.Id == eid, ct),
                };
                if (!existe) throw new InvalidOperationException("El elemento afectado no existe en esta copropiedad.");
                s.ElementoId = eid; s.ElementoOtro = null;
                break;
            default:
                throw new InvalidOperationException("Tipo de elemento afectado no valido.");
        }
        s.ElementoTipo = tipo;
    }

    private async Task ValidarResponsableAsync(Guid? personaId, CancellationToken ct)
    {
        if (personaId is not { } pid || pid == Guid.Empty) return;
        var vinculada = await _db.DirectorioVinculos.AsNoTracking()
            .AnyAsync(v => v.EntidadTipo == EntidadDirectorio.Persona && v.EntidadId == pid, ct);
        if (!vinculada)
            throw new InvalidOperationException("El responsable no pertenece a esta copropiedad. Vinculalo primero en el Directorio.");
    }

    /// <summary>La poliza debe ser de la copropiedad y cubrir la fecha de ocurrencia (fuera de vigencia no responde).</summary>
    private async Task<Guid?> ValidarPolizaAsync(Guid? polizaId, DateOnly fechaOcurrencia, CancellationToken ct)
    {
        if (polizaId is not { } pid || pid == Guid.Empty) return null;
        var p = await _db.Polizas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == pid, ct)
            ?? throw new InvalidOperationException("La poliza indicada no existe en esta copropiedad.");
        if ((p.FechaInicio is { } ini && fechaOcurrencia < ini) || (p.FechaFin is { } fin && fechaOcurrencia > fin))
            throw new InvalidOperationException(
                $"El siniestro ocurrio el {fechaOcurrencia:dd/MM/yyyy}, fuera de la vigencia de la poliza " +
                $"{p.NumeroPoliza ?? p.Aseguradora} ({p.FechaInicio:dd/MM/yyyy} - {p.FechaFin:dd/MM/yyyy}).");
        return pid;
    }

    private async Task<IReadOnlyList<SiniestroDto>> ToDtosAsync(List<Siniestro> lista, CancellationToken ct)
    {
        if (lista.Count == 0) return Array.Empty<SiniestroDto>();
        var ids = lista.Select(s => s.Id).ToList();

        var tipos = (await _opciones.OpcionesAsync(ListaTipo, incluirOcultas: true, ct))
            .GroupBy(o => o.Clave).ToDictionary(g => g.Key, g => g.First().Label);

        var equipoIds = lista.Where(s => s.ElementoTipo == ElementoSiniestro.Equipo && s.ElementoId != null).Select(s => s.ElementoId!.Value).Distinct().ToList();
        var zonaIds = lista.Where(s => s.ElementoTipo == ElementoSiniestro.Zona && s.ElementoId != null).Select(s => s.ElementoId!.Value).Distinct().ToList();
        var unidadIds = lista.Where(s => s.ElementoTipo == ElementoSiniestro.Unidad && s.ElementoId != null).Select(s => s.ElementoId!.Value).Distinct().ToList();
        var equipos = equipoIds.Count == 0 ? new Dictionary<Guid, string>()
            : await _db.EquiposActivos.AsNoTracking().Where(e => equipoIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Nombre, ct);
        var zonas = zonaIds.Count == 0 ? new Dictionary<Guid, string>()
            : await _db.ZonasComunes.AsNoTracking().Where(z => zonaIds.Contains(z.Id)).ToDictionaryAsync(z => z.Id, z => z.Nombre, ct);
        var unidades = unidadIds.Count == 0 ? new Dictionary<Guid, string>()
            : await _db.UnidadesPrivadas.AsNoTracking().Where(u => unidadIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Numero, ct);

        var respIds = lista.Where(s => s.ResponsablePersonaId != null).Select(s => s.ResponsablePersonaId!.Value).Distinct().ToList();
        var responsables = respIds.Count == 0 ? new Dictionary<Guid, string>()
            : (await _db.Personas.AsNoTracking().Where(p => respIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Nombres, p.Apellidos }).ToListAsync(ct))
                .ToDictionary(p => p.Id, p => $"{p.Nombres} {p.Apellidos}".Trim());

        var docs = await _db.SiniestroDocumentos.AsNoTracking().Where(d => ids.Contains(d.SiniestroId))
            .GroupBy(d => d.SiniestroId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        var coms = await _db.SiniestroComentarios.AsNoTracking().Where(c => ids.Contains(c.SiniestroId) && !c.EsSistema)
            .GroupBy(c => c.SiniestroId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);

        // Tarea de reparacion (S3): numero y estado; una tarea eliminada se ignora.
        var tareaIds = lista.Where(s => s.TareaId != null).Select(s => s.TareaId!.Value).Distinct().ToList();
        var tareas = tareaIds.Count == 0 ? new Dictionary<Guid, (string Numero, string? Estado)>()
            : (await _db.Tareas.AsNoTracking().Where(t => tareaIds.Contains(t.Id) && !t.Eliminada)
                .Select(t => new { t.Id, t.NumeroTarea, Estado = t.Estado != null ? t.Estado.Nombre : null }).ToListAsync(ct))
                .ToDictionary(t => t.Id, t => (Numero: t.NumeroTarea, t.Estado));

        return lista.Select(s =>
        {
            string? elemento = s.ElementoTipo switch
            {
                ElementoSiniestro.Equipo => s.ElementoId is { } e && equipos.TryGetValue(e, out var n1) ? n1 : "(equipo eliminado)",
                ElementoSiniestro.Zona => s.ElementoId is { } z && zonas.TryGetValue(z, out var n2) ? n2 : "(zona eliminada)",
                ElementoSiniestro.Unidad => s.ElementoId is { } u && unidades.TryGetValue(u, out var n3) ? n3 : "(unidad eliminada)",
                ElementoSiniestro.Otro => s.ElementoOtro,
                _ => null,
            };
            return new SiniestroDto(
                s.Id, s.Numero, s.Titulo, s.Descripcion,
                s.TipoClave, s.TipoClave is { } tc ? (tipos.TryGetValue(tc, out var tl) ? tl : tc) : null,
                s.FechaOcurrencia, s.FechaReporte,
                s.ElementoTipo, s.ElementoId, s.ElementoOtro, elemento,
                s.PolizaId, s.Poliza?.NumeroPoliza, s.Poliza?.Aseguradora, s.NumeroReclamacion,
                s.ResponsablePersonaId, s.ResponsablePersonaId is { } r && responsables.TryGetValue(r, out var rn) ? rn : null,
                s.MontoEstimado, s.MontoReclamado, s.Deducible, s.MontoReconocido, s.FechaPago,
                s.EstadoId, s.Estado?.Nombre ?? "", s.Estado?.Color, s.Estado?.Clave,
                s.FechaCierre, s.TareaId, s.CreatedAt,
                docs.TryGetValue(s.Id, out var dn) ? dn : 0,
                coms.TryGetValue(s.Id, out var cn) ? cn : 0,
                s.TareaId is { } ti && tareas.TryGetValue(ti, out var tv) ? tv.Numero : null,
                s.TareaId is { } ti2 && tareas.TryGetValue(ti2, out var tv2) ? tv2.Estado : null);
        }).ToList();
    }

    // ================================ Estados ================================

    /// <summary>Siembra perezosa de los 6 estados base por copropiedad (idempotente).</summary>
    private async Task AsegurarEstadosBaseAsync(CancellationToken ct)
    {
        if (await _db.SiniestroEstados.AnyAsync(e => e.Clave != null, ct)) return;
        var baseEstados = new (string Clave, string Nombre, string Color)[]
        {
            (EstadoSiniestroClave.Reportado, "Reportado", "#516F90"),
            (EstadoSiniestroClave.EnTramite, "En tr\u00e1mite con aseguradora", "#3B82F6"),
            (EstadoSiniestroClave.Aprobado, "Aprobado", "#22C55E"),
            (EstadoSiniestroClave.Objetado, "Objetado", "#C0383C"),
            (EstadoSiniestroClave.Pagado, "Pagado", "#0FB5A5"),
            (EstadoSiniestroClave.Cerrado, "Cerrado", "#6B7280"),
        };
        for (var i = 0; i < baseEstados.Length; i++)
            _db.SiniestroEstados.Add(new SiniestroEstado
            {
                Clave = baseEstados[i].Clave,
                Nombre = baseEstados[i].Nombre,
                Color = baseEstados[i].Color,
                Orden = i + 1,
            });
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // Dos requests sembrando a la vez: el indice unico (tenant, clave) frena el segundo. Se descarta lo propio.
            foreach (var e in _db.ChangeTracker.Entries<SiniestroEstado>().Where(x => x.State == EntityState.Added).ToList())
                e.State = EntityState.Detached;
        }
    }

    public async Task<IReadOnlyList<SiniestroEstadoDto>> ListEstadosAsync(CancellationToken ct)
    {
        await AsegurarEstadosBaseAsync(ct);
        var conteo = await _db.Siniestros.AsNoTracking().GroupBy(s => s.EstadoId)
            .Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        return (await _db.SiniestroEstados.AsNoTracking().OrderBy(e => e.Orden).ThenBy(e => e.Nombre).ToListAsync(ct))
            .Select(e => new SiniestroEstadoDto(e.Id, e.Nombre, e.Orden, e.Color, e.Activo, e.Clave, conteo.TryGetValue(e.Id, out var n) ? n : 0))
            .ToList();
    }

    public async Task<SiniestroEstadoDto> CrearEstadoAsync(CrearSiniestroEstadoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("El nombre del estado es obligatorio.");
        await AsegurarEstadosBaseAsync(ct);
        var nombre = req.Nombre.Trim();
        if (await _db.SiniestroEstados.AnyAsync(e => e.Nombre.ToLower() == nombre.ToLower(), ct))
            throw new InvalidOperationException($"Ya existe un estado llamado '{nombre}'.");
        var orden = (await _db.SiniestroEstados.MaxAsync(e => (int?)e.Orden, ct) ?? 0) + 1;
        var e = new SiniestroEstado { Nombre = nombre, Color = Limpio(req.Color), Orden = orden };
        _db.SiniestroEstados.Add(e);
        await _db.SaveChangesAsync(ct);
        return new SiniestroEstadoDto(e.Id, e.Nombre, e.Orden, e.Color, e.Activo, e.Clave);
    }

    public async Task<bool> ActualizarEstadoAsync(Guid estadoId, ActualizarSiniestroEstadoRequest req, CancellationToken ct)
    {
        var e = await _db.SiniestroEstados.FirstOrDefaultAsync(x => x.Id == estadoId, ct);
        if (e is null) return false;
        if (req.Nombre is not null)
        {
            var nombre = req.Nombre.Trim();
            if (nombre.Length == 0) throw new InvalidOperationException("El nombre del estado es obligatorio.");
            if (await _db.SiniestroEstados.AnyAsync(x => x.Id != estadoId && x.Nombre.ToLower() == nombre.ToLower(), ct))
                throw new InvalidOperationException($"Ya existe un estado llamado '{nombre}'.");
            e.Nombre = nombre;
        }
        if (req.Color is not null) e.Color = Limpio(req.Color);
        if (req.Activo is { } activo)
        {
            // "Reportado" es el estado de entrada de las altas: no se puede inactivar.
            if (!activo && e.Clave == EstadoSiniestroClave.Reportado)
                throw new InvalidOperationException("El estado de entrada (Reportado) no se puede inactivar.");
            e.Activo = activo;
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Solo se eliminan estados PROPIOS (sin clave); sus siniestros pasan a "Reportado".</summary>
    public async Task<bool> EliminarEstadoAsync(Guid estadoId, CancellationToken ct)
    {
        var e = await _db.SiniestroEstados.FirstOrDefaultAsync(x => x.Id == estadoId, ct);
        if (e is null) return false;
        if (e.Clave is not null)
            throw new InvalidOperationException($"'{e.Nombre}' es un estado base: se puede renombrar o inactivar, pero no eliminar.");
        var reportado = await _db.SiniestroEstados.FirstAsync(x => x.Clave == EstadoSiniestroClave.Reportado, ct);
        await _db.Siniestros.Where(s => s.EstadoId == estadoId)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.EstadoId, reportado.Id), ct);
        // ExecuteUpdate no toca el change tracker: alinear los siniestros ya cargados en este contexto, o EF
        // los tomaria como hijos huerfanos del estado borrado.
        foreach (var tracked in _db.ChangeTracker.Entries<Siniestro>().Where(x => x.Entity.EstadoId == estadoId))
        {
            tracked.Entity.EstadoId = reportado.Id;
            tracked.Entity.Estado = reportado;
            tracked.State = EntityState.Unchanged;
        }
        _db.SiniestroEstados.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task ReordenarEstadosAsync(ReordenarSiniestroEstadosRequest req, CancellationToken ct)
    {
        if (req.Orden is null || req.Orden.Count == 0) return;
        var estados = await _db.SiniestroEstados.ToListAsync(ct);
        for (var i = 0; i < req.Orden.Count; i++)
        {
            var e = estados.FirstOrDefault(x => x.Id == req.Orden[i]);
            if (e is not null) e.Orden = i + 1;
        }
        await _db.SaveChangesAsync(ct);
    }

    // ============================ Campos EAV ============================
    // Fuentes de SISTEMA para las Formulas (fechas -> DayNumber; montos).
    private static readonly IReadOnlyDictionary<string, TipoCampoTablero> FuentesSistema =
        new Dictionary<string, TipoCampoTablero>(StringComparer.Ordinal)
        {
            ["fechaocurrencia"] = TipoCampoTablero.Fecha,
            ["fechareporte"] = TipoCampoTablero.Fecha,
            ["fechapago"] = TipoCampoTablero.Fecha,
            ["montoestimado"] = TipoCampoTablero.Moneda,
            ["montoreclamado"] = TipoCampoTablero.Moneda,
            ["deducible"] = TipoCampoTablero.Moneda,
            ["montoreconocido"] = TipoCampoTablero.Moneda,
        };

    private static IReadOnlyDictionary<string, decimal?> SistemaDe(Siniestro s) =>
        new Dictionary<string, decimal?>(StringComparer.Ordinal)
        {
            ["fechaocurrencia"] = s.FechaOcurrencia.DayNumber,
            ["fechareporte"] = s.FechaReporte?.DayNumber,
            ["fechapago"] = s.FechaPago?.DayNumber,
            ["montoestimado"] = s.MontoEstimado,
            ["montoreclamado"] = s.MontoReclamado,
            ["deducible"] = s.Deducible,
            ["montoreconocido"] = s.MontoReconocido,
        };

    private async Task<string?> PrepararOpcionesAsync(TipoCampoTablero tipo, string? opciones, Guid? excluirId, CancellationToken ct)
    {
        if (tipo != TipoCampoTablero.Formula) return Limpio(opciones);
        var cfg = CampoFormulaConfig.Parse(opciones)
            ?? throw new InvalidOperationException("La formula necesita una operacion y al menos un campo fuente.");
        var defs = (await _db.SiniestroCampos.AsNoTracking().Where(c => c.Activo && (excluirId == null || c.Id != excluirId))
            .Select(c => new { c.Id, c.Tipo }).ToListAsync(ct)).ToDictionary(c => c.Id, c => c.Tipo);
        var err = CampoFormulaConfig.Validar(cfg, clave =>
        {
            if (clave.StartsWith("cd:", StringComparison.Ordinal))
                return Guid.TryParse(clave[3..], out var g) && defs.TryGetValue(g, out var t) ? t : (TipoCampoTablero?)null;
            return FuentesSistema.TryGetValue(clave, out var ts) ? ts : (TipoCampoTablero?)null;
        });
        if (err is not null) throw new InvalidOperationException(err);
        return cfg.Serializar();
    }

    private static string? ComputarFormula(string? opciones, IReadOnlyDictionary<Guid, string?> cd, IReadOnlyDictionary<string, decimal?> sistema)
        => CampoFormulaConfig.ComputarTexto(opciones, clave =>
        {
            if (clave.StartsWith("cd:", StringComparison.Ordinal))
                return Guid.TryParse(clave[3..], out var g) && cd.TryGetValue(g, out var v) ? CampoFormulaConfig.ParseFuente(v) : (decimal?)null;
            return sistema.TryGetValue(clave, out var sv) ? sv : (decimal?)null;
        });

    public async Task<IReadOnlyList<SiniestroCampoDto>> ListCamposAsync(CancellationToken ct)
        => await _db.SiniestroCampos.AsNoTracking().Where(c => c.Activo).OrderBy(c => c.Orden)
            .Select(c => new SiniestroCampoDto(c.Id, c.Label, c.Orden, c.Tipo, c.Opciones, c.Descripcion, c.Activo))
            .ToListAsync(ct);

    public async Task<SiniestroCampoDto> CrearCampoAsync(CrearSiniestroCampoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Label)) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        var orden = (await _db.SiniestroCampos.MaxAsync(c => (int?)c.Orden, ct) ?? 0) + 1;
        var c = new SiniestroCampo
        {
            Label = req.Label.Trim(),
            Tipo = req.Tipo,
            Orden = orden,
            Descripcion = Limpio(req.Descripcion),
            Opciones = await PrepararOpcionesAsync(req.Tipo, req.Opciones, null, ct),
        };
        _db.SiniestroCampos.Add(c);
        await _db.SaveChangesAsync(ct);
        return new SiniestroCampoDto(c.Id, c.Label, c.Orden, c.Tipo, c.Opciones, c.Descripcion, c.Activo);
    }

    public async Task<bool> ActualizarCampoAsync(Guid campoId, ActualizarSiniestroCampoRequest req, CancellationToken ct)
    {
        var c = await _db.SiniestroCampos.FirstOrDefaultAsync(x => x.Id == campoId, ct);
        if (c is null) return false;
        if (!string.IsNullOrWhiteSpace(req.Label)) c.Label = req.Label.Trim();
        c.Tipo = req.Tipo;
        c.Opciones = await PrepararOpcionesAsync(req.Tipo, req.Opciones, campoId, ct);
        c.Orden = req.Orden;
        if (req.Descripcion is not null) c.Descripcion = Limpio(req.Descripcion);
        if (req.Activo is not null) c.Activo = req.Activo.Value;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarCampoAsync(Guid campoId, CancellationToken ct)
    {
        var c = await _db.SiniestroCampos.FirstOrDefaultAsync(x => x.Id == campoId, ct);
        if (c is null) return false;
        c.Activo = false;   // soft-delete: conserva los valores
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> GuardarCampoValorAsync(Guid siniestroId, Guid campoId, GuardarSiniestroCampoValorRequest req, CancellationToken ct)
    {
        if (!await _db.Siniestros.AnyAsync(s => s.Id == siniestroId, ct)) return false;
        var def = await _db.SiniestroCampos.AsNoTracking().FirstOrDefaultAsync(c => c.Id == campoId && c.Activo, ct);
        if (def is null) return false;
        if (def.Tipo == TipoCampoTablero.Formula) throw new InvalidOperationException("Un campo calculado no se edita a mano.");
        var val = string.IsNullOrWhiteSpace(req.Valor) ? null : req.Valor.Trim();
        var v = await _db.SiniestroCampoValores.FirstOrDefaultAsync(x => x.SiniestroId == siniestroId && x.SiniestroCampoId == campoId, ct);
        if (v is null)
        {
            if (val is null) return true;
            _db.SiniestroCampoValores.Add(new SiniestroCampoValor { SiniestroId = siniestroId, SiniestroCampoId = campoId, Valor = val });
        }
        else if (val is null) _db.SiniestroCampoValores.Remove(v);
        else v.Valor = val;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<SiniestroCampoValorFlatDto>> ListTodosCamposValoresAsync(CancellationToken ct)
    {
        var valores = await _db.SiniestroCampoValores.AsNoTracking().Where(v => v.Valor != null && v.Valor != "")
            .Select(v => new { v.SiniestroId, v.SiniestroCampoId, v.Valor }).ToListAsync(ct);
        var res = valores.Select(v => new SiniestroCampoValorFlatDto(v.SiniestroId, v.SiniestroCampoId, v.Valor)).ToList();
        var formulas = await _db.SiniestroCampos.AsNoTracking().Where(c => c.Activo && c.Tipo == TipoCampoTablero.Formula)
            .Select(d => new { d.Id, d.Opciones }).ToListAsync(ct);
        if (formulas.Count == 0) return res;

        var siniestros = await _db.Siniestros.AsNoTracking().ToListAsync(ct);
        var porReg = valores.GroupBy(v => v.SiniestroId)
            .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<Guid, string?>)g.ToDictionary(x => x.SiniestroCampoId, x => x.Valor));
        IReadOnlyDictionary<Guid, string?> vacio = new Dictionary<Guid, string?>();
        foreach (var s in siniestros)
        {
            var cd = porReg.TryGetValue(s.Id, out var d0) ? d0 : vacio;
            var sistema = SistemaDe(s);
            foreach (var f in formulas)
            {
                var txt = ComputarFormula(f.Opciones, cd, sistema);
                if (txt is not null) res.Add(new SiniestroCampoValorFlatDto(s.Id, f.Id, txt));
            }
        }
        return res;
    }

    public async Task<IReadOnlyList<SiniestroCampoDinDto>> ListCamposDinAsync(Guid siniestroId, CancellationToken ct)
    {
        var s = await _db.Siniestros.AsNoTracking().FirstOrDefaultAsync(x => x.Id == siniestroId, ct);
        if (s is null) return Array.Empty<SiniestroCampoDinDto>();
        var defs = await _db.SiniestroCampos.AsNoTracking().Where(c => c.Activo).OrderBy(d => d.Orden).ThenBy(d => d.Label).ToListAsync(ct);
        var vals = await _db.SiniestroCampoValores.AsNoTracking().Where(v => v.SiniestroId == siniestroId).ToListAsync(ct);
        var dict = (IReadOnlyDictionary<Guid, string?>)vals.GroupBy(v => v.SiniestroCampoId).ToDictionary(g => g.Key, g => g.First().Valor);
        var sistema = SistemaDe(s);
        return defs.Select(d => new SiniestroCampoDinDto(d.Id, d.Label, d.Orden,
            d.Tipo == TipoCampoTablero.Formula ? ComputarFormula(d.Opciones, dict, sistema) : (dict.TryGetValue(d.Id, out var v) ? v : null),
            d.Tipo, d.Opciones)).ToList();
    }

    // ============================== Documentos ==============================

    public async Task<IReadOnlyList<SiniestroDocumentoDto>> ListDocumentosAsync(Guid siniestroId, CancellationToken ct)
    {
        if (!await _db.Siniestros.AnyAsync(s => s.Id == siniestroId, ct)) return Array.Empty<SiniestroDocumentoDto>();
        return await _db.SiniestroDocumentos.AsNoTracking().Where(d => d.SiniestroId == siniestroId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new SiniestroDocumentoDto(d.Id, d.Nombre, d.Url, d.CreatedAt)).ToListAsync(ct);
    }

    public async Task<SiniestroDocumentoDto?> AgregarDocumentoAsync(Guid siniestroId, string nombre, string url, CancellationToken ct)
    {
        if (!await _db.Siniestros.AnyAsync(s => s.Id == siniestroId, ct)) return null;
        var d = new SiniestroDocumento { SiniestroId = siniestroId, Nombre = nombre, Url = url };
        _db.SiniestroDocumentos.Add(d);
        await _db.SaveChangesAsync(ct);
        return new SiniestroDocumentoDto(d.Id, d.Nombre, d.Url, d.CreatedAt);
    }

    public async Task<bool> EliminarDocumentoAsync(Guid docId, CancellationToken ct)
    {
        var d = await _db.SiniestroDocumentos.FirstOrDefaultAsync(x => x.Id == docId, ct);
        if (d is null) return false;
        _db.SiniestroDocumentos.Remove(d);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // =============================== Actividad ===============================

    public async Task<IReadOnlyList<SiniestroComentarioDto>> ListComentariosAsync(Guid siniestroId, CancellationToken ct)
    {
        if (!await _db.Siniestros.AnyAsync(s => s.Id == siniestroId, ct)) return Array.Empty<SiniestroComentarioDto>();
        return await _db.SiniestroComentarios.AsNoTracking().Where(c => c.SiniestroId == siniestroId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new SiniestroComentarioDto(c.Id, c.Texto, c.AutorNombre, c.CreatedAt, c.AutorUsuarioId, c.EsSistema))
            .ToListAsync(ct);
    }

    public async Task<SiniestroComentarioDto> AgregarComentarioAsync(Guid siniestroId, AgregarComentarioSiniestroRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Texto)) throw new InvalidOperationException("El comentario no puede estar vacio.");
        if (!await _db.Siniestros.AnyAsync(s => s.Id == siniestroId, ct)) throw new InvalidOperationException("Siniestro no encontrado.");
        var (uid, nombre) = ActorActual();
        var c = new SiniestroComentario { SiniestroId = siniestroId, Texto = req.Texto.Trim(), AutorUsuarioId = uid, AutorNombre = nombre };
        _db.SiniestroComentarios.Add(c);
        await _db.SaveChangesAsync(ct);
        return new SiniestroComentarioDto(c.Id, c.Texto, c.AutorNombre, c.CreatedAt, c.AutorUsuarioId, c.EsSistema);
    }

    private static string? Limpio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
