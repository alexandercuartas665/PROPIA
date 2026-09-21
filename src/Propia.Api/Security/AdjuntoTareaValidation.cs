using Microsoft.AspNetCore.Http;

namespace Propia.Api.Security;

/// <summary>
/// T-09 (modulo 2.10 Tareas): lista blanca de adjuntos de tarjeta. Antes el upload aceptaba CUALQUIER
/// archivo (sin validar tipo); un .exe/.svg/.html/.js podia subirse y luego servirse desde el blob. Aqui
/// se valida extension + Content-Type (falsificables por el cliente) y, para imagenes/PDF/office, tambien
/// la firma real del binario (magic bytes) para rechazar archivos disfrazados. 10 MB por archivo (limite
/// existente del endpoint). Espejo del patron de <see cref="AdjuntoPqrsdValidation"/> pero con el set y el
/// limite de Tareas; CONSUME <see cref="ImageValidation"/> sin modificarlo. Mensajes en espanol.
/// </summary>
public static class AdjuntoTareaValidation
{
    public const long MaxBytes = 10L * 1000 * 1000;   // 10 MB (coincide con el limite del endpoint)

    // Extension -> Content-Types aceptados para esa extension. octet-stream (generico) siempre se admite.
    private static readonly Dictionary<string, string[]> Permitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = new[] { "image/jpeg" },
        [".jpeg"] = new[] { "image/jpeg" },
        [".png"] = new[] { "image/png" },
        [".pdf"] = new[] { "application/pdf" },
        [".docx"] = new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        [".xlsx"] = new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        [".txt"] = new[] { "text/plain" },
        [".csv"] = new[] { "text/csv", "text/plain", "application/csv", "application/vnd.ms-excel" },
    };

    private const string FormatosLegibles = "JPG, PNG, PDF, DOCX, XLSX, TXT y CSV";

    /// <summary>
    /// Valida un archivo. Devuelve el mensaje de error (en espanol) o null si es valido. Deja el stream
    /// que abre para la firma reposicionado al inicio (no afecta al OpenReadStream que use el caller luego).
    /// </summary>
    public static async Task<string?> ValidarAsync(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return "El archivo esta vacio.";
        if (file.Length > MaxBytes) return $"El archivo supera el maximo de 10 MB (\"{file.FileName}\").";

        var ext = System.IO.Path.GetExtension(file.FileName ?? string.Empty);
        if (string.IsNullOrEmpty(ext) || !Permitidos.TryGetValue(ext, out var tiposOk))
            return $"Formato no permitido (\"{file.FileName}\"). Se aceptan: {FormatosLegibles}.";

        // Content-Type debe concordar con la extension (o venir generico octet-stream).
        var ctDeclarado = file.ContentType ?? string.Empty;
        var ctGenerico = string.IsNullOrEmpty(ctDeclarado)
            || ctDeclarado.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase);
        if (!ctGenerico && !tiposOk.Contains(ctDeclarado, StringComparer.OrdinalIgnoreCase))
            return $"El tipo del archivo no coincide con su extension (\"{file.FileName}\").";

        // Firma real (magic bytes): el binario no puede hacerse pasar por otro (S-09). Imagen y PDF y los
        // office (docx/xlsx son contenedores ZIP). Los planos (txt/csv) no tienen firma fiable: bastan
        // extension + Content-Type (se sirven como texto, no se ejecutan).
        if (ext is ".jpg" or ".jpeg" or ".png")
        {
            await using var s = file.OpenReadStream();
            if (!await ImageValidation.EsImagenValidaAsync(s, null, ct))
                return $"El archivo no es una imagen valida (\"{file.FileName}\").";
        }
        else if (ext == ".pdf")
        {
            if (!await FirmaCoincideAsync(file, ct, 0x25, 0x50, 0x44, 0x46, 0x2D))   // "%PDF-"
                return $"El archivo no es un PDF valido (\"{file.FileName}\").";
        }
        else if (ext is ".docx" or ".xlsx")
        {
            if (!await FirmaCoincideAsync(file, ct, 0x50, 0x4B, 0x03, 0x04))         // ZIP "PK\x03\x04"
                return $"El archivo no es un {ext.TrimStart('.').ToUpperInvariant()} valido (\"{file.FileName}\").";
        }
        return null;
    }

    // Lee los primeros bytes del archivo y confirma que empiezan con la firma dada. Deja el stream cerrado
    // (se abre uno propio); el caller usa su propio OpenReadStream para subir.
    private static async Task<bool> FirmaCoincideAsync(IFormFile file, CancellationToken ct, params byte[] firma)
    {
        await using var s = file.OpenReadStream();
        var head = new byte[firma.Length];
        var leidos = 0;
        while (leidos < head.Length)
        {
            var n = await s.ReadAsync(head.AsMemory(leidos, head.Length - leidos), ct);
            if (n == 0) break;
            leidos += n;
        }
        if (leidos < firma.Length) return false;
        for (var i = 0; i < firma.Length; i++)
            if (head[i] != firma[i]) return false;
        return true;
    }
}
