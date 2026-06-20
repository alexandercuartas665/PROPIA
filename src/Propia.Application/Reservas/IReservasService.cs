using Propia.Domain.Enums;

namespace Propia.Application.Reservas;

/// <summary>
/// Modulo 2.13 Reservas de Zonas Comunes - servicio de aplicacion (spec v1.0 MVP).
///
/// Alcance MVP:
///  - Configuracion por zona (franjas + tarifa + cancelacion + reglamento).
///  - Galeria de zonas reservables consumida de 2.3.
///  - Calculo de disponibilidad backend (RN-05 no doble reserva).
///  - Crear reserva con auto-confirmacion si hay disponibilidad (RN-04).
///  - Validaciones de anticipacion / duracion / cartera (consume 2.7).
///  - Cancelar (residente o admin) con politica de penalidad (RN-09, RN-13).
///  - Bloqueos manuales del admin (RN-15).
///  - Vista del dia para portero (consumida por 2.12).
///  - Reglamento aceptado con timestamp (RN-12).
///
/// Diferido a Fase 2:
///  - Reservas recurrentes (entidad presente, motor diferido).
///  - Pago real via Wompi (registra Pendiente; T.2 integracion completa despues).
///  - Notificaciones reales por WhatsApp/in-app via T.2.
///  - Cobro de penalidad automatica.
///  - Job de expiracion a 15 min.
///  - Agente IA WhatsApp.
///  - Listener automatico desde 2.3 cuando zona pasa a EnMantenimiento.
/// </summary>
public interface IReservasService
{
    // ----- Configuracion de zona -----
    Task<IReadOnlyList<ZonaConfigDto>> ListarConfigsAsync(CancellationToken ct);
    Task<ZonaConfigDto?> GetConfigAsync(Guid zonaComunId, CancellationToken ct);
    Task<ZonaConfigDto> GuardarConfigAsync(GuardarConfigRequest req, CancellationToken ct);

    // ----- Galeria + disponibilidad -----
    Task<IReadOnlyList<ZonaGaleriaDto>> ListarGaleriaAsync(bool soloVisibles, CancellationToken ct);
    Task<DisponibilidadDto> CalcularDisponibilidadAsync(Guid zonaComunId, DateOnly desde, DateOnly hasta, CancellationToken ct);

    // ----- Reservas -----
    Task<IReadOnlyList<ReservaDto>> ListarReservasAsync(
        DateOnly? desde, DateOnly? hasta, Guid? zonaId, Guid? personaId,
        EstadoReserva? estado, CancellationToken ct);
    Task<ReservaDto?> GetReservaAsync(Guid id, CancellationToken ct);

    /// <summary>Crea la reserva. Si TieneTarifa, crea ReservaPago en Pendiente.</summary>
    Task<ReservaDto> CrearReservaAsync(CrearReservaRequest req, CancellationToken ct);

    Task<bool> CancelarComoResidenteAsync(Guid id, CancelarReservaRequest req, CancellationToken ct);
    Task<bool> CancelarComoAdminAsync(Guid id, CancelarReservaRequest req, CancellationToken ct);

    /// <summary>Aprueba una reserva en PendienteAprobacion (la pasa a Confirmada). RN-04.</summary>
    Task<bool> AprobarAsync(Guid id, CancellationToken ct);

    // ----- Bloqueos -----
    Task<IReadOnlyList<BloqueoDto>> ListarBloqueosAsync(Guid? zonaId, DateOnly? desde, DateOnly? hasta, CancellationToken ct);
    Task<BloqueoDto> CrearBloqueoAsync(CrearBloqueoRequest req, CancellationToken ct);
    Task<bool> EliminarBloqueoAsync(Guid id, CancellationToken ct);

    // ----- Vista portero (consumida por 2.12) -----
    Task<IReadOnlyList<ReservaDelDiaDto>> ListarReservasDelDiaAsync(DateOnly fecha, CancellationToken ct);

    // ----- Resumen -----
    Task<ResumenReservasDto> GetResumenAsync(CancellationToken ct);
}
