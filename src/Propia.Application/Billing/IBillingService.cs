namespace Propia.Application.Billing;

/// <summary>
/// Casos de uso del modulo 0.2 Billing y Suscripciones.
/// Toda accion del Super Admin sobre planes, suscripciones o estados de cliente queda
/// en super_admin_logs (regla RN-17 del spec). Las acciones automaticas/internas quedan
/// en suscripcion_historial append-only.
/// </summary>
public interface IBillingService
{
    // ---- Planes ----
    Task<IReadOnlyList<PlanDto>> ListPlanesAsync(CancellationToken ct);
    Task<PlanDto> CrearPlanAsync(CrearPlanRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct);
    Task<PlanDto?> ActualizarPlanAsync(Guid planId, ActualizarPlanRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct);

    // ---- Suscripciones ----
    Task<IReadOnlyList<SuscripcionDto>> ListSuscripcionesAsync(CancellationToken ct);
    Task<SuscripcionDto> CrearSuscripcionAsync(CrearSuscripcionRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct);
    Task<SuscripcionDto?> CambiarPlanSuscripcionAsync(Guid suscripcionId, CambiarPlanRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct);
    Task<SuscripcionDto?> CambiarEstadoSuscripcionAsync(Guid suscripcionId, CambiarEstadoSuscripcionRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct);
    Task<IReadOnlyList<SuscripcionHistorialDto>> GetHistorialAsync(Guid suscripcionId, CancellationToken ct);

    // ---- Facturas ----
    Task<IReadOnlyList<FacturaDto>> ListFacturasAsync(Guid? suscripcionId, CancellationToken ct);
    Task<FacturaDto> GenerarFacturaAsync(GenerarFacturaRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct);
    Task<FacturaDto?> RegistrarPagoAsync(Guid facturaId, RegistrarPagoFacturaRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct);

    // ---- Config ----
    Task<BillingConfigDto> GetConfigAsync(CancellationToken ct);
    Task<BillingConfigDto> ActualizarConfigAsync(ActualizarBillingConfigRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct);
}
