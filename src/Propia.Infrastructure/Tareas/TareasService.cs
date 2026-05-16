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

    public TareasService(PropiaDbContext db, ITenantContext tenantContext, IHttpContextAccessor http)
    {
        _db = db;
        _tenantContext = tenantContext;
        _http = http;
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
        CancellationToken ct)
    {
        await AsegurarEstadosBaseAsync(ct);
        IQueryable<Tarea> q = _db.Tareas.AsNoTracking();
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
                t.PadreId
            }
        ).ToListAsync(ct);

        var ids = rows.Select(r => r.Id).ToList();
        var subs = await _db.Tareas.AsNoTracking().Where(t => t.PadreId != null && ids.Contains(t.PadreId!.Value))
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
            etiquetasMap.GetValueOrDefault(r.Id, new List<EtiquetaTareaDto>()))).ToList();
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

        var asigNombre = t.AsignadoPersona is null ? null
            : ((t.AsignadoPersona.Nombres ?? "") + " " + (t.AsignadoPersona.Apellidos ?? "")).Trim();
        var estadoDto = new EstadoTareaDto(t.Estado!.Id, t.Estado.Nombre, t.Estado.Color, t.Estado.Orden, t.Estado.EsTerminal, t.Estado.EsBase, t.Estado.Activo);

        return new TareaDetalleDto(
            t.Id, t.NumeroTarea, t.Titulo, t.Descripcion, t.Prioridad,
            estadoDto, t.AsignadoPersonaId, asigNombre,
            t.FechaInicio, t.FechaVencimiento, t.FechaCompletada,
            t.PadreId, t.Padre?.Titulo, t.Origen, t.ModuloOrigenCodigo, t.ModuloOrigenEntidadId,
            t.CreatedAt, t.CreadoPorUsuarioId,
            etiquetas, subtareas, comentarios, historial, colabs);
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

        Guid estadoId;
        if (req.EstadoId.HasValue)
        {
            estadoId = req.EstadoId.Value;
            if (!await _db.TareasEstados.AnyAsync(e => e.Id == estadoId, ct))
                throw new InvalidOperationException("Estado invalido.");
        }
        else
        {
            estadoId = (await _db.TareasEstados.Where(e => e.Nombre == EstadoTareaBase.Pendiente).Select(e => e.Id).FirstAsync(ct));
        }

        if (req.PadreId.HasValue && !await _db.Tareas.AnyAsync(t => t.Id == req.PadreId.Value, ct))
            throw new InvalidOperationException("Tarea padre no encontrada.");

        var numero = await GenerarNumeroAsync(ct);
        var t = new Tarea
        {
            NumeroTarea = numero,
            Titulo = req.Titulo.Trim(),
            Descripcion = req.Descripcion?.Trim(),
            Prioridad = req.Prioridad,
            EstadoId = estadoId,
            AsignadoPersonaId = req.AsignadoPersonaId,
            FechaInicio = req.FechaInicio,
            FechaVencimiento = req.FechaVencimiento,
            PadreId = req.PadreId,
            Origen = OrigenTarea.Manual,
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

        await RegistrarHistorial(t.Id, TipoEventoTarea.Creada, $"Tarea creada con prioridad {req.Prioridad}", null, new { titulo = t.Titulo, prioridad = req.Prioridad.ToString() }, ct);
        await _db.SaveChangesAsync(ct);

        return (await GetTareaAsync(t.Id, ct))!;
    }

    public async Task<bool> ActualizarTareaAsync(Guid id, ActualizarTareaRequest req, CancellationToken ct)
    {
        var t = await _db.Tareas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return false;
        if (string.IsNullOrWhiteSpace(req.Titulo)) throw new InvalidOperationException("Titulo obligatorio.");

        var cambios = new List<string>();
        var prevAsig = t.AsignadoPersonaId;
        var prevFv = t.FechaVencimiento;
        var prevPri = t.Prioridad;

        t.Titulo = req.Titulo.Trim();
        t.Descripcion = req.Descripcion?.Trim();
        t.Prioridad = req.Prioridad;
        t.AsignadoPersonaId = req.AsignadoPersonaId;
        t.FechaInicio = req.FechaInicio;
        t.FechaVencimiento = req.FechaVencimiento;
        t.UpdatedAt = DateTimeOffset.UtcNow;

        if (prevPri != req.Prioridad)
        {
            await RegistrarHistorial(t.Id, TipoEventoTarea.PrioridadCambiada, $"Prioridad cambiada a {req.Prioridad}",
                new { prev = prevPri.ToString() }, new { nuevo = req.Prioridad.ToString() }, ct);
        }
        if (prevAsig != req.AsignadoPersonaId)
        {
            await RegistrarHistorial(t.Id, TipoEventoTarea.AsignacionCambiada,
                req.AsignadoPersonaId is null ? "Responsable removido" : "Responsable cambiado",
                new { prev = prevAsig }, new { nuevo = req.AsignadoPersonaId }, ct);
        }
        if (prevFv != req.FechaVencimiento)
        {
            await RegistrarHistorial(t.Id, TipoEventoTarea.FechaCambiada, "Fecha de vencimiento actualizada",
                new { prev = prevFv?.ToString("yyyy-MM-dd") }, new { nuevo = req.FechaVencimiento?.ToString("yyyy-MM-dd") }, ct);
        }
        await _db.SaveChangesAsync(ct);
        return true;
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
        if (!await _db.Tareas.AnyAsync(t => t.Id == tareaId, ct))
            throw new InvalidOperationException("Tarea no encontrada.");
        var c = new TareaComentario { TareaId = tareaId, AutorUsuarioId = GetUsuarioActualId(), Texto = req.Texto.Trim() };
        _db.TareaComentarios.Add(c);
        await RegistrarHistorial(tareaId, TipoEventoTarea.ComentarioAgregado, "Comentario agregado", null, new { len = req.Texto.Length }, ct);
        await _db.SaveChangesAsync(ct);
        return new TareaComentarioDto(c.Id, c.AutorUsuarioId, c.Texto, c.CreatedAt);
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
        var tareas = await _db.Tareas.AsNoTracking().Include(t => t.Estado).ToListAsync(ct);
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
}
