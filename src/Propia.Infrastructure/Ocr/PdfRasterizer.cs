using PDFtoImage;
using SkiaSharp;

namespace Propia.Infrastructure.Ocr;

/// <summary>
/// Convierte un PDF a imagenes PNG (una por pagina) usando PDFtoImage (PDFium). Necesario porque
/// Azure Computer Vision (Image Analysis) solo procesa imagenes, no PDF: antes de mandarlo al OCR,
/// rasterizamos el PDF. Cap de paginas para no disparar el costo del OCR en documentos largos.
/// </summary>
internal static class PdfRasterizer
{
    public static bool EsPdf(string? contentType, byte[] bytes)
    {
        if (!string.IsNullOrWhiteSpace(contentType) && contentType.Contains("pdf", System.StringComparison.OrdinalIgnoreCase))
            return true;
        // Firma magica "%PDF-"
        return bytes.Length >= 5 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46 && bytes[4] == 0x2D;
    }

    /// <summary>Rasteriza cada pagina del PDF a PNG. Devuelve la lista de bytes (max maxPaginas).</summary>
    public static List<byte[]> RasterizarPaginas(byte[] pdf, int maxPaginas = 8, int dpi = 200)
    {
        var paginas = new List<byte[]>();
        var opciones = new RenderOptions(Dpi: dpi);
        foreach (var bmp in Conversion.ToImages(pdf, password: null, options: opciones))
        {
            using (bmp)
            {
                using var data = bmp.Encode(SKEncodedImageFormat.Png, 90);
                paginas.Add(data.ToArray());
            }
            if (paginas.Count >= maxPaginas) break;
        }
        return paginas;
    }
}
