using Propia.Domain.Enums;

namespace Propia.Application.Cartera;

/// <summary>
/// Modulo 2.7 Cartera y Estado de Cuenta - spec v1.0 MVP.
/// Operativo: gestiona mora a partir de las liquidaciones del modulo 2.6.
/// </summary>
public interface ICarteraService
{
    // ---------- Sincronizacion + tablero ----------
    /// <summary>Reconstruye los DeudaDetalle y CarteraUnidad a partir de LiquidacionUnidad del modulo 2.6.</summary>
    Task<int> SincronizarDesdePresupuestoAsync(CancellationToken ct);

    Task<CarteraTableroDto> GetTableroAsync(CancellationToken ct);

    Task<CarteraUnidadDetalleDto?> GetUnidadAsync(Guid unidadPrivadaId, CancellationToken ct);

    Task<MiCuotaDto?> GetMiCuotaAsync(Guid? unidadPrivadaId, CancellationToken ct);

    // ---------- Estados de gestion ----------
    Task<IReadOnlyList<EstadoCarteraDto>> ListarEstadosAsync(CancellationToken ct);
    Task<bool> CambiarEstadoUnidadAsync(Guid unidadPrivadaId, CambiarEstadoUnidadRequest req, CancellationToken ct);

    // ---------- Acuerdos de pago ----------
    Task<IReadOnlyList<AcuerdoPagoDto>> ListarAcuerdosAsync(EstadoAcuerdoPago? estado, CancellationToken ct);
    Task<AcuerdoPagoDto?> GetAcuerdoAsync(Guid id, CancellationToken ct);
    Task<AcuerdoPagoDto> CrearAcuerdoAsync(CrearAcuerdoRequest req, CancellationToken ct);
    Task<bool> EnviarParaAceptacionAsync(Guid acuerdoId, CancellationToken ct);
    Task<bool> AceptarAcuerdoAsync(Guid acuerdoId, AceptarAcuerdoRequest req, CancellationToken ct);
    Task<bool> CancelarAcuerdoAsync(Guid acuerdoId, CancelarAcuerdoRequest req, CancellationToken ct);
    Task<bool> RegistrarPagoAcuerdoAsync(RegistrarPagoAcuerdoRequest req, CancellationToken ct);

    // ---------- Condonaciones ----------
    Task<IReadOnlyList<CondonacionDto>> ListarCondonacionesAsync(Guid? unidadId, CancellationToken ct);
    Task<CondonacionDto> AplicarCondonacionAsync(AplicarCondonacionRequest req, CancellationToken ct);

    // ---------- Paz y salvo ----------
    Task<IReadOnlyList<PazSalvoDto>> ListarPazSalvosAsync(Guid? unidadId, CancellationToken ct);
    Task<PazSalvoDto> EmitirPazSalvoAsync(EmitirPazSalvoRequest req, CancellationToken ct);
    Task<bool> AnularPazSalvoAsync(Guid id, AnularPazSalvoRequest req, CancellationToken ct);

    // ---------- Configuracion ----------
    Task<CarteraConfigDto> GetConfigAsync(CancellationToken ct);
    Task<bool> ActualizarConfigAsync(CarteraConfigDto req, CancellationToken ct);
}
