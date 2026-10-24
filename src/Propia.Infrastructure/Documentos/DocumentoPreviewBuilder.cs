using System.Text;
using Propia.Application.Documentos;

namespace Propia.Infrastructure.Documentos;

/// <summary>
/// Construye la vista previa renderizable de un archivo (imagen/pdf como data URI, texto plano,
/// Excel y Word convertidos a HTML simple). Compartido por la vista Folders; sin estado.
/// </summary>
public static class DocumentoPreviewBuilder
{
    public static DocumentoPreviewDto Build(byte[] bytes, string nombre, string mime)
    {
        var ext = (Path.GetExtension(nombre) ?? "").TrimStart('.').ToLowerInvariant();
        mime = string.IsNullOrWhiteSpace(mime) ? "application/octet-stream" : mime;

        bool esImagen = mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || ext is "png" or "jpg" or "jpeg" or "gif" or "webp" or "bmp";
        bool esPdf = mime.Contains("pdf", StringComparison.OrdinalIgnoreCase) || ext == "pdf";
        bool esTexto = mime.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            || ext is "txt" or "csv" or "tsv" or "md" or "log" or "json" or "xml" or "yml" or "yaml" or "ini";
        bool esExcel = ext is "xlsx" or "xlsm" or "xltx" || mime.Contains("spreadsheetml", StringComparison.OrdinalIgnoreCase);
        bool esWord = ext == "docx" || mime.Contains("wordprocessingml", StringComparison.OrdinalIgnoreCase);

        if (esImagen)
            return new DocumentoPreviewDto("image", nombre, mime, DataUri: $"data:{mime};base64,{Convert.ToBase64String(bytes)}");
        if (esPdf)
            return new DocumentoPreviewDto("pdf", nombre, "application/pdf", DataUri: $"data:application/pdf;base64,{Convert.ToBase64String(bytes)}");
        if (esTexto)
            return new DocumentoPreviewDto("text", nombre, mime, Texto: Truncar(DecodificarTexto(bytes), 300_000));
        if (esExcel)
        {
            try { return new DocumentoPreviewDto("html", nombre, mime, Html: ExcelAHtml(bytes)); }
            catch { return new DocumentoPreviewDto("none", nombre, mime); }
        }
        if (esWord)
        {
            try { return new DocumentoPreviewDto("html", nombre, mime, Html: WordAHtml(bytes)); }
            catch { return new DocumentoPreviewDto("none", nombre, mime); }
        }
        return new DocumentoPreviewDto("none", nombre, mime);
    }

    private static string DecodificarTexto(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            bytes = bytes[3..];
        try { return new UTF8Encoding(false, true).GetString(bytes); }
        catch { return Encoding.Latin1.GetString(bytes); }
    }

    private static string Truncar(string s, int max)
        => s.Length <= max ? s : s[..max] + "\n\n... (contenido truncado para la vista previa)";

    private static string ExcelAHtml(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var wb = new ClosedXML.Excel.XLWorkbook(ms);
        var sb = new StringBuilder();
        foreach (var ws in wb.Worksheets)
        {
            sb.Append("<div class=\"prev-sheet\"><div class=\"prev-sheet-name\">").Append(Html(ws.Name)).Append("</div>");
            var range = ws.RangeUsed();
            if (range is null) { sb.Append("<div class=\"prev-empty\">Hoja vacía</div></div>"); continue; }
            int firstRow = range.RangeAddress.FirstAddress.RowNumber;
            int firstCol = range.RangeAddress.FirstAddress.ColumnNumber;
            int rows = Math.Min(range.RowCount(), 250);
            int cols = Math.Min(range.ColumnCount(), 40);
            sb.Append("<table class=\"prev-table\">");
            for (int i = 0; i < rows; i++)
            {
                sb.Append("<tr>");
                for (int j = 0; j < cols; j++)
                    sb.Append("<td>").Append(Html(ws.Cell(firstRow + i, firstCol + j).GetFormattedString())).Append("</td>");
                sb.Append("</tr>");
            }
            sb.Append("</table>");
            if (range.RowCount() > rows) sb.Append("<div class=\"prev-more\">... ").Append(range.RowCount() - rows).Append(" fila(s) más</div>");
            sb.Append("</div>");
        }
        return sb.ToString();
    }

    private static string WordAHtml(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(ms, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return "<div class=\"prev-empty\">Documento vacío</div>";
        var sb = new StringBuilder("<div class=\"prev-doc\">");
        foreach (var el in body.Elements())
        {
            if (el is DocumentFormat.OpenXml.Wordprocessing.Paragraph p)
            {
                var texto = string.Concat(p.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().Select(t => t.Text));
                var estilo = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "";
                if (string.IsNullOrWhiteSpace(texto)) { sb.Append("<p>&nbsp;</p>"); continue; }
                if (estilo.Equals("Title", StringComparison.OrdinalIgnoreCase) || estilo.StartsWith("Heading1", StringComparison.OrdinalIgnoreCase))
                    sb.Append("<h2>").Append(Html(texto)).Append("</h2>");
                else if (estilo.StartsWith("Heading", StringComparison.OrdinalIgnoreCase))
                    sb.Append("<h3>").Append(Html(texto)).Append("</h3>");
                else
                    sb.Append("<p>").Append(Html(texto)).Append("</p>");
            }
            else if (el is DocumentFormat.OpenXml.Wordprocessing.Table tbl)
            {
                sb.Append("<table class=\"prev-table\">");
                foreach (var tr in tbl.Elements<DocumentFormat.OpenXml.Wordprocessing.TableRow>())
                {
                    sb.Append("<tr>");
                    foreach (var tc in tr.Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>())
                    {
                        var ctexto = string.Concat(tc.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().Select(t => t.Text));
                        sb.Append("<td>").Append(Html(ctexto)).Append("</td>");
                    }
                    sb.Append("</tr>");
                }
                sb.Append("</table>");
            }
        }
        sb.Append("</div>");
        return sb.ToString();
    }

    private static string Html(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
}
