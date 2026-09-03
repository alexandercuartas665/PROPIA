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
    // Comentarios, etiquetas y colaboradores de una tarea.
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

        // Resolver el nombre completo del autor (usuario->persona) para mostrarlo de una en el chat sin recargar.
        var personaId = await _db.Users.AsNoTracking().Where(u => u.Id == c.AutorUsuarioId).Select(u => u.PersonaId).FirstOrDefaultAsync(ct);
        string? autorNombre = personaId is Guid pid
            ? await _db.Personas.AsNoTracking().Where(p => p.Id == pid)
                .Select(p => (((p.Nombres ?? "") + " " + (p.Apellidos ?? "")).Trim())).FirstOrDefaultAsync(ct)
            : null;
        return new TareaComentarioDto(c.Id, c.AutorUsuarioId, c.Texto, c.CreatedAt, string.IsNullOrWhiteSpace(autorNombre) ? null : autorNombre);
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

        foreach (var pid in matches)
            await _noti.EnviarEventoUsuarioAsync(pid,
                $"Mencion en {tarea.NumeroTarea}",
                $"Te mencionaron en la tarea {tarea.NumeroTarea} - {tarea.Titulo}",
                "2.10", tarea.Id, _tenantContext.CurrentTenantId, PrioridadNotificacion.Normal, ct);

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

}
