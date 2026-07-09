using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Propia.Infrastructure.Storage;

/// <summary>
/// Implementacion local (filesystem) de IBlobStorage para Development y tests.
/// Escribe a {ContentRoot}/wwwroot/uploads/{key} y devuelve URLs relativas /uploads/{key}.
/// En produccion se usa R2BlobStorage.
/// </summary>
public sealed class LocalBlobStorage : IBlobStorage
{
    private readonly IHostEnvironment _env;
    private readonly ILogger<LocalBlobStorage> _logger;

    public LocalBlobStorage(IHostEnvironment env, ILogger<LocalBlobStorage> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        var fullPath = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", key.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(dir);

        await using (var stream = File.Create(fullPath))
        {
            await content.CopyToAsync(stream, ct);
        }

        _logger.LogInformation("Blob local guardado: {Path}", fullPath);
        return GetPublicUrl(key);
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var fullPath = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", key.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public async Task<byte[]?> DownloadAsync(string key, CancellationToken ct)
    {
        var fullPath = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", key.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath)) return null;
        return await File.ReadAllBytesAsync(fullPath, ct);
    }

    public string GetPublicUrl(string key)
    {
        return $"/uploads/{key}?v={DateTime.UtcNow.Ticks}";
    }

    public string? ResolveUrl(string? storedValueOrKey)
    {
        if (string.IsNullOrWhiteSpace(storedValueOrKey)) return null;
        var val = storedValueOrKey.Trim();
        // Ya es ruta relativa servida por el app o data URI: dejar igual.
        if (val.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || val.StartsWith("/")) return val;
        // URL absoluta (dato viejo: se guardaba con el host del request, ej. http://localhost:8080/uploads/...).
        // El path ya es la ruta servida por el app; devolverla RELATIVA al mismo origen (sin re-prefijar
        // /uploads/, que la duplicaria). Si el path trae un prefijo raro antes de /uploads/, se recorta.
        if (val.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || val.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(val, UriKind.Absolute, out var uri)) return val;
            var pathQuery = uri.PathAndQuery;
            var idx = pathQuery.IndexOf("/uploads/", StringComparison.OrdinalIgnoreCase);
            return idx >= 0 ? pathQuery.Substring(idx) : pathQuery;
        }
        return GetPublicUrl(val);
    }
}
