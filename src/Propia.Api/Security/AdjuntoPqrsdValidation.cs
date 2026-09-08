using Microsoft.AspNetCore.Http;

namespace Propia.Api.Security;

/// <summary>
/// G-05 / spec 2.9 seccion 14 + S-09: validacion de adjuntos PQRSD. Lista blanca de formatos
/// (jpg/jpeg/png/mp4/pdf/docx), 25 MB por archivo y tope de 5 adjuntos por expediente. Se valida
/// extension + Content-Type (falsificables por el cliente) y, para imagenes y PDF, tambien la firma
/// real del binario (magic bytes) para rechazar archivos disfrazados. Mensajes en espanol.
/// </summary>
public static class AdjuntoPqrsdValidation
{
    public const long MaxBytes = 25L * 1024 * 1024;   // 25 MB por archivo
    public const int MaxPorExpediente = 5;            // spec 14: 5 archivos por radicacion

    // Extension -> Content-Types aceptados para esa extension.
    private static readonly Dictionary<string, string[]> Permitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = new[] { "image/jpeg" },
        [".jpeg"] = new[] { "image/jpeg" },
        [".png"] = new[] { "image/png" },
        [".mp4"] = new[] { "video/mp4" },
        [".pdf"] = new[] { "application/pdf" },
        [".docx"] = new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
    };

    /// <summary>
    /// Valida un archivo. Devuelve el mensaje de error (en espanol) o null si es valido.
    /// Deja el stream reposicionado al inicio.
    /// </summary>
    public static async Task<string?> ValidarAsync(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return "El archivo esta vacio.";
        if (file.Length > MaxBytes) return $"El archivo supera el maximo de 25 MB (\"{file.FileName}\").";

        var ext = System.IO.Path.GetExtension(file.FileName ?? string.Empty);
        if (string.IsNullOrEmpty(ext) || !Permitidos.TryGetValue(ext, out var tiposOk))
            return $"Formato no permitido (\"{file.FileName}\"). Se aceptan: JPG, PNG, MP4, PDF y DOCX.";

        // Content-Type debe concordar con la extension (o venir generico octet-stream).
        var ct2 = file.ContentType ?? string.Empty;
        var ctGenerico = string.IsNullOrEmpty(ct2) || ct2.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase);
        if (!ctGenerico && !tiposOk.Contains(ct2, StringComparer.OrdinalIgnoreCase))
            return $"El tipo del archivo no coincide con su extension (\"{file.FileName}\").";

        // Firma real (magic bytes) para imagen y PDF (S-09: el binario no puede hacerse pasar por otro).
        await using var stream = file.OpenReadStream();
        if (ext is ".jpg" or ".jpeg" or ".png")
        {
            if (!await ImageValidation.EsImagenValidaAsync(stream, null, ct))
                return $"El archivo no es una imagen valida (\"{file.FileName}\").";
        }
        else if (ext == ".pdf")
        {
            var head = new byte[5];
            var n = await stream.ReadAsync(head.AsMemory(0, 5), ct);
            if (stream.CanSeek) stream.Position = 0;
            // "%PDF-"
            if (n < 5 || head[0] != 0x25 || head[1] != 0x50 || head[2] != 0x44 || head[3] != 0x46 || head[4] != 0x2D)
                return $"El archivo no es un PDF valido (\"{file.FileName}\").";
        }
        return null;
    }
}
