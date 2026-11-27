namespace Propia.Web.Components.Shared;

/// <summary>
/// Opciones listas para <see cref="ListaBuscable"/> en las vistas tabla que reemplazan el select nativo por la
/// lista con busqueda y chips pastel (piloto Tareas replicado a Unidades / Equipos / Zonas, Alex 2026-11-26).
/// Los valores guardados no cambian: "Si"/"No" para los campos Booleano, "true"/"false" para Reservable, el
/// texto K para los campos de lista.
/// </summary>
public static class OpcionesListaBuscable
{
    /// <summary>Campo propio Booleano (guarda "Si" / "No"; "" = sin valor).</summary>
    public static readonly IReadOnlyList<ListaBuscable.Opcion> SiNoCampo = new[]
    {
        new ListaBuscable.Opcion("", "-"),
        new ListaBuscable.Opcion("Si", "S\u00ed"),
        new ListaBuscable.Opcion("No", "No"),
    };

    /// <summary>Booleano del sistema guardado como "true" / "false" (p.ej. Reservable).</summary>
    public static readonly IReadOnlyList<ListaBuscable.Opcion> SiNoBool = new[]
    {
        new ListaBuscable.Opcion("false", "No"),
        new ListaBuscable.Opcion("true", "S\u00ed"),
    };

    /// <summary>Texto visible de un valor Si/No guardado.</summary>
    public static string? TextoSiNo(string? v) => v switch
    {
        "Si" or "true" => "S\u00ed",
        "No" or "false" => "No",
        _ => null,
    };

    /// <summary>
    /// Opciones de un campo de lista (Opciones en JSON o legado por linea). Las ocultas no se ofrecen salvo la
    /// que ya tiene el registro. Primera opcion "Sin valor" para poder vaciar.
    /// </summary>
    public static IReadOnlyList<ListaBuscable.Opcion> DeCampo(string? opcionesRaw, string? actual = null)
        => new[] { new ListaBuscable.Opcion("", "Sin valor") }
            .Concat(OpcionCampo.Parse(opcionesRaw)
                .Where(o => !o.Oculta || string.Equals(o.K, actual, StringComparison.OrdinalIgnoreCase))
                .Select(o => new ListaBuscable.Opcion(o.K, o.K, o.Color ?? PaletaChips.Neutro)))
            .ToList();

    /// <summary>Color de la opcion elegida de un campo de lista (null = sin color).</summary>
    public static string? ColorDe(string? opcionesRaw, string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null
           : OpcionCampo.Parse(opcionesRaw).FirstOrDefault(o => string.Equals(o.K, valor, StringComparison.OrdinalIgnoreCase))?.Color;
}
