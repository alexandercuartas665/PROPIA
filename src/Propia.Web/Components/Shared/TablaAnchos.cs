using System.Globalization;
using System.Text;

namespace Propia.Web.Components.Shared;

/// <summary>
/// Anchos por defecto de las columnas de vista tabla (grids por fila). Regla (Alex, 2026-11-23): por defecto el
/// TITULO de cada columna se ve completo; el ancho de la pista nunca es menor que el titulo + el espacio fijo de la
/// cabecera (padding, chevron de opciones, handle de ancho, flecha de orden). Solo aplica al ancho por defecto: un
/// ancho que el usuario fijo arrastrando el borde se respeta tal cual.
/// </summary>
public static class TablaAnchos
{
    // Ancho (px) de cada caracter del titulo: Lexend 11.5px / 700 / letter-spacing .4px, en mayusculas (canon del
    // header). Medido con canvas en el navegador (2026-11-23); contra el DOM el error es < 1px por titulo.
    private static readonly Dictionary<char, double> Ancho = new()
    {
        ['A'] = 8.38, ['B'] = 8.46, ['C'] = 8.38, ['D'] = 9.28, ['E'] = 7.82, ['F'] = 7.51, ['G'] = 9.39,
        ['H'] = 9.36, ['I'] = 6.78, ['J'] = 8.05, ['K'] = 8.90, ['L'] = 7.40, ['M'] = 10.50, ['N'] = 9.68,
        ['O'] = 9.74, ['P'] = 8.01, ['Q'] = 9.74, ['R'] = 8.47, ['S'] = 7.69, ['T'] = 7.93, ['U'] = 9.20,
        ['V'] = 8.59, ['W'] = 11.82, ['X'] = 8.51, ['Y'] = 8.32, ['Z'] = 8.25,
        ['0'] = 8.16, ['1'] = 6.85, ['2'] = 6.70, ['3'] = 7.20, ['4'] = 8.01, ['5'] = 7.25, ['6'] = 7.06,
        ['7'] = 6.76, ['8'] = 7.29, ['9'] = 7.04,
        [' '] = 4.08, ['.'] = 3.36, [','] = 3.70, [':'] = 3.36, [';'] = 3.75, ['-'] = 5.55, ['_'] = 8.30,
        ['/'] = 7.24, ['('] = 4.88, [')'] = 4.88, ['#'] = 8.84, ['%'] = 11.53, ['&'] = 9.32, ['+'] = 7.27,
        ['*'] = 5.46, ['?'] = 6.56, ['!'] = 3.95, ['\''] = 3.30, ['"'] = 5.89,
    };
    private const double AnchoDefecto = 8.5;

    /// <summary>Espacio de la celda de cabecera fuera del texto (medido 34-39px + flecha de orden).</summary>
    public const int ExtraCabecera = 48;

    /// <summary>Ancho en px del titulo tal como lo pinta el header (mayusculas; tildes = letra base).</summary>
    public static int Titulo(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return 0;
        double w = 0;
        foreach (var ch in label.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            w += Ancho.TryGetValue(ch, out var a) ? a : AnchoDefecto;
        }
        return (int)Math.Ceiling(w) + 1;
    }

    /// <summary>Ancho minimo de la columna para que el titulo se vea completo.</summary>
    public static int Piso(string? label, int extra = ExtraCabecera) => Titulo(label) + extra;

    /// <summary>
    /// Aplica el piso del titulo a una pista de grid por defecto: "Npx" -> max(N, piso)px; "minmax(Apx,B)" ->
    /// minmax(max(A, piso)px,B); cualquier otra ("1fr", "1.2fr") -> minmax(pisopx,X).
    /// </summary>
    public static string ConTitulo(string pista, string? label, int extra = ExtraCabecera)
    {
        var piso = Piso(label, extra);
        var p = pista.Trim();
        if (p.EndsWith("px", StringComparison.Ordinal) && int.TryParse(p[..^2], out var px))
            return Math.Max(px, piso) + "px";
        if (p.StartsWith("minmax(", StringComparison.Ordinal))
        {
            var coma = p.IndexOf(',');
            if (coma > 7)
            {
                var min = p[7..coma].Trim();
                var resto = p[(coma + 1)..];
                if (min.EndsWith("px", StringComparison.Ordinal) && int.TryParse(min[..^2], out var m) && m >= piso) return p;
                return $"minmax({piso}px,{resto}";
            }
        }
        return $"minmax({piso}px,{p})";
    }

    /// <summary>Minimo en px de una pista ("Npx" o "minmax(Apx,..)"); 0 si no tiene minimo fijo.</summary>
    public static int MinimoPx(string pista)
    {
        var p = pista.Trim();
        if (p.EndsWith("px", StringComparison.Ordinal) && int.TryParse(p[..^2], out var px)) return px;
        if (p.StartsWith("minmax(", StringComparison.Ordinal))
        {
            var coma = p.IndexOf(',');
            var min = coma > 7 ? p[7..coma].Trim() : "";
            if (min.EndsWith("px", StringComparison.Ordinal) && int.TryParse(min[..^2], out var m)) return m;
        }
        return 0;
    }
}
