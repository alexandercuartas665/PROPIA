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

    public async Task<EstadoTareaDto> CrearEstadoAsync(CrearEstadoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre) || req.Nombre.Trim().Length < 2)
            throw new InvalidOperationException("Nombre minimo 2 caracteres.");
        var nom = req.Nombre.Trim();
        if (await _db.TareasEstados.AnyAsync(e => e.Nombre == nom, ct))
            throw new InvalidOperationException("Ya existe un estado con este nombre.");
        var e = new TareaEstado { Nombre = nom, Color = req.Color, Orden = req.Orden, EsTerminal = false, EsBase = false, Activo = true };
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
        if (await _db.Tareas.AnyAsync(t => t.EstadoId == id, ct))
            throw new InvalidOperationException("No puedes eliminar un estado con tareas asociadas. Reasignalas primero.");
        _db.TareasEstados.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Etiquetas =====================

    public async Task<IReadOnlyList<EtiquetaTareaDto>> ListarEtiquetasAsync(CancellationToken ct)
    {
        var rows = await _db.TareaEtiquetas.AsNoTracking().OrderBy(e => e.Nombre).ToListAsync(ct);
        var counts = await _db.TareaEtiquetaAsignaciones.AsNoTracking()
            .GroupBy(a => a.EtiquetaId)
            .Select(g => new { g.Key, Cant = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Cant, ct);
        return rows.Select(e => new EtiquetaTareaDto(e.Id, e.Nombre, e.Color, e.Activo, counts.GetValueOrDefault(e.Id, 0))).ToList();
    }

    public async Task<EtiquetaTareaDto> CrearEtiquetaAsync(CrearEtiquetaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre obligatorio.");
        var nom = req.Nombre.Trim();
        if (await _db.TareaEtiquetas.AnyAsync(e => e.Nombre == nom, ct))
            throw new InvalidOperationException("Ya existe una etiqueta con este nombre.");
        var e = new TareaEtiqueta { Nombre = nom, Color = req.Color, Activo = true };
        _db.TareaEtiquetas.Add(e);
        await _db.SaveChangesAsync(ct);
        return new EtiquetaTareaDto(e.Id, e.Nombre, e.Color, true, 0);
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
                t.PadreId,
                t.Progreso,
                t.Color,
                t.EsProyecto,
                t.Valor
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
            select new { a.TareaId, Dto = new EtiquetaTareaDto(e.Id, e.Nombre, e.Color, e.Activo, 0) }
        ).ToListAsync(ct);
        var etiquetasMap = etiquetas.GroupBy(x => x.TareaId).ToDictionary(g => g.Key, g => (IReadOnlyList<EtiquetaTareaDto>)g.Select(x => x.Dto).ToList());

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        return rows.Select(r => new TareaListaDto(
            r.Id, r.NumeroTarea, r.Titulo, r.Prioridad, r.EstadoId, r.EstadoNombre, r.EstadoColor, r.EstadoEsTerminal,
            r.AsignadoPersonaId, r.AsigNombre,
            r.FechaVencimiento,
            !r.EstadoEsTerminal && r.FechaVencimiento.HasValue && r.FechaVencimiento.Value < hoy,
            r.PadreId,
            subs.GetValueOrDefault(r.Id, 0),
            coms.GetValueOrDefault(r.Id, 0),
            etiquetasMap.GetValueOrDefault(r.Id, new List<EtiquetaTareaDto>()),
            r.Progreso, r.Color, r.EsProyecto, r.Valor)).ToList();
    }

    public async Task<TareaDetalleDto?> GetTareaAsync(Guid id, CancellationToken ct)
    {
        var t = await _db.Tareas.AsNoTracking()
            .Include(x => x.Estado)
            .Include(x => x.AsignadoPersona)
            .Include(x => x.Padre)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return null;

        var etiquetasIds = await _db.TareaEtiquetaAsignaciones.AsNoTracking()
            .Where(a => a.TareaId == id).Select(a => a.EtiquetaId).ToListAsync(ct);
        var etiquetas = await _db.TareaEtiquetas.AsNoTracking()
            .Where(e => etiquetasIds.Contains(e.Id))
            .Select(e => new EtiquetaTareaDto(e.Id, e.Nombre, e.Color, e.Activo, 0))
            .ToListAsync(ct);

        var subtareas = await ListarTareasAsync(null, null, null, id, null, null, ct);

        var comentarios = await _db.TareaComentarios.AsNoTracking()
            .Where(c => c.TareaId == id)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new TareaComentarioDto(c.Id, c.AutorUsuarioId, c.Texto, c.CreatedAt))
            .ToListAsync(ct);

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

        var adjuntos = await _db.TareaAdjuntos.AsNoTracking()
            .Where(a => a.TareaId == id)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new TareaAdjuntoDto(a.Id, a.Nombre, a.Url))
            .ToListAsync(ct);

        var checklist = await _db.TareaSubtareas.AsNoTracking()
            .Where(s => s.TareaId == id)
            .OrderBy(s => s.Orden)
            .Select(s => new SubtareaCheckDto(s.Id, s.Titulo, s.Hecho, s.Orden))
            .ToListAsync(ct);

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
            t.OrigenTipo, t.OrigenReferencia, t.TableroId, adjuntos, checklist);
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

        await RegistrarHistorial(t.Id, TipoEventoTarea.Creada, $"Tarea creada con prioridad {req.Prioridad}", null, new { titulo = t.Titulo, prioridad = req.Prioridad.ToString() }, ct);
        await _db.SaveChangesAsync(ct);

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

        return true;
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
        return true;
    }

    public async Task<TareaAdjuntoDto?> AgregarAdjuntoAsync(Guid tareaId, string nombre, string url, CancellationToken ct)
    {
        if (!await _db.Tareas.AnyAsync(x => x.Id == tareaId && !x.Eliminada, ct)) return null;
        var a = new TareaAdjunto { TareaId = tareaId, Nombre = nombre, Url = url };
        _db.TareaAdjuntos.Add(a);
        await RegistrarHistorial(tareaId, TipoEventoTarea.AdjuntoAgregado, $"Adjunto agregado: {nombre}", null, null, ct);
        await _db.SaveChangesAsync(ct);
        return new TareaAdjuntoDto(a.Id, a.Nombre, a.Url);
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
        if (nuevo.EsTerminal && nuevo.Nombre == EstadoTareaBase.Completada)
            t.FechaCompletada = DateTimeOffset.UtcNow;
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
        return new ResumenTareasDto(
            tareas.Count,
            tareas.Count(t => t.EstadoId == pendienteId),
            tareas.Count(t => t.EstadoId == enProgresoId),
            tareas.Count(t => !t.Estado!.EsTerminal && t.FechaVencimiento.HasValue && t.FechaVencimiento.Value < hoy),
            tareas.Count(t => t.Estado!.Nombre == EstadoTareaBase.Completada && t.FechaCompletada.HasValue && t.FechaCompletada.Value.UtcDateTime >= mesAtras),
            porEstado,
            porPri);
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
        return new TableroDto(t.Id, t.Nombre, t.Descripcion, t.Color, t.Orden, nCards, usuarios);
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
        t.Progreso = Math.Clamp(progreso, 0, 100);
        t.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
