using System.Net;
using System.Text.RegularExpressions;
using Propia.Application.Plantillas;
using Propia.Domain.Enums;

namespace Propia.Web.Components.Shared.Plantillas;

/// <summary>
/// Composicion del documento de una plantilla en la Web (vista previa, miniaturas, franjas del editor): encabezado +
/// cuerpo + pie, con los datos {grupo.campo} reemplazados por los de EJEMPLO del catalogo (el nombre de la copropiedad
/// es el real, {copropiedad.logo} la imagen real y {documento.datos} un ejemplo). Replica de componer(), resolver(),
/// mbDe() y mbSelTexto() en replica/js/mod-plantillas-doc.js.
/// </summary>
public static class PlantillaDoc
{
    private static readonly Regex Token = new(@"\{[a-z_]+\.[a-z_]+\}", RegexOptions.Compiled);

    public static string NombreTipo(TipoMembrete t) => t == TipoMembrete.Encabezado ? "Encabezado" : "Pie de página";
    public static string PluralTipo(TipoMembrete t) => t == TipoMembrete.Encabezado ? "Encabezados" : "Pies de página";
    public static string NingunoTipo(TipoMembrete t) => t == TipoMembrete.Encabezado ? "Sin encabezado" : "Sin pie de página";
    public static string IconoTipo(TipoMembrete t) => t == TipoMembrete.Encabezado ? "fi-rr-border-top" : "fi-rr-border-bottom";

    /// <summary>Documento de ejemplo de la vista previa de un encabezado / pie (replica MB_MUESTRA).</summary>
    public const string MuestraMembrete = "<p>{copropiedad.ciudad}, {fecha.hoy}</p><p>Señor(a) residente,</p><p>Este es un texto de ejemplo para ver cómo queda el documento con este encabezado y pie de página. El cuerpo real lo pone cada plantilla.</p><p>Atentamente,<br>{administrador.nombre}</p>";

    /// <summary>Contenido inicial de un encabezado / pie nuevo (replica MB_NUEVO). El color es del documento.</summary>
    public static string HtmlNuevo(TipoMembrete t) => t == TipoMembrete.Encabezado
        ? "<p><strong>{copropiedad.nombre}</strong><br><span style=\"font-size:12px;color:#5B6B7F\">NIT {copropiedad.nit}</span></p>"
        : "<p style=\"text-align:center;font-size:11px;color:#5B6B7F\">{copropiedad.direccion} · Tel. {copropiedad.telefono}</p>";

    // Colores de DATO de las opciones de las columnas ENCABEZADO / PIE (replica syncOpts), pintados como chip.
    public const string ColorPred = "#E3E8EF", ColorNinguno = "#F1F3F6", ColorFijo = "#D9CCFF";
    public static string ColorSel(string? sel)
        => string.IsNullOrEmpty(sel) || sel == MembreteEleccion.Predeterminado ? ColorPred : sel == MembreteEleccion.Ninguno ? ColorNinguno : ColorFijo;
    /// <summary>Estilo de chip del select de la tabla segun la eleccion (mismo formato que EstiloCat).</summary>
    public static string EstiloSel(string? sel)
    {
        var c = ColorSel(sel);
        return $"background:{c}; border-color:{c}; color:{PaletaChips.TextoSobre(c)};";
    }

    public static MembreteDto? Predeterminado(MembretesDto? mb, TipoMembrete t) => mb?.Items.FirstOrDefault(m => m.Tipo == t && m.Predeterminado);

    /// <summary>El membrete que usa una eleccion ("pred" | "ninguno" | Id) para el tipo t; null = ninguno.</summary>
    public static MembreteDto? De(MembretesDto? mb, string? sel, TipoMembrete t)
    {
        sel = string.IsNullOrEmpty(sel) ? MembreteEleccion.Predeterminado : sel;
        if (sel == MembreteEleccion.Ninguno) return null;
        return (sel != MembreteEleccion.Predeterminado && Guid.TryParse(sel, out var id) ? mb?.Items.FirstOrDefault(m => m.Id == id && m.Tipo == t) : null)
               ?? Predeterminado(mb, t);
    }

    /// <summary>Texto de una eleccion: "Predeterminado · Institucional", "Sin encabezado" o el nombre.</summary>
    public static string SelTexto(MembretesDto? mb, string? sel, TipoMembrete t)
    {
        sel = string.IsNullOrEmpty(sel) ? MembreteEleccion.Predeterminado : sel;
        if (sel == MembreteEleccion.Predeterminado) { var pd = Predeterminado(mb, t); return "Predeterminado" + (pd is null ? "" : " · " + pd.Nombre); }
        if (sel == MembreteEleccion.Ninguno) return NingunoTipo(t);
        return De(mb, sel, t)?.Nombre ?? "Predeterminado";
    }

    /// <summary>Reemplaza los datos por los valores de ejemplo de la categoria (los desconocidos quedan literales).
    /// conDatosDoc=false omite {documento.datos} (tipo, radicado y fecha): miniaturas y franjas del editor.</summary>
    public static string Resolver(string? html, string? categoria, string? copropiedadNombre, MembretesDto? mb, bool conDatosDoc = true)
    {
        var mapa = PlantillasCatalogo.TokensDe(categoria).ToDictionary(t => t.Token, t => WebUtility.HtmlEncode(t.Ejemplo), StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(copropiedadNombre)) mapa["{copropiedad.nombre}"] = WebUtility.HtmlEncode(copropiedadNombre);
        if (mb is not null)
        {
            mapa["{copropiedad.logo}"] = mb.LogoHtml;
            mapa["{documento.datos}"] = conDatosDoc ? mb.DocumentoDatosEjemplo : "";
        }
        return Token.Replace(html ?? "", m => mapa.TryGetValue(m.Value, out var v) ? v : m.Value);
    }

    /// <summary>Documento completo: encabezado + cuerpo + pie. crudo = cuerpo sin reemplazar (miniaturas de la
    /// galeria, que muestran los {datos}); el membrete siempre se ve como queda.</summary>
    public static string Componer(string? cuerpoHtml, string? categoria, string? encabezado, string? pie, string? copropiedadNombre, MembretesDto? mb, bool crudo = false)
    {
        var e = De(mb, encabezado, TipoMembrete.Encabezado);
        var f = De(mb, pie, TipoMembrete.Pie);
        var cuerpo = crudo ? cuerpoHtml ?? "" : Resolver(cuerpoHtml, categoria, copropiedadNombre, mb);
        return (e is null ? "" : "<div class=\"pld-doc-enc\">" + Resolver(e.Html, categoria, copropiedadNombre, mb, !crudo) + "</div>")
             + "<div class=\"pld-doc-body\">" + cuerpo + "</div>"
             + (f is null ? "" : "<div class=\"pld-doc-pie\">" + Resolver(f.Html, categoria, copropiedadNombre, mb, !crudo) + "</div>");
    }
}
