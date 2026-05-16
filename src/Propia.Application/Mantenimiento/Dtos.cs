using Propia.Domain.Enums;

namespace Propia.Application.Mantenimiento;

// ===========================================================================
// Panel y vistas resumidas
// ===========================================================================

/// <summary>Fila de activo en el panel principal con semaforo calculado en backend.</summary>
public record ActivoPanelDto(
    TipoActivoMantenimiento ActivoTipo,
    Guid ActivoId,
    string Nombre,
    string? Categoria,
    string EstadoActivo,
    SemaforoMantenimiento Semaforo,
    string? PlanProximo,
    DateOnly? ProximaEjecucion,
    int? DiasParaVencer,
    string? ProveedorPreferido,
    DateOnly? UltimoMantenimiento,
    int TotalPlanesActivos,
    int IntervencionesAbiertas);

public record ResumenMantenimientoDto(
    int ActivosVerde,
    int ActivosAmarillo,
    int ActivosRojo,
    int ActivosNegro,
    int IntervencionesAbiertas,
    int IntervencionesVencidas,
    int PreventivosMes,
    int CorrectivosMes);

// ===========================================================================
// Plan preventivo
// ===========================================================================

public record PlanDto(
    Guid Id,
    TipoActivoMantenimiento ActivoTipo,
    Guid ActivoId,
    string ActivoNombre,
    string Nombre,
    string? Descripcion,
    FrecuenciaMantenimiento Frecuencia,
    int? FrecuenciaDias,
    DateOnly FechaInicio,
    DateOnly ProximaEjecucion,
    Guid? ProveedorPreferidoId,
    string? ProveedorPreferidoNombre,
    DisparoPlanMantenimiento Disparo,
    int DiasAlertaPrevio,
    bool GeneraNotifResidentes,
    bool Activo,
    SemaforoMantenimiento Semaforo,
    int? DiasParaVencer);

public record CrearPlanRequest(
    TipoActivoMantenimiento ActivoTipo,
    Guid ActivoId,
    string Nombre,
    string? Descripcion,
    FrecuenciaMantenimiento Frecuencia,
    int? FrecuenciaDias,
    DateOnly FechaInicio,
    Guid? ProveedorPreferidoId,
    DisparoPlanMantenimiento Disparo,
    int DiasAlertaPrevio,
    bool GeneraNotifResidentes);

public record ActualizarPlanRequest(
    string Nombre,
    string? Descripcion,
    FrecuenciaMantenimiento Frecuencia,
    int? FrecuenciaDias,
    Guid? ProveedorPreferidoId,
    DisparoPlanMantenimiento Disparo,
    int DiasAlertaPrevio,
    bool GeneraNotifResidentes);

// ===========================================================================
// Intervencion
// ===========================================================================

public record IntervencionListaDto(
    Guid Id,
    string Codigo,
    TipoIntervencionMantenimiento Tipo,
    TipoActivoMantenimiento ActivoTipo,
    Guid ActivoId,
    string ActivoNombre,
    string Titulo,
    EstadoIntervencion Estado,
    PrioridadIntervencion Prioridad,
    OrigenIntervencion Origen,
    string? ProveedorNombre,
    DateOnly? FechaProgramada,
    DateOnly? FechaCierre,
    Guid? TareaId,
    string? TareaNumero,
    bool Vencida);

public record IntervencionDetalleDto(
    Guid Id,
    string Codigo,
    TipoIntervencionMantenimiento Tipo,
    TipoActivoMantenimiento ActivoTipo,
    Guid ActivoId,
    string ActivoNombre,
    Guid? PlanId,
    string? PlanNombre,
    OrigenIntervencion Origen,
    Guid? OrigenReferenciaId,
    string Titulo,
    string? Descripcion,
    EstadoIntervencion Estado,
    PrioridadIntervencion Prioridad,
    Guid? ProveedorId,
    string? ProveedorNombre,
    Guid? ResponsableInternoId,
    string? ResponsableInternoNombre,
    DateOnly? FechaProgramada,
    DateOnly? FechaInicioReal,
    DateOnly? FechaCierre,
    Guid? TareaId,
    string? TareaNumero,
    bool CambioEstadoActivo,
    string? EstadoActivoNuevo,
    bool NotificarResidentes,
    string? MotivoCancelacion,
    DateTimeOffset CreadoAt,
    IReadOnlyList<BitacoraEntradaDto> Bitacora);

public record CrearIntervencionRequest(
    TipoIntervencionMantenimiento Tipo,
    TipoActivoMantenimiento ActivoTipo,
    Guid ActivoId,
    Guid? PlanId,
    OrigenIntervencion Origen,
    Guid? OrigenReferenciaId,
    string Titulo,
    string? Descripcion,
    PrioridadIntervencion Prioridad,
    Guid? ProveedorId,
    Guid? ResponsableInternoId,
    DateOnly? FechaProgramada,
    bool NotificarResidentes);

public record ActualizarIntervencionRequest(
    string Titulo,
    string? Descripcion,
    PrioridadIntervencion Prioridad,
    Guid? ProveedorId,
    Guid? ResponsableInternoId,
    DateOnly? FechaProgramada,
    bool NotificarResidentes);

public record CambiarEstadoIntervencionRequest(
    EstadoIntervencion NuevoEstado,
    string? Motivo,
    string? ContenidoBitacora);

public record CerrarIntervencionRequest(
    DateOnly FechaCierre,
    string ContenidoBitacora,
    bool CambiarEstadoActivo,
    string? EstadoActivoNuevo,
    bool NotificarResidentes,
    string? MotivoCambioEstado);

public record CancelarIntervencionRequest(string MotivoCancelacion);

// ===========================================================================
// Bitacora
// ===========================================================================

public record BitacoraEntradaDto(
    Guid Id,
    Guid AutorUsuarioId,
    string AutorNombre,
    TipoAutorBitacoraMantenimiento TipoAutor,
    string Contenido,
    DateTimeOffset CreadoAt,
    IReadOnlyList<AdjuntoBitacoraDto> Adjuntos);

public record AdjuntoBitacoraDto(
    Guid Id,
    string NombreArchivo,
    string TipoMime,
    long TamanoBytes,
    string UrlStorage,
    DateTimeOffset SubidoAt);

public record AgregarBitacoraRequest(
    string Contenido,
    TipoAutorBitacoraMantenimiento TipoAutor);

// ===========================================================================
// Cambio de estado del activo
// ===========================================================================

public record CambioEstadoActivoRequest(
    TipoActivoMantenimiento ActivoTipo,
    Guid ActivoId,
    string EstadoNuevo,
    string? Motivo,
    bool NotificarResidentes,
    Guid? IntervencionId);

public record HistorialEstadoActivoDto(
    Guid Id,
    string EstadoAnterior,
    string EstadoNuevo,
    string? Motivo,
    bool NotificadoResidentes,
    Guid ActorUsuarioId,
    Guid? IntervencionId,
    string? IntervencionCodigo,
    DateTimeOffset CreadoAt);
