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
    // Helpers, dependencias entre tareas (Fase 2) y acciones masivas (Fase 2).
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

        // T-04: el lote sigue las MISMAS reglas que el cambio individual. El motivo de cierre se
        // resuelve UNA vez y ANTES del bucle: si el estado destino es terminal y no hay motivo, no se
        // toca ninguna tarea. Antes el lote cerraba tareas sin motivo, sin marcarlas como cerradas y
        // poniendole fecha de completada incluso a las canceladas.
        var motivo = await ResolverMotivoCierreAsync(nuevoEstado, req.MotivoCierreId, ct);

        var tareas = await _db.Tareas.Include(t => t.Estado)
            .Where(t => req.TareaIds.Contains(t.Id)).ToListAsync(ct);

        // Los ids pedidos que no aparecen no existen o son de otra copropiedad (los quita la query
        // filter). Antes se contaban como "omitidos" sin decir por que: la lista de errores existia
        // pero nunca se llenaba.
        var errores = req.TareaIds.Where(x => tareas.All(t => t.Id != x))
            .Select(x => $"Tarea {x}: no existe en esta copropiedad.").ToList();

        var ancestros = new HashSet<Guid>();
        int aplicados = 0;
        foreach (var t in tareas)
        {
            if (t.EstadoId == req.NuevoEstadoId) { continue; }
            await AplicarCambioEstadoAsync(t, nuevoEstado, motivo, "Bulk: ", req.Nota, ct);
            if (t.PadreId is Guid pid) ancestros.Add(pid);
            aplicados++;
        }
        await _db.SaveChangesAsync(ct);
        // El progreso del padre se deriva del de sus hijas: se recalcula una vez por rama afectada
        // (antes el lote no lo recalculaba, asi que el padre se quedaba con el progreso viejo).
        foreach (var pid in ancestros)
            await RecomputarProgresoAncestrosAsync(pid, ct);

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
        // T-02: el asignado es UNO para todo el lote. Si no pertenece a la copropiedad no hay nada
        // que salvar, asi que falla el lote entero antes de modificar ninguna tarea.
        await ValidarPersonaDelTenantAsync(req.AsignadoPersonaId, "asignado", ct);
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

}
