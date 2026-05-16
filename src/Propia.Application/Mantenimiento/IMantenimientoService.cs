using Propia.Domain.Enums;

namespace Propia.Application.Mantenimiento;

/// <summary>
/// Modulo 2.11 Mantenimiento y Activos - servicio de aplicacion (spec v1.0 MVP).
/// Gestiona planes preventivos, intervenciones (preventivas y correctivas), bitacora
/// append-only, cambio de estado del activo y vinculo bidireccional con 2.10 Tareas.
/// </summary>
public interface IMantenimientoService
{
    // ----- Panel y resumen -----
    Task<IReadOnlyList<ActivoPanelDto>> ListarActivosPanelAsync(
        TipoActivoMantenimiento? activoTipo,
        SemaforoMantenimiento? semaforo,
        string? query,
        CancellationToken ct);

    Task<ResumenMantenimientoDto> GetResumenAsync(CancellationToken ct);

    // ----- Planes preventivos -----
    Task<IReadOnlyList<PlanDto>> ListarPlanesAsync(
        TipoActivoMantenimiento? activoTipo,
        Guid? activoId,
        bool? activos,
        CancellationToken ct);

    Task<PlanDto?> GetPlanAsync(Guid id, CancellationToken ct);

    Task<PlanDto> CrearPlanAsync(CrearPlanRequest req, CancellationToken ct);

    Task<bool> ActualizarPlanAsync(Guid id, ActualizarPlanRequest req, CancellationToken ct);

    /// <summary>Soft delete (RN-09) - activo=false. Conserva historial.</summary>
    Task<bool> DesactivarPlanAsync(Guid id, CancellationToken ct);

    Task<bool> ReactivarPlanAsync(Guid id, CancellationToken ct);

    // ----- Intervenciones -----
    Task<IReadOnlyList<IntervencionListaDto>> ListarIntervencionesAsync(
        EstadoIntervencion? estado,
        TipoIntervencionMantenimiento? tipo,
        TipoActivoMantenimiento? activoTipo,
        Guid? activoId,
        Guid? proveedorId,
        string? query,
        CancellationToken ct);

    Task<IntervencionDetalleDto?> GetIntervencionAsync(Guid id, CancellationToken ct);

    /// <summary>Crea la intervencion + tarea vinculada en 2.10 (RN-03). Atomico.</summary>
    Task<IntervencionDetalleDto> CrearIntervencionAsync(CrearIntervencionRequest req, CancellationToken ct);

    Task<bool> ActualizarIntervencionAsync(Guid id, ActualizarIntervencionRequest req, CancellationToken ct);

    /// <summary>Cambia estado simple (no es cierre). Valida transiciones permitidas.</summary>
    Task<bool> CambiarEstadoIntervencionAsync(Guid id, CambiarEstadoIntervencionRequest req, CancellationToken ct);

    /// <summary>Cierre completo: marca Completada, recalcula proxima_ejecucion del plan,
    /// opcionalmente cambia estado del activo. Requiere bitacora (validacion seccion 16).</summary>
    Task<bool> CerrarIntervencionAsync(Guid id, CerrarIntervencionRequest req, CancellationToken ct);

    Task<bool> CancelarIntervencionAsync(Guid id, CancelarIntervencionRequest req, CancellationToken ct);

    // ----- Bitacora -----
    Task<BitacoraEntradaDto> AgregarEntradaBitacoraAsync(
        Guid intervencionId,
        AgregarBitacoraRequest req,
        CancellationToken ct);

    Task<IReadOnlyList<BitacoraEntradaDto>> ListarBitacoraAsync(Guid intervencionId, CancellationToken ct);

    // ----- Cambio de estado del activo -----
    Task<bool> CambiarEstadoActivoAsync(CambioEstadoActivoRequest req, CancellationToken ct);

    Task<IReadOnlyList<HistorialEstadoActivoDto>> ListarHistorialEstadoAsync(
        TipoActivoMantenimiento activoTipo,
        Guid activoId,
        CancellationToken ct);
}
