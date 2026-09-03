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

}
