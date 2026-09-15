namespace Propia.Web.Components.Shared;

/// <summary>
/// Paleta unica de colores para los chips de opcion (listas de campos) y las etiquetas de Tareas.
/// Son colores de DATO elegidos por el usuario (no colores de diseno del tema), por eso viven aqui como
/// valores y se pintan inline desde el dato: no aplica la regla D-01 (que es sobre hex de diseno en el
/// markup .razor). Se guarda el valor tal cual (igual que `TareaEtiqueta.Color`).
///
/// Fuente: los 8 colores que ya usaban las etiquetas de Tareas (`_colores` en TableroTareas). Se
/// centralizan aqui para no duplicarlos; TableroTareas y ConfigCamposEntidad/CamposDinamicosRegistro
/// los leen de aca.
/// </summary>
public static class PaletaChips
{
    /// <summary>Los 8 colores ofrecidos al elegir el color de una opcion. Orden estable (indice = identidad visual).</summary>
    public static readonly IReadOnlyList<string> Colores = new[]
    {
        "#6D4FE3", // morado (marca)
        "#2563EB", // azul
        "#1A8754", // verde
        "#F59E0B", // ambar
        "#EF4444", // rojo
        "#7C3AED", // violeta
        "#0EA5E9", // celeste
        "#EC4899", // rosa
    };

    /// <summary>
    /// Paleta PASTEL (10 colores) del prototipo de Unidades Privadas, para las opciones de los campos
    /// LISTA/Seleccion del gestor de campos. Se ofrece como CHOOSER (el color guardado es un valor libre;
    /// el chip lo pinta con TextoSobre para el contraste). Es SEPARADA de <see cref="Colores"/> (que sigue
    /// usando Tareas) para no cambiar las etiquetas de Tareas: aqui solo cambia el chooser del campo Lista.
    /// </summary>
    public static readonly IReadOnlyList<string> ColoresPasteles = new[]
    {
        "#AAD4FF", "#AEEBC7", "#FFE39B", "#FFC2A8", "#FFB9DA",
        "#D2C0FB", "#BEEAEA", "#C9E7A9", "#F3C6C6", "#D9D9D9",
    };

    /// <summary>Color de chip cuando la opcion no tiene color asignado (chip neutro).</summary>
    public const string Neutro = "#90A4B7";

    /// <summary>true si el string es uno de los colores de la paleta o el neutro (validacion basica).</summary>
    public static bool EsValido(string? color)
        => !string.IsNullOrWhiteSpace(color)
           && (string.Equals(color, Neutro, StringComparison.OrdinalIgnoreCase)
               || Colores.Any(c => string.Equals(c, color, StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// Color de TEXTO legible sobre un fondo dado (blanco sobre oscuro, tinta sobre claro), por
    /// luminancia relativa. Necesario porque el fondo es un valor de dato en runtime, no un token.
    /// </summary>
    public static string TextoSobre(string? colorHex)
    {
        var c = (colorHex ?? Neutro).TrimStart('#');
        if (c.Length != 6) return "#FFFFFF";
        try
        {
            var r = Convert.ToInt32(c.Substring(0, 2), 16);
            var g = Convert.ToInt32(c.Substring(2, 2), 16);
            var b = Convert.ToInt32(c.Substring(4, 2), 16);
            // Luminancia perceptual aproximada (0..255). Umbral ~150: claro -> texto oscuro.
            var lum = (0.299 * r + 0.587 * g + 0.114 * b);
            return lum > 150 ? "#1B2A3A" : "#FFFFFF";
        }
        catch { return "#FFFFFF"; }
    }
}
