namespace Propia.Application.Prestamos;

/// <summary>
/// Prestamos de equipos/activos reservables + trazabilidad de entrega/devolucion con fotos,
/// reutilizada tambien por las reservas de zona (OrigenTipo "reserva").
/// </summary>
public interface IPrestamosService
{
    // ----- Prestamos de equipo -----
    Task<IReadOnlyList<PrestamoEquipoDto>> ListarAsync(Guid? equipoId, CancellationToken ct);
    Task<PrestamoEquipoDto?> GetAsync(Guid id, CancellationToken ct);
    Task<PrestamoEquipoDto> CrearAsync(CrearPrestamoRequest req, CancellationToken ct);
    Task<bool> RegistrarEntregaAsync(Guid id, RegistrarEntregaRequest req, CancellationToken ct);
    Task<bool> RegistrarDevolucionAsync(Guid id, RegistrarEntregaRequest req, CancellationToken ct);
    Task<bool> CancelarAsync(Guid id, string? motivo, CancellationToken ct);

    // ----- Entrega/devolucion de una reserva de ZONA -----
    Task<bool> RegistrarEntregaReservaAsync(Guid reservaId, RegistrarEntregaRequest req, CancellationToken ct);
    Task<bool> RegistrarDevolucionReservaAsync(Guid reservaId, RegistrarEntregaRequest req, CancellationToken ct);

    // ----- Fotos (generico: "prestamo" | "reserva") -----
    Task<IReadOnlyList<EntregaFotoDto>> ListarFotosAsync(string origenTipo, Guid origenId, CancellationToken ct);
}
