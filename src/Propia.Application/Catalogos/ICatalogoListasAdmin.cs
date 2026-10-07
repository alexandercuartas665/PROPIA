namespace Propia.Application.Catalogos;

/// <summary>Resumen de una lista para el indice de la consola A&D.</summary>
public record CatalogoListaInfoDto(string Lista, string Nombre, bool ConLogica, int Opciones, int Activas);

/// <summary>Una opcion tal como la edita A&D (incluye inactivas, id y si es semilla).</summary>
public record CatalogoOpcionAdminDto(
    Guid Id, string Lista, string Clave, string Label, int Orden, string? Color,
    bool Activo, bool EsSemilla, string? Meta);

public record CrearOpcionCatalogoRequest(string Label, string? Color = null, string? Meta = null);
public record ActualizarOpcionCatalogoRequest(string Label, string? Color, bool Activo, string? Meta);

/// <summary>
/// Escritura del catalogo global desde la consola A&D (solo SuperAdmin). Toda mutacion invalida el
/// cache del lector <see cref="ICatalogoListas"/> en el proceso. Las claves NO se editan: las semilla
/// conservan su clave (e:&lt;Enum&gt; / texto) y las nuevas nacen con una string-clave derivada del label.
/// </summary>
public interface ICatalogoListasAdmin
{
    /// <summary>Indice de listas (las conocidas + cualquiera presente en la tabla), con conteos.</summary>
    Task<IReadOnlyList<CatalogoListaInfoDto>> ListasAsync(CancellationToken ct = default);

    /// <summary>Todas las opciones de una lista (incluidas inactivas), en orden.</summary>
    Task<IReadOnlyList<CatalogoOpcionAdminDto>> OpcionesAsync(string lista, CancellationToken ct = default);

    /// <summary>Crea una opcion NUEVA (EsSemilla=false) con clave derivada del label (unica en la lista).</summary>
    Task<CatalogoOpcionAdminDto> CrearAsync(string lista, CrearOpcionCatalogoRequest req, CancellationToken ct = default);

    /// <summary>Edita label/color/activo/meta de una opcion (no toca la clave ni el orden).</summary>
    Task<CatalogoOpcionAdminDto?> ActualizarAsync(Guid id, ActualizarOpcionCatalogoRequest req, CancellationToken ct = default);

    /// <summary>Fija el orden de la lista segun la secuencia de ids recibida.</summary>
    Task ReordenarAsync(string lista, IReadOnlyList<Guid> ordenIds, CancellationToken ct = default);

    /// <summary>
    /// Re-siembra las opciones de fabrica que FALTEN en TODAS las listas conocidas (idempotente: no pisa
    /// lo que A&D edito) e invalida el cache del lector. Devuelve cuantas opciones se insertaron. Util
    /// para restaurar una opcion base borrada o registrar listas nuevas sin reiniciar el proceso.
    /// </summary>
    Task<int> ResembrarAsync(CancellationToken ct = default);
}
