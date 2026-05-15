using Propia.Domain.Enums;

namespace Propia.Application.Tareas;

/// <summary>Servicio del modulo 2.10 Tareas y Proyectos (spec v1.0 - MVP).</summary>
public interface ITareasService
{
    // Estados (catalogo de la copropiedad)
    Task<IReadOnlyList<EstadoTareaDto>> ListarEstadosAsync(CancellationToken ct);
    Task<EstadoTareaDto> CrearEstadoAsync(CrearEstadoRequest req, CancellationToken ct);
    Task<bool> ActualizarEstadoAsync(Guid id, ActualizarEstadoRequest req, CancellationToken ct);
    Task<bool> EliminarEstadoAsync(Guid id, CancellationToken ct);

    // Etiquetas
    Task<IReadOnlyList<EtiquetaTareaDto>> ListarEtiquetasAsync(CancellationToken ct);
    Task<EtiquetaTareaDto> CrearEtiquetaAsync(CrearEtiquetaRequest req, CancellationToken ct);
    Task<bool> ActualizarEtiquetaAsync(Guid id, ActualizarEtiquetaRequest req, CancellationToken ct);
    Task<bool> EliminarEtiquetaAsync(Guid id, CancellationToken ct);

    // Tareas
    Task<IReadOnlyList<TareaListaDto>> ListarTareasAsync(Guid? estadoId, PrioridadTarea? prioridad, Guid? asignadoPersonaId, Guid? padreId, bool? soloRaiz, string? query, CancellationToken ct);
    Task<TareaDetalleDto?> GetTareaAsync(Guid id, CancellationToken ct);
    Task<TareaDetalleDto> CrearTareaAsync(CrearTareaRequest req, CancellationToken ct);
    Task<bool> ActualizarTareaAsync(Guid id, ActualizarTareaRequest req, CancellationToken ct);
    Task<bool> CambiarEstadoAsync(Guid id, CambiarEstadoRequest req, CancellationToken ct);

    // Comentarios + Etiquetas + Colaboradores
    Task<TareaComentarioDto> AgregarComentarioAsync(Guid tareaId, CrearComentarioRequest req, CancellationToken ct);
    Task<bool> AsignarEtiquetaAsync(Guid tareaId, AsignarEtiquetaRequest req, CancellationToken ct);
    Task<bool> RemoverEtiquetaAsync(Guid tareaId, Guid etiquetaId, CancellationToken ct);
    Task<TareaColaboradorDto> AgregarColaboradorAsync(Guid tareaId, AgregarColaboradorRequest req, CancellationToken ct);
    Task<bool> RemoverColaboradorAsync(Guid tareaId, Guid colaboradorId, CancellationToken ct);

    // Indicadores
    Task<ResumenTareasDto> GetResumenAsync(CancellationToken ct);
}
