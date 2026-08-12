using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Tareas;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Tareas;

/// <summary>Modulo 2.10 Tareas y Proyectos - MVP del spec v1.0.</summary>
public class TareasService : ITareasService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _http;
    private readonly Propia.Application.Notificaciones.INotificacionDispatcher _noti;

    public TareasService(
        PropiaDbContext db,
        ITenantContext tenantContext,
        IHttpContextAccessor http,
        Propia.Application.Notificaciones.INotificacionDispatcher noti)
    {
        _db = db;
        _tenantContext = tenantContext;
        _http = http;
        _noti = noti;
    }

    private Guid GetUsuarioActualId()
    {
        var sub = _http.HttpContext?.User?.FindFirstValue("user_id");
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    // ===================== Seed lazy de estados =====================

    private async Task AsegurarEstadosBaseAsync(CancellationToken ct)
    {
        var hay = await _db.TareasEstados.AnyAsync(ct);
        if (hay) return;
        foreach (var (nombre, orden, esTerminal) in EstadoTareaBase.Base)
        {
            _db.TareasEstados.Add(new TareaEstado
            {
                Nombre = nombre,
                Orden = orden,
                EsTerminal = esTerminal,
                EsBase = true,
                Activo = true,
                Color = nombre switch
                {
                    EstadoTareaBase.Pendiente => "#94a3b8",
                    EstadoTareaBase.EnProgreso => "#3b82f6",
                    EstadoTareaBase.EnRevision => "#f59e0b",
                    EstadoTareaBase.Bloqueada => "#ef4444",
                    EstadoTareaBase.Completada => "#22c55e",
                    EstadoTareaBase.Cancelada => "#6b7280",
                    _ => null
                }
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    // ===================== Estados =====================

    public async Task<IReadOnlyList<EstadoTareaDto>> ListarEstadosAsync(CancellationToken ct)
    {
        await AsegurarEstadosBaseAsync(ct);
        return await _db.TareasEstados.AsNoTracking()
            .OrderBy(e => e.Orden).ThenBy(e => e.Nombre)
            .Select(e => new EstadoTareaDto(e.Id, e.Nombre, e.Color, e.Orden, e.EsTerminal, e.EsBase, e.Activo))
            .ToListAsync(ct);
    }

    private static readonly string[] _estadoPalette = { "#6D4FE3", "#0EA5E9", "#EC4899", "#14B8A6", "#A855F7", "#F97316" };

    public async Task<EstadoTareaDto> CrearEstadoAsync(CrearEstadoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre) || req.Nombre.Trim().Length < 2)
            throw new InvalidOperationException("Nombre minimo 2 caracteres.");
        var nom = req.Nombre.Trim();
        var tableroId = req.TableroId ?? await AsegurarTableroDefaultAsync(ct);
        if (await _db.TareasEstados.AnyAsync(e => e.TableroId == tableroId && e.Nombre == nom, ct))
            throw new InvalidOperationException("Ya existe un estado con este nombre en el tablero.");

        // Insertar el nuevo estado justo antes de los estados terminales (Completada/Cancelada).
        var enBoard = await _db.TareasEstados.Where(e => e.TableroId == tableroId).ToListAsync(ct);
        var terminales = enBoard.Where(e => e.EsTerminal).ToList();
        int orden;
        if (req.Orden > 0) orden = req.Orden;
        else if (terminales.Count > 0)
        {
            orden = terminales.Min(e => e.Orden);
            foreach (var term in terminales) { term.Orden += 1; term.UpdatedAt = DateTimeOffset.UtcNow; }
        }
        else orden = (enBoard.Count > 0 ? enBoard.Max(e => e.Orden) : 0) + 1;

        var color = string.IsNullOrWhiteSpace(req.Color)
            ? _estadoPalette[enBoard.Count(e => !e.EsBase) % _estadoPalette.Length]
            : req.Color;
        var e = new TareaEstado { TableroId = tableroId, Nombre = nom, Color = color, Orden = orden, EsTerminal = false, EsBase = false, Activo = true };
        _db.TareasEstados.Add(e);
        await _db.SaveChangesAsync(ct);
        return new EstadoTareaDto(e.Id, e.Nombre, e.Color, e.Orden, false, false, true);
    }

    public async Task<bool> ActualizarEstadoAsync(Guid id, ActualizarEstadoRequest req, CancellationToken ct)
    {
        var e = await _db.TareasEstados.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return false;
        if (e.EsTerminal) throw new InvalidOperationException("Los estados terminales no son editables.");
        var nom = req.Nombre.Trim();
        if (e.Nombre != nom && await _db.TareasEstados.AnyAsync(x => x.Nombre == nom && x.Id != id, ct))
            throw new InvalidOperationException("Ya existe un estado con este nombre.");
        e.Nombre = nom;
        e.Color = req.Color;
        e.Orden = req.Orden;
        e.Activo = req.Activo;
        e.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarEstadoAsync(Guid id, CancellationToken ct)
    {
        var e = await _db.TareasEstados.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return false;
        if (e.EsTerminal || e.EsBase) throw new InvalidOperationException("Estados base o terminales no eliminables.");
        if (await _db.Tareas.AnyAsync(t => t.EstadoId == id && !t.Eliminada, ct))
            throw new InvalidOperationException("No puedes eliminar un estado con tareas asociadas. Reasignalas primero.");
        _db.TareasEstados.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Etiquetas =====================

    public async Task<IReadOnlyList<EtiquetaTareaDto>> ListarEtiquetasAsync(Guid? tableroId, CancellationToken ct)
    {
        var q = _db.TareaEtiquetas.AsNoTracking().AsQueryable();
        // Etiquetas EXCLUSIVAS del tablero (no hay globales). Sin tablero: todas (admin/legacy).
        if (tableroId.HasValue) q = q.Where(e => e.TableroId == tableroId.Value);
        var rows = await q.OrderBy(e => e.Nombre).ToListAsync(ct);
        var counts = await _db.TareaEtiquetaAsignaciones.AsNoTracking()
            .GroupBy(a => a.EtiquetaId)
            .Select(g => new { g.Key, Cant = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Cant, ct);
        return rows.Select(e => new EtiquetaTareaDto(e.Id, e.Nombre, e.Color, e.Activo, counts.GetValueOrDefault(e.Id, 0), e.TableroId)).ToList();
    }

    public async Task<EtiquetaTareaDto> CrearEtiquetaAsync(CrearEtiquetaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre obligatorio.");
        if (req.TableroId is null) throw new InvalidOperationException("La etiqueta debe pertenecer a un tablero.");
        var nom = req.Nombre.Trim();
        // Unicidad dentro del mismo tablero.
        if (await _db.TareaEtiquetas.AnyAsync(e => e.Nombre == nom && e.TableroId == req.TableroId, ct))
            throw new InvalidOperationException("Ya existe una etiqueta con este nombre en este tablero.");
        var e = new TareaEtiqueta { Nombre = nom, Color = req.Color, Activo = true, TableroId = req.TableroId };
        _db.TareaEtiquetas.Add(e);
        await _db.SaveChangesAsync(ct);
        return new EtiquetaTareaDto(e.Id, e.Nombre, e.Color, true, 0, e.TableroId);
    }

    public async Task<bool> ActualizarEtiquetaAsync(Guid id, ActualizarEtiquetaRequest req, CancellationToken ct)
    {
        var e = await _db.TareaEtiquetas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return false;
        e.Nombre = req.Nombre.Trim();
        e.Color = req.Color;
        e.Activo = req.Activo;
        e.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarEtiquetaAsync(Guid id, CancellationToken ct)
    {
        var e = await _db.TareaEtiquetas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return false;
        _db.TareaEtiquetas.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Tareas =====================

    public async Task<IReadOnlyList<TareaListaDto>> ListarTareasAsync(
        Guid? estadoId, PrioridadTarea? prioridad, Guid? asignadoPersonaId, Guid? padreId, bool? soloRaiz, string? query,
        CancellationToken ct, Guid? tableroId = null)
    {
        await AsegurarEstadosBaseAsync(ct);
        IQueryable<Tarea> q = _db.Tareas.AsNoTracking().Where(t => !t.Eliminada);
        if (tableroId.HasValue) q = q.Where(t => t.TableroId == tableroId.Value);
        if (estadoId.HasValue) q = q.Where(t => t.EstadoId == estadoId.Value);
        if (prioridad.HasValue) q = q.Where(t => t.Prioridad == prioridad.Value);
        if (asignadoPersonaId.HasValue) q = q.Where(t => t.AsignadoPersonaId == asignadoPersonaId.Value);
        if (padreId.HasValue) q = q.Where(t => t.PadreId == padreId.Value);
        if (soloRaiz == true) q = q.Where(t => t.PadreId == null);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var qn = query.Trim().ToLower();
            q = q.Where(t => t.Titulo.ToLower().Contains(qn) || t.NumeroTarea.ToLower().Contains(qn));
        }

        var rows = await (
            from t in q
            join e in _db.TareasEstados on t.EstadoId equals e.Id
            join p in _db.Personas on t.AsignadoPersonaId equals p.Id into pj
            from p in pj.DefaultIfEmpty()
            orderby t.Prioridad, t.FechaVencimiento, t.CreatedAt descending
            select new
            {
                t.Id,
                t.NumeroTarea,
                t.Titulo,
                t.Prioridad,
                t.EstadoId,
                EstadoNombre = e.Nombre,
                EstadoColor = e.Color,
                EstadoEsTerminal = e.EsTerminal,
                t.AsignadoPersonaId,
                AsigNombre = p == null ? null : ((p.Nombres ?? "") + " " + (p.Apellidos ?? "")).Trim(),
                t.FechaVencimiento,
                t.FechaInicio,
                t.PadreId,
                t.Progreso,
                t.Color,
                t.EsProyecto,
                t.Valor,
                t.Descripcion,
                t.OrigenTipo,
                t.OrigenReferencia,
                t.EstadoDesde
            }
        ).ToListAsync(ct);

        var ids = rows.Select(r => r.Id).ToList();
        var subs = await _db.Tareas.AsNoTracking().Where(t => !t.Eliminada && t.PadreId != null && ids.Contains(t.PadreId!.Value))
            .GroupBy(t => t.PadreId!.Value).Select(g => new { g.Key, Cant = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Cant, ct);
        var coms = await _db.TareaComentarios.AsNoTracking().Where(c => ids.Contains(c.TareaId))
            .GroupBy(c => c.TareaId).Select(g => new { g.Key, Cant = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Cant, ct);
        var etiquetas = await (
            from a in _db.TareaEtiquetaAsignaciones.AsNoTracking().Where(x => ids.Contains(x.TareaId))
            join e in _db.TareaEtiquetas.AsNoTracking() on a.EtiquetaId equals e.Id
            select new { a.TareaId, Dto = new EtiquetaTareaDto(e.Id, e.Nombre, e.Color, e.Activo, 0, e.TableroId) }
        ).ToListAsync(ct);
        var etiquetasMap = etiquetas.GroupBy(x => x.TareaId).ToDictionary(g => g.Key, g => (IReadOnlyList<EtiquetaTareaDto>)g.Select(x => x.Dto).ToList());

        var camposVals = await _db.TareaCampoValores.AsNoTracking().Where(v => ids.Contains(v.TareaId))
            .Select(v => new { v.TareaId, v.TableroCampoId, v.Valor }).ToListAsync(ct);
        var camposMap = camposVals.GroupBy(x => x.TareaId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<TareaCampoValorDto>)g.Select(x => new TareaCampoValorDto(x.TableroCampoId, x.Valor)).ToList());

        // Responsables (asignado principal + colaboradores) para la vista tabla editable.
        var colabs = await (
            from c in _db.TareaColaboradores.AsNoTracking().Where(c => ids.Contains(c.TareaId))
            join p in _db.Personas.AsNoTracking() on c.PersonaId equals p.Id
            select new { c.TareaId, c.PersonaId, Nombre = ((p.Nombres ?? "") + " " + (p.Apellidos ?? "")).Trim() }
        ).ToListAsync(ct);
        var colabsMap = colabs.GroupBy(x => x.TareaId).ToDictionary(g => g.Key, g => g.ToList());

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        return rows.Select(r =>
        {
            var resp = new List<ResponsableMiniDto>();
            if (r.AsignadoPersonaId is Guid ap) resp.Add(new ResponsableMiniDto(ap, r.AsigNombre ?? "", null));
            if (colabsMap.TryGetValue(r.Id, out var cs)) resp.AddRange(cs.Select(c => new ResponsableMiniDto(c.PersonaId, c.Nombre, null)));
            return new TareaListaDto(
                r.Id, r.NumeroTarea, r.Titulo, r.Prioridad, r.EstadoId, r.EstadoNombre, r.EstadoColor, r.EstadoEsTerminal,
                r.AsignadoPersonaId, r.AsigNombre,
                r.FechaVencimiento,
                !r.EstadoEsTerminal && r.FechaVencimiento.HasValue && r.FechaVencimiento.Value < hoy,
                r.PadreId,
                subs.GetValueOrDefault(r.Id, 0),
                coms.GetValueOrDefault(r.Id, 0),
                etiquetasMap.GetValueOrDefault(r.Id, new List<EtiquetaTareaDto>()),
                r.Progreso, r.Color, r.EsProyecto, r.Valor, r.FechaInicio,
                camposMap.GetValueOrDefault(r.Id),
                resp, r.Descripcion, r.OrigenTipo, r.OrigenReferencia, r.EstadoDesde);
        }).ToList();
    }

    public async Task<TareaDetalleDto?> GetTareaAsync(Guid id, CancellationToken ct)
    {
        var t = await _db.Tareas.AsNoTracking()
            .Include(x => x.Estado)
            .Include(x => x.AsignadoPersona)
            .Include(x => x.Padre)
            .Include(x => x.CopiaDe)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return null;

        var etiquetasIds = await _db.TareaEtiquetaAsignaciones.AsNoTracking()
            .Where(a => a.TareaId == id).Select(a => a.EtiquetaId).ToListAsync(ct);
        var etiquetas = await _db.TareaEtiquetas.AsNoTracking()
            .Where(e => etiquetasIds.Contains(e.Id))
            .Select(e => new EtiquetaTareaDto(e.Id, e.Nombre, e.Color, e.Activo, 0, e.TableroId))
            .ToListAsync(ct);

        var subtareas = await ListarTareasAsync(null, null, null, id, null, null, ct);

        var comentariosRaw = await _db.TareaComentarios.AsNoTracking()
            .Where(c => c.TareaId == id)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new { c.Id, c.AutorUsuarioId, c.Texto, c.CreatedAt })
            .ToListAsync(ct);

        // Resolver nombre del autor (best-effort) via usuario -> persona.
        var autorIds = comentariosRaw.Select(c => c.AutorUsuarioId).Distinct().ToList();
        var usuariosAutor = await _db.Users.AsNoTracking()
            .Where(u => autorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.PersonaId })
            .ToListAsync(ct);
        var personaAutorIds = usuariosAutor.Where(u => u.PersonaId.HasValue)
            .Select(u => u.PersonaId!.Value).Distinct().ToList();
        var personasAutor = await _db.Personas.AsNoTracking()
            .Where(p => personaAutorIds.Contains(p.Id))
            .Select(p => new { p.Id, Nombre = (((p.Nombres ?? "") + " " + (p.Apellidos ?? "")).Trim()) })
            .ToDictionaryAsync(p => p.Id, p => p.Nombre, ct);

        string? ResolverAutorComentario(Guid uid)
        {
            var u = usuariosAutor.FirstOrDefault(x => x.Id == uid);
            if (u?.PersonaId is Guid pid && personasAutor.TryGetValue(pid, out var n) && !string.IsNullOrWhiteSpace(n))
                return n;
            return null;
        }

        var comentarios = comentariosRaw
            .Select(c => new TareaComentarioDto(c.Id, c.AutorUsuarioId, c.Texto, c.CreatedAt, ResolverAutorComentario(c.AutorUsuarioId)))
            .ToList();

        var historial = await _db.TareaHistorial.AsNoTracking()
            .Where(h => h.TareaId == id)
            .OrderByDescending(h => h.OcurridoAt)
            .Take(50)
            .Select(h => new TareaHistorialDto(h.TipoEvento, h.Descripcion, h.RealizadoPorUsuarioId, h.OcurridoAt))
            .ToListAsync(ct);

        var colabs = await (
            from c in _db.TareaColaboradores.AsNoTracking().Where(c => c.TareaId == id)
            join p in _db.Personas.AsNoTracking() on c.PersonaId equals p.Id
            select new TareaColaboradorDto(c.Id, p.Id, ((p.Nombres ?? "") + " " + (p.Apellidos ?? "")).Trim())
        ).ToListAsync(ct);

        var adjuntosRaw = await _db.TareaAdjuntos.AsNoTracking()
            .Where(a => a.TareaId == id)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new { a.Id, a.Nombre, a.Url, a.CreatedBy, a.CreatedAt })
            .ToListAsync(ct);
        // Resolver "subido por" (best-effort) via usuario -> persona, igual que los comentarios.
        var adjUserIds = adjuntosRaw.Where(a => a.CreatedBy.HasValue).Select(a => a.CreatedBy!.Value).Distinct().ToList();
        var adjUsuarios = await _db.Users.AsNoTracking()
            .Where(u => adjUserIds.Contains(u.Id))
            .Select(u => new { u.Id, u.PersonaId })
            .ToListAsync(ct);
        var adjPersonaIds = adjUsuarios.Where(u => u.PersonaId.HasValue).Select(u => u.PersonaId!.Value).Distinct().ToList();
        var adjPersonas = await _db.Personas.AsNoTracking()
            .Where(p => adjPersonaIds.Contains(p.Id))
            .Select(p => new { p.Id, Nombre = (((p.Nombres ?? "") + " " + (p.Apellidos ?? "")).Trim()) })
            .ToDictionaryAsync(p => p.Id, p => p.Nombre, ct);
        string? ResolverSubidoPor(Guid? uid)
        {
            if (uid is not Guid u) return null;
            var usr = adjUsuarios.FirstOrDefault(x => x.Id == u);
            if (usr?.PersonaId is Guid pid && adjPersonas.TryGetValue(pid, out var n) && !string.IsNullOrWhiteSpace(n)) return n;
            return null;
        }
        var adjuntos = adjuntosRaw
            .Select(a => new TareaAdjuntoDto(a.Id, a.Nombre, a.Url, ResolverSubidoPor(a.CreatedBy), a.CreatedAt))
            .ToList();

        var checklist = await _db.TareaSubtareas.AsNoTracking()
            .Where(s => s.TareaId == id)
            .OrderBy(s => s.Orden)
            .Select(s => new SubtareaCheckDto(s.Id, s.Titulo, s.Hecho, s.Orden))
            .ToListAsync(ct);

        var camposValores = await _db.TareaCampoValores.AsNoTracking()
            .Where(v => v.TareaId == id)
            .Select(v => new TareaCampoValorDto(v.TableroCampoId, v.Valor))
            .ToListAsync(ct);

        // Traza: copias hechas a partir de esta tarea (copias independientes, no subtareas).
        var copias = await (
            from c in _db.Tareas.AsNoTracking().Where(c => c.CopiaDeTareaId == id && !c.Eliminada)
            join e in _db.TareasEstados on c.EstadoId equals e.Id
            orderby c.CreatedAt
            select new TareaListaDto(
                c.Id, c.NumeroTarea, c.Titulo, c.Prioridad, c.EstadoId, e.Nombre, e.Color, e.EsTerminal,
                c.AsignadoPersonaId, null, c.FechaVencimiento, false, c.PadreId, 0, 0,
                new List<EtiquetaTareaDto>(), c.Progreso, c.Color, c.EsProyecto, c.Valor, c.FechaInicio,
                null, null, null, c.OrigenTipo, c.OrigenReferencia, c.EstadoDesde)
        ).ToListAsync(ct);

        var asigNombre = t.AsignadoPersona is null ? null
            : ((t.AsignadoPersona.Nombres ?? "") + " " + (t.AsignadoPersona.Apellidos ?? "")).Trim();
        var estadoDto = new EstadoTareaDto(t.Estado!.Id, t.Estado.Nombre, t.Estado.Color, t.Estado.Orden, t.Estado.EsTerminal, t.Estado.EsBase, t.Estado.Activo);

        return new TareaDetalleDto(
            t.Id, t.NumeroTarea, t.Titulo, t.Descripcion, t.Prioridad,
            estadoDto, t.AsignadoPersonaId, asigNombre,
            t.FechaInicio, t.FechaVencimiento, t.FechaCompletada,
            t.PadreId, t.Padre?.Titulo, t.Origen, t.ModuloOrigenCodigo, t.ModuloOrigenEntidadId,
            t.CreatedAt, t.CreadoPorUsuarioId,
            etiquetas, subtareas, comentarios, historial, colabs,
            t.Color, t.EsProyecto, t.Valor, t.Progreso, t.HoraInicio, t.HoraFin,
            t.OrigenTipo, t.OrigenReferencia, t.TableroId, adjuntos, checklist, camposValores,
            t.CopiaDeTareaId, t.CopiaDe?.NumeroTarea, t.CopiaDe?.Titulo, copias);
    }

    private async Task<string> GenerarNumeroAsync(CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var prefijo = $"T-{year}-";
        var ultimos = await _db.Tareas.AsNoTracking()
            .Where(t => t.NumeroTarea.StartsWith(prefijo))
            .Select(t => t.NumeroTarea)
            .ToListAsync(ct);
        int max = 0;
        foreach (var n in ultimos)
        {
            if (int.TryParse(n.Substring(prefijo.Length), out var s) && s > max) max = s;
        }
        return $"{prefijo}{(max + 1):D4}";
    }

    public async Task<TareaDetalleDto> CrearTareaAsync(CrearTareaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Titulo)) throw new InvalidOperationException("Titulo obligatorio.");
        await AsegurarEstadosBaseAsync(ct);

        // Tablero destino: el de la tarea padre si se hereda, el indicado, o el "General".
        Guid? tableroId = req.TableroId;
        if (req.PadreId.HasValue)
        {
            var padre = await _db.Tareas.AsNoTracking().Where(t => t.Id == req.PadreId.Value)
                .Select(t => new { t.TableroId }).FirstOrDefaultAsync(ct);
            if (padre is null) throw new InvalidOperationException("Tarea padre no encontrada.");
            tableroId ??= padre.TableroId;
        }
        tableroId ??= await AsegurarTableroDefaultAsync(ct);

        Guid estadoId;
        if (req.EstadoId.HasValue)
        {
            estadoId = req.EstadoId.Value;
            if (!await _db.TareasEstados.AnyAsync(e => e.Id == estadoId, ct))
                throw new InvalidOperationException("Estado invalido.");
        }
        else
        {
            // Primera columna del tablero (orden), o la base Pendiente como respaldo.
            estadoId = await _db.TareasEstados.Where(e => e.TableroId == tableroId)
                .OrderBy(e => e.Orden).Select(e => e.Id).FirstOrDefaultAsync(ct);
            if (estadoId == Guid.Empty)
                estadoId = await _db.TareasEstados.Where(e => e.Nombre == EstadoTareaBase.Pendiente).Select(e => e.Id).FirstAsync(ct);
        }

        // Responsables: el primero es el asignado principal; el resto son colaboradores.
        var responsables = req.ResponsablePersonaIds?.Where(x => x != Guid.Empty).Distinct().ToList();
        var asignado = responsables is { Count: > 0 } ? responsables[0] : req.AsignadoPersonaId;

        var numero = await GenerarNumeroAsync(ct);
        var t = new Tarea
        {
            NumeroTarea = numero,
            TableroId = tableroId,
            Titulo = req.Titulo.Trim(),
            Descripcion = req.Descripcion?.Trim(),
            Prioridad = req.Prioridad,
            EstadoId = estadoId,
            AsignadoPersonaId = asignado,
            FechaInicio = req.FechaInicio,
            FechaVencimiento = req.FechaVencimiento,
            PadreId = req.PadreId,
            Origen = OrigenTarea.Manual,
            Color = req.Color,
            EsProyecto = req.EsProyecto,
            Valor = req.Valor,
            HoraInicio = req.HoraInicio,
            HoraFin = req.HoraFin,
            OrigenTipo = string.IsNullOrWhiteSpace(req.OrigenTipo) ? null : req.OrigenTipo,
            OrigenReferencia = string.IsNullOrWhiteSpace(req.OrigenReferencia) ? null : req.OrigenReferencia,
            CreadoPorUsuarioId = GetUsuarioActualId()
        };
        _db.Tareas.Add(t);
        await _db.SaveChangesAsync(ct);

        if (req.EtiquetaIds is { Count: > 0 })
        {
            foreach (var eid in req.EtiquetaIds.Distinct())
            {
                if (await _db.TareaEtiquetas.AnyAsync(e => e.Id == eid, ct))
                    _db.TareaEtiquetaAsignaciones.Add(new TareaEtiquetaAsignacion { TareaId = t.Id, EtiquetaId = eid });
            }
            await _db.SaveChangesAsync(ct);
        }

        if (responsables is { Count: > 1 })
        {
            foreach (var pid in responsables.Skip(1))
                _db.TareaColaboradores.Add(new TareaColaborador { TareaId = t.Id, PersonaId = pid });
            await _db.SaveChangesAsync(ct);
        }

        await ReemplazarChecklistAsync(t.Id, req.Checklist, ct);
        await ReemplazarCamposValoresAsync(t.Id, req.CamposValores, ct);

        await RegistrarHistorial(t.Id, TipoEventoTarea.Creada, $"Tarea creada con prioridad {req.Prioridad}", null, new { titulo = t.Titulo, prioridad = req.Prioridad.ToString() }, ct);
        await _db.SaveChangesAsync(ct);

        // Si es subtarea, el padre ahora se vuelve derivado: recalcular su progreso.
        if (req.PadreId.HasValue) await RecomputarProgresoAncestrosAsync(req.PadreId, ct);

        return (await GetTareaAsync(t.Id, ct))!;
    }

    public async Task<bool> ActualizarTareaAsync(Guid id, ActualizarTareaRequest req, CancellationToken ct)
    {
        var t = await _db.Tareas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return false;
        if (string.IsNullOrWhiteSpace(req.Titulo)) throw new InvalidOperationException("Titulo obligatorio.");

        var prevAsig = t.AsignadoPersonaId;
        var prevFv = t.FechaVencimiento;
        var prevPri = t.Prioridad;
        var prevProyecto = t.EsProyecto;

        // Responsables: el primero es el asignado principal; el resto son colaboradores.
        var responsables = req.ResponsablePersonaIds?.Where(x => x != Guid.Empty).Distinct().ToList();
        var asignado = responsables is not null
            ? (responsables.Count > 0 ? responsables[0] : (Guid?)null)
            : req.AsignadoPersonaId;

        t.Titulo = req.Titulo.Trim();
        t.Descripcion = req.Descripcion?.Trim();
        t.Prioridad = req.Prioridad;
        t.AsignadoPersonaId = asignado;
        t.FechaInicio = req.FechaInicio;
        t.FechaVencimiento = req.FechaVencimiento;
        t.Color = req.Color;
        t.EsProyecto = req.EsProyecto;
        t.Valor = req.Valor;
        if (req.Progreso.HasValue) t.Progreso = Math.Clamp(req.Progreso.Value, 0, 100);
        t.HoraInicio = req.HoraInicio;
        t.HoraFin = req.HoraFin;
        t.OrigenTipo = string.IsNullOrWhiteSpace(req.OrigenTipo) ? null : req.OrigenTipo;
        t.OrigenReferencia = string.IsNullOrWhiteSpace(req.OrigenReferencia) ? null : req.OrigenReferencia;
        t.UpdatedAt = DateTimeOffset.UtcNow;

        if (prevPri != req.Prioridad)
        {
            await RegistrarHistorial(t.Id, TipoEventoTarea.PrioridadCambiada, $"Prioridad cambiada a {req.Prioridad}",
                new { prev = prevPri.ToString() }, new { nuevo = req.Prioridad.ToString() }, ct);
        }
        if (prevAsig != asignado)
        {
            await RegistrarHistorial(t.Id, TipoEventoTarea.AsignacionCambiada,
                asignado is null ? "Responsable removido" : "Responsable cambiado",
                new { prev = prevAsig }, new { nuevo = asignado }, ct);
        }
        if (prevFv != req.FechaVencimiento)
        {
            await RegistrarHistorial(t.Id, TipoEventoTarea.FechaCambiada, "Fecha de vencimiento actualizada",
                new { prev = prevFv?.ToString("yyyy-MM-dd") }, new { nuevo = req.FechaVencimiento?.ToString("yyyy-MM-dd") }, ct);
        }
        if (!prevProyecto && req.EsProyecto)
        {
            await RegistrarHistorial(t.Id, TipoEventoTarea.Actualizada, "Marcada como proyecto", null, null, ct);
        }
        await _db.SaveChangesAsync(ct);

        // Estado (mismo flujo de historial que CambiarEstado).
        if (req.EstadoId.HasValue && req.EstadoId.Value != Guid.Empty && req.EstadoId.Value != t.EstadoId)
            await CambiarEstadoAsync(id, new CambiarEstadoRequest(req.EstadoId.Value, null), ct);

        // Colaboradores (reemplazo total) cuando se envian responsables.
        if (responsables is not null)
        {
            await _db.TareaColaboradores.Where(c => c.TareaId == id).ExecuteDeleteAsync(ct);
            foreach (var pid in responsables.Skip(1))
                _db.TareaColaboradores.Add(new TareaColaborador { TareaId = id, PersonaId = pid });
            await _db.SaveChangesAsync(ct);
        }

        // Checklist (reemplazo total) cuando se envia.
        if (req.Checklist is not null)
            await ReemplazarChecklistAsync(id, req.Checklist, ct);

        // Valores de campos personalizados (upsert) cuando se envian.
        if (req.CamposValores is not null)
            await ReemplazarCamposValoresAsync(id, req.CamposValores, ct);

        return true;
    }

    public async Task<bool> ActualizarCampoInlineAsync(Guid id, InlineUpdateTareaRequest req, CancellationToken ct)
    {
        var t = await _db.Tareas.FirstOrDefaultAsync(x => x.Id == id && !x.Eliminada, ct);
        if (t is null) return false;
        switch ((req.Campo ?? "").Trim().ToLowerInvariant())
        {
            case "titulo":
                if (string.IsNullOrWhiteSpace(req.Texto)) throw new InvalidOperationException("Titulo obligatorio.");
                t.Titulo = req.Texto.Trim();
                break;
            case "descripcion":
                t.Descripcion = string.IsNullOrWhiteSpace(req.Texto) ? null : req.Texto.Trim();
                break;
            case "valor":
                t.Valor = req.Numero;
                break;
            case "prioridad":
                if (req.Numero is decimal pn && Enum.IsDefined(typeof(PrioridadTarea), (int)pn)) t.Prioridad = (PrioridadTarea)(int)pn;
                else if (Enum.TryParse<PrioridadTarea>(req.Texto, true, out var pp)) t.Prioridad = pp;
                else throw new InvalidOperationException("Prioridad invalida.");
                break;
            case "fechavencimiento":
                t.FechaVencimiento = req.Fecha;
                break;
            case "fechainicio":
                t.FechaInicio = req.Fecha;
                break;
            case "asignados":
                var ids = (req.Guids ?? new List<Guid>()).Where(g => g != Guid.Empty).Distinct().ToList();
                t.AsignadoPersonaId = ids.Count > 0 ? ids[0] : (Guid?)null;
                await _db.TareaColaboradores.Where(c => c.TareaId == id).ExecuteDeleteAsync(ct);
                foreach (var extra in ids.Skip(1))
                    _db.TareaColaboradores.Add(new TareaColaborador { TareaId = id, PersonaId = extra });
                break;
            case "origen":
                t.OrigenTipo = string.IsNullOrWhiteSpace(req.Texto) ? null : req.Texto.Trim();
                t.OrigenReferencia = t.OrigenTipo is null || string.IsNullOrWhiteSpace(req.TextoAux) ? null : req.TextoAux.Trim();
                break;
            default:
                throw new InvalidOperationException($"Campo '{req.Campo}' no editable inline.");
        }
        t.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> SetCampoValorAsync(Guid tareaId, Guid campoId, string? valor, CancellationToken ct)
    {
        var t = await _db.Tareas.Where(x => x.Id == tareaId && !x.Eliminada).Select(x => new { x.TableroId }).FirstOrDefaultAsync(ct);
        if (t is null) return false;
        var campoValido = await _db.TableroCampos.AnyAsync(c => c.Id == campoId && c.TableroId == t.TableroId, ct);
        if (!campoValido) throw new InvalidOperationException("El campo no pertenece al tablero de la tarea.");
        var val = string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
        var ex = await _db.TareaCampoValores.FirstOrDefaultAsync(v => v.TareaId == tareaId && v.TableroCampoId == campoId, ct);
        if (ex is null)
        {
            if (val is not null)
                _db.TareaCampoValores.Add(new TareaCampoValor { TareaId = tareaId, TableroCampoId = campoId, Valor = val });
        }
        else { ex.Valor = val; ex.UpdatedAt = DateTimeOffset.UtcNow; }
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<TareaListaDto?> DuplicarTareaAsync(Guid id, CancellationToken ct)
    {
        var src = await _db.Tareas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.Eliminada, ct);
        if (src is null) return null;
        var numero = await GenerarNumeroAsync(ct);
        var nueva = new Tarea
        {
            NumeroTarea = numero,
            TableroId = src.TableroId,
            Titulo = (src.Titulo + " (copia)").Trim(),
            Descripcion = src.Descripcion,
            Prioridad = src.Prioridad,
            EstadoId = src.EstadoId,
            AsignadoPersonaId = src.AsignadoPersonaId,
            FechaInicio = src.FechaInicio,
            FechaVencimiento = src.FechaVencimiento,
            PadreId = src.PadreId,
            Origen = OrigenTarea.Manual,
            Color = src.Color,
            EsProyecto = src.EsProyecto,
            Valor = src.Valor,
            HoraInicio = src.HoraInicio,
            HoraFin = src.HoraFin,
            Progreso = 0,
            CreadoPorUsuarioId = GetUsuarioActualId()
        };
        _db.Tareas.Add(nueva);
        await _db.SaveChangesAsync(ct);

        // Copiar colaboradores + campos personalizados + etiquetas (NO subtareas hijas ni checklist).
        var colabs = await _db.TareaColaboradores.AsNoTracking().Where(c => c.TareaId == id).ToListAsync(ct);
        foreach (var c in colabs) _db.TareaColaboradores.Add(new TareaColaborador { TareaId = nueva.Id, PersonaId = c.PersonaId });
        var campos = await _db.TareaCampoValores.AsNoTracking().Where(v => v.TareaId == id).ToListAsync(ct);
        foreach (var v in campos) _db.TareaCampoValores.Add(new TareaCampoValor { TareaId = nueva.Id, TableroCampoId = v.TableroCampoId, Valor = v.Valor });
        var etis = await _db.TareaEtiquetaAsignaciones.AsNoTracking().Where(e => e.TareaId == id).ToListAsync(ct);
        foreach (var e in etis) _db.TareaEtiquetaAsignaciones.Add(new TareaEtiquetaAsignacion { TareaId = nueva.Id, EtiquetaId = e.EtiquetaId });
        if (colabs.Count > 0 || campos.Count > 0 || etis.Count > 0) await _db.SaveChangesAsync(ct);

        await RegistrarHistorial(nueva.Id, TipoEventoTarea.Creada, $"Tarea duplicada de {src.NumeroTarea}", null, new { origen = src.NumeroTarea }, ct);
        await _db.SaveChangesAsync(ct);

        var lista = await ListarTareasAsync(null, null, null, null, null, null, ct, nueva.TableroId);
        return lista.FirstOrDefault(x => x.Id == nueva.Id);
    }

    public async Task<IReadOnlyList<TareaListaDto>> CopiarTareaAsync(Guid id, CopiarTareaRequest req, CancellationToken ct)
    {
        var src = await _db.Tareas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.Eliminada, ct);
        if (src is null) return new List<TareaListaDto>();

        var cantidad = Math.Clamp(req.Cantidad <= 0 ? 1 : req.Cantidad, 1, 20);
        var estadoId = req.EstadoId ?? src.EstadoId;
        if (req.EstadoId is Guid es && es != src.EstadoId && !await _db.TareasEstados.AnyAsync(e => e.Id == es, ct))
            estadoId = src.EstadoId;
        var baseTitulo = string.IsNullOrWhiteSpace(req.Titulo) ? (src.Titulo + " (copia)").Trim() : req.Titulo.Trim();

        // Datos a conservar (leidos una sola vez).
        List<Guid> colabIds = new();
        List<TareaCampoValorDto> campoVals = new();
        List<Guid> etiquetaIds = new();
        if (req.ConservarResponsables) colabIds = await _db.TareaColaboradores.AsNoTracking().Where(c => c.TareaId == id).Select(c => c.PersonaId).ToListAsync(ct);
        if (req.ConservarCampos) campoVals = await _db.TareaCampoValores.AsNoTracking().Where(v => v.TareaId == id).Select(v => new TareaCampoValorDto(v.TableroCampoId, v.Valor)).ToListAsync(ct);
        if (req.ConservarEtiquetas) etiquetaIds = await _db.TareaEtiquetaAsignaciones.AsNoTracking().Where(e => e.TareaId == id).Select(e => e.EtiquetaId).ToListAsync(ct);

        var nuevasIds = new List<Guid>();
        for (int i = 0; i < cantidad; i++)
        {
            var numero = await GenerarNumeroAsync(ct);
            var titulo = cantidad > 1 ? $"{baseTitulo} ({i + 1})" : baseTitulo;
            if (titulo.Length > 200) titulo = titulo[..200];
            var nueva = new Tarea
            {
                NumeroTarea = numero,
                TableroId = src.TableroId,
                Titulo = titulo,
                Descripcion = src.Descripcion,
                Prioridad = src.Prioridad,
                EstadoId = estadoId,
                AsignadoPersonaId = req.ConservarResponsables ? src.AsignadoPersonaId : null,
                FechaInicio = src.FechaInicio,
                FechaVencimiento = src.FechaVencimiento,
                PadreId = null,                 // una copia NUNCA es subtarea
                CopiaDeTareaId = src.Id,         // traza: enlace a la original
                Origen = OrigenTarea.Manual,
                Color = src.Color,
                EsProyecto = src.EsProyecto,
                Valor = src.Valor,
                HoraInicio = src.HoraInicio,
                HoraFin = src.HoraFin,
                OrigenTipo = req.ConservarRelacion ? src.OrigenTipo : null,
                OrigenReferencia = req.ConservarRelacion ? src.OrigenReferencia : null,
                Progreso = 0,
                CreadoPorUsuarioId = GetUsuarioActualId()
            };
            _db.Tareas.Add(nueva);
            await _db.SaveChangesAsync(ct);

            foreach (var pid in colabIds) _db.TareaColaboradores.Add(new TareaColaborador { TareaId = nueva.Id, PersonaId = pid });
            foreach (var v in campoVals) _db.TareaCampoValores.Add(new TareaCampoValor { TareaId = nueva.Id, TableroCampoId = v.CampoId, Valor = v.Valor });
            foreach (var eid in etiquetaIds) _db.TareaEtiquetaAsignaciones.Add(new TareaEtiquetaAsignacion { TareaId = nueva.Id, EtiquetaId = eid });
            if (colabIds.Count > 0 || campoVals.Count > 0 || etiquetaIds.Count > 0) await _db.SaveChangesAsync(ct);

            await RegistrarHistorial(nueva.Id, TipoEventoTarea.Creada, $"Copia de {src.NumeroTarea}", null, new { origen = src.NumeroTarea }, ct);
            await _db.SaveChangesAsync(ct);
            nuevasIds.Add(nueva.Id);
        }

        var lista = await ListarTareasAsync(null, null, null, null, null, null, ct, src.TableroId);
        return lista.Where(x => nuevasIds.Contains(x.Id)).ToList();
    }

    /// <summary>Reemplaza la checklist (tareas relacionadas) de una tarjeta.</summary>
    private async Task ReemplazarChecklistAsync(Guid tareaId, IReadOnlyList<SubtareaCheckItem>? items, CancellationToken ct)
    {
        if (items is null) return;
        await _db.TareaSubtareas.Where(s => s.TareaId == tareaId).ExecuteDeleteAsync(ct);
        var orden = 0;
        foreach (var it in items.Where(x => !string.IsNullOrWhiteSpace(x.Titulo)))
            _db.TareaSubtareas.Add(new TareaSubtarea { TareaId = tareaId, Titulo = it.Titulo.Trim(), Hecho = it.Hecho, Orden = orden++ });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> EliminarTareaAsync(Guid id, CancellationToken ct)
    {
        var t = await _db.Tareas.FirstOrDefaultAsync(x => x.Id == id && !x.Eliminada, ct);
        if (t is null) return false;
        t.Eliminada = true;
        t.UpdatedAt = DateTimeOffset.UtcNow;
        // Soft-delete en cascada de las tareas hijas (sub-cards).
        var hijos = await _db.Tareas.Where(x => x.PadreId == id && !x.Eliminada).ToListAsync(ct);
        foreach (var h in hijos) { h.Eliminada = true; h.UpdatedAt = DateTimeOffset.UtcNow; }
        await RegistrarHistorial(id, TipoEventoTarea.Eliminada, "Tarjeta eliminada", null, null, ct);
        await _db.SaveChangesAsync(ct);
        await RecomputarProgresoAncestrosAsync(t.PadreId, ct);
        return true;
    }

    public async Task<TareaAdjuntoDto?> AgregarAdjuntoAsync(Guid tareaId, string nombre, string url, CancellationToken ct)
    {
        if (!await _db.Tareas.AnyAsync(x => x.Id == tareaId && !x.Eliminada, ct)) return null;
        var uid = GetUsuarioActualId();
        var a = new TareaAdjunto { TareaId = tareaId, Nombre = nombre, Url = url, CreatedBy = uid };
        _db.TareaAdjuntos.Add(a);
        await RegistrarHistorial(tareaId, TipoEventoTarea.AdjuntoAgregado, $"Adjunto agregado: {nombre}", null, null, ct);
        await _db.SaveChangesAsync(ct);
        // Nombre de quien subio (best-effort) para etiquetar el archivo en el chat/lista.
        var personaId = await _db.Users.AsNoTracking().Where(u => u.Id == uid).Select(u => u.PersonaId).FirstOrDefaultAsync(ct);
        string? subidoPor = personaId is Guid pid
            ? await _db.Personas.AsNoTracking().Where(p => p.Id == pid)
                .Select(p => (((p.Nombres ?? "") + " " + (p.Apellidos ?? "")).Trim())).FirstOrDefaultAsync(ct)
            : null;
        return new TareaAdjuntoDto(a.Id, a.Nombre, a.Url, string.IsNullOrWhiteSpace(subidoPor) ? null : subidoPor, a.CreatedAt);
    }

    public async Task<bool> EliminarAdjuntoAsync(Guid tareaId, Guid adjuntoId, CancellationToken ct)
    {
        var n = await _db.TareaAdjuntos.Where(a => a.Id == adjuntoId && a.TareaId == tareaId).ExecuteDeleteAsync(ct);
        return n > 0;
    }

    public async Task<bool> CambiarEstadoAsync(Guid id, CambiarEstadoRequest req, CancellationToken ct)
    {
        var t = await _db.Tareas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return false;
        var nuevo = await _db.TareasEstados.FirstOrDefaultAsync(e => e.Id == req.EstadoId, ct);
        if (nuevo is null) throw new InvalidOperationException("Estado destino invalido.");
        if (t.EstadoId == nuevo.Id) return true;

        var anterior = await _db.TareasEstados.AsNoTracking().FirstOrDefaultAsync(e => e.Id == t.EstadoId, ct);

        // Si va a Cancelada, requiere motivo
        if (nuevo.Nombre == EstadoTareaBase.Cancelada && string.IsNullOrWhiteSpace(req.MotivoCancelacion))
            throw new InvalidOperationException("Se requiere motivo para cancelar la tarea.");

        t.EstadoId = nuevo.Id;
        t.EstadoDesde = DateTimeOffset.UtcNow;   // reinicia el reloj "tiempo en este estado".
        if (nuevo.EsTerminal && nuevo.Nombre == EstadoTareaBase.Completada)
        {
            t.FechaCompletada = DateTimeOffset.UtcNow;
            t.Progreso = 100;   // cerrar una tarea la deja al 100% (alimenta el progreso del padre).
        }
        if (nuevo.Nombre == EstadoTareaBase.Cancelada)
            t.MotivoCancelacion = req.MotivoCancelacion;
        t.UpdatedAt = DateTimeOffset.UtcNow;

        await RegistrarHistorial(t.Id,
            nuevo.Nombre == EstadoTareaBase.Cancelada ? TipoEventoTarea.Cancelada : TipoEventoTarea.EstadoCambiado,
            $"Estado cambiado de {anterior?.Nombre ?? "?"} a {nuevo.Nombre}",
            new { prev = anterior?.Nombre },
            new { nuevo = nuevo.Nombre, motivo = req.MotivoCancelacion },
            ct);
        await _db.SaveChangesAsync(ct);
        await RecomputarProgresoAncestrosAsync(t.PadreId, ct);
        return true;
    }

    // ===================== Comentarios + Etiquetas + Colaboradores =====================

    public async Task<TareaComentarioDto> AgregarComentarioAsync(Guid tareaId, CrearComentarioRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Texto)) throw new InvalidOperationException("Texto obligatorio.");
        var tarea = await _db.Tareas.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tareaId, ct)
            ?? throw new InvalidOperationException("Tarea no encontrada.");
        var c = new TareaComentario { TareaId = tareaId, AutorUsuarioId = GetUsuarioActualId(), Texto = req.Texto.Trim() };
        _db.TareaComentarios.Add(c);
        await RegistrarHistorial(tareaId, TipoEventoTarea.ComentarioAgregado, "Comentario agregado", null, new { len = req.Texto.Length }, ct);
        await _db.SaveChangesAsync(ct);

        // Menciones @persona: extraer @nombre-apellido y notificar via T.2.
        // Patron: @[a-zA-Z0-9._-]+  (acepta acentos basicos en codepoints).
        // Resolucion: matchear PersonaDirectorio por (Nombres+Apellidos sin espacios lower).
        await NotificarMencionesAsync(tarea, c.Texto, ct);

        return new TareaComentarioDto(c.Id, c.AutorUsuarioId, c.Texto, c.CreatedAt);
    }

    private async Task NotificarMencionesAsync(Tarea tarea, string texto, CancellationToken ct)
    {
        var menciones = System.Text.RegularExpressions.Regex.Matches(texto, @"@([A-Za-z0-9._-]{2,40})")
            .Select(m => m.Groups[1].Value.ToLowerInvariant()).Distinct().ToList();
        if (menciones.Count == 0) return;

        var personas = await _db.Personas.AsNoTracking()
            .Select(p => new { p.Id, Nombre = (p.Nombres + " " + p.Apellidos).ToLower().Replace(" ", ".") })
            .ToListAsync(ct);
        var matches = personas
            .Where(p => menciones.Any(m => p.Nombre.Contains(m)))
            .Select(p => p.Id).Distinct().ToList();
        if (matches.Count == 0) return;

        var lote = matches.Select(pid => new Propia.Application.Notificaciones.EnviarNotificacionRequest(
            Canal: CanalNotificacion.InApp,
            Cuerpo: $"Te mencionaron en la tarea {tarea.NumeroTarea} - {tarea.Titulo}",
            TenantId: _tenantContext.CurrentTenantId,
            PersonaDestinatariaId: pid,
            Asunto: $"Mencion en {tarea.NumeroTarea}",
            Prioridad: PrioridadNotificacion.Normal,
            ModuloOrigenCodigo: "2.10",
            EntidadOrigenId: tarea.Id));
        await _noti.EnviarLoteAsync(lote, ct);

        foreach (var pid in matches)
        {
            await RegistrarHistorial(tarea.Id, TipoEventoTarea.PersonaMencionada,
                $"Persona mencionada en comentario", null, new { personaId = pid }, ct);
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> AsignarEtiquetaAsync(Guid tareaId, AsignarEtiquetaRequest req, CancellationToken ct)
    {
        if (!await _db.Tareas.AnyAsync(t => t.Id == tareaId, ct)) return false;
        if (!await _db.TareaEtiquetas.AnyAsync(e => e.Id == req.EtiquetaId, ct)) return false;
        if (await _db.TareaEtiquetaAsignaciones.AnyAsync(a => a.TareaId == tareaId && a.EtiquetaId == req.EtiquetaId, ct)) return true;
        _db.TareaEtiquetaAsignaciones.Add(new TareaEtiquetaAsignacion { TareaId = tareaId, EtiquetaId = req.EtiquetaId });
        await RegistrarHistorial(tareaId, TipoEventoTarea.EtiquetaAsignada, "Etiqueta asignada", null, new { etiquetaId = req.EtiquetaId }, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RemoverEtiquetaAsync(Guid tareaId, Guid etiquetaId, CancellationToken ct)
    {
        var a = await _db.TareaEtiquetaAsignaciones.FirstOrDefaultAsync(x => x.TareaId == tareaId && x.EtiquetaId == etiquetaId, ct);
        if (a is null) return false;
        _db.TareaEtiquetaAsignaciones.Remove(a);
        await RegistrarHistorial(tareaId, TipoEventoTarea.EtiquetaRemovida, "Etiqueta removida", new { etiquetaId }, null, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<TareaColaboradorDto> AgregarColaboradorAsync(Guid tareaId, AgregarColaboradorRequest req, CancellationToken ct)
    {
        if (!await _db.Tareas.AnyAsync(t => t.Id == tareaId, ct))
            throw new InvalidOperationException("Tarea no encontrada.");
        if (await _db.TareaColaboradores.AnyAsync(c => c.TareaId == tareaId && c.PersonaId == req.PersonaId, ct))
            throw new InvalidOperationException("La persona ya es colaboradora de esta tarea.");
        var p = await _db.Personas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == req.PersonaId, ct)
            ?? throw new InvalidOperationException("Persona no encontrada.");
        var c = new TareaColaborador { TareaId = tareaId, PersonaId = req.PersonaId };
        _db.TareaColaboradores.Add(c);
        await RegistrarHistorial(tareaId, TipoEventoTarea.ColaboradorAgregado, $"Colaborador agregado: {p.Nombres}", null, new { personaId = req.PersonaId }, ct);
        await _db.SaveChangesAsync(ct);
        return new TareaColaboradorDto(c.Id, p.Id, $"{p.Nombres} {p.Apellidos}".Trim());
    }

    public async Task<bool> RemoverColaboradorAsync(Guid tareaId, Guid colaboradorId, CancellationToken ct)
    {
        var c = await _db.TareaColaboradores.FirstOrDefaultAsync(x => x.Id == colaboradorId && x.TareaId == tareaId, ct);
        if (c is null) return false;
        _db.TareaColaboradores.Remove(c);
        await RegistrarHistorial(tareaId, TipoEventoTarea.ColaboradorRemovido, "Colaborador removido", new { colaboradorId }, null, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ResumenTareasDto> GetResumenAsync(CancellationToken ct)
    {
        await AsegurarEstadosBaseAsync(ct);
        var tareas = await _db.Tareas.AsNoTracking().Where(t => !t.Eliminada).Include(t => t.Estado).ToListAsync(ct);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var mesAtras = DateTime.UtcNow.AddMonths(-1);
        var pendienteId = await _db.TareasEstados.Where(e => e.Nombre == EstadoTareaBase.Pendiente).Select(e => e.Id).FirstAsync(ct);
        var enProgresoId = await _db.TareasEstados.Where(e => e.Nombre == EstadoTareaBase.EnProgreso).Select(e => e.Id).FirstAsync(ct);

        var porEstado = tareas.GroupBy(t => t.Estado!.Nombre).Select(g => (g.Key, g.Count())).ToList();
        var porPri = tareas.GroupBy(t => t.Prioridad.ToString()).Select(g => (g.Key, g.Count())).ToList();

        // KPIs del tablero (prototipo v2). "Completada" = estado Completada o progreso 100%.
        bool EstaCompletada(Tarea t) => t.Estado!.Nombre == EstadoTareaBase.Completada || t.Progreso >= 100;
        var total = tareas.Count;
        var activas = tareas.Count(t => !t.Estado!.EsTerminal && !EstaCompletada(t));
        var vencenHoy = tareas.Count(t => !t.Estado!.EsTerminal && !EstaCompletada(t) && t.FechaVencimiento == hoy);
        var completadas = tareas.Count(EstaCompletada);
        var avancePct = total > 0 ? (int)Math.Round(completadas * 100.0 / total) : 0;

        return new ResumenTareasDto(
            total,
            tareas.Count(t => t.EstadoId == pendienteId),
            tareas.Count(t => t.EstadoId == enProgresoId),
            tareas.Count(t => !t.Estado!.EsTerminal && !EstaCompletada(t) && t.FechaVencimiento.HasValue && t.FechaVencimiento.Value < hoy),
            tareas.Count(t => t.Estado!.Nombre == EstadoTareaBase.Completada && t.FechaCompletada.HasValue && t.FechaCompletada.Value.UtcDateTime >= mesAtras),
            porEstado,
            porPri,
            activas,
            vencenHoy,
            completadas,
            avancePct);
    }

    // ===================== Helpers =====================

    private async Task RegistrarHistorial(Guid tareaId, TipoEventoTarea tipo, string desc, object? valorAnterior, object? valorNuevo, CancellationToken ct)
    {
        _db.TareaHistorial.Add(new TareaHistorial
        {
            TareaId = tareaId,
            TipoEvento = tipo,
            Descripcion = desc.Length > 300 ? desc[..300] : desc,
            ValorAnterior = valorAnterior is null ? null : JsonSerializer.Serialize(valorAnterior),
            ValorNuevo = valorNuevo is null ? null : JsonSerializer.Serialize(valorNuevo),
            RealizadoPorUsuarioId = GetUsuarioActualId(),
            OcurridoAt = DateTimeOffset.UtcNow
        });
        await Task.CompletedTask;
    }

    // ===================== Dependencias (Fase 2) =====================

    public async Task<TareaDependenciaDto> AgregarDependenciaAsync(
        Guid tareaId, AgregarDependenciaRequest req, CancellationToken ct)
    {
        if (req.DependeDeTareaId == tareaId)
            throw new InvalidOperationException("Una tarea no puede depender de si misma.");

        var t = await _db.Tareas.FirstOrDefaultAsync(x => x.Id == tareaId, ct)
            ?? throw new InvalidOperationException("Tarea no encontrada.");
        var dep = await _db.Tareas.FirstOrDefaultAsync(x => x.Id == req.DependeDeTareaId, ct)
            ?? throw new InvalidOperationException("Tarea predecesora no encontrada.");

        // Evitar ciclos: si dep depende (transitivamente) de t, agregar t->dep crearia ciclo.
        if (await CrearCicloAsync(req.DependeDeTareaId, tareaId, ct))
            throw new InvalidOperationException("La dependencia crearia un ciclo entre tareas.");

        var existente = await _db.TareaDependencias.AnyAsync(
            x => x.TareaId == tareaId && x.DependeDeTareaId == req.DependeDeTareaId, ct);
        if (existente) throw new InvalidOperationException("La dependencia ya existe.");

        var nuevoVal = new TareaDependencia
        {
            TareaId = tareaId,
            DependeDeTareaId = req.DependeDeTareaId,
            Tipo = req.Tipo,
            CreadoPorUsuarioId = GetUsuarioActualId()
        };
        _db.TareaDependencias.Add(nuevoVal);
        await RegistrarHistorial(tareaId, TipoEventoTarea.DependenciaAgregada,
            $"Dependencia agregada con tarea {dep.NumeroTarea} ({req.Tipo})",
            null, new { dep.NumeroTarea, req.Tipo }, ct);
        await _db.SaveChangesAsync(ct);

        return await MapDependenciaAsync(nuevoVal.Id, ct)
            ?? throw new InvalidOperationException("Error mapeando dependencia recien creada.");
    }

    public async Task<bool> RemoverDependenciaAsync(Guid tareaId, Guid dependenciaId, CancellationToken ct)
    {
        var d = await _db.TareaDependencias.FirstOrDefaultAsync(
            x => x.Id == dependenciaId && x.TareaId == tareaId, ct);
        if (d is null) return false;
        var numero = await _db.Tareas.Where(t => t.Id == d.DependeDeTareaId)
            .Select(t => t.NumeroTarea).FirstOrDefaultAsync(ct) ?? "?";
        _db.TareaDependencias.Remove(d);
        await RegistrarHistorial(tareaId, TipoEventoTarea.DependenciaRemovida,
            $"Dependencia removida con tarea {numero}",
            new { numero }, null, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<TareaDependenciaDto>> ListarDependenciasAsync(
        Guid tareaId, CancellationToken ct)
    {
        var deps = await _db.TareaDependencias.AsNoTracking()
            .Where(x => x.TareaId == tareaId)
            .Include(x => x.DependeDeTarea).ThenInclude(t => t!.Estado)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);
        return deps.Select(d => new TareaDependenciaDto(
            d.Id, d.TareaId, d.DependeDeTareaId,
            d.DependeDeTarea?.NumeroTarea ?? "?",
            d.DependeDeTarea?.Titulo ?? "?",
            d.DependeDeTarea?.Estado?.Nombre ?? "?",
            d.DependeDeTarea?.Estado?.EsTerminal ?? false,
            d.Tipo, d.CreatedAt)).ToList();
    }

    private async Task<TareaDependenciaDto?> MapDependenciaAsync(Guid id, CancellationToken ct)
    {
        var d = await _db.TareaDependencias.AsNoTracking()
            .Where(x => x.Id == id)
            .Include(x => x.DependeDeTarea).ThenInclude(t => t!.Estado)
            .FirstOrDefaultAsync(ct);
        if (d is null) return null;
        return new TareaDependenciaDto(
            d.Id, d.TareaId, d.DependeDeTareaId,
            d.DependeDeTarea?.NumeroTarea ?? "?",
            d.DependeDeTarea?.Titulo ?? "?",
            d.DependeDeTarea?.Estado?.Nombre ?? "?",
            d.DependeDeTarea?.Estado?.EsTerminal ?? false,
            d.Tipo, d.CreatedAt);
    }

    /// <summary>
    /// Detecta si agregar la dependencia (tareaId depende de origen) crearia un ciclo.
    /// La nueva arista crearia ciclo sii `origen` ya depende (transitiva) de `destino` (=tareaId).
    /// BFS desde origen siguiendo aristas "X depende de Y" hasta ver si alcanza destino.
    /// </summary>
    private async Task<bool> CrearCicloAsync(Guid origen, Guid destino, CancellationToken ct)
    {
        var visitados = new HashSet<Guid>();
        var cola = new Queue<Guid>();
        cola.Enqueue(origen);
        while (cola.Count > 0)
        {
            var actual = cola.Dequeue();
            if (actual == destino) return true;
            if (!visitados.Add(actual)) continue;
            // Predecesoras de `actual` = todas las tareas Y tales que (actual depende de Y).
            var predecesoras = await _db.TareaDependencias.AsNoTracking()
                .Where(d => d.TareaId == actual)
                .Select(d => d.DependeDeTareaId).ToListAsync(ct);
            foreach (var p in predecesoras) cola.Enqueue(p);
        }
        return false;
    }

    // ===================== Bulk actions (Fase 2) =====================

    public async Task<BulkResultDto> BulkCambiarEstadoAsync(
        BulkCambiarEstadoRequest req, CancellationToken ct)
    {
        if (req.TareaIds.Count == 0)
            return new BulkResultDto(0, 0, 0, Array.Empty<string>());
        var nuevoEstado = await _db.TareasEstados.FirstOrDefaultAsync(e => e.Id == req.NuevoEstadoId, ct)
            ?? throw new InvalidOperationException("Estado no encontrado.");

        var tareas = await _db.Tareas.Include(t => t.Estado)
            .Where(t => req.TareaIds.Contains(t.Id)).ToListAsync(ct);

        var errores = new List<string>();
        int aplicados = 0;
        foreach (var t in tareas)
        {
            if (t.EstadoId == req.NuevoEstadoId) { continue; }
            var estadoAnterior = t.Estado?.Nombre ?? "?";
            t.EstadoId = req.NuevoEstadoId;
            t.EstadoDesde = DateTimeOffset.UtcNow;   // reinicia el reloj "tiempo en este estado".
            t.UpdatedAt = DateTimeOffset.UtcNow;
            if (nuevoEstado.EsTerminal && t.FechaCompletada is null)
                t.FechaCompletada = DateTimeOffset.UtcNow;
            await RegistrarHistorial(t.Id, TipoEventoTarea.EstadoCambiado,
                $"Bulk: estado cambiado de {estadoAnterior} a {nuevoEstado.Nombre}",
                new { estadoAnterior }, new { nuevoEstado = nuevoEstado.Nombre, req.Nota }, ct);
            aplicados++;
        }
        await _db.SaveChangesAsync(ct);
        var omitidos = req.TareaIds.Count - aplicados;
        return new BulkResultDto(req.TareaIds.Count, aplicados, omitidos, errores);
    }

    public async Task<BulkResultDto> BulkCambiarPrioridadAsync(
        BulkCambiarPrioridadRequest req, CancellationToken ct)
    {
        if (req.TareaIds.Count == 0)
            return new BulkResultDto(0, 0, 0, Array.Empty<string>());
        var tareas = await _db.Tareas
            .Where(t => req.TareaIds.Contains(t.Id)).ToListAsync(ct);
        int aplicados = 0;
        foreach (var t in tareas)
        {
            if (t.Prioridad == req.Prioridad) continue;
            var anterior = t.Prioridad;
            t.Prioridad = req.Prioridad;
            t.UpdatedAt = DateTimeOffset.UtcNow;
            await RegistrarHistorial(t.Id, TipoEventoTarea.PrioridadCambiada,
                $"Bulk: prioridad {anterior} -> {req.Prioridad}",
                new { anterior }, new { req.Prioridad }, ct);
            aplicados++;
        }
        await _db.SaveChangesAsync(ct);
        return new BulkResultDto(req.TareaIds.Count, aplicados,
            req.TareaIds.Count - aplicados, Array.Empty<string>());
    }

    public async Task<BulkResultDto> BulkAsignarPersonaAsync(
        BulkAsignarPersonaRequest req, CancellationToken ct)
    {
        if (req.TareaIds.Count == 0)
            return new BulkResultDto(0, 0, 0, Array.Empty<string>());
        var tareas = await _db.Tareas
            .Where(t => req.TareaIds.Contains(t.Id)).ToListAsync(ct);
        var personaNombre = req.AsignadoPersonaId is { } pid
            ? await _db.Personas.Where(p => p.Id == pid)
                .Select(p => p.Nombres + " " + p.Apellidos).FirstOrDefaultAsync(ct)
            : null;
        int aplicados = 0;
        var lote = new List<Propia.Application.Notificaciones.EnviarNotificacionRequest>();
        foreach (var t in tareas)
        {
            if (t.AsignadoPersonaId == req.AsignadoPersonaId) continue;
            var anterior = t.AsignadoPersonaId;
            t.AsignadoPersonaId = req.AsignadoPersonaId;
            t.UpdatedAt = DateTimeOffset.UtcNow;
            await RegistrarHistorial(t.Id, TipoEventoTarea.AsignacionCambiada,
                $"Bulk: asignado a {personaNombre ?? "(sin asignar)"}",
                new { anterior }, new { req.AsignadoPersonaId, personaNombre }, ct);
            aplicados++;

            // T.2: notifica al nuevo asignado (InApp via PersonaDestinatariaId)
            if (req.AsignadoPersonaId is { } nuevoPid)
            {
                lote.Add(new Propia.Application.Notificaciones.EnviarNotificacionRequest(
                    Canal: CanalNotificacion.InApp,
                    Cuerpo: $"Te asignaron la tarea {t.NumeroTarea} - {t.Titulo}",
                    TenantId: _tenantContext.CurrentTenantId,
                    PersonaDestinatariaId: nuevoPid,
                    Asunto: $"Nueva tarea asignada: {t.NumeroTarea}",
                    Prioridad: t.Prioridad == PrioridadTarea.Urgente
                        ? PrioridadNotificacion.Alta : PrioridadNotificacion.Normal,
                    ModuloOrigenCodigo: "2.10",
                    EntidadOrigenId: t.Id));
            }
        }
        await _db.SaveChangesAsync(ct);
        if (lote.Count > 0) await _noti.EnviarLoteAsync(lote, ct);

        return new BulkResultDto(req.TareaIds.Count, aplicados,
            req.TareaIds.Count - aplicados, Array.Empty<string>());
    }

    // ===================== Tableros de trabajo =====================

    private static string? ColorEstadoBase(string nombre) => nombre switch
    {
        EstadoTareaBase.Pendiente => "#94a3b8",
        EstadoTareaBase.EnProgreso => "#3b82f6",
        EstadoTareaBase.EnRevision => "#f59e0b",
        EstadoTareaBase.Bloqueada => "#ef4444",
        EstadoTareaBase.Completada => "#22c55e",
        EstadoTareaBase.Cancelada => "#6b7280",
        _ => "#94a3b8"
    };

    private static string Iniciales(string? nombre)
    {
        var parts = (nombre ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        if (parts.Length == 1) return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
        return ("" + parts[0][0] + parts[1][0]).ToUpperInvariant();
    }

    private async Task SembrarEstadosTableroAsync(Guid tableroId, CancellationToken ct)
    {
        foreach (var (nombre, orden, esTerminal) in EstadoTareaBase.Base)
            _db.TareasEstados.Add(new TareaEstado
            {
                TableroId = tableroId,
                Nombre = nombre,
                Orden = orden,
                EsTerminal = esTerminal,
                EsBase = true,
                Activo = true,
                Color = ColorEstadoBase(nombre)
            });
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Crea el tablero "General" si no existe y migra estados/tareas legacy (TableroId null) a el.</summary>
    private async Task<Guid> AsegurarTableroDefaultAsync(CancellationToken ct)
    {
        var existing = await _db.Tableros.OrderBy(t => t.Orden).Select(t => t.Id).FirstOrDefaultAsync(ct);
        if (existing != Guid.Empty) return existing;

        var t = new Tablero { Nombre = "General", Descripcion = "Tablero principal de tareas.", Color = "#6D4FE3", Orden = 0, Activo = true };
        _db.Tableros.Add(t);
        await _db.SaveChangesAsync(ct);

        // Migrar estados y tareas legacy (sin tablero) al tablero por defecto.
        await _db.TareasEstados.Where(e => e.TableroId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.TableroId, t.Id), ct);
        await _db.Tareas.Where(x => x.TableroId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TableroId, t.Id), ct);
        return t.Id;
    }

    private async Task<TableroDto> MapTableroAsync(Tablero t, CancellationToken ct)
    {
        var nCards = await _db.Tareas.AsNoTracking().CountAsync(x => x.TableroId == t.Id && x.PadreId == null && !x.Eliminada, ct);
        var usuariosIds = await _db.TableroUsuarios.AsNoTracking().Where(u => u.TableroId == t.Id).Select(u => u.PersonaId).ToListAsync(ct);
        var personas = await _db.Personas.AsNoTracking().Where(p => usuariosIds.Contains(p.Id))
            .Select(p => new { p.Id, Nombre = p.Nombres + " " + p.Apellidos }).ToListAsync(ct);
        var usuarios = personas.Select(p => new TableroUsuarioDto(p.Id, p.Nombre.Trim(), Iniciales(p.Nombre))).ToList();
        // Solo campos ACTIVOS: los archivados quedan fuera del modal, columnas y filtros (datos conservados).
        var campos = await _db.TableroCampos.AsNoTracking().Where(c => c.TableroId == t.Id && c.Activo)
            .OrderBy(c => c.Orden)
            .Select(c => new TableroCampoDto(c.Id, c.Label, c.Orden, c.Tipo, c.Opciones, c.MostrarEnFiltro, c.Columna, c.Descripcion, c.Requerido, c.ValorPorDefecto, c.PermiteVarios, c.CamposSuma, c.Activo))
            .ToListAsync(ct);
        return new TableroDto(t.Id, t.Nombre, t.Descripcion, t.Color, t.Orden, nCards, usuarios, campos);
    }

    private static int ClampColumna(int c) => c < 1 ? 1 : (c > 2 ? 2 : c);

    public async Task<TableroCampoDto> AgregarCampoAsync(Guid tableroId, GuardarCampoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Label) || req.Label.Trim().Length < 2)
            throw new InvalidOperationException("Etiqueta minimo 2 caracteres.");
        var lab = req.Label.Trim();
        if (!await _db.Tableros.AnyAsync(t => t.Id == tableroId, ct))
            throw new InvalidOperationException("Tablero no encontrado.");
        if (await _db.TableroCampos.AnyAsync(c => c.TableroId == tableroId && c.Label == lab && c.Activo, ct))
            throw new InvalidOperationException("Ya existe un campo con esa etiqueta.");
        var orden = (await _db.TableroCampos.Where(c => c.TableroId == tableroId).Select(c => (int?)c.Orden).MaxAsync(ct) ?? 0) + 1;
        var c2 = new TableroCampo
        {
            TableroId = tableroId,
            Label = lab,
            Orden = orden,
            Tipo = req.Tipo,
            Opciones = string.IsNullOrWhiteSpace(req.Opciones) ? null : req.Opciones.Trim(),
            MostrarEnFiltro = req.MostrarEnFiltro,
            Columna = ClampColumna(req.Columna),
            Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim(),
            Requerido = req.Requerido,
            ValorPorDefecto = string.IsNullOrWhiteSpace(req.ValorPorDefecto) ? null : req.ValorPorDefecto.Trim(),
            PermiteVarios = req.PermiteVarios,
            CamposSuma = string.IsNullOrWhiteSpace(req.CamposSuma) ? null : req.CamposSuma.Trim()
        };
        _db.TableroCampos.Add(c2);
        await _db.SaveChangesAsync(ct);
        return new TableroCampoDto(c2.Id, c2.Label, c2.Orden, c2.Tipo, c2.Opciones, c2.MostrarEnFiltro, c2.Columna, c2.Descripcion, c2.Requerido, c2.ValorPorDefecto, c2.PermiteVarios, c2.CamposSuma, c2.Activo);
    }

    public async Task<bool> ActualizarCampoAsync(Guid tableroId, Guid campoId, GuardarCampoRequest req, CancellationToken ct)
    {
        var c = await _db.TableroCampos.FirstOrDefaultAsync(x => x.Id == campoId && x.TableroId == tableroId, ct);
        if (c is null) return false;
        if (string.IsNullOrWhiteSpace(req.Label) || req.Label.Trim().Length < 2)
            throw new InvalidOperationException("Etiqueta minimo 2 caracteres.");
        var lab = req.Label.Trim();
        if (await _db.TableroCampos.AnyAsync(x => x.TableroId == tableroId && x.Label == lab && x.Id != campoId && x.Activo, ct))
            throw new InvalidOperationException("Ya existe un campo con esa etiqueta.");
        c.Label = lab;
        c.Tipo = req.Tipo;
        c.Opciones = string.IsNullOrWhiteSpace(req.Opciones) ? null : req.Opciones.Trim();
        c.MostrarEnFiltro = req.MostrarEnFiltro;
        c.Columna = ClampColumna(req.Columna);
        c.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        c.Requerido = req.Requerido;
        c.ValorPorDefecto = string.IsNullOrWhiteSpace(req.ValorPorDefecto) ? null : req.ValorPorDefecto.Trim();
        c.PermiteVarios = req.PermiteVarios;
        c.CamposSuma = string.IsNullOrWhiteSpace(req.CamposSuma) ? null : req.CamposSuma.Trim();
        c.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Inserta/actualiza los valores de campos personalizados de una tarjeta (solo campos validos del tablero).</summary>
    private async Task ReemplazarCamposValoresAsync(Guid tareaId, IReadOnlyList<TareaCampoValorDto>? valores, CancellationToken ct)
    {
        if (valores is null) return;
        var tableroId = await _db.Tareas.Where(t => t.Id == tareaId).Select(t => t.TableroId).FirstOrDefaultAsync(ct);
        var validos = (await _db.TableroCampos.Where(c => c.TableroId == tableroId).Select(c => c.Id).ToListAsync(ct)).ToHashSet();
        var existentes = await _db.TareaCampoValores.Where(v => v.TareaId == tareaId).ToListAsync(ct);
        foreach (var v in valores)
        {
            if (!validos.Contains(v.CampoId)) continue;
            var val = string.IsNullOrWhiteSpace(v.Valor) ? null : v.Valor.Trim();
            var ex = existentes.FirstOrDefault(x => x.TableroCampoId == v.CampoId);
            if (ex is null)
            {
                if (val is not null)
                    _db.TareaCampoValores.Add(new TareaCampoValor { TareaId = tareaId, TableroCampoId = v.CampoId, Valor = val });
            }
            else
            {
                ex.Valor = val;
                ex.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> EliminarCampoAsync(Guid tableroId, Guid campoId, CancellationToken ct)
    {
        var c = await _db.TableroCampos.FirstOrDefaultAsync(x => x.Id == campoId && x.TableroId == tableroId, ct);
        if (c is null) return false;
        await _db.TareaCampoValores.Where(v => v.TableroCampoId == campoId).ExecuteDeleteAsync(ct);
        _db.TableroCampos.Remove(c);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Archiva (activo=false) o restaura (activo=true) un campo. Los valores capturados se
    /// conservan; el campo solo desaparece/aparece del modal, columnas y filtros (distinto de eliminar).</summary>
    public async Task<bool> SetCampoActivoAsync(Guid tableroId, Guid campoId, bool activo, CancellationToken ct)
    {
        var c = await _db.TableroCampos.FirstOrDefaultAsync(x => x.Id == campoId && x.TableroId == tableroId, ct);
        if (c is null) return false;
        if (activo && await _db.TableroCampos.AnyAsync(x => x.TableroId == tableroId && x.Id != campoId && x.Label == c.Label && x.Activo, ct))
            throw new InvalidOperationException($"Ya existe un campo activo llamado '{c.Label}'. Renombra uno antes de restaurar.");
        c.Activo = activo;
        c.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Lista los campos ARCHIVADOS (activo=false) de un tablero, para poder restaurarlos.</summary>
    public async Task<IReadOnlyList<TableroCampoDto>> ListarCamposArchivadosAsync(Guid tableroId, CancellationToken ct) =>
        await _db.TableroCampos.AsNoTracking().Where(c => c.TableroId == tableroId && !c.Activo)
            .OrderBy(c => c.Label)
            .Select(c => new TableroCampoDto(c.Id, c.Label, c.Orden, c.Tipo, c.Opciones, c.MostrarEnFiltro, c.Columna, c.Descripcion, c.Requerido, c.ValorPorDefecto, c.PermiteVarios, c.CamposSuma, c.Activo))
            .ToListAsync(ct);

    /// <summary>Sube (direccion &lt; 0) o baja (direccion &gt;= 0) un campo, intercambiando el
    /// Orden con el campo vecino. Normaliza los ordenes a 0..n-1 para tolerar huecos/empates.</summary>
    public async Task<bool> ReordenarCampoAsync(Guid tableroId, Guid campoId, int direccion, CancellationToken ct)
    {
        var campos = await _db.TableroCampos.Where(c => c.TableroId == tableroId)
            .OrderBy(c => c.Orden).ThenBy(c => c.Id).ToListAsync(ct);
        for (int i = 0; i < campos.Count; i++) campos[i].Orden = i;
        var idx = campos.FindIndex(c => c.Id == campoId);
        if (idx < 0) return false;
        var swap = idx + (direccion < 0 ? -1 : 1);
        if (swap < 0 || swap >= campos.Count) return false;   // ya esta en el extremo
        (campos[idx].Orden, campos[swap].Orden) = (campos[swap].Orden, campos[idx].Orden);
        campos[idx].UpdatedAt = campos[swap].UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<TableroDto>> ListarTablerosAsync(CancellationToken ct)
    {
        await AsegurarEstadosBaseAsync(ct);
        await AsegurarTableroDefaultAsync(ct);
        var tableros = await _db.Tableros.AsNoTracking().Where(t => t.Activo).OrderBy(t => t.Orden).ToListAsync(ct);
        var result = new List<TableroDto>(tableros.Count);
        foreach (var t in tableros) result.Add(await MapTableroAsync(t, ct));
        return result;
    }

    public async Task<TableroDto?> GetTableroAsync(Guid id, CancellationToken ct)
    {
        var t = await _db.Tableros.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return t is null ? null : await MapTableroAsync(t, ct);
    }

    private async Task SetTableroUsuariosAsync(Guid tableroId, IReadOnlyList<Guid>? personaIds, CancellationToken ct)
    {
        await _db.TableroUsuarios.Where(u => u.TableroId == tableroId).ExecuteDeleteAsync(ct);
        foreach (var pid in (personaIds ?? Array.Empty<Guid>()).Distinct())
            _db.TableroUsuarios.Add(new TableroUsuario { TableroId = tableroId, PersonaId = pid });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<TableroDto> CrearTableroAsync(GuardarTableroRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre del tablero requerido.");
        var maxOrden = await _db.Tableros.AnyAsync(ct) ? await _db.Tableros.MaxAsync(t => t.Orden, ct) : -1;
        var t = new Tablero
        {
            Nombre = req.Nombre.Trim(),
            Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim(),
            Color = string.IsNullOrWhiteSpace(req.Color) ? "#6D4FE3" : req.Color.Trim(),
            Orden = maxOrden + 1,
            Activo = true
        };
        _db.Tableros.Add(t);
        await _db.SaveChangesAsync(ct);
        await SetTableroUsuariosAsync(t.Id, req.UsuarioPersonaIds, ct);
        await SembrarEstadosTableroAsync(t.Id, ct);
        return (await GetTableroAsync(t.Id, ct))!;
    }

    public async Task<bool> ActualizarTableroAsync(Guid id, GuardarTableroRequest req, CancellationToken ct)
    {
        var t = await _db.Tableros.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return false;
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre del tablero requerido.");
        t.Nombre = req.Nombre.Trim();
        t.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        t.Color = string.IsNullOrWhiteSpace(req.Color) ? t.Color : req.Color.Trim();
        t.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await SetTableroUsuariosAsync(t.Id, req.UsuarioPersonaIds, ct);
        return true;
    }

    // Enlazar/desenlazar una persona a un tablero (2.5.D: gestion de tableros desde el usuario).
    public async Task<bool> AgregarUsuarioTableroAsync(Guid tableroId, Guid personaId, CancellationToken ct)
    {
        var existe = await _db.Tableros.AnyAsync(t => t.Id == tableroId, ct);
        if (!existe) return false;
        var ya = await _db.TableroUsuarios.AnyAsync(u => u.TableroId == tableroId && u.PersonaId == personaId, ct);
        if (!ya)
        {
            _db.TableroUsuarios.Add(new TableroUsuario { TableroId = tableroId, PersonaId = personaId });
            await _db.SaveChangesAsync(ct);
        }
        return true;
    }

    public async Task<bool> QuitarUsuarioTableroAsync(Guid tableroId, Guid personaId, CancellationToken ct)
    {
        var n = await _db.TableroUsuarios.Where(u => u.TableroId == tableroId && u.PersonaId == personaId).ExecuteDeleteAsync(ct);
        return n > 0;
    }

    public async Task<bool> EliminarTableroAsync(Guid id, CancellationToken ct)
    {
        var t = await _db.Tableros.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return false;
        // Soft-delete: ocultamos el tablero pero conservamos sus tarjetas/estados.
        t.Activo = false;
        t.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<TableroBoardDto?> GetTableroBoardAsync(Guid tableroId, CancellationToken ct)
    {
        await AsegurarTableroDefaultAsync(ct);
        var t = await _db.Tableros.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tableroId && x.Activo, ct);
        if (t is null) return null;
        var dto = await MapTableroAsync(t, ct);
        var estados = await _db.TareasEstados.AsNoTracking().Where(e => e.TableroId == tableroId)
            .OrderBy(e => e.Orden).ThenBy(e => e.Nombre)
            .Select(e => new EstadoTareaDto(e.Id, e.Nombre, e.Color, e.Orden, e.EsTerminal, e.EsBase, e.Activo))
            .ToListAsync(ct);
        var tareas = await ListarTareasAsync(null, null, null, null, null, null, ct, tableroId);
        return new TableroBoardDto(dto, estados, tareas);
    }

    public async Task<bool> ActualizarProgresoAsync(Guid tareaId, int progreso, CancellationToken ct)
    {
        var t = await _db.Tareas.FirstOrDefaultAsync(x => x.Id == tareaId, ct);
        if (t is null) return false;
        // El progreso de una tarea PADRE se deriva de sus hijas; solo las hojas se editan directo.
        var tieneHijos = await _db.Tareas.AnyAsync(x => x.PadreId == tareaId && !x.Eliminada, ct);
        if (!tieneHijos)
        {
            t.Progreso = Math.Clamp(progreso, 0, 100);
            t.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        await RecomputarProgresoAncestrosAsync(t.PadreId, ct);
        return true;
    }

    /// <summary>Recalcula el progreso de los ancestros (padre, abuelo...) como promedio del progreso
    /// efectivo de sus hijas directas. Una hija en estado Completada cuenta como 100%.</summary>
    private async Task RecomputarProgresoAncestrosAsync(Guid? padreId, CancellationToken ct)
    {
        var changed = false;
        while (padreId is { } pid)
        {
            var padre = await _db.Tareas.FirstOrDefaultAsync(x => x.Id == pid && !x.Eliminada, ct);
            if (padre is null) break;
            var hijos = await _db.Tareas.Where(x => x.PadreId == pid && !x.Eliminada)
                .Select(x => new { x.Progreso, Nombre = x.Estado!.Nombre }).ToListAsync(ct);
            if (hijos.Count > 0)
            {
                var prom = (int)Math.Round(hijos.Average(h => h.Nombre == EstadoTareaBase.Completada ? 100.0 : h.Progreso));
                if (padre.Progreso != prom) { padre.Progreso = prom; padre.UpdatedAt = DateTimeOffset.UtcNow; changed = true; }
            }
            padreId = padre.PadreId;
        }
        if (changed) await _db.SaveChangesAsync(ct);
    }
}
