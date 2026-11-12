using System.Net;
using System.Text;
using Propia.Application.Documents;
using Propia.Domain.Entities;

namespace Propia.Infrastructure.Documents;

/// <summary>
/// Compone el HTML (standalone, estilos inline, @page A4) de un documento oficial. Es el MARCO del documento:
/// hoja A4, datos del documento ({documento.datos}: tipo, radicado y fecha), destinatario, cuerpo, firma y
/// numeracion. El encabezado y el pie de pagina son los membretes de la copropiedad (Configuracion > Plantillas
/// de documentos > Encabezados y pies), que llegan ya resueltos. Si el encabezado no trae {documento.datos}, los
/// datos del documento van debajo de el, a la derecha. Este HTML es el que luego renderiza Chromium a PDF.
/// </summary>
public sealed class MembreteDocumentBuilder : IMembreteDocumentBuilder
{
    public string Construir(Tenant t, MembreteDocContenido c, string? encabezadoHtml, string? pieHtml)
    {
        var accent = MembreteTokens.Accent(t);
        var firmNombre = Enc(Primero(c.FirmanteNombre, t.MembreteFirmanteNombre));
        var firmCargo = Enc(Primero(c.FirmanteCargo, t.MembreteFirmanteCargo, "Administrador"));

        var ciudad = string.IsNullOrWhiteSpace(t.Ciudad) ? "" : t.Ciudad!.Trim();
        var fechaTxt = (ciudad.Length > 0 ? ciudad + ", " : "") + MembreteTokens.FechaLarga(c.Fecha);
        var datosDoc = MembreteTokens.DocumentoDatosHtml(c.TipoBadge, c.RadicadoLabel, c.Radicado, fechaTxt, accent);

        // Destinatario (opcional).
        var destHtml = "";
        if (!string.IsNullOrWhiteSpace(c.DestinatarioNombre))
        {
            var linea = string.IsNullOrWhiteSpace(c.DestinatarioLinea) ? "" : $"{Enc(c.DestinatarioLinea!)}";
            destHtml = $"<div class=\"m-to\">Se&ntilde;ores<b>{Enc(c.DestinatarioNombre!)}</b>{linea}</div>";
        }

        var sb = new StringBuilder(8192);
        sb.Append("<!doctype html><html lang=\"es\"><head><meta charset=\"utf-8\">");
        sb.Append("<style>").Append(Css).Append("</style></head><body><div class=\"sheet\">");

        // ---- ENCABEZADO (membrete) + datos del documento ----
        var enc = encabezadoHtml ?? "";
        if (enc.Contains(MembreteTokens.DocumentoDatos, StringComparison.Ordinal))
            sb.Append("<div class=\"m-enc\">").Append(enc.Replace(MembreteTokens.DocumentoDatos, datosDoc, StringComparison.Ordinal)).Append("</div>");
        else
        {
            if (enc.Length > 0) sb.Append("<div class=\"m-enc\">").Append(enc).Append("</div>");
            sb.Append("<div class=\"m-docdatos\">").Append(datosDoc).Append("</div>");
        }

        // ---- CUERPO ----
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

        // ---- PIE (membrete) + numeracion ----
        if (!string.IsNullOrEmpty(pieHtml) || t.MembreteMostrarNumeracion)
        {
            sb.Append("<div class=\"m-pie\">").Append(pieHtml ?? "");
            if (t.MembreteMostrarNumeracion) sb.Append("<div class=\"pg\">P&aacute;gina 1</div>");
            sb.Append("</div>");
        }

        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    private const string Css = @"
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
.m-enc { margin: 0 0 26px; }
.m-enc p { margin: 0 0 6px; }
.m-enc table, .m-pie table { border-collapse: collapse; }
.m-docdatos { margin: 0 0 22px; }
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
.m-pie { margin-top: 34px; padding-top: 12px; page-break-inside: avoid; }
.m-pie p { margin: 0 0 4px; }
.m-pie .pg { margin-top: 4px; text-align: right; font-family: Arial, sans-serif; font-size: 10px; color: #63748A; }
";

    // ---- helpers ----
    private static string Enc(string? s) => WebUtility.HtmlEncode(s ?? "").Trim();

    private static string? Primero(params string?[] vals)
        => vals.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}
