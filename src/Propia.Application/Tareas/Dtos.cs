using Propia.Domain.Enums;

namespace Propia.Application.Tareas;

public record EstadoTareaDto(Guid Id, string Nombre, string? Color, int Orden, bool EsTerminal, bool EsBase, bool Activo);
public record EtiquetaTareaDto(Guid Id, string Nombre, string? Color, bool Activo, int CantidadTareas);

public record TareaListaDto(
    Guid Id,
    string NumeroTarea,
    string Titulo,
    PrioridadTarea Prioridad,
    Guid EstadoId,
    string EstadoNombre,
    string? EstadoColor,
    bool EstadoEsTerminal,
    Guid? AsignadoPersonaId,
    string? AsignadoNombre,
    DateOnly? FechaVencimiento,
    bool Vencida,
    Guid? PadreId,
    int CantidadSubtareas,
    int CantidadComentarios,
    IReadOnlyList<EtiquetaTareaDto> Etiquetas);

public record TareaDetalleDto(
    Guid Id,
    string NumeroTarea,
    string Titulo,
    string? Descripcion,
    PrioridadTarea Prioridad,
    EstadoTareaDto Estado,
    Guid? AsignadoPersonaId,
    string? AsignadoNombre,
    DateOnly? FechaInicio,
    DateOnly? FechaVencimiento,
    DateTimeOffset? FechaCompletada,
    Guid? PadreId,
    string? PadreTitulo,
    OrigenTarea Origen,
    string? ModuloOrigenCodigo,
    Guid? ModuloOrigenEntidadId,
    DateTimeOffset CreatedAt,
    Guid? CreadoPorUsuarioId,
    IReadOnlyList<EtiquetaTareaDto> Etiquetas,
    IReadOnlyList<TareaListaDto> Subtareas,
    IReadOnlyList<TareaComentarioDto> Comentarios,
    IReadOnlyList<TareaHistorialDto> Historial,
    IReadOnlyList<TareaColaboradorDto> Colaboradores);

public record TareaComentarioDto(Guid Id, Guid AutorUsuarioId, string Texto, DateTimeOffset CreatedAt);
public record TareaHistorialDto(TipoEventoTarea TipoEvento, string Descripcion, Guid RealizadoPorUsuarioId, DateTimeOffset OcurridoAt);
public record TareaColaboradorDto(Guid Id, Guid PersonaId, string Nombre);

public record CrearTareaRequest(
    string Titulo,
    string? Descripcion,
    PrioridadTarea Prioridad,
    Guid? EstadoId,
    Guid? AsignadoPersonaId,
    DateOnly? FechaInicio,
    DateOnly? FechaVencimiento,
    Guid? PadreId,
    IReadOnlyList<Guid>? EtiquetaIds);

public record ActualizarTareaRequest(
    string Titulo,
    string? Descripcion,
    PrioridadTarea Prioridad,
    Guid? AsignadoPersonaId,
    DateOnly? FechaInicio,
    DateOnly? FechaVencimiento);

public record CambiarEstadoRequest(Guid EstadoId, string? MotivoCancelacion);
public record CrearComentarioRequest(string Texto);
public record AsignarEtiquetaRequest(Guid EtiquetaId);
public record AgregarColaboradorRequest(Guid PersonaId);

public record CrearEstadoRequest(string Nombre, string? Color, int Orden);
public record ActualizarEstadoRequest(string Nombre, string? Color, int Orden, bool Activo);

public record CrearEtiquetaRequest(string Nombre, string? Color);
public record ActualizarEtiquetaRequest(string Nombre, string? Color, bool Activo);

public record ResumenTareasDto(
    int Total,
    int Pendientes,
    int EnProgreso,
    int Vencidas,
    int CompletadasUltimoMes,
    IReadOnlyList<(string Estado, int Cantidad)> PorEstado,
    IReadOnlyList<(string Prioridad, int Cantidad)> PorPrioridad);
