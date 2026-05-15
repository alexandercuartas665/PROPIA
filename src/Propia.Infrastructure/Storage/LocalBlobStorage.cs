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

    public string GetPublicUrl(string key)
    {
        return $"/uploads/{key}?v={DateTime.UtcNow.Ticks}";
    }
}
