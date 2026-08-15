using Microsoft.AspNetCore.Components;

namespace Propia.Web.Components.Shared;

/// <summary>
/// Set de iconos SVG (estilo line, Lucide/Feather) para las etiquetas del Directorio.
/// En BD se guarda solo la CLAVE (ej. "home"); aqui se resuelve al SVG para pintar chips y el picker.
/// Reemplaza los emojis para que las etiquetas se vean como el resto del sistema.
/// </summary>
public static class EtiquetaIconos
{
    /// <summary>(Clave, Etiqueta legible, contenido SVG interno).</summary>
    public static readonly (string Key, string Label, string Inner)[] Catalogo = new (string, string, string)[]
    {
        ("tag",        "Etiqueta",   @"<path d=""M20.59 13.41l-7.17 7.17a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z""/><line x1=""7"" y1=""7"" x2=""7.01"" y2=""7""/>"),
        ("home",       "Casa",       @"<path d=""M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z""/><polyline points=""9 22 9 12 15 12 15 22""/>"),
        ("key",        "Llave",      @"<circle cx=""7.5"" cy=""15.5"" r=""5.5""/><path d=""M21 2l-9.6 9.6""/><path d=""M15.5 7.5l3 3L22 7l-3-3""/>"),
        ("user",       "Persona",    @"<path d=""M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2""/><circle cx=""12"" cy=""7"" r=""4""/>"),
        ("users",      "Familia",    @"<path d=""M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2""/><circle cx=""9"" cy=""7"" r=""4""/><path d=""M23 21v-2a4 4 0 0 0-3-3.87""/><path d=""M16 3.13a4 4 0 0 1 0 7.75""/>"),
        ("briefcase",  "Trabajo",    @"<rect x=""2"" y=""7"" width=""20"" height=""14"" rx=""2"" ry=""2""/><path d=""M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16""/>"),
        ("hard-hat",   "Obra",       @"<path d=""M2 18a1 1 0 0 0 1 1h18a1 1 0 0 0 1-1v-2a1 1 0 0 0-1-1H3a1 1 0 0 0-1 1z""/><path d=""M10 10V5a1 1 0 0 1 1-1h2a1 1 0 0 1 1 1v5""/><path d=""M4 15v-3a6 6 0 0 1 6-6""/><path d=""M14 6a6 6 0 0 1 6 6v3""/>"),
        ("wrench",     "Herramienta",@"<path d=""M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z""/>"),
        ("paintbrush", "Aseo",       @"<path d=""M9.06 11.9l8.07-8.06a2.85 2.85 0 1 1 4.03 4.03l-8.06 8.08""/><path d=""M7.07 14.94c-1.66 0-3 1.35-3 3.02 0 1.33-2.5 1.52-2 2.02 1.08 1.1 2.49 2.02 4 2.02 2.2 0 4-1.8 4-4.04a3.01 3.01 0 0 0-3-3.02z""/>"),
        ("package",    "Paquete",    @"<line x1=""16.5"" y1=""9.4"" x2=""7.5"" y2=""4.21""/><path d=""M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z""/><polyline points=""3.27 6.96 12 12.01 20.73 6.96""/><line x1=""12"" y1=""22.08"" x2=""12"" y2=""12""/>"),
        ("file-text",  "Documento",  @"<path d=""M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z""/><polyline points=""14 2 14 8 20 8""/><line x1=""16"" y1=""13"" x2=""8"" y2=""13""/><line x1=""16"" y1=""17"" x2=""8"" y2=""17""/>"),
        ("shield",     "Seguridad",  @"<path d=""M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z""/>"),
        ("truck",      "Transporte", @"<rect x=""1"" y=""3"" width=""15"" height=""13""/><polygon points=""16 8 20 8 23 11 23 16 16 16 16 8""/><circle cx=""5.5"" cy=""18.5"" r=""2.5""/><circle cx=""18.5"" cy=""18.5"" r=""2.5""/>"),
        ("building",   "Empresa",    @"<rect x=""4"" y=""2"" width=""16"" height=""20"" rx=""2""/><path d=""M9 22v-4h6v4""/><path d=""M8 6h.01M16 6h.01M12 6h.01M12 10h.01M12 14h.01M16 10h.01M16 14h.01M8 10h.01M8 14h.01""/>"),
        ("car",        "Vehiculo",   @"<path d=""M14 16H9m10 0h3v-3.15a1 1 0 0 0-.84-.99L16 11l-2.7-3.6a1 1 0 0 0-.8-.4H5.24a2 2 0 0 0-1.8 1.1l-.8 1.63A6 6 0 0 0 2 12.42V16h2""/><circle cx=""6.5"" cy=""16.5"" r=""2.5""/><circle cx=""16.5"" cy=""16.5"" r=""2.5""/>"),
        ("award",      "Consejo",    @"<circle cx=""12"" cy=""8"" r=""7""/><polyline points=""8.21 13.89 7 23 12 20 17 23 15.79 13.88""/>"),
        ("phone",      "Contacto",   @"<path d=""M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z""/>"),
        ("bell",       "Alerta",     @"<path d=""M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9""/><path d=""M13.73 21a2 2 0 0 1-3.46 0""/>"),
        ("star",       "Destacado",  @"<polygon points=""12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2""/>"),
        ("heart",      "Favorito",   @"<path d=""M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 1 0-7.78 7.78L12 21.23l8.84-8.84a5.5 5.5 0 0 0 0-7.78z""/>"),
    };

    private static readonly Dictionary<string, string> Map = Catalogo.ToDictionary(x => x.Key, x => x.Inner);

    /// <summary>Devuelve el SVG (MarkupString) de una clave; si no existe o es null usa el icono generico "tag".</summary>
    public static MarkupString Svg(string? key, int size = 14)
    {
        var inner = (!string.IsNullOrWhiteSpace(key) && Map.TryGetValue(key!, out var found)) ? found : Map["tag"];
        return new MarkupString(
            $"<svg width=\"{size}\" height=\"{size}\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" style=\"vertical-align:-2px;flex:0 0 auto;\">{inner}</svg>");
    }
}
