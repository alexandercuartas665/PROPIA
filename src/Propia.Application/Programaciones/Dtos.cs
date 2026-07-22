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
    IReadOnlyList<ResponsableProgramacionDto> Responsables,
    TipoProgramacion Tipo = TipoProgramacion.Periodicidad,
    string? CronExpresion = null,
    string ZonaHoraria = "America/Bogota",
    DateTimeOffset? ProximaEjecucionUtc = null,
    bool NotificarPorCorreo = false);

public record CrearProgramacionRequest(
    string Titulo, string? Descripcion, PrioridadTarea Prioridad,
    Guid? TableroId, PeriodicidadProgramacion Periodicidad,
    DateOnly FechaProximaEjecucion, DateOnly? FechaFin,
    IReadOnlyList<ResponsableProgramacionDto>? Responsables,
    string? ModuloOrigenCodigo = null, Guid? EntidadOrigenId = null, string? OrigenReferencia = null,
    TipoProgramacion Tipo = TipoProgramacion.Periodicidad,
    string? CronExpresion = null,
    string? ZonaHoraria = null,
    bool NotificarPorCorreo = false);

public record ActualizarProgramacionRequest(
    string Titulo, string? Descripcion, PrioridadTarea Prioridad,
    Guid? TableroId, PeriodicidadProgramacion Periodicidad,
    DateOnly FechaProximaEjecucion, DateOnly? FechaFin, bool Activa,
    IReadOnlyList<ResponsableProgramacionDto>? Responsables,
    TipoProgramacion Tipo = TipoProgramacion.Periodicidad,
    string? CronExpresion = null,
    string? ZonaHoraria = null,
    bool NotificarPorCorreo = false);

/// <summary>Previsualizacion de una expresion cron: si es valida y cuando correria.</summary>
public record CronPreviewRequest(string CronExpresion, string? ZonaHoraria = null, int Cuantas = 5);

public record CronPreviewResult(bool Valida, string? Error, IReadOnlyList<DateTimeOffset> Proximas);
