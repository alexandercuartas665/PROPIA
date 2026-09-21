using Microsoft.AspNetCore.Http;
using Propia.Api.Security;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// T-09 (modulo 2.10 Tareas): lista blanca de adjuntos. Unit test del validador (no toca BD ni HTTP):
/// rechaza tipos no permitidos (.exe/.svg/.html/.js) y binarios disfrazados (magic bytes), acepta los
/// formatos de la lista blanca. Cierra el hueco de que el upload aceptaba cualquier archivo.
/// </summary>
public class TareasAdjuntoValidationTests
{
    private static IFormFile File(byte[] bytes, string fileName, string contentType)
    {
        var ms = new MemoryStream(bytes);
        return new FormFile(ms, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary { ["Content-Type"] = contentType }
        };
    }

    // Cabeceras reales (magic bytes) de cada formato, con relleno suficiente para el lector (>=12 bytes).
    private static byte[] Png() => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 1, 2, 3 };
    private static byte[] Pdf() => System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\n%aaa\n");
    private static byte[] Zip() => new byte[] { 0x50, 0x4B, 0x03, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 9, 9 };
    private static byte[] Texto() => System.Text.Encoding.UTF8.GetBytes("col1,col2\n1,2\n");

    [Fact]
    public async Task Acepta_los_formatos_de_la_lista_blanca()
    {
        Assert.Null(await AdjuntoTareaValidation.ValidarAsync(File(Png(), "foto.png", "image/png"), default));
        Assert.Null(await AdjuntoTareaValidation.ValidarAsync(File(Pdf(), "doc.pdf", "application/pdf"), default));
        Assert.Null(await AdjuntoTareaValidation.ValidarAsync(File(Zip(), "hoja.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"), default));
        Assert.Null(await AdjuntoTareaValidation.ValidarAsync(File(Zip(), "carta.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"), default));
        Assert.Null(await AdjuntoTareaValidation.ValidarAsync(File(Texto(), "datos.csv", "text/csv"), default));
        Assert.Null(await AdjuntoTareaValidation.ValidarAsync(File(Texto(), "notas.txt", "text/plain"), default));
        // Content-Type generico (octet-stream) se admite si la extension y la firma son validas.
        Assert.Null(await AdjuntoTareaValidation.ValidarAsync(File(Png(), "foto.png", "application/octet-stream"), default));
    }

    [Fact]
    public async Task Rechaza_tipos_no_permitidos_por_extension()
    {
        foreach (var (name, ctype) in new[] { ("virus.exe", "application/octet-stream"), ("x.svg", "image/svg+xml"),
                                              ("p.html", "text/html"), ("s.js", "text/javascript"), ("v.mp4", "video/mp4") })
        {
            var err = await AdjuntoTareaValidation.ValidarAsync(File(Texto(), name, ctype), default);
            Assert.NotNull(err);
            Assert.Contains("Formato no permitido", err);
        }
    }

    [Fact]
    public async Task Rechaza_binario_disfrazado_por_magic_bytes()
    {
        // Un .png cuyo contenido NO es una imagen real (p.ej. HTML renombrado) se rechaza por la firma.
        var htmlBytes = System.Text.Encoding.ASCII.GetBytes("<html><script>alert(1)</script></html>");
        var err = await AdjuntoTareaValidation.ValidarAsync(File(htmlBytes, "fake.png", "image/png"), default);
        Assert.NotNull(err);
        Assert.Contains("no es una imagen valida", err);

        // Un .pdf sin la firma %PDF- se rechaza.
        var errPdf = await AdjuntoTareaValidation.ValidarAsync(File(htmlBytes, "fake.pdf", "application/pdf"), default);
        Assert.NotNull(errPdf);
        Assert.Contains("no es un PDF valido", errPdf);

        // Un .docx que no es un contenedor ZIP se rechaza.
        var errDocx = await AdjuntoTareaValidation.ValidarAsync(File(htmlBytes, "fake.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"), default);
        Assert.NotNull(errDocx);
    }

    [Fact]
    public async Task Rechaza_content_type_que_no_concuerda_con_la_extension()
    {
        // Extension permitida (.png) pero Content-Type declarado de otro tipo NO generico -> rechazo.
        var err = await AdjuntoTareaValidation.ValidarAsync(File(Png(), "foto.png", "text/html"), default);
        Assert.NotNull(err);
        Assert.Contains("no coincide", err);
    }

    [Fact]
    public async Task Rechaza_vacio()
    {
        var err = await AdjuntoTareaValidation.ValidarAsync(File(Array.Empty<byte>(), "x.png", "image/png"), default);
        Assert.NotNull(err);
        Assert.Contains("vacio", err);
    }
}
