using Microsoft.EntityFrameworkCore;
using Propia.Application.Historial;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Historial;

/// <summary>
/// Historial relacionado cross-modulo (base de las pestanas "Historial" de las fichas).
/// Reune, para una entidad (unidad/zona/equipo), sus mantenimientos, tareas y PQRSD y los
/// ordena por fecha descendente. El filtrado por tenant lo aplica el HasQueryFilter global.
/// </summary>
public class HistorialRelacionadoService : IHistorialRelacionadoService
{
    private readonly PropiaDbContext _db;

    public HistorialRelacionadoService(PropiaDbContext db) => _db = db;

    public async Task<HistorialRelacionadoDto> GetAsync(TipoEntidadHistorial tipo, Guid entidadId, CancellationToken ct)
    {
        var items = new List<HistorialItemDto>();

        // --- Mantenimiento: vinculo fuerte por ActivoTipo + ActivoId (solo zona/equipo) ---
        if (tipo is TipoEntidadHistorial.ZonaComun or TipoEntidadHistorial.Equipo)
        {
            var activoTipo = tipo == TipoEntidadHistorial.Equipo
                ? TipoActivoMantenimiento.Equipo
                : TipoActivoMantenimiento.ZonaComun;

            var mant = await _db.MantenimientoIntervenciones.AsNoTracking()
                .Where(i => i.ActivoTipo == activoTipo && i.ActivoId == entidadId)
                .Select(i => new { i.Id, i.Titulo, i.Codigo, i.Estado, i.FechaCierre, i.FechaInicioReal, i.FechaProgramada, i.CreatedAt })
                .ToListAsync(ct);

            items.AddRange(mant.Select(i => new HistorialItemDto(
                i.Id, OrigenHistorial.Mantenimiento, i.Titulo, i.Codigo, i.Estado.ToString(),
                FechaDe(i.FechaCierre, i.FechaInicioReal, i.FechaProgramada, i.CreatedAt), "/mantenimiento")));
        }

        // --- Tareas: por OrigenEntidadId (fiable) + fallback por nombre para tareas legado ---
        var nombreEntidad = await NombreEntidadAsync(tipo, entidadId, ct);
        var origenTipoTxt = tipo switch
        {
            TipoEntidadHistorial.ZonaComun => "zona",
            TipoEntidadHistorial.Equipo => "equipo",
            TipoEntidadHistorial.Unidad => "inmueble",
            _ => null
        };

        var tareas = await _db.Tareas.AsNoTracking()
            .Where(t => !t.Eliminada &&
                (t.OrigenEntidadId == entidadId ||
                 (t.OrigenEntidadId == null && nombreEntidad != null &&
                  t.OrigenTipo == origenTipoTxt && t.OrigenReferencia == nombreEntidad)))
            .Select(t => new { t.Id, t.Titulo, t.NumeroTarea, Estado = t.Estado != null ? t.Estado.Nombre : null, t.FechaCompletada, t.CreatedAt })
            .ToListAsync(ct);

        items.AddRange(tareas.Select(t => new HistorialItemDto(
            t.Id, OrigenHistorial.Tarea, t.Titulo, t.NumeroTarea, t.Estado,
            t.FechaCompletada ?? t.CreatedAt, "/tareas")));

        // --- PQRSD: por UnidadPrivadaId (solo unidad; no hay vinculo a zona/equipo) ---
        if (tipo == TipoEntidadHistorial.Unidad)
        {
            var pqrs = await _db.PqrsdExpedientes.AsNoTracking()
                .Where(p => p.UnidadPrivadaId == entidadId && !p.Archivado)
                .Select(p => new { p.Id, p.Descripcion, p.NumeroRadicado, p.Estado, p.FechaCierre, p.CreatedAt })
                .ToListAsync(ct);

            items.AddRange(pqrs.Select(p => new HistorialItemDto(
                p.Id, OrigenHistorial.Pqrsd, Resumir(p.Descripcion), p.NumeroRadicado, p.Estado.ToString(),
                p.FechaCierre ?? p.CreatedAt, "/pqrs")));
        }

        var ordenados = items.OrderByDescending(i => i.Fecha).ToList();
        return new HistorialRelacionadoDto(
            ordenados.Count(i => i.Origen == OrigenHistorial.Tarea),
            ordenados.Count(i => i.Origen == OrigenHistorial.Pqrsd),
            ordenados.Count(i => i.Origen == OrigenHistorial.Mantenimiento),
            ordenados);
    }

    // Fecha representativa del mantenimiento: cierre > inicio real > programada > creacion.
    private static DateTimeOffset FechaDe(DateOnly? cierre, DateOnly? inicio, DateOnly? programada, DateTimeOffset creacion)
    {
        var d = cierre ?? inicio ?? programada;
        return d.HasValue ? new DateTimeOffset(d.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : creacion;
    }

    // Nombre legible del elemento, para casar las tareas legado que solo guardan OrigenReferencia.
    private async Task<string?> NombreEntidadAsync(TipoEntidadHistorial tipo, Guid id, CancellationToken ct) => tipo switch
    {
        TipoEntidadHistorial.ZonaComun => await _db.ZonasComunes.AsNoTracking().Where(z => z.Id == id).Select(z => (string?)z.Nombre).FirstOrDefaultAsync(ct),
        TipoEntidadHistorial.Equipo => await _db.EquiposActivos.AsNoTracking().Where(e => e.Id == id).Select(e => (string?)e.Nombre).FirstOrDefaultAsync(ct),
        TipoEntidadHistorial.Unidad => await _db.UnidadesPrivadas.AsNoTracking().Where(u => u.Id == id).Select(u => (string?)u.Numero).FirstOrDefaultAsync(ct),
        _ => null
    };

    private static string Resumir(string? texto)
    {
        var t = (texto ?? "").Trim();
        if (t.Length == 0) return "PQRSD";
        return t.Length <= 80 ? t : t[..80].TrimEnd() + "...";
    }
}
