namespace Propia.Api.Security;

/// <summary>
/// S-09: validacion de "magic bytes" para subidas de imagen. El Content-Type y la extension los
/// controla el cliente y son falsificables; aqui verificamos la firma real del binario para
/// rechazar archivos que se hacen pasar por imagen (HTML/SVG/ejecutables con Content-Type: image/png).
/// Soporta JPEG, PNG y WEBP (los formatos aceptados por la plataforma).
/// </summary>
public static class ImageValidation
{
    /// <summary>
    /// Lee el encabezado del stream y confirma que corresponde a un JPEG/PNG/WEBP real y, si se
    /// pasa <paramref name="contentTypeDeclarado"/>, que ademas concuerda con el declarado.
    /// Deja el stream reposicionado al inicio para poder subirlo despues.
    /// </summary>
    public static async Task<bool> EsImagenValidaAsync(Stream stream, string? contentTypeDeclarado, CancellationToken ct)
    {
        if (stream is null || !stream.CanRead) return false;

        var head = new byte[12];
        var leidos = 0;
        while (leidos < head.Length)
        {
            var n = await stream.ReadAsync(head.AsMemory(leidos, head.Length - leidos), ct);
            if (n == 0) break;
            leidos += n;
        }
        if (stream.CanSeek) stream.Position = 0;
        if (leidos < 12) return false;

        var tipoReal = Detectar(head);
        if (tipoReal is null) return false;

        // Si el cliente declaro un content-type, debe concordar con el binario real.
        if (!string.IsNullOrEmpty(contentTypeDeclarado)
            && !string.Equals(contentTypeDeclarado, tipoReal, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return true;
    }

    /// <summary>Devuelve el content-type real segun la firma, o null si no es imagen soportada.</summary>
    private static string? Detectar(ReadOnlySpan<byte> b)
    {
        // JPEG: FF D8 FF
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            return "image/jpeg";
        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47
            && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A)
            return "image/png";
        // WEBP: "RIFF" .... "WEBP"
        if (b.Length >= 12 && b[0] == (byte)'R' && b[1] == (byte)'I' && b[2] == (byte)'F' && b[3] == (byte)'F'
            && b[8] == (byte)'W' && b[9] == (byte)'E' && b[10] == (byte)'B' && b[11] == (byte)'P')
            return "image/webp";
        return null;
    }
}
