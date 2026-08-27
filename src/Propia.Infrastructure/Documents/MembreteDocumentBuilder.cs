using System.Globalization;
using System.Net;
using System.Text;
using Propia.Application.Documents;
using Propia.Domain.Entities;

namespace Propia.Infrastructure.Documents;

/// <summary>
/// Compone el HTML (standalone, estilos inline, @page A4) de un documento oficial con el
/// membrete de la copropiedad. El header/footer se derivan de la identidad del Tenant y de su
/// config de membrete (auto + campos). Este HTML es el que luego renderiza Chromium a PDF.
/// </summary>
public sealed class MembreteDocumentBuilder : IMembreteDocumentBuilder
{
    private const string AccentDefault = "#0E7A6E";
    private const string LegalDefault = "R&eacute;gimen de Propiedad Horizontal - Ley 675 de 2001";

    private static readonly string[] Meses =
    {
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"
    };

    public string Construir(Tenant t, MembreteDocContenido c)
    {
        var accent = SanitizarColor(t.MembreteColorAcento) ?? AccentDefault;
        var legal = Coalesce(t.MembreteLineaLegal, LegalDefault, encode: true);
        var contacto = ContactoFooter(t);
        var firmNombre = Enc(Primero(c.FirmanteNombre, t.MembreteFirmanteNombre));
        var firmCargo = Enc(Primero(c.FirmanteCargo, t.MembreteFirmanteCargo, "Administrador"));

        var ciudad = string.IsNullOrWhiteSpace(t.Ciudad) ? "" : Enc(t.Ciudad!.Trim());
        var fecha = c.Fecha.ToLocalTime();
        var fechaTxt = $"{(ciudad.Length > 0 ? ciudad + ", " : "")}{fecha.Day} de {Meses[fecha.Month - 1]} de {fecha.Year}";

        // Sub-linea de identidad: NIT + reglamento + direccion/ciudad/departamento.
        var idBits = new List<string>();
        if (!string.IsNullOrWhiteSpace(t.Nit))
        {
            var nit = Enc(t.Nit!.Trim());
            if (!string.IsNullOrWhiteSpace(t.DigitoVerificacion)) nit += "-" + Enc(t.DigitoVerificacion!.Trim());
            idBits.Add($"NIT <b>{nit}</b>");
        }
        if (!string.IsNullOrWhiteSpace(t.NumeroReglamentoPh))
            idBits.Add($"Reglamento PH <b>{Enc(t.NumeroReglamentoPh!.Trim())}</b>");

        var ubic = new List<string>();
        if (!string.IsNullOrWhiteSpace(t.Direccion)) ubic.Add(Enc(t.Direccion!.Trim()));
        var ciudadDep = new List<string>();
        if (!string.IsNullOrWhiteSpace(t.Ciudad)) ciudadDep.Add(Enc(t.Ciudad!.Trim()));
        if (!string.IsNullOrWhiteSpace(t.Departamento)) ciudadDep.Add(Enc(t.Departamento!.Trim()));
        if (ciudadDep.Count > 0) ubic.Add(string.Join(", ", ciudadDep));

        var subLinea = string.Join(" &nbsp;&middot;&nbsp; ", idBits);
        var subUbic = string.Join(" &mdash; ", ubic);

        // Logo: imagen si existe y esta habilitada; si no, recuadro con iniciales.
        var logoHtml = "";
        if (t.MembreteMostrarLogo)
        {
            if (!string.IsNullOrWhiteSpace(t.LogoUrl))
                logoHtml = $"<img class=\"m-logo-img\" src=\"{Enc(t.LogoUrl!.Trim())}\" alt=\"\" />";
            else
                logoHtml = $"<div class=\"m-logo\">{Iniciales(t.Nombre)}</div>";
        }

        // Destinatario (opcional).
        var destHtml = "";
        if (!string.IsNullOrWhiteSpace(c.DestinatarioNombre))
        {
            var linea = string.IsNullOrWhiteSpace(c.DestinatarioLinea) ? "" : $"{Enc(c.DestinatarioLinea!)}";
            destHtml = $"<div class=\"m-to\">Se&ntilde;ores<b>{Enc(c.DestinatarioNombre!)}</b>{linea}</div>";
        }

        var pageNum = t.MembreteMostrarNumeracion
            ? "<div class=\"pg\">P&aacute;gina 1</div>"
            : "";

        var sb = new StringBuilder(8192);
        sb.Append("<!doctype html><html lang=\"es\"><head><meta charset=\"utf-8\">");
        sb.Append("<style>");
        sb.Append(Css(accent));
        sb.Append("</style></head><body><div class=\"sheet\">");

        // ---- HEADER ----
        sb.Append("<div class=\"m-head\"><div class=\"m-id\">");
        sb.Append(logoHtml);
        sb.Append("<div class=\"m-id-txt\"><h2>").Append(Enc(t.Nombre)).Append("</h2>");
        if (subLinea.Length > 0) sb.Append("<div class=\"sub\">").Append(subLinea).Append("</div>");
        if (subUbic.Length > 0) sb.Append("<div class=\"sub\">").Append(subUbic).Append("</div>");
        sb.Append("</div></div>");
        sb.Append("<div class=\"m-doc\"><span class=\"m-badge\">").Append(Enc(c.TipoBadge)).Append("</span>");
        sb.Append("<div class=\"m-rad\"><small>").Append(Enc(c.RadicadoLabel)).Append("</small>")
          .Append(Enc(c.Radicado)).Append("</div>");
        sb.Append("<div class=\"m-date\">").Append(fechaTxt).Append("</div>");
        sb.Append("</div></div>");
        sb.Append("<div class=\"m-rule\"></div>");

        // ---- BODY ----
        sb.Append("<div class=\"m-body\">");
        sb.Append(destHtml);
        sb.Append("<div class=\"m-cuerpo\">").Append(c.CuerpoHtml ?? "").Append("</div>");
        if (firmNombre.Length > 0)
        {
            sb.Append("<div class=\"m-sign\"><div class=\"line\"></div>");
            sb.Append("<div class=\"nm\">").Append(firmNombre).Append("</div>");
            sb.Append("<div class=\"cg\">").Append(firmCargo);
            sb.Append(" &mdash; ").Append(Enc(t.Nombre)).Append("</div></div>");
        }
        sb.Append("</div>");

        // ---- FOOTER ----
        sb.Append("<div class=\"m-foot\"><div class=\"frule\"></div><div class=\"cols\">");
        sb.Append("<div class=\"legal\"><b>").Append(Enc(t.Nombre)).Append("</b><br>").Append(legal).Append("</div>");
        if (contacto.Length > 0)
            sb.Append("<div class=\"contact\"><b>Contacto</b><br>").Append(contacto).Append("</div>");
        sb.Append("<div class=\"op\"><div class=\"brandline\">Operado por A&amp;D GROUP &middot; <em>PROPIA</em></div>");
        sb.Append(pageNum).Append("</div>");
        sb.Append("</div></div>");

        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    private static string ContactoFooter(Tenant t)
    {
        if (!string.IsNullOrWhiteSpace(t.MembreteContactoFooter))
            return Enc(t.MembreteContactoFooter!.Trim());
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(t.TelefonoContacto)) parts.Add(Enc(t.TelefonoContacto!.Trim()));
        if (!string.IsNullOrWhiteSpace(t.EmailContacto)) parts.Add(Enc(t.EmailContacto!.Trim()));
        var linea1 = string.Join(" &middot; ", parts);
        var dir = new List<string>();
        if (!string.IsNullOrWhiteSpace(t.Direccion)) dir.Add(Enc(t.Direccion!.Trim()));
        if (!string.IsNullOrWhiteSpace(t.Ciudad)) dir.Add(Enc(t.Ciudad!.Trim()));
        var linea2 = string.Join(", ", dir);
        return string.Join("<br>", new[] { linea1, linea2 }.Where(s => s.Length > 0));
    }

