using Propia.Domain.Enums;

namespace Propia.Application.Prestamos;

public record PrestamoEquipoDto(
    Guid Id, string Codigo,
    Guid EquipoActivoId, string EquipoNombre,
    Guid PersonaId, string PersonaNombre,
    Guid? UnidadPrivadaId, string? UnidadNumero,
    DateOnly Fecha, TimeOnly? HoraInicio, TimeOnly? HoraFin,
    EstadoPrestamoEquipo Estado, string? Observaciones,
    DateTimeOffset? EntregadoAt, string? EntregaObservacion,
    DateTimeOffset? DevueltoAt, string? DevolucionObservacion,
    int FotosEntrega, int FotosDevolucion);

public record CrearPrestamoRequest(
    Guid EquipoActivoId, Guid PersonaId, Guid? UnidadPrivadaId,
    DateOnly Fecha, TimeOnly? HoraInicio, TimeOnly? HoraFin, string? Observaciones);

/// <summary>Registro de entrega o devolucion con foto opcional (base64).</summary>
public record RegistrarEntregaRequest(
    string? Observacion, string? FotoNombre, string? FotoTipoMime, string? FotoBase64);

public record EntregaFotoDto(Guid Id, string Url, MomentoEntrega Momento, DateTimeOffset CreadoAt);
