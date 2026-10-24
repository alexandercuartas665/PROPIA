using Propia.Domain.Enums;

namespace Propia.Web.Components.Shared.Folders;

/// <summary>Helpers de presentacion compartidos por los componentes de Folders (Documentos v2).</summary>
public static class FolderUi
{
    /// <summary>Clase CSS de la CATEGORIA del tipo (Basicos / Fecha / Seleccion / Relacion / Otros); colores por tokens.</summary>
    public static string CatClase(TipoCampoTablero t) => t switch
    {
        TipoCampoTablero.Texto or TipoCampoTablero.AreaTexto or TipoCampoTablero.Numero or TipoCampoTablero.Moneda or TipoCampoTablero.Porcentaje or TipoCampoTablero.ModuloContributivo => "cat-basico",
        TipoCampoTablero.Fecha or TipoCampoTablero.FechaHora or TipoCampoTablero.Hora => "cat-fecha",
        TipoCampoTablero.Seleccion or TipoCampoTablero.Booleano => "cat-sel",
        TipoCampoTablero.Usuario or TipoCampoTablero.Directorio => "cat-rel",
        _ => "cat-otro"
    };

    public static string TipoCorto(TipoCampoTablero t) => t switch
    {
        TipoCampoTablero.Texto => "Texto",
        TipoCampoTablero.AreaTexto => "Texto largo",
        TipoCampoTablero.Numero => "Número",
        TipoCampoTablero.Moneda => "Moneda",
        TipoCampoTablero.Porcentaje => "Porcentaje",
        TipoCampoTablero.Fecha => "Fecha",
        TipoCampoTablero.FechaHora => "Fecha y hora",
        TipoCampoTablero.Hora => "Hora",
        TipoCampoTablero.Seleccion => "Selección",
        TipoCampoTablero.Booleano => "Sí / No",
        TipoCampoTablero.Telefono => "Teléfono",
        TipoCampoTablero.Email => "Email",
        TipoCampoTablero.Url => "URL",
        TipoCampoTablero.Usuario => "Usuario",
        TipoCampoTablero.Directorio => "Directorio",
        TipoCampoTablero.Formula => "Fórmula",
        _ => t.ToString()
    };

    public static string TipoIcon(TipoCampoTablero t) => t switch
    {
        TipoCampoTablero.Texto => "fi-rr-text",
        TipoCampoTablero.AreaTexto => "fi-rr-align-left",
        TipoCampoTablero.Numero => "fi-rr-calculator",
        TipoCampoTablero.Moneda => "fi-rr-usd-circle",
        TipoCampoTablero.Porcentaje => "fi-rr-percentage",
        TipoCampoTablero.Fecha => "fi-rr-calendar",
        TipoCampoTablero.FechaHora or TipoCampoTablero.Hora => "fi-rr-clock",
        TipoCampoTablero.Seleccion => "fi-rr-caret-circle-down",
        TipoCampoTablero.Booleano => "fi-rr-checkbox",
        TipoCampoTablero.Telefono => "fi-rr-phone-call",
        TipoCampoTablero.Email => "fi-rr-envelope",
        TipoCampoTablero.Url => "fi-rr-link-alt",
        TipoCampoTablero.Usuario => "fi-rr-user",
        TipoCampoTablero.Directorio => "fi-rr-address-book",
        TipoCampoTablero.Formula => "fi-rr-calculator-money",
        _ => "fi-rr-text"
    };

    /// <summary>Etiqueta y clase del badge de extension de una miniatura (PDF / DOC / XLS / IMG / otro).</summary>
    public static (string Label, string Clase) ExtBadge(string? nombreArchivo)
    {
        var ext = (System.IO.Path.GetExtension(nombreArchivo ?? "") ?? "").TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "pdf" => ("PDF", "ext-pdf"),
            "doc" or "docx" or "odt" or "rtf" => ("DOC", "ext-doc"),
            "xls" or "xlsx" or "xlsm" or "csv" or "tsv" or "ods" => ("XLS", "ext-xls"),
            "ppt" or "pptx" => ("PPT", "ext-doc"),
            "png" or "jpg" or "jpeg" or "gif" or "webp" or "bmp" => ("IMG", "ext-img"),
            "" => ("DOC", "ext-otro"),
            _ => (ext.ToUpperInvariant().Length > 4 ? ext.ToUpperInvariant()[..4] : ext.ToUpperInvariant(), "ext-otro")
        };
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "-";
        string[] units = { "B", "KB", "MB", "GB" };
        var i = 0; double size = bytes;
        while (size >= 1024 && i < units.Length - 1) { size /= 1024; i++; }
        return $"{size:0.#} {units[i]}";
    }
}
