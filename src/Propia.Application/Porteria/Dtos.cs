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
    DateTimeOffset CreadoAt,
    string? TipoVisitanteClave = null);

public record CrearAutorizacionRequest(
    Guid UnidadPrivadaId,
    string NombreVisitante,
    string? DocumentoVisitante,
    TipoVisitante TipoVisitante,
    DateTimeOffset VigenciaInicio,
    DateTimeOffset VigenciaFin,
    string? NotaPortero,
    int UsosMaximos = 1,
    OrigenAutorizacion Origen = OrigenAutorizacion.PortalResidente,
    string? TipoVisitanteClave = null);

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
    Guid GuardaPersonaId,
    IReadOnlyList<PorteriaCampoValorDto>? CamposValores = null,
    string? TipoVisitanteClave = null);

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
    bool AvisoTratamientoConfirmado,
    IReadOnlyList<PorteriaCampoValorDto>? CamposValores = null,
    string? TipoVisitanteClave = null);

// ===========================================================================
// Campos personalizados del modulo (estilo CUBOT.travels)
// ===========================================================================

public record PorteriaCampoDto(
    Guid Id, string Label, int Orden,
    TipoCampoTablero Tipo = TipoCampoTablero.Texto,
    string? Opciones = null,
    bool MostrarEnFiltro = false,
    int Columna = 1,
    string? Descripcion = null);

public record GuardarPorteriaCampoRequest(
    string Label,
    TipoCampoTablero Tipo = TipoCampoTablero.Texto,
    string? Opciones = null,
    bool MostrarEnFiltro = false,
    int Columna = 1,
    string? Descripcion = null);

public record PorteriaCampoValorDto(Guid CampoId, string? Valor);

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
    Guid? GuardaEntregaPersonaId,
    string? EntregadoDoc = null,
    int Recordatorios = 0,
    DateTimeOffset? UltimoRecordatorioAt = null);

public record RegistrarCorrespondenciaRequest(
    Guid UnidadPrivadaId,
    TipoCorrespondencia Tipo,
    string? Remitente,
    string? Descripcion);

/// <summary>Entrega con constancia: quien retira (obligatorio) y su documento (opcional).</summary>
public record EntregarCorrespondenciaRequest(string? EntregadoA, string? EntregadoDoc = null);
public record DevolverCorrespondenciaRequest(string MotivoDevolucion);

// ===========================================================================
// Novedades
// ===========================================================================

public record NovedadDto(
    Guid Id,
    Guid TurnoId,
    Guid GuardaPersonaId,
    TipoNovedadPorteria Tipo,
    string Descripcion,
    DestinoNovedad? DestinoTipo,
    Guid? DestinoId,
    string? DestinoNombre,
    bool GeneraTarea,
    Guid? TareaId,
    DateTimeOffset CreadoAt,
    Guid? IntervencionId = null,
    /// <summary>A quien se aviso al registrarla (solo en la respuesta del alta): "administracion", "vigilancia", "el residente de la Unidad 101".</summary>
    IReadOnlyList<string>? Avisados = null,
    /// <summary>Que se genero: "Mantenimiento MT-0001" o "Tareas #12" (solo en la respuesta del alta).</summary>
    string? TareaGenerada = null);

/// <summary>Alerta de seguridad (panico): novedad tipo Seguridad con aviso inmediato a administracion y vigilancia.</summary>
public record AlertaSeguridadRequest(string Descripcion, DestinoNovedad? DestinoTipo = null, Guid? DestinoId = null, bool GeneraTarea = false);

public record CrearNovedadRequest(
    string Descripcion,
    bool GeneraTarea,
    TipoNovedadPorteria Tipo = TipoNovedadPorteria.General,
    DestinoNovedad? DestinoTipo = null,
    Guid? DestinoId = null);

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
