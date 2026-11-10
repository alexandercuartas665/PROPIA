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

    // Citacion (estado Borrador -> Citada)
    Task<bool> EnviarCitacionAsync(Guid sesionId, EnviarCitacionRequest req, CancellationToken ct);

    // Poderes
    Task<SesionPoderDto> OtorgarPoderAsync(Guid sesionId, OtorgarPoderRequest req, CancellationToken ct);
    Task<bool> DecidirPoderAsync(Guid poderId, DecidirPoderRequest req, CancellationToken ct);

    // Sala (estado Citada -> EnCurso)
    Task<bool> AbrirSalaAsync(Guid sesionId, CancellationToken ct);
    Task<bool> CheckInParticipanteAsync(Guid sesionId, CheckInParticipanteRequest req, CancellationToken ct);

    // Votaciones (durante EnCurso)
    Task<VotacionDto> AbrirVotacionAsync(Guid sesionId, AbrirVotacionRequest req, CancellationToken ct);
    Task<bool> EmitirVotoAsync(Guid votacionId, EmitirVotoRequest req, CancellationToken ct);
    Task<VotacionDto> CerrarVotacionAsync(Guid votacionId, CerrarVotacionRequest req, CancellationToken ct);

    // Cierre (estado EnCurso -> Cerrada o QuorumFallido)
    Task<bool> CerrarSesionAsync(Guid sesionId, CerrarSesionRequest req, CancellationToken ct);

    // Acta (post-sesion)
    Task<ActaDto?> GenerarActaAsync(Guid sesionId, CancellationToken ct);
    Task<bool> FirmarActaAsync(Guid actaId, FirmarActaRequest req, CancellationToken ct);
    Task<bool> PublicarActaAsync(Guid actaId, PublicarActaRequest req, CancellationToken ct);

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
}
