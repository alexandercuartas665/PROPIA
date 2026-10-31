namespace Propia.Application.Catalogos;

/// <summary>
/// Una opcion EFECTIVA de una lista para la copropiedad actual (lo que el modulo ofrece HOY): la copia por
/// tenant (<see cref="Propia.Domain.Entities.ListaOpcion"/>) ya compuesta (base del catalogo global + ediciones
/// y opciones propias de la copropiedad).
/// </summary>
public record OpcionEfectivaDto(string Clave, string Label, int Orden, string? Color, bool Activo, bool EsBase);

/// <summary>
/// Opciones efectivas de una lista del sistema para la copropiedad ACTIVA (Ola 4, modelo por-tenant). Lee la
/// copia del tenant (NO el catalogo global en vivo): asi A&D editar el catalogo no se propaga hasta un
/// re-sembrar por-tenant (ver <see cref="IListasTenant"/>). Asegura la materializacion perezosa de la lista.
/// </summary>
public interface IOpcionesEfectivas
{
    /// <summary>Opciones de la lista para el tenant actual, en orden. Por defecto solo las activas.</summary>
    Task<IReadOnlyList<OpcionEfectivaDto>> OpcionesAsync(
        string lista, bool incluirOcultas = false, CancellationToken ct = default);
}
