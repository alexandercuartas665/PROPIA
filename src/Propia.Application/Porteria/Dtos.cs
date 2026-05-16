using Propia.Domain.Enums;

namespace Propia.Application.Porteria;

// ===========================================================================
// Turno
// ===========================================================================

public record TurnoDto(
    Guid Id,
    Guid GuardaPersonaId,
    string GuardaNombre,
    string PuntoAcceso,
    EstadoTurnoPorteria Estado,
    DateTimeOffset FechaApertura,
    DateTimeOffset? FechaCierre,
    string? NotaCierre,
    int TotalIngresos,
    int TotalEgresos,
    int TotalPaquetes,
    int TotalNovedades);

public record AbrirTurnoRequest(Guid GuardaPersonaId, string PuntoAcceso);
public record CerrarTurnoRequest(string? NotaCierre);

public record ResumenTurnoDto(
    int Ingresos,
    int Egresos,
    int VisitantesActuales,
    int VehiculosActuales,
    int PaquetesRecibidos,
    int PaquetesEntregados,
    int PaquetesPendientes,
    int Novedades);

// ===========================================================================
// Visitante frecuente
// ===========================================================================

public record VisitanteFrecuenteDto(
    Guid Id,
    string Nombre,
    string? Documento,
    TipoDocumentoVisitante? TipoDocumento,
    DateTimeOffset? UltimoIngresoAt,
    int TotalVisitas);

// ===========================================================================
// Autorizacion previa + codigo
// ===========================================================================

public record AutorizacionPreviaDto(
    Guid Id,
    Guid UnidadPrivadaId,
    string UnidadNumero,
    OrigenAutorizacion Origen,
    string NombreVisitante,
    string? DocumentoVisitante,
    TipoVisitante TipoVisitante,
    DateTimeOffset VigenciaInicio,
    DateTimeOffset VigenciaFin,
    string? NotaPortero,
    EstadoAutorizacion Estado,
    int UsosMaximos,
    int UsosRealizados,
    string? CodigoNumerico,
    DateTimeOffset CreadoAt);

public record CrearAutorizacionRequest(
    Guid UnidadPrivadaId,
    string NombreVisitante,
    string? DocumentoVisitante,
    TipoVisitante TipoVisitante,
    DateTimeOffset VigenciaInicio,
    DateTimeOffset VigenciaFin,
    string? NotaPortero,
    int UsosMaximos = 1,
    OrigenAutorizacion Origen = OrigenAutorizacion.PortalResidente);

public record ValidarCodigoRequest(string Codigo);

public record CodigoValidadoDto(
    bool Valido,
    string? Motivo,
    AutorizacionPreviaDto? Autorizacion);

// ===========================================================================
// Registro de visita
// ===========================================================================

public record RegistroVisitaDto(
    Guid Id,
    Guid TurnoId,
    TipoEventoAccesoPorteria TipoEvento,
    TipoVisitante TipoVisitante,
    string Nombre,
    string? Documento,
    DestinoVisita? DestinoTipo,
    Guid? DestinoId,
    string? DestinoNombre,
    Guid? AutorizacionId,
    string? Observacion,
    DateTimeOffset Timestamp,
    Guid GuardaPersonaId);

public record RegistrarVisitaRequest(
    TipoEventoAccesoPorteria TipoEvento,
    TipoVisitante TipoVisitante,
    string Nombre,
    string? Documento,
    TipoDocumentoVisitante? TipoDocumento,
    DestinoVisita? DestinoTipo,
    Guid? DestinoId,
    Guid? AutorizacionId,
    Guid? VisitanteFrecuenteId,
    string? Observacion,
    bool AvisoTratamientoConfirmado);

// ===========================================================================
// Vehiculos
// ===========================================================================

public record VehiculoAutorizadoDto(
    Guid Id,
    Guid UnidadPrivadaId,
    string UnidadNumero,
    string Placa,
    TipoVehiculo Tipo,
    string? Marca,
    string? Modelo,
    string? Color,
    string? ParqueaderoAsignado,
    bool Activo);

