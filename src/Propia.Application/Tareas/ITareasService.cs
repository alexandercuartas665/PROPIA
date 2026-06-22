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
    Task<IReadOnlyList<TareaListaDto>> ListarTareasAsync(Guid? estadoId, PrioridadTarea? prioridad, Guid? asignadoPersonaId, Guid? padreId, bool? soloRaiz, string? query, CancellationToken ct, Guid? tableroId = null);
    Task<TareaDetalleDto?> GetTareaAsync(Guid id, CancellationToken ct);
    Task<TareaDetalleDto> CrearTareaAsync(CrearTareaRequest req, CancellationToken ct);
    Task<bool> ActualizarTareaAsync(Guid id, ActualizarTareaRequest req, CancellationToken ct);
    Task<bool> CambiarEstadoAsync(Guid id, CambiarEstadoRequest req, CancellationToken ct);
    /// <summary>Soft-delete de una tarjeta y sus tareas hijas.</summary>
    Task<bool> EliminarTareaAsync(Guid id, CancellationToken ct);

    // Adjuntos de la tarjeta
    Task<TareaAdjuntoDto?> AgregarAdjuntoAsync(Guid tareaId, string nombre, string url, CancellationToken ct);
    Task<bool> EliminarAdjuntoAsync(Guid tareaId, Guid adjuntoId, CancellationToken ct);

    // Comentarios + Etiquetas + Colaboradores
    Task<TareaComentarioDto> AgregarComentarioAsync(Guid tareaId, CrearComentarioRequest req, CancellationToken ct);
    Task<bool> AsignarEtiquetaAsync(Guid tareaId, AsignarEtiquetaRequest req, CancellationToken ct);
    Task<bool> RemoverEtiquetaAsync(Guid tareaId, Guid etiquetaId, CancellationToken ct);
    Task<TareaColaboradorDto> AgregarColaboradorAsync(Guid tareaId, AgregarColaboradorRequest req, CancellationToken ct);
    Task<bool> RemoverColaboradorAsync(Guid tareaId, Guid colaboradorId, CancellationToken ct);

    // Dependencias (Fase 2)
    Task<TareaDependenciaDto> AgregarDependenciaAsync(Guid tareaId, AgregarDependenciaRequest req, CancellationToken ct);
    Task<bool> RemoverDependenciaAsync(Guid tareaId, Guid dependenciaId, CancellationToken ct);
    Task<IReadOnlyList<TareaDependenciaDto>> ListarDependenciasAsync(Guid tareaId, CancellationToken ct);

    // Bulk actions (Fase 2): aplicar en lote a N tareas
    Task<BulkResultDto> BulkCambiarEstadoAsync(BulkCambiarEstadoRequest req, CancellationToken ct);
    Task<BulkResultDto> BulkCambiarPrioridadAsync(BulkCambiarPrioridadRequest req, CancellationToken ct);
    Task<BulkResultDto> BulkAsignarPersonaAsync(BulkAsignarPersonaRequest req, CancellationToken ct);

    // Indicadores
    Task<ResumenTareasDto> GetResumenAsync(CancellationToken ct);

    // Tableros de trabajo (2.10)
    Task<IReadOnlyList<TableroDto>> ListarTablerosAsync(CancellationToken ct);
    Task<TableroDto?> GetTableroAsync(Guid id, CancellationToken ct);
    Task<TableroDto> CrearTableroAsync(GuardarTableroRequest req, CancellationToken ct);
    Task<bool> ActualizarTableroAsync(Guid id, GuardarTableroRequest req, CancellationToken ct);
    Task<bool> EliminarTableroAsync(Guid id, CancellationToken ct);

    /// <summary>Vista completa de un tablero (tablero + estados + tarjetas).</summary>
    Task<TableroBoardDto?> GetTableroBoardAsync(Guid tableroId, CancellationToken ct);
    Task<bool> ActualizarProgresoAsync(Guid tareaId, int progreso, CancellationToken ct);

    // Campos personalizados del tablero (tipados)
    Task<TableroCampoDto> AgregarCampoAsync(Guid tableroId, GuardarCampoRequest req, CancellationToken ct);
    Task<bool> ActualizarCampoAsync(Guid tableroId, Guid campoId, GuardarCampoRequest req, CancellationToken ct);
    Task<bool> EliminarCampoAsync(Guid tableroId, Guid campoId, CancellationToken ct);
}
