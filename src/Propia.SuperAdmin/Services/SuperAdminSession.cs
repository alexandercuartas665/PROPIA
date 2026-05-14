namespace Propia.SuperAdmin.Services;

/// <summary>
/// Sesion del SuperAdmin en memoria del server (scope por circuit Blazor Server).
/// Guarda el JWT tras login para reusarlo en llamadas al API.
/// En produccion: migrar a cookie HttpOnly + Redis para persistencia entre instancias.
/// </summary>
public class SuperAdminSession
{
    public string? AccessToken { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public string? Email { get; private set; }
    public string? Rol { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken) && (ExpiresAt is null || ExpiresAt > DateTimeOffset.UtcNow);

    public void SetSession(string accessToken, DateTimeOffset expiresAt, string email, string rol)
    {
        AccessToken = accessToken;
        ExpiresAt = expiresAt;
        Email = email;
        Rol = rol;
    }

    public void Clear()
    {
        AccessToken = null;
        ExpiresAt = null;
        Email = null;
        Rol = null;
    }
}
