using Propia.Domain.Enums;

namespace Propia.Application.Historial;

/// <summary>Una entrada del historial relacionado de una entidad (tarea, PQRSD o mantenimiento).</summary>
public record HistorialItemDto(
    Guid Id,
    OrigenHistorial Origen,
    string Titulo,
    string? Codigo,
    string? Estado,
    DateTimeOffset Fecha,
    string Ruta);

/// <summary>Historial relacionado de una entidad: entradas ordenadas por fecha (desc) + conteos por origen.</summary>
public record HistorialRelacionadoDto(
    int TotalTareas,
    int TotalPqrsd,
    int TotalMantenimientos,
    IReadOnlyList<HistorialItemDto> Items);
