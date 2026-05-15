using Propia.Domain.Enums;

namespace Propia.Application.Pqrsd;

/// <summary>Modulo 2.9 PQRSD y Convivencia - spec v1.0 MVP.</summary>
public interface IPqrsdService
{
    // Categorias y plazos
    Task<IReadOnlyList<PqrsdCategoriaDto>> ListarCategoriasAsync(CancellationToken ct);
    Task<PqrsdCategoriaDto> CrearCategoriaAsync(CrearCategoriaRequest req, CancellationToken ct);
    Task<bool> ActualizarCategoriaAsync(Guid id, ActualizarCategoriaRequest req, CancellationToken ct);
    Task<bool> EliminarCategoriaAsync(Guid id, CancellationToken ct);
    Task<int> RestablecerCategoriasBaseAsync(CancellationToken ct);

    Task<IReadOnlyList<PqrsdPlazoDto>> ListarPlazosAsync(CancellationToken ct);
    Task<bool> ActualizarPlazoAsync(TipoPqrsd tipo, ActualizarPlazoRequest req, CancellationToken ct);

    // Bandeja + ficha
    Task<PqrsdBandejaDto> GetBandejaAsync(EstadoPqrsd? estado, TipoPqrsd? tipo, Guid? categoriaId, string? query, CancellationToken ct);
    Task<PqrsdExpedienteDetalleDto?> GetExpedienteAsync(Guid id, CancellationToken ct);

    // Radicacion (residente o admin)
    Task<PqrsdExpedienteDetalleDto> RadicarAsync(RadicarPqrsdRequest req, CancellationToken ct);

    // Vista del residente
    Task<IReadOnlyList<PqrsdBandejaItemDto>> ListarMisPqrsdAsync(CancellationToken ct);

    // Ciclo de gestion
    Task<bool> TomarExpedienteAsync(Guid id, TomarExpedienteRequest req, CancellationToken ct);
    Task<bool> ResponderAsync(Guid id, ResponderExpedienteRequest req, CancellationToken ct);
    Task<bool> ManifestarInconformidadAsync(Guid id, ManifestarInconformidadRequest req, CancellationToken ct);
    Task<bool> CerrarDefinitivoAsync(Guid id, CerrarDefinitivoRequest req, CancellationToken ct);

    // Tutela
    Task<bool> ActivarTutelaAsync(Guid id, ActivarTutelaRequest req, CancellationToken ct);

    // Comite de Convivencia (solo Denuncia)
    Task<PqrsdComiteSesionDto> EscalarAComiteAsync(Guid expedienteId, EscalarAComiteRequest req, CancellationToken ct);
    Task<bool> RegistrarSesionComiteAsync(Guid sesionId, RegistrarSesionComiteRequest req, CancellationToken ct);
}
