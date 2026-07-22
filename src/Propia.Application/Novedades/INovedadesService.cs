using Propia.Domain.Enums;

namespace Propia.Application.Novedades;

/// <summary>
/// Muro de novedades reutilizable. Cualquier entidad (zona comun, equipo/activo, y lo que
/// se agregue al enum) puede tener su muro con publicaciones, comentarios y likes.
/// </summary>
public interface INovedadesService
{
    Task<IReadOnlyList<NovedadDto>> ListarAsync(TipoEntidadNovedad tipo, Guid entidadId, Guid? personaId, CancellationToken ct);
    Task<NovedadDto?> PublicarAsync(TipoEntidadNovedad tipo, Guid entidadId, PublicarNovedadRequest req, Guid? personaId, CancellationToken ct);
    Task<bool> EliminarAsync(Guid novedadId, CancellationToken ct);
    Task<NovedadComentarioDto?> ComentarAsync(Guid novedadId, ComentarNovedadRequest req, Guid? personaId, CancellationToken ct);
    Task<int> ToggleLikeAsync(Guid novedadId, Guid? personaId, CancellationToken ct);
}
