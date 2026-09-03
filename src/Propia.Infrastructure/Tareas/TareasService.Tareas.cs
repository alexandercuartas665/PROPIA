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

// Particion de TareasService por area (clase parcial: comparte _db/_tenantContext/_http/_noti
// y GetUsuarioActualId del archivo principal). Mismo comportamiento.
public partial class TareasService
{
    // CRUD y logica de tareas (creacion, edicion, movimiento, cierre, asignaciones).
    // ===================== Tareas =====================

    public async Task<IReadOnlyList<TareaListaDto>> ListarTareasAsync(
        Guid? estadoId, PrioridadTarea? prioridad, Guid? asignadoPersonaId, Guid? padreId, bool? soloRaiz, string? query,
        CancellationToken ct, Guid? tableroId = null, bool verCerradas = false)
    {
        await AsegurarEstadosBaseAsync(ct);
        // Las tareas CERRADAS desaparecen del tablero activo; solo se ven en la pestana "Cerrados".
        IQueryable<Tarea> q = _db.Tareas.AsNoTracking().Where(t => !t.Eliminada && t.Cerrada == verCerradas);
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
                t.EstadoDesde,
                t.MotivoCancelacion,
                t.CerradaAt
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
                resp, r.Descripcion, r.OrigenTipo, r.OrigenReferencia, r.EstadoDesde,
                r.MotivoCancelacion, r.CerradaAt);
        }).ToList();
    }

    public async Task<TareaDetalleDto?> GetTareaAsync(Guid id, CancellationToken ct)
    {
        var t = await _db.Tareas.AsNoTracking()
            .Include(x => x.Estado)
            .Include(x => x.AsignadoPersona)
            .Include(x => x.SolicitantePersona)
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
            .Select(a => new { a.Id, a.Nombre, a.Url, a.CreatedBy, a.CreatedAt, a.Texto })
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
            .Select(a => new TareaAdjuntoDto(a.Id, a.Nombre, a.Url, ResolverSubidoPor(a.CreatedBy), a.CreatedAt, a.CreatedBy, a.Texto))
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
                null, null, null, c.OrigenTipo, c.OrigenReferencia, c.EstadoDesde, c.MotivoCancelacion, c.CerradaAt,
                null, null)
        ).ToListAsync(ct);

        var asigNombre = t.AsignadoPersona is null ? null
            : ((t.AsignadoPersona.Nombres ?? "") + " " + (t.AsignadoPersona.Apellidos ?? "")).Trim();
        var solNombre = t.SolicitantePersona is null ? null
            : ((t.SolicitantePersona.Nombres ?? "") + " " + (t.SolicitantePersona.Apellidos ?? "")).Trim();
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
            t.CopiaDeTareaId, t.CopiaDe?.NumeroTarea, t.CopiaDe?.Titulo, copias,
            t.SolicitantePersonaId, string.IsNullOrWhiteSpace(solNombre) ? null : solNombre);
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

        // Solicitante: por defecto el usuario que crea (su persona del directorio); editable con el selector.
        var uidActual = GetUsuarioActualId();
        var solicitante = req.SolicitantePersonaId;
        if (solicitante is null && uidActual != Guid.Empty)
            solicitante = await _db.Users.AsNoTracking().Where(u => u.Id == uidActual).Select(u => u.PersonaId).FirstOrDefaultAsync(ct);

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
            SolicitantePersonaId = solicitante,
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
            OrigenEntidadId = req.OrigenEntidadId,
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

        // Notificar al responsable asignado (todos sus canales configurados).
        if (asignado is { } asigNuevo)
            await _noti.EnviarEventoUsuarioAsync(asigNuevo, $"Tarea asignada: {t.NumeroTarea}",
                $"Te asignaron la tarea {t.NumeroTarea} - {t.Titulo}", "2.10", t.Id,
                _tenantContext.CurrentTenantId, PrioridadNotificacion.Normal, ct);

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
        // OrigenEntidadId: solo se sobreescribe si el request lo trae (null en edicion = conservar).
        if (req.OrigenEntidadId.HasValue) t.OrigenEntidadId = req.OrigenEntidadId;
        // Solicitante (opcional): solo se cambia si viene en el request y es distinto; valida que la persona exista.
        if (req.SolicitantePersonaId is { } spid && spid != t.SolicitantePersonaId)
        {
            if (!await _db.Personas.AsNoTracking().AnyAsync(p => p.Id == spid, ct))
                throw new InvalidOperationException("La persona solicitante no existe.");
            t.SolicitantePersonaId = spid;
        }
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

        // Notificar al nuevo responsable si cambio la asignacion (todos sus canales).
        if (prevAsig != asignado && asignado is { } nuevoAsig)
            await _noti.EnviarEventoUsuarioAsync(nuevoAsig, $"Tarea asignada: {t.NumeroTarea}",
                $"Te asignaron la tarea {t.NumeroTarea} - {t.Titulo}", "2.10", t.Id,
                _tenantContext.CurrentTenantId, PrioridadNotificacion.Normal, ct);

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

    public async Task<TareaAdjuntoDto?> AgregarAdjuntoAsync(Guid tareaId, string nombre, string url, string? texto, CancellationToken ct)
    {
        var tarea = await _db.Tareas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tareaId && !x.Eliminada, ct);
        if (tarea is null) return null;
        var uid = GetUsuarioActualId();
        var cap = string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
        var a = new TareaAdjunto { TareaId = tareaId, Nombre = nombre, Url = url, Texto = cap, CreatedBy = uid };
        _db.TareaAdjuntos.Add(a);
        await RegistrarHistorial(tareaId, TipoEventoTarea.AdjuntoAgregado, $"Adjunto agregado: {nombre}", null, null, ct);
        await _db.SaveChangesAsync(ct);
        // El caption puede traer menciones @persona; notificar igual que un comentario.
        if (cap is not null) await NotificarMencionesAsync(tarea, cap, ct);
        // Nombre de quien subio (best-effort) para etiquetar el archivo en el chat/lista.
        var personaId = await _db.Users.AsNoTracking().Where(u => u.Id == uid).Select(u => u.PersonaId).FirstOrDefaultAsync(ct);
        string? subidoPor = personaId is Guid pid
            ? await _db.Personas.AsNoTracking().Where(p => p.Id == pid)
                .Select(p => (((p.Nombres ?? "") + " " + (p.Apellidos ?? "")).Trim())).FirstOrDefaultAsync(ct)
            : null;
        return new TareaAdjuntoDto(a.Id, a.Nombre, a.Url, string.IsNullOrWhiteSpace(subidoPor) ? null : subidoPor, a.CreatedAt, uid, a.Texto);
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

        // Cierre: al mover a un estado TERMINAL se pide un motivo de cierre y la tarea se CIERRA
        // (se archiva y desaparece del tablero activo, queda en la pestana "Cerrados").
        string? motivoNombre = null;
        if (nuevo.EsTerminal)
        {
            if (req.MotivoCierreId is not Guid mcid)
                throw new InvalidOperationException("Debes elegir un motivo de cierre.");
            var motivo = await _db.MotivosCierre.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == mcid && m.Modulo == "tareas", ct)
                ?? throw new InvalidOperationException("Motivo de cierre invalido.");
            motivoNombre = motivo.Nombre;
            t.Cerrada = true;
            t.CerradaAt = DateTimeOffset.UtcNow;
            t.MotivoCierreId = mcid;
            t.MotivoCancelacion = motivo.Nombre;   // compat: la ficha muestra el motivo
        }
        else if (t.Cerrada)
        {
            // Reabrir: vuelve de un estado terminal a uno activo -> reaparece en el tablero.
            t.Cerrada = false;
            t.CerradaAt = null;
            t.MotivoCierreId = null;
        }

        t.EstadoId = nuevo.Id;
        t.EstadoDesde = DateTimeOffset.UtcNow;   // reinicia el reloj "tiempo en este estado".
        if (nuevo.EsTerminal && nuevo.Nombre == EstadoTareaBase.Completada)
        {
            t.FechaCompletada = DateTimeOffset.UtcNow;
            t.Progreso = 100;   // cerrar una tarea la deja al 100% (alimenta el progreso del padre).
        }
        t.UpdatedAt = DateTimeOffset.UtcNow;

        await RegistrarHistorial(t.Id,
            nuevo.Nombre == EstadoTareaBase.Cancelada ? TipoEventoTarea.Cancelada : TipoEventoTarea.EstadoCambiado,
            nuevo.EsTerminal ? $"Cerrada ({nuevo.Nombre}) - motivo: {motivoNombre}" : $"Estado cambiado de {anterior?.Nombre ?? "?"} a {nuevo.Nombre}",
            new { prev = anterior?.Nombre },
            new { nuevo = nuevo.Nombre, motivo = motivoNombre },
            ct);
        await _db.SaveChangesAsync(ct);
        await RecomputarProgresoAncestrosAsync(t.PadreId, ct);
        return true;
    }

}
