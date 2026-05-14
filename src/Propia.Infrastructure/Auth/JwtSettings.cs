namespace Propia.Infrastructure.Auth;

/// <summary>
/// Configuracion del JWT - se popula desde appsettings (seccion "Jwt").
/// En produccion, SigningKey debe venir de variable de entorno o KeyVault.
/// </summary>
public class JwtSettings
{
    public string Issuer { get; set; } = "propia-api";
    public string Audience { get; set; } = "propia-clients";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60;
}
