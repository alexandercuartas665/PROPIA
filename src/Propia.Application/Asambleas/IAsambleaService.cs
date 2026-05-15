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
    Task<bool> ActualizarConfigAsync(AsambleaConfigDto req, CancellationToken ct);
}
