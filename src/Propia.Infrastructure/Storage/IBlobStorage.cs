namespace Propia.Infrastructure.Storage;

/// <summary>
/// Abstraccion para almacenamiento de blobs (adjuntos, imagenes, PDFs).
/// MVP: implementacion Cloudflare R2 (S3-compatible). Futuro: AWS S3 Bogota
/// cuando se valide costo y residencia obligatoria en Colombia.
/// </summary>
public interface IBlobStorage
{
    /// <summary>Sube el contenido al storage. Devuelve la URL publica.</summary>
    /// <param name="key">Path relativo dentro del bucket, ej. "tenants/abc/logo.jpg".</param>
    Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct);

    /// <summary>Borra el objeto si existe. No-op si no existe.</summary>
    Task DeleteAsync(string key, CancellationToken ct);

    /// <summary>URL publica para servir el objeto (incluye custom domain si esta configurado).</summary>
    string GetPublicUrl(string key);

    /// <summary>
    /// Descarga el contenido completo del blob (modulo 2.15 sirve PDFs por API).
    /// Devuelve null si la key no existe.
    /// </summary>
    Task<byte[]?> DownloadAsync(string key, CancellationToken ct);
}
