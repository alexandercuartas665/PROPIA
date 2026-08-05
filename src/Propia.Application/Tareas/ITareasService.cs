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
    /// <summary>Edicion inline de un solo campo (vista tabla tipo Excel), preservando el resto.</summary>
    Task<bool> ActualizarCampoInlineAsync(Guid id, InlineUpdateTareaRequest req, CancellationToken ct);
    /// <summary>Set del valor de un campo personalizado (TableroCampo) de la tarea.</summary>
    Task<bool> SetCampoValorAsync(Guid tareaId, Guid campoId, string? valor, CancellationToken ct);
    /// <summary>Duplica la tarea como una NUEVA (mismos datos + "(copia)"), sin subtareas hijas.</summary>
    Task<TareaListaDto?> DuplicarTareaAsync(Guid id, CancellationToken ct);
    /// <summary>Copia una tarea N veces con opciones (titulo, etapa, que conservar). Cada copia
    /// es INDEPENDIENTE y queda enlazada al original via CopiaDeTareaId (traza). Devuelve las copias.</summary>
    Task<IReadOnlyList<TareaListaDto>> CopiarTareaAsync(Guid id, CopiarTareaRequest req, CancellationToken ct);
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
    Task<bool> AgregarUsuarioTableroAsync(Guid tableroId, Guid personaId, CancellationToken ct);
    Task<bool> QuitarUsuarioTableroAsync(Guid tableroId, Guid personaId, CancellationToken ct);

    /// <summary>Vista completa de un tablero (tablero + estados + tarjetas).</summary>
    Task<TableroBoardDto?> GetTableroBoardAsync(Guid tableroId, CancellationToken ct);
    Task<bool> ActualizarProgresoAsync(Guid tareaId, int progreso, CancellationToken ct);

    // Campos personalizados del tablero (tipados)
    Task<TableroCampoDto> AgregarCampoAsync(Guid tableroId, GuardarCampoRequest req, CancellationToken ct);
    Task<bool> ActualizarCampoAsync(Guid tableroId, Guid campoId, GuardarCampoRequest req, CancellationToken ct);
    Task<bool> EliminarCampoAsync(Guid tableroId, Guid campoId, CancellationToken ct);
    Task<bool> ReordenarCampoAsync(Guid tableroId, Guid campoId, int direccion, CancellationToken ct);
    /// <summary>Archiva (activo=false) o restaura (activo=true) un campo sin borrar sus valores.</summary>
    Task<bool> SetCampoActivoAsync(Guid tableroId, Guid campoId, bool activo, CancellationToken ct);
    /// <summary>Campos archivados de un tablero (para restaurar).</summary>
    Task<IReadOnlyList<TableroCampoDto>> ListarCamposArchivadosAsync(Guid tableroId, CancellationToken ct);
}
