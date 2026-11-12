using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Propia.Domain.Entities;

namespace Propia.Infrastructure.Documents;

/// <summary>
/// Datos {grupo.campo} de los encabezados y pies de pagina (membretes) y semilla "Institucional" de cada
/// copropiedad. La semilla replica el membrete que tenian los documentos oficiales (identidad + linea de color
/// arriba; nombre, linea legal, contacto y "Operado por" abajo) y absorbe la config de membrete del Tenant
/// (color de acento, linea legal, contacto, mostrar logo) en el propio HTML, que la copropiedad edita despues.
/// Los colores van en style del HTML: son contenido del documento, no tokens de la interfaz.
/// </summary>
public static class MembreteTokens
{
    public const string AccentDefault = "#0E7A6E";
    private const string LegalDefault = "R&eacute;gimen de Propiedad Horizontal - Ley 675 de 2001";
    /// <summary>Lo pone el armador del documento: tipo, radicado y fecha del documento.</summary>
    public const string DocumentoDatos = "{documento.datos}";

    private static readonly Regex Token = new(@"\{([a-z_]+\.[a-z_]+)\}", RegexOptions.Compiled);
    private static readonly string[] Meses =
        { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };

    private static string Enc(string? s) => WebUtility.HtmlEncode((s ?? "").Trim());

    public static string Accent(Tenant t) => SanitizarColor(t.MembreteColorAcento) ?? AccentDefault;

    public static string FechaLarga(DateTimeOffset f) { var l = f.ToLocalTime(); return $"{l.Day} de {Meses[l.Month - 1]} de {l.Year}"; }

    public static string Nit(Tenant t)
        => string.IsNullOrWhiteSpace(t.Nit) ? "" : t.Nit!.Trim() + (string.IsNullOrWhiteSpace(t.DigitoVerificacion) ? "" : "-" + t.DigitoVerificacion!.Trim());

