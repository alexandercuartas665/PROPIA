namespace Propia.Infrastructure.Storage;

/// <summary>
/// Configuracion del backend Cloudflare R2 (S3-compatible).
/// Bindeada desde la seccion "Storage:R2" en appsettings / variables de entorno.
///
/// En produccion (Railway):
///   Storage__R2__AccountId, Storage__R2__AccessKeyId, Storage__R2__SecretAccessKey,
///   Storage__R2__BucketName, Storage__R2__PublicUrl
/// </summary>
public sealed class R2Options
{
    public const string SectionName = "Storage:R2";

    /// <summary>Cloudflare Account ID (parte del endpoint S3).</summary>
    public string AccountId { get; set; } = string.Empty;

    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// URL publica base para servir objetos. Si custom domain esta configurado, ej.
    /// "https://uploads.propia.cubot.com.co". Si no, se puede dejar vacio y se
    /// construye con el endpoint R2 publico (acceso autenticado).
    /// </summary>
    public string PublicUrl { get; set; } = string.Empty;

    public string Endpoint => $"https://{AccountId}.r2.cloudflarestorage.com";
}