    private static string Css(string accent) => @"
@page { size: A4; margin: 0; }
* { box-sizing: border-box; }
html, body { margin: 0; padding: 0; }
body { background: #e8ecf1; }
/* En pantalla: hoja A4 blanca centrada sobre lienzo gris. En print: la hoja ES la pagina. */
.sheet {
  width: 794px; max-width: 100%; margin: 26px auto; background: #fff;
  padding: 52px 60px 42px; border-radius: 3px; box-sizing: border-box;
  box-shadow: 0 8px 34px rgba(27,42,58,.17);
  display: flex; flex-direction: column; min-height: 1040px;
  font-family: Georgia, 'Times New Roman', serif; color: #1B2A3A;
  font-size: 13.5px; line-height: 1.68;
}
@media print {
  body { background: #fff; }
  .sheet { width: auto; max-width: none; min-height: auto; margin: 0;
    padding: 16mm 18mm; box-shadow: none; border-radius: 0; }
}
.m-head { display: flex; justify-content: space-between; align-items: flex-start; gap: 22px; padding-bottom: 14px; }
.m-id { display: flex; gap: 15px; align-items: flex-start; }
.m-logo, .m-logo-img { width: 56px; height: 56px; border-radius: 10px; flex: 0 0 56px; object-fit: contain; }
.m-logo { display: grid; place-items: center; font-family: Arial, sans-serif; font-weight: 700; font-size: 19px;
  letter-spacing: -.5px; color: ACCENT; background: ACCENT14; border: 1px solid ACCENT33; }
.m-id-txt h2 { margin: 0 0 3px; font-size: 19px; font-weight: 700; line-height: 1.15; letter-spacing: -.2px; }
.m-id-txt .sub { font-family: Arial, sans-serif; font-size: 10.8px; color: #63748A; line-height: 1.5; }
.m-id-txt .sub b { color: #1B2A3A; font-weight: 600; }
.m-doc { text-align: right; font-family: Arial, sans-serif; white-space: nowrap; }
.m-badge { display: inline-block; background: ACCENT; color: #fff; font-size: 9.5px; font-weight: 700;
  letter-spacing: .8px; text-transform: uppercase; padding: 5px 11px; border-radius: 6px; }
.m-rad { margin-top: 8px; font-size: 12px; font-weight: 700; }
.m-rad small { display: block; font-size: 9px; color: #63748A; font-weight: 600; letter-spacing: .5px; text-transform: uppercase; }
.m-date { margin-top: 6px; font-size: 11px; color: #63748A; }
.m-rule { height: 2.5px; background: ACCENT; border-radius: 2px; margin: 0 0 26px; }
.m-body { flex: 1 1 auto; font-size: 13.5px; line-height: 1.7; }
.m-to { margin-bottom: 18px; font-family: Arial, sans-serif; font-size: 11.5px; color: #63748A; }
.m-to b { display: block; color: #1B2A3A; font-size: 13.5px; font-weight: 600; font-family: Georgia, serif; }
.m-cuerpo p { margin: 0 0 12px; text-align: justify; }
.m-cuerpo ul, .m-cuerpo ol { margin: 0 0 12px; padding-left: 22px; }
.m-cuerpo table { border-collapse: collapse; width: 100%; margin: 0 0 12px; }
.m-cuerpo td, .m-cuerpo th { border: 1px solid #D8E0E8; padding: 6px 9px; }
.m-sign { margin-top: 32px; }
.m-sign .line { width: 220px; border-top: 1px solid #1B2A3A; margin-bottom: 6px; }
.m-sign .nm { font-weight: 700; font-size: 13px; }
.m-sign .cg { font-family: Arial, sans-serif; font-size: 11px; color: #63748A; }
.m-foot { margin-top: 34px; padding-top: 12px; font-family: Arial, sans-serif; page-break-inside: avoid; }
.m-foot .frule { height: 1px; background: #E4E9EF; margin-bottom: 11px; position: relative; }
.m-foot .frule::before { content: ''; position: absolute; left: 0; top: 0; width: 60px; height: 2.5px; background: ACCENT; border-radius: 2px; }
.m-foot .cols { display: flex; justify-content: space-between; gap: 18px; font-size: 10px; color: #63748A; line-height: 1.5; }
.m-foot .cols b { color: #1B2A3A; font-weight: 600; }
.m-foot .legal { max-width: 300px; }
.m-foot .op { text-align: right; }
.m-foot .op .brandline { color: #1B2A3A; font-weight: 600; }
.m-foot .op .brandline em { color: ACCENT; font-style: normal; }
.m-foot .op .pg { color: #63748A; margin-top: 3px; }
"
        .Replace("ACCENT14", Mezcla(accent, 0.12))
        .Replace("ACCENT33", Mezcla(accent, 0.33))
        .Replace("ACCENT", accent);

    // ---- helpers ----
    private static string Enc(string? s) => WebUtility.HtmlEncode(s ?? "").Trim();

    private static string Coalesce(string? val, string fallback, bool encode)
    {
        if (string.IsNullOrWhiteSpace(val)) return fallback;
        return encode ? Enc(val) : val!;
    }

    private static string? Primero(params string?[] vals)
        => vals.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string Iniciales(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return "?";
        var limpio = nombre.Trim();
        foreach (var pre in new[] { "Conjunto ", "Edificio ", "Unidad ", "Torre ", "Agrupacion " })
            if (limpio.StartsWith(pre, StringComparison.OrdinalIgnoreCase))
                limpio = limpio[pre.Length..];
        var palabras = limpio.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var ini = string.Concat(palabras.Take(2).Select(p => char.ToUpper(p[0], CultureInfo.InvariantCulture)));
        return WebUtility.HtmlEncode(ini.Length == 0 ? "?" : ini);
    }

    // Valida un color hex (#RGB o #RRGGBB). Devuelve null si no es valido.
    private static string? SanitizarColor(string? c)
    {
        if (string.IsNullOrWhiteSpace(c)) return null;
        c = c.Trim();
        if (c.Length is not (4 or 7) || c[0] != '#') return null;
        for (var i = 1; i < c.Length; i++)
            if (!Uri.IsHexDigit(c[i])) return null;
        return c;
    }

    // Mezcla un hex con blanco (t = 0..1 hacia el color). Devuelve rgb() para usar como fondo/borde suave.
    private static string Mezcla(string hex, double t)
    {
        var (r, g, b) = HexToRgb(hex);
        int mix(int ch) => (int)Math.Round(255 + (ch - 255) * t);
        return $"rgb({mix(r)},{mix(g)},{mix(b)})";
    }

    private static (int r, int g, int b) HexToRgb(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 3)
            hex = string.Concat(hex.Select(ch => new string(ch, 2)));
        return (
            Convert.ToInt32(hex.Substring(0, 2), 16),
            Convert.ToInt32(hex.Substring(2, 2), 16),
            Convert.ToInt32(hex.Substring(4, 2), 16));
    }
}