    /// <summary>Datos comunes ya en HTML seguro (texto codificado; el logo es una imagen).</summary>
    public static Dictionary<string, string> DatosCopropiedad(Tenant t, string? administrador)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["copropiedad.nombre"] = Enc(t.Nombre),
            ["copropiedad.nit"] = Enc(Nit(t)),
            ["copropiedad.direccion"] = Enc(t.Direccion),
            ["copropiedad.ciudad"] = Enc(t.Ciudad),
            ["copropiedad.telefono"] = Enc(t.TelefonoContacto),
            ["copropiedad.correo"] = Enc(t.EmailContacto),
            ["copropiedad.logo"] = LogoHtml(t),
            ["administrador.nombre"] = Enc(string.IsNullOrWhiteSpace(t.MembreteFirmanteNombre) ? administrador : t.MembreteFirmanteNombre),
            ["fecha.hoy"] = Enc(FechaLarga(DateTimeOffset.Now)),
        };

    /// <summary>Reemplaza los datos conocidos; los desconocidos quedan literales (ej. {documento.datos}).</summary>
    public static string Resolver(string? html, IReadOnlyDictionary<string, string> mapaHtml)
        => Token.Replace(html ?? "", m => mapaHtml.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);

    /// <summary>{copropiedad.logo}: la imagen del logo; sin logo, un recuadro con las iniciales en el color de acento.</summary>
    public static string LogoHtml(Tenant t)
    {
        if (!string.IsNullOrWhiteSpace(t.LogoUrl))
            return $"<img class=\"pld-logo\" src=\"{Enc(t.LogoUrl)}\" alt=\"\" style=\"width:56px;height:56px;object-fit:contain;border-radius:10px\" />";
        var a = Accent(t);
        return $"<span class=\"pld-logo\" style=\"display:inline-block;width:56px;height:56px;line-height:56px;text-align:center;border-radius:10px;"
             + $"font-family:Arial,sans-serif;font-weight:700;font-size:19px;color:{a};background:{Mezcla(a, 0.12)};border:1px solid {Mezcla(a, 0.33)}\">{Iniciales(t.Nombre)}</span>";
    }

    /// <summary>{documento.datos}: pastilla con el tipo de documento, radicado y fecha (alineado a la derecha).</summary>
    public static string DocumentoDatosHtml(string tipo, string radicadoLabel, string radicado, string fechaTxt, string accent)
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"text-align:right;font-family:Arial,sans-serif;white-space:nowrap\">");
        sb.Append($"<span style=\"display:inline-block;background:{accent};color:#ffffff;font-size:9.5px;font-weight:700;letter-spacing:.8px;text-transform:uppercase;padding:5px 11px;border-radius:6px\">{Enc(tipo)}</span>");
        if (!string.IsNullOrWhiteSpace(radicado))
            sb.Append($"<div style=\"margin-top:8px;font-size:12px;font-weight:700;color:#1B2A3A\"><span style=\"display:block;font-size:9px;color:#63748A;font-weight:600;letter-spacing:.5px;text-transform:uppercase\">{Enc(radicadoLabel)}</span>{Enc(radicado)}</div>");
        sb.Append($"<div style=\"margin-top:6px;font-size:11px;color:#63748A\">{Enc(fechaTxt)}</div></div>");
        return sb.ToString();
    }

    // ===================== Semilla "Institucional" =====================

    /// <summary>Encabezado institucional: logo (si la copropiedad lo mostraba) + identidad + datos del documento, y la linea de color.</summary>
    public static string EncabezadoInstitucional(Tenant t)
    {
        var a = Accent(t);
        var sub = "font-family:Arial,sans-serif;font-size:10.8px;line-height:1.5;color:#63748A";
        var idBits = new List<string>();
        if (!string.IsNullOrWhiteSpace(t.Nit)) idBits.Add("NIT <b style=\"color:#1B2A3A\">{copropiedad.nit}</b>");
        if (!string.IsNullOrWhiteSpace(t.NumeroReglamentoPh)) idBits.Add($"Reglamento PH <b style=\"color:#1B2A3A\">{Enc(t.NumeroReglamentoPh)}</b>");
        var ubic = new List<string>();
        if (!string.IsNullOrWhiteSpace(t.Direccion)) ubic.Add("{copropiedad.direccion}");
        var ciudadDep = new List<string>();
        if (!string.IsNullOrWhiteSpace(t.Ciudad)) ciudadDep.Add("{copropiedad.ciudad}");
        if (!string.IsNullOrWhiteSpace(t.Departamento)) ciudadDep.Add(Enc(t.Departamento));
        if (ciudadDep.Count > 0) ubic.Add(string.Join(", ", ciudadDep));

        var sb = new StringBuilder();
        sb.Append("<table style=\"width:100%;border-collapse:collapse\"><tbody><tr>");
        if (t.MembreteMostrarLogo) sb.Append("<td style=\"width:71px;vertical-align:top;padding:0 15px 0 0\">{copropiedad.logo}</td>");
        sb.Append("<td style=\"vertical-align:top;padding:0\">");
        sb.Append("<div style=\"font-family:Georgia,'Times New Roman',serif;font-size:19px;font-weight:700;line-height:1.15;color:#1B2A3A;margin:0 0 3px\">{copropiedad.nombre}</div>");
        if (idBits.Count > 0) sb.Append($"<div style=\"{sub}\">{string.Join(" &nbsp;&middot;&nbsp; ", idBits)}</div>");
        if (ubic.Count > 0) sb.Append($"<div style=\"{sub}\">{string.Join(" &mdash; ", ubic)}</div>");
        sb.Append("</td><td style=\"vertical-align:top;text-align:right;padding:0 0 0 22px\">").Append(DocumentoDatos).Append("</td>");
        sb.Append("</tr></tbody></table>");
        sb.Append($"<hr style=\"border:0;height:3px;background:{a};border-radius:2px;margin:14px 0 0\" />");
        return sb.ToString();
    }

    /// <summary>Pie institucional: nombre + linea legal | contacto | "Operado por A&amp;D GROUP · PROPIA".</summary>
    public static string PieInstitucional(Tenant t)
    {
        var a = Accent(t);
        var legal = string.IsNullOrWhiteSpace(t.MembreteLineaLegal) ? LegalDefault : Enc(t.MembreteLineaLegal);
        string contacto;
        if (!string.IsNullOrWhiteSpace(t.MembreteContactoFooter)) contacto = Enc(t.MembreteContactoFooter);
        else
        {
            var l1 = new List<string>();
            if (!string.IsNullOrWhiteSpace(t.TelefonoContacto)) l1.Add("{copropiedad.telefono}");
            if (!string.IsNullOrWhiteSpace(t.EmailContacto)) l1.Add("{copropiedad.correo}");
            var l2 = new List<string>();
            if (!string.IsNullOrWhiteSpace(t.Direccion)) l2.Add("{copropiedad.direccion}");
            if (!string.IsNullOrWhiteSpace(t.Ciudad)) l2.Add("{copropiedad.ciudad}");
            contacto = string.Join("<br />", new[] { string.Join(" &middot; ", l1), string.Join(", ", l2) }.Where(s => s.Length > 0));
        }
        var cell = "vertical-align:top;padding:0";
        var sb = new StringBuilder();
        sb.Append($"<hr style=\"border:0;border-top:1px solid #E4E9EF;margin:0 0 11px\" />");
        sb.Append("<table style=\"width:100%;border-collapse:collapse;font-family:Arial,sans-serif;font-size:10px;line-height:1.5;color:#63748A\"><tbody><tr>");
        sb.Append($"<td style=\"{cell};width:42%\"><b style=\"color:#1B2A3A\">{{copropiedad.nombre}}</b><br />{legal}</td>");
        sb.Append($"<td style=\"{cell} 18px\">{(contacto.Length > 0 ? "<b style=\"color:#1B2A3A\">Contacto</b><br />" + contacto : "")}</td>");
        sb.Append($"<td style=\"{cell};text-align:right\"><b style=\"color:#1B2A3A\">Operado por A&amp;D GROUP &middot; <span style=\"color:{a}\">PROPIA</span></b></td>");
        sb.Append("</tr></tbody></table>");
        return sb.ToString();
    }

    // ---- helpers ----
    private static string Iniciales(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return "?";
        var limpio = nombre.Trim();
        foreach (var pre in new[] { "Conjunto ", "Edificio ", "Unidad ", "Torre ", "Agrupacion " })
            if (limpio.StartsWith(pre, StringComparison.OrdinalIgnoreCase)) limpio = limpio[pre.Length..];
        var ini = string.Concat(limpio.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpper(p[0], CultureInfo.InvariantCulture)));
        return WebUtility.HtmlEncode(ini.Length == 0 ? "?" : ini);
    }

    // Valida un color hex (#RGB o #RRGGBB). Devuelve null si no es valido.
    private static string? SanitizarColor(string? c)
    {
        if (string.IsNullOrWhiteSpace(c)) return null;
        c = c.Trim();
        if (c.Length is not (4 or 7) || c[0] != '#') return null;
        for (var i = 1; i < c.Length; i++) if (!Uri.IsHexDigit(c[i])) return null;
        return c;
    }

    // Mezcla un hex con blanco (t = 0..1 hacia el color): fondo/borde suave.
    public static string Mezcla(string hex, double t)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 3) hex = string.Concat(hex.Select(ch => new string(ch, 2)));
        int Mix(int ch) => (int)Math.Round(255 + (ch - 255) * t);
        return $"rgb({Mix(Convert.ToInt32(hex[..2], 16))},{Mix(Convert.ToInt32(hex.Substring(2, 2), 16))},{Mix(Convert.ToInt32(hex.Substring(4, 2), 16))})";
    }
}
