using Propia.Domain.Enums;

namespace Propia.Application.Presupuesto;

/// <summary>
/// Servicio del modulo 2.6 (spec v1.0). Editor de presupuesto, motor de liquidacion
/// idempotente, panel de recaudo, pagos manuales, cuotas extraordinarias, auditoria.
/// </summary>
public interface IPresupuestoService
{
    // --- Editor de presupuesto ---
    Task<IReadOnlyList<PresupuestoDto>> ListarPresupuestosAsync(CancellationToken ct);
    Task<PresupuestoDetalleDto?> GetPresupuestoDetalleAsync(Guid id, CancellationToken ct);
    Task<PresupuestoDto> CrearPresupuestoAsync(CrearPresupuestoRequest req, CancellationToken ct);
    Task<bool> ActualizarRubroAsync(Guid rubroId, ActualizarRubroRequest req, CancellationToken ct);
    Task<RubroDto> AgregarRubroPersonalizadoAsync(Guid presupuestoId, AgregarRubroPersonalizadoRequest req, CancellationToken ct);
    Task<bool> EliminarRubroAsync(Guid rubroId, CancellationToken ct);
    Task<bool> EnviarAAprobacionAsync(Guid presupuestoId, CancellationToken ct);
    Task<bool> AprobarPresupuestoAsync(Guid presupuestoId, AprobarPresupuestoRequest req, CancellationToken ct);
    Task<bool> ActivarVigenciaAsync(Guid presupuestoId, CancellationToken ct);
    Task<bool> CerrarVigenciaAsync(Guid presupuestoId, CancellationToken ct);
    Task<bool> EliminarBorradorAsync(Guid presupuestoId, CancellationToken ct);

    // --- Motor de liquidacion (idempotente: re-ejecutar mismo periodo no duplica) ---
    Task<LiquidacionDto> EmitirLiquidacionAsync(EmitirLiquidacionRequest req, CancellationToken ct);
    Task<IReadOnlyList<LiquidacionDto>> ListarLiquidacionesAsync(Guid? presupuestoId, CancellationToken ct);
    Task<LiquidacionUnidadDto?> GetLiquidacionUnidadAsync(Guid liquidacionUnidadId, CancellationToken ct);

    // --- Panel de recaudo (vista del admin) ---
    Task<RecaudoResumenDto> GetRecaudoResumenAsync(DateOnly periodo, CancellationToken ct);
    Task<IReadOnlyList<UnidadRecaudoDto>> ListarUnidadesRecaudoAsync(DateOnly periodo, EstadoPagoLiquidacion? estado, CancellationToken ct);

    // --- Pagos manuales ---
    Task<PagoDto> RegistrarPagoManualAsync(RegistrarPagoManualRequest req, CancellationToken ct);
    Task<IReadOnlyList<PagoDto>> ListarPagosAsync(Guid? unidadId, CancellationToken ct);

    // --- Cuotas extraordinarias ---
    Task<IReadOnlyList<CuotaExtraordinariaDto>> ListarCuotasExtraordinariasAsync(CancellationToken ct);
    Task<CuotaExtraordinariaDto> CrearCuotaExtraordinariaAsync(CrearCuotaExtraordinariaRequest req, CancellationToken ct);
    Task<bool> AprobarCuotaExtraordinariaAsync(Guid cuotaId, AprobarCuotaExtraordinariaRequest req, CancellationToken ct);

    // --- Vista del residente ---
    Task<MiCuotaDto?> GetMiCuotaAsync(Guid unidadId, DateOnly? periodo, CancellationToken ct);

    // --- Auditoria ---
    Task<IReadOnlyList<AuditPresupuestoDto>> ListarAuditoriaAsync(int limit, CancellationToken ct);
}
