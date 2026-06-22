using Propia.Domain.Enums;

namespace Propia.Application.Porteria;

/// <summary>
/// Modulo 2.12 Porteria y Control de Acceso - servicio de aplicacion (spec v1.0 MVP).
///
/// Alcance MVP:
///  - Turnos: abrir/cerrar con RN-01 (un solo activo por guarda+punto).
///  - Visitas: ingreso/egreso, reconocimiento de visitantes frecuentes,
///    aviso de tratamiento de datos (RN-09).
///  - Autorizaciones previas + codigos numericos (RN-04, RN-05, RN-03).
///  - Vehiculos: catalogo + registro inmutable ingreso/egreso (RN-10).
///  - Correspondencia: ciclo Recibido -> Notificado -> Entregado | Devuelto
///    con semaforo backend (RN-13) sin almacenar contenido (RN-06).
///  - Novedades por turno (sin generacion tarea automatica - opcional).
///  - Panel monitoreo admin + configuracion singleton.
///
/// Diferido a Fase 2/3:
///  - Modo Portero como rol especifico (requiere extension de 2.5).
///  - Autoregistro QR fijo (solicitud_ingreso).
///  - Webhooks LPR / biometrico (extension hardware).
///  - Notificaciones reales via T.2 (WhatsApp/Email/Push).
///  - Adjuntos de novedades y correspondencia (requiere IBlobStorage).
///  - Job nocturno de purga (RN-08).
///  - Generacion automatica de tarea 2.10 desde novedad (hook listo).
/// </summary>
public interface IPorteriaService
{
    // ----- Turno -----
    Task<TurnoDto?> GetTurnoActivoAsync(Guid guardaPersonaId, string puntoAcceso, CancellationToken ct);
    Task<TurnoDto> AbrirTurnoAsync(AbrirTurnoRequest req, CancellationToken ct);
    Task<TurnoDto> CerrarTurnoAsync(Guid turnoId, CerrarTurnoRequest req, CancellationToken ct);
    Task<ResumenTurnoDto> GetResumenTurnoAsync(Guid turnoId, CancellationToken ct);
    Task<IReadOnlyList<TurnoDto>> ListarTurnosAsync(DateOnly? desde, DateOnly? hasta, CancellationToken ct);

    // ----- Visitantes frecuentes -----
    Task<IReadOnlyList<VisitanteFrecuenteDto>> BuscarVisitantesFrecuentesAsync(string? query, CancellationToken ct);

    // ----- Autorizaciones previas + codigo -----
    Task<IReadOnlyList<AutorizacionPreviaDto>> ListarAutorizacionesAsync(EstadoAutorizacion? estado, Guid? unidadId, CancellationToken ct);
    Task<AutorizacionPreviaDto?> GetAutorizacionAsync(Guid id, CancellationToken ct);
    Task<AutorizacionPreviaDto> CrearAutorizacionAsync(CrearAutorizacionRequest req, CancellationToken ct);
    Task<bool> RevocarAutorizacionAsync(Guid id, CancellationToken ct);
    Task<CodigoValidadoDto> ValidarCodigoAsync(ValidarCodigoRequest req, CancellationToken ct);

    // ----- Registro visitas -----
    Task<RegistroVisitaDto> RegistrarVisitaAsync(Guid turnoId, RegistrarVisitaRequest req, CancellationToken ct);
    Task<IReadOnlyList<RegistroVisitaDto>> ListarVisitasAsync(Guid? turnoId, DateOnly? fecha, CancellationToken ct);

    // ----- Vehiculos -----
    Task<IReadOnlyList<VehiculoAutorizadoDto>> ListarVehiculosAutorizadosAsync(Guid? unidadId, CancellationToken ct);
    Task<VehiculoAutorizadoDto> CrearVehiculoAutorizadoAsync(CrearVehiculoRequest req, CancellationToken ct);
    Task<bool> ActualizarVehiculoAutorizadoAsync(Guid id, ActualizarVehiculoRequest req, CancellationToken ct);
    Task<bool> DesactivarVehiculoAsync(Guid id, CancellationToken ct);

    Task<RegistroVehiculoDto> RegistrarVehiculoAsync(Guid turnoId, RegistrarVehiculoRequest req, CancellationToken ct);
    Task<IReadOnlyList<RegistroVehiculoDto>> ListarRegistrosVehiculoAsync(Guid? turnoId, DateOnly? fecha, CancellationToken ct);

    // ----- Correspondencia -----
    Task<IReadOnlyList<CorrespondenciaDto>> ListarCorrespondenciaAsync(EstadoCorrespondencia? estado, Guid? unidadId, CancellationToken ct);
    Task<CorrespondenciaDto> RegistrarCorrespondenciaAsync(Guid turnoId, RegistrarCorrespondenciaRequest req, CancellationToken ct);
    Task<bool> EntregarCorrespondenciaAsync(Guid id, EntregarCorrespondenciaRequest req, CancellationToken ct);
    Task<bool> DevolverCorrespondenciaAsync(Guid id, DevolverCorrespondenciaRequest req, CancellationToken ct);

    // ----- Novedades -----
    Task<NovedadDto> CrearNovedadAsync(Guid turnoId, CrearNovedadRequest req, CancellationToken ct);
    Task<IReadOnlyList<NovedadDto>> ListarNovedadesAsync(Guid? turnoId, DateOnly? fecha, CancellationToken ct);

    // ----- Panel monitoreo admin -----
    Task<PanelMonitoreoDto> GetPanelMonitoreoAsync(CancellationToken ct);

    // ----- Configuracion -----
    Task<ConfiguracionDto> GetConfiguracionAsync(CancellationToken ct);
    Task<ConfiguracionDto> ActualizarConfiguracionAsync(ActualizarConfiguracionRequest req, CancellationToken ct);

    // ----- Campos personalizados (estilo CUBOT.travels) -----
    Task<IReadOnlyList<PorteriaCampoDto>> ListarCamposAsync(CancellationToken ct);
    Task<PorteriaCampoDto> CrearCampoAsync(GuardarPorteriaCampoRequest req, CancellationToken ct);
    Task<bool> ActualizarCampoAsync(Guid campoId, GuardarPorteriaCampoRequest req, CancellationToken ct);
    Task<bool> EliminarCampoAsync(Guid campoId, CancellationToken ct);
}
