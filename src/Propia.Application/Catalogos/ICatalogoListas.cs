namespace Propia.Application.Catalogos;

/// <summary>
/// Una opcion resuelta de una lista del catalogo global (lo que la UI/logica consume).
/// </summary>
public record CatalogoOpcionDto(
    string Clave, string Label, int Orden, string? Color, bool Activo, bool EsSemilla, string? Meta);

/// <summary>
/// Lector del catalogo global de listas (fuente de verdad de "fabrica" editable desde la consola A&D).
/// Solo LECTURA con cache; la escritura la hace la consola A&D (Ola 2) y llama <see cref="InvalidarCache"/>.
/// El override por copropiedad (ocultar/renombrar/agregar) NO vive aqui: se compone encima en cada
/// modulo (Ola 3+), este servicio entrega la capa GLOBAL.
/// </summary>
public interface ICatalogoListas
{
    /// <summary>Opciones de una lista, ordenadas. Por defecto solo las activas.</summary>
    Task<IReadOnlyList<CatalogoOpcionDto>> OpcionesAsync(
        string lista, bool incluirInactivas = false, CancellationToken ct = default);

    /// <summary>Label de una clave concreta (null si no existe).</summary>
    Task<string?> LabelAsync(string lista, string clave, CancellationToken ct = default);

    /// <summary>Invalida el cache (una lista o todo). La consola A&D la llama tras escribir.</summary>
    void InvalidarCache(string? lista = null);
}
