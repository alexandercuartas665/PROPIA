using Propia.Domain.Enums;

namespace Propia.Application.Asambleas;

/// <summary>Modulo 2.8 Asambleas y Organos de Gobierno - spec v1.0 MVP.</summary>
public interface IAsambleaService
{
    // Bandeja + ficha
    Task<SesionBandejaDto> GetBandejaAsync(EstadoSesion? estado, TipoSesion? tipo, CancellationToken ct);
    Task<SesionDetalleDto?> GetSesionAsync(Guid id, CancellationToken ct);

    // Creacion y configuracion (estado Borrador)
    Task<SesionDetalleDto> CrearSesionAsync(CrearSesionRequest req, CancellationToken ct);
    Task<bool> ActualizarPuntoAsync(Guid puntoId, ActualizarPuntoRequest req, CancellationToken ct);
    Task<SesionDocumentoDto> AgregarDocumentoAsync(Guid sesionId, AgregarDocumentoRequest req, CancellationToken ct);
    Task<bool> EliminarDocumentoAsync(Guid documentoId, CancellationToken ct);
    /// <summary>Elimina una sesion en Borrador o Cancelada (con sus puntos, participantes, poderes y documentos).
    /// Las que ya siguen su ciclo (citada, en curso, cerrada, quorum fallido) no se borran.</summary>
    Task<bool> EliminarSesionAsync(Guid sesionId, CancellationToken ct);
    /// <summary>PUT MERGE: titulo/fecha/lugar/modalidad en Borrador; la videollamada (sala propia o enlace) hasta Citada.</summary>
    Task<bool> ActualizarSesionAsync(Guid sesionId, ActualizarSesionRequest req, CancellationToken ct);
    /// <summary>Orden del dia en Borrador (la citacion sale con ese orden).</summary>
    Task<bool> AgregarPuntoAsync(Guid sesionId, CrearPuntoRequest req, CancellationToken ct);
    Task<bool> EliminarPuntoAsync(Guid puntoId, CancellationToken ct);
    /// <summary>Borrador o Citada -> Cancelada, con motivo obligatorio.</summary>
    Task<bool> CancelarSesionAsync(Guid sesionId, CancelarSesionRequest req, CancellationToken ct);
    /// <summary>Quorum fallido -> nueva sesion (Borrador) el 3er dia habil a las 8 p. m. con el mismo orden del dia (art. 41).</summary>
    Task<Guid> ConvocarSegundaAsync(Guid sesionId, CancellationToken ct);

    // Citacion (estado Borrador -> Citada)
    Task<bool> EnviarCitacionAsync(Guid sesionId, EnviarCitacionRequest req, CancellationToken ct);

    // Poderes
    Task<SesionPoderDto> OtorgarPoderAsync(Guid sesionId, OtorgarPoderRequest req, CancellationToken ct);
    Task<bool> DecidirPoderAsync(Guid poderId, DecidirPoderRequest req, CancellationToken ct);

    // Sala (estado Citada -> EnCurso)
    Task<bool> AbrirSalaAsync(Guid sesionId, CancellationToken ct);
    Task<bool> CheckInParticipanteAsync(Guid sesionId, CheckInParticipanteRequest req, CancellationToken ct);
    /// <summary>Instalacion (Ley 675 art. 47): con quorum, presidente y secretario. Sin instalacion no se abren votaciones.</summary>
    Task<bool> InstalarAsync(Guid sesionId, InstalarSesionRequest req, CancellationToken ct);
    Task<bool> IniciarPuntoAsync(Guid puntoId, CancellationToken ct);
    // Transcripcion (ola 4): bitacora de voz y eventos del sistema, asociada al punto en curso.
    Task<IReadOnlyList<TranscripcionLineaDto>> GetTranscripcionAsync(Guid sesionId, DateTimeOffset? desde, CancellationToken ct);
    Task<bool> ActivarTranscripcionAsync(Guid sesionId, ActivarTranscripcionRequest req, CancellationToken ct);
    Task<TranscripcionLineaDto> AgregarTranscripcionAsync(Guid sesionId, AgregarTranscripcionRequest req, CancellationToken ct);
    Task<bool> MarcarTratadoAsync(Guid puntoId, CancellationToken ct);

    // Votaciones (durante EnCurso)
    Task<VotacionDto> AbrirVotacionAsync(Guid sesionId, AbrirVotacionRequest req, CancellationToken ct);
    Task<bool> EmitirVotoAsync(Guid votacionId, EmitirVotoRequest req, CancellationToken ct);
    /// <summary>Votos de la sala en bloque (presencial/mixta): cada voto con las reglas del voto individual.</summary>
    Task<VotarBloqueResultado> VotarBloqueAsync(Guid votacionId, VotarBloqueRequest req, CancellationToken ct);
    Task<VotacionDto> CerrarVotacionAsync(Guid votacionId, CerrarVotacionRequest req, CancellationToken ct);

    // Cierre (estado EnCurso -> Cerrada o QuorumFallido)
    Task<bool> CerrarSesionAsync(Guid sesionId, CerrarSesionRequest req, CancellationToken ct);

    // Acta (post-sesion)
    Task<ActaDto?> GenerarActaAsync(Guid sesionId, CancellationToken ct);
    // Firma (presidente + secretario) y publicacion: IAsambleaActaService (ola 3).

    // Configuracion
    Task<AsambleaConfigDto> GetConfigAsync(CancellationToken ct);
    Task<bool> ActualizarConfigAsync(ActualizarAsambleaConfigRequest req, CancellationToken ct);

    // Self-service del residente (Ola 1). La persona solo actua sobre unidades donde es Propietario
    // activo o apoderado con poder Aprobado en esa sesion (Ley 675: vota el propietario). Si no,
    // UnauthorizedAccessException. Las reglas del voto (presente, RN-09, opcion) son las del admin.
    /// <summary>Flujo publico por token (sin JWT): fija el tenant del enlace. False si no existe o no esta Activa.</summary>
    Task<bool> ActivarTenantPublicoAsync(Guid tenantId, CancellationToken ct);
    Task<SesionResidenteDto?> GetSesionParaResidenteAsync(Guid sesionId, Guid personaId, CancellationToken ct);
    Task<bool> MiCheckInAsync(Guid sesionId, Guid personaId, Guid unidadId, bool presente, CancellationToken ct);
    Task<bool> MiVotoAsync(Guid sesionId, Guid votacionId, Guid personaId, Guid unidadId, string opcion, CancellationToken ct);

    // Sala virtual (ola 5): acceso firmado, asistencia automatica por webhook y grabacion.
    SalaConfigDto ConfigSala();
    Task<SalaAccesoDto?> AccesoSalaAdminAsync(Guid sesionId, CancellationToken ct);
    Task<SalaAccesoDto?> AccesoSalaResidenteAsync(Guid sesionId, Guid personaId, CancellationToken ct);
    /// <summary>Evento validado del proveedor de video; devuelve cuando vence una salida pendiente (o null).</summary>
    Task<DateTimeOffset?> ProcesarEventoSalaAsync(Guid sesionId, VideoSalaEvento ev, CancellationToken ct);
    Task<int> ConfirmarSalidasPendientesAsync(Guid sesionId, CancellationToken ct);
    Task<bool> IniciarGrabacionAsync(Guid sesionId, CancellationToken ct);
    Task<bool> DetenerGrabacionAsync(Guid sesionId, CancellationToken ct);
}
