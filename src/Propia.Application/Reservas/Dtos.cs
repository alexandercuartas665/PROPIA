using Propia.Domain.Enums;

namespace Propia.Application.Reservas;

// ===========================================================================
// Configuracion de zona reservable
// ===========================================================================

public record ZonaConfigDto(
    Guid Id,
    Guid ZonaComunId,
    string ZonaNombre,
    bool RequiereAprobacion,
    int? MaxReservasActivasPorUnidad,
    bool BloqueoPorCartera,
    int AnticipacionMinimaHoras,
    int AnticipacionMaximaDias,
    int DuracionMinimaMinutos,
    int DuracionMaximaMinutos,
    int IntervaloBloqueMinutos,
    bool TieneTarifa,
    ModalidadCobroReserva ModalidadCobro,
    decimal ValorTarifa,
    PoliticaReembolso PoliticaReembolso,
    decimal ValorPenalidadCancelacion,
    int LimiteCancelacionHoras,
    bool PermiteCancelacionResidente,
    ComportamientoCancelacionTardia CancelacionTardia,
    string? ReglamentoTexto,
    bool RequiereAceptacionReglamento,
    bool VisibleParaResidentes,
    IReadOnlyList<FranjaDto> Franjas);

public record FranjaDto(Guid Id, DiaSemanaReserva DiaSemana, TimeOnly HoraApertura, TimeOnly HoraCierre, bool Activa);

public record GuardarConfigRequest(
    Guid ZonaComunId,
    bool RequiereAprobacion,
    int? MaxReservasActivasPorUnidad,
    bool BloqueoPorCartera,
    int AnticipacionMinimaHoras,
    int AnticipacionMaximaDias,
    int DuracionMinimaMinutos,
    int DuracionMaximaMinutos,
    int IntervaloBloqueMinutos,
    bool TieneTarifa,
    ModalidadCobroReserva ModalidadCobro,
    decimal ValorTarifa,
    PoliticaReembolso PoliticaReembolso,
    decimal ValorPenalidadCancelacion,
    int LimiteCancelacionHoras,
    bool PermiteCancelacionResidente,
    ComportamientoCancelacionTardia CancelacionTardia,
    string? ReglamentoTexto,
    bool RequiereAceptacionReglamento,
    bool VisibleParaResidentes,
    IReadOnlyList<FranjaInput> Franjas);

public record FranjaInput(DiaSemanaReserva DiaSemana, TimeOnly HoraApertura, TimeOnly HoraCierre, bool Activa);

// ===========================================================================
// Galeria y disponibilidad
// ===========================================================================

public record ZonaGaleriaDto(
    Guid ZonaComunId,
    string Nombre,
    string? Descripcion,
    string? FotoUrl,
    int? Aforo,
    bool Reservable,
    string Estado,
    bool TieneTarifa,
    decimal? ValorTarifa,
    ModalidadCobroReserva? ModalidadCobro);

public record DisponibilidadDto(
    Guid ZonaComunId,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    IReadOnlyList<SlotDisponibilidadDto> Slots);

public record SlotDisponibilidadDto(
    DateOnly Fecha,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    string Estado,        // "DISPONIBLE" | "OCUPADO" | "BLOQUEADO" | "FUERA_HORARIO"
    string? Motivo);      // motivo del bloqueo si visible

// ===========================================================================
// Reservas
// ===========================================================================

public record ReservaDto(
    Guid Id,
    string Codigo,
    Guid ZonaComunId,
    string ZonaNombre,
    Guid PersonaId,
    string PersonaNombre,
    Guid UnidadPrivadaId,
    string UnidadNumero,
    DateOnly Fecha,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    EstadoReserva Estado,
    bool EsRecurrente,
    Guid? ReservaRecurrenteId,
    bool ReglamentoAceptado,
    string? MotivoCancelacion,
    DateTimeOffset? CanceladaAt,
    decimal? MontoPago,
    EstadoPagoReserva? EstadoPago,
    DateTimeOffset CreadoAt,
    DateTimeOffset? EntregadaAt = null,
    string? EntregaObservacion = null,
    DateTimeOffset? DevueltaAt = null,
    string? DevolucionObservacion = null);

public record CrearReservaRequest(
    Guid ZonaComunId,
    Guid PersonaId,
    Guid UnidadPrivadaId,
    DateOnly Fecha,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    bool ReglamentoAceptado);

public record CancelarReservaRequest(string Motivo);

public record ReprogramarReservaRequest(DateOnly Fecha, TimeOnly HoraInicio, TimeOnly HoraFin);

public record ResumenReservasDto(
    int Confirmadas,
    int Pendientes,
    int CanceladasUltimoMes,
    int CompletadasUltimoMes,
    decimal IngresosUltimoMes,
    decimal TasaCancelacion);

// ===========================================================================
// Bloqueos
// ===========================================================================

public record BloqueoDto(
    Guid Id,
    Guid ZonaComunId,
    string ZonaNombre,
    TipoBloqueoZona Tipo,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    TimeOnly? HoraInicio,
    TimeOnly? HoraFin,
    EtiquetaBloqueo Etiqueta,
    string? MotivoPersonalizado,
    bool VisibleParaResidentes,
    OrigenBloqueoZona Origen);

public record CrearBloqueoRequest(
    Guid ZonaComunId,
    TipoBloqueoZona Tipo,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    TimeOnly? HoraInicio,
    TimeOnly? HoraFin,
    EtiquetaBloqueo Etiqueta,
    string? MotivoPersonalizado,
    bool VisibleParaResidentes);

// ===========================================================================
// Vista portero (consumida por 2.12)
// ===========================================================================

public record ReservaDelDiaDto(
    string ZonaNombre,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    string UnidadNumero,
    string PersonaNombre,
    string Codigo);
