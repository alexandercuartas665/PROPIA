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

    /// <summary>Un color de la paleta unica, con su nombre visible.</summary>
    public sealed record ColorPaleta(string Nombre, string Hex);

    /// <summary>
    /// Paleta UNICA de 10 colores (piloto Tareas, Alex 2026-11-26): la misma en etapas, prioridades, etiquetas
    /// y opciones de campos de lista. Se pinta como chip pastel (<see cref="ChipEstilo"/>). Las otras paletas
    /// quedan para los modulos que aun no migran.
    /// </summary>
    public static readonly IReadOnlyList<ColorPaleta> Unica = new[]
    {
        new ColorPaleta("Violeta", "#6D4FE3"),
        new ColorPaleta("Azul", "#2563EB"),
        new ColorPaleta("Celeste", "#0EA5E9"),
        new ColorPaleta("Verde", "#1A8754"),
        new ColorPaleta("Lima", "#65A30D"),
        new ColorPaleta("Ámbar", "#D97706"),
        new ColorPaleta("Naranja", "#EA580C"),
        new ColorPaleta("Rojo", "#DC2626"),
        new ColorPaleta("Rosa", "#DB2777"),
        new ColorPaleta("Gris", "#64748B"),
    };

    /// <summary>Solo los hex de <see cref="Unica"/>, en el mismo orden.</summary>
    public static readonly IReadOnlyList<string> UnicaHex = Unica.Select(c => c.Hex).ToArray();

    private const string GrisUnica = "#64748B";
    private const string VioletaUnica = "#6D4FE3";

    /// <summary>
    /// Color de la paleta unica mas parecido a un color guardado (por tono; los grises y poco saturados van
    /// a Gris). Solo para MOSTRAR: el dato guardado no cambia. Un valor que no es hex (ej. un token css) cae
    /// en Violeta, que es el color de marca.
    /// </summary>
    public static string Cercano(string? color)
    {
        if (!TryHsl(color, out var h, out var s, out _)) return string.IsNullOrWhiteSpace(color) ? GrisUnica : VioletaUnica;
        foreach (var c in Unica)
            if (string.Equals(c.Hex, color!.Trim(), StringComparison.OrdinalIgnoreCase)) return c.Hex;
        if (s < 0.3) return GrisUnica;
        string mejor = VioletaUnica; var dMin = double.MaxValue;
        foreach (var c in Unica)
        {
            if (c.Hex == GrisUnica || !TryHsl(c.Hex, out var hc, out _, out _)) continue;
            var d = Math.Abs(h - hc); d = Math.Min(d, 360 - d);
            if (d < dMin) { dMin = d; mejor = c.Hex; }
        }
        return mejor;
    }

    /// <summary>
    /// Estilo inline de chip pastel para un color: fondo claro + texto del color (mezclado con los tokens,
    /// asi funciona igual en modo claro y oscuro). El color se normaliza a la paleta unica.
    /// </summary>
    public static string ChipEstilo(string? color)
    {
        var c = Cercano(color);
        return "background:color-mix(in srgb, " + c + " 15%, var(--propia-bg-card));"
             + "color:color-mix(in srgb, " + c + " 80%, var(--propia-text-h1));"
             + "border-color:color-mix(in srgb, " + c + " 28%, var(--propia-bg-card))";
    }

    private static bool TryHsl(string? color, out double h, out double s, out double l)
    {
        h = s = l = 0;
        var c = (color ?? "").Trim().TrimStart('#');
        if (c.Length != 6) return false;
        try
        {
            var r = Convert.ToInt32(c.Substring(0, 2), 16) / 255.0;
            var g = Convert.ToInt32(c.Substring(2, 2), 16) / 255.0;
            var b = Convert.ToInt32(c.Substring(4, 2), 16) / 255.0;
            var max = Math.Max(r, Math.Max(g, b)); var min = Math.Min(r, Math.Min(g, b)); var d = max - min;
            l = (max + min) / 2;
            s = d == 0 ? 0 : d / (1 - Math.Abs(2 * l - 1));
            if (d == 0) h = 0;
            else if (max == r) h = 60 * (((g - b) / d) % 6);
            else if (max == g) h = 60 * (((b - r) / d) + 2);
            else h = 60 * (((r - g) / d) + 4);
            if (h < 0) h += 360;
            return true;
        }
        catch { return false; }
    }

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
