using Propia.Domain.Enums;

namespace Propia.Application.Programaciones;

/// <summary>Usuario afectado por una programacion (Persona del Directorio).</summary>
public record ResponsableProgramacionDto(Guid PersonaId, string? Nombre);

public record ProgramacionTareaDto(
    Guid Id, string Titulo, string? Descripcion, PrioridadTarea Prioridad,
    Guid? TableroId, string? TableroNombre, PeriodicidadProgramacion Periodicidad,
    DateOnly FechaProximaEjecucion, DateOnly? FechaFin, bool Activa,
    string? ModuloOrigenCodigo, Guid? EntidadOrigenId, string? OrigenReferencia,
    int TareasGeneradas, DateTimeOffset? UltimaEjecucion,
    IReadOnlyList<ResponsableProgramacionDto> Responsables);

public record CrearProgramacionRequest(
    string Titulo, string? Descripcion, PrioridadTarea Prioridad,
    Guid? TableroId, PeriodicidadProgramacion Periodicidad,
    DateOnly FechaProximaEjecucion, DateOnly? FechaFin,
    IReadOnlyList<ResponsableProgramacionDto>? Responsables,
    string? ModuloOrigenCodigo = null, Guid? EntidadOrigenId = null, string? OrigenReferencia = null);

public record ActualizarProgramacionRequest(
    string Titulo, string? Descripcion, PrioridadTarea Prioridad,
    Guid? TableroId, PeriodicidadProgramacion Periodicidad,
    DateOnly FechaProximaEjecucion, DateOnly? FechaFin, bool Activa,
    IReadOnlyList<ResponsableProgramacionDto>? Responsables);