public record CrearVehiculoRequest(
    Guid UnidadPrivadaId,
    string Placa,
    TipoVehiculo Tipo,
    string? Marca,
    string? Modelo,
    string? Color,
    string? ParqueaderoAsignado);

public record ActualizarVehiculoRequest(
    string Placa,
    TipoVehiculo Tipo,
    string? Marca,
    string? Modelo,
    string? Color,
    string? ParqueaderoAsignado);

public record RegistroVehiculoDto(
    Guid Id,
    Guid TurnoId,
    TipoEventoAccesoPorteria TipoEvento,
    string Placa,
    Guid? VehiculoAutorizadoId,
    Guid? UnidadPrivadaId,
    string? UnidadNumero,
    bool EsVisita,
    string? Conductor,
    OrigenRegistroVehiculo OrigenRegistro,
    DateTimeOffset Timestamp);

public record RegistrarVehiculoRequest(
    TipoEventoAccesoPorteria TipoEvento,
    string Placa,
    string? Conductor,
    string? Observacion);

// ===========================================================================
// Correspondencia
// ===========================================================================

public record CorrespondenciaDto(
    Guid Id,
    Guid TurnoId,
    Guid UnidadPrivadaId,
    string UnidadNumero,
    TipoCorrespondencia Tipo,
    string? Remitente,
    string? Descripcion,
    EstadoCorrespondencia Estado,
    SemaforoPaquete Semaforo,
    DateTimeOffset RecibidoAt,
    DateTimeOffset? NotificadoAt,
    DateTimeOffset? EntregadoAt,
    string? EntregadoA,
    DateTimeOffset? DevueltoAt,
    string? MotivoDevolucion,
    Guid GuardaRecibePersonaId,
    Guid? GuardaEntregaPersonaId);

public record RegistrarCorrespondenciaRequest(
    Guid UnidadPrivadaId,
    TipoCorrespondencia Tipo,
    string? Remitente,
    string? Descripcion);

public record EntregarCorrespondenciaRequest(string? EntregadoA);
public record DevolverCorrespondenciaRequest(string MotivoDevolucion);

// ===========================================================================
// Novedades
// ===========================================================================

public record NovedadDto(
    Guid Id,
    Guid TurnoId,
    Guid GuardaPersonaId,
    string Descripcion,
    bool GeneraTarea,
    Guid? TareaId,
    DateTimeOffset CreadoAt);

public record CrearNovedadRequest(string Descripcion, bool GeneraTarea);

// ===========================================================================
// Panel monitoreo + configuracion
// ===========================================================================

public record PanelMonitoreoDto(
    TurnoDto? TurnoActivo,
    ResumenTurnoDto Hoy,
    IReadOnlyList<CorrespondenciaDto> PaquetesPendientes,
    IReadOnlyList<EventoRecienteDto> UltimosEventos);

public record EventoRecienteDto(
    string Tipo,        // "VISITA" | "VEHICULO" | "PAQUETE" | "NOVEDAD"
    string Descripcion,
    DateTimeOffset At);

public record ConfiguracionDto(
    bool DestinoVisitanteObligatorio,
    CanalNotificacionPaquete CanalNotificacionPaquetes,
    int UmbralPaqueteAmarilloMin,
    int UmbralPaqueteRojoMin,
    bool AutoregistroQrActivo,
    bool GeneraTareaDesdeNovedad,
    int RetencionDatosVisitantesDias);

public record ActualizarConfiguracionRequest(
    bool DestinoVisitanteObligatorio,
    CanalNotificacionPaquete CanalNotificacionPaquetes,
    int UmbralPaqueteAmarilloMin,
    int UmbralPaqueteRojoMin,
    bool AutoregistroQrActivo,
    bool GeneraTareaDesdeNovedad,
    int RetencionDatosVisitantesDias);
