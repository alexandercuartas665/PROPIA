using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Propia.Infrastructure.Storage;

/// <summary>
/// Implementacion de IBlobStorage contra Cloudflare R2 via AWSSDK.S3.
/// R2 expone API S3-compatible en https://{accountId}.r2.cloudflarestorage.com.
/// </summary>
public sealed class R2BlobStorage : IBlobStorage, IDisposable
{
    private readonly R2Options _options;
    private readonly IAmazonS3 _client;
    private readonly ILogger<R2BlobStorage> _logger;

    public R2BlobStorage(IOptions<R2Options> options, ILogger<R2BlobStorage> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.AccountId)
            || string.IsNullOrWhiteSpace(_options.AccessKeyId)
            || string.IsNullOrWhiteSpace(_options.SecretAccessKey)
            || string.IsNullOrWhiteSpace(_options.BucketName))
        {
            throw new InvalidOperationException(
                "Storage:R2 mal configurado. Faltan AccountId/AccessKeyId/SecretAccessKey/BucketName.");
        }

        var s3Config = new AmazonS3Config
        {
            ServiceURL = _options.Endpoint,
            ForcePathStyle = true,  // R2 prefiere path-style
            // R2 ignora la region pero el SDK la exige; cualquier valor sirve.
            AuthenticationRegion = "auto"
        };

        _client = new AmazonS3Client(_options.AccessKeyId, _options.SecretAccessKey, s3Config);
    }

    public async Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            DisablePayloadSigning = true  // R2 no soporta chunked signing
        };

        await _client.PutObjectAsync(request, ct);
        _logger.LogInformation("Blob subido a R2: bucket={Bucket} key={Key}", _options.BucketName, key);
        return GetPublicUrl(key);
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        try
        {
            await _client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = _options.BucketName,
                Key = key
            }, ct);
            _logger.LogInformation("Blob borrado de R2: bucket={Bucket} key={Key}", _options.BucketName, key);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // No-op si no existe.
        }
    }

    public string GetPublicUrl(string key)
    {
        if (!string.IsNullOrWhiteSpace(_options.PublicUrl))
        {
            var baseUrl = _options.PublicUrl.TrimEnd('/');
            return $"{baseUrl}/{key}";
        }

        // Fallback: URL directa del endpoint R2 (requiere acceso publico configurado en el bucket).
        return $"{_options.Endpoint}/{_options.BucketName}/{key}";
    }

    public void Dispose() => _client.Dispose();
}
