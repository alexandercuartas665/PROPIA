using Ganss.Xss;

namespace Propia.Infrastructure.Common;

/// <summary>
/// S-19: saneamiento de HTML enriquecido capturado por el usuario (cuerpos de comunicados y de
/// respuestas PQRSD) antes de persistirlo. Elimina <c>script</c>, manejadores <c>on*</c>, iframes,
/// URLs javascript: y demas vectores de XSS almacenado, conservando el formato seguro (negritas,
/// listas, enlaces http/https, imagenes, colores/estilos en linea). Se sanea al GUARDAR para que
/// todo punto de render reciba contenido ya limpio.
/// </summary>
public static class HtmlSanitization
{
    // HtmlSanitizer.Sanitize es seguro para uso concurrente mientras no se mute la config; usamos
    // una unica instancia con la lista blanca por defecto (formato comun; sin script/on*/iframe).
    private static readonly HtmlSanitizer _sanitizer = new();

    /// <summary>Devuelve el HTML saneado. Null/espacios se devuelven tal cual.</summary>
    public static string? Clean(string? html)
        => string.IsNullOrWhiteSpace(html) ? html : _sanitizer.Sanitize(html);
}
