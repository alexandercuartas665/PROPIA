namespace Propia.Application.Integraciones;

/// <summary>Vista del config de Google para el Super Admin (sin exponer el client secret).</summary>
public sealed record GoogleAuthConfigDto(string? ClientId, bool HasSecret, bool IsEnabled);

public sealed record SaveGoogleAuthConfigRequest(string? ClientId, string? ClientSecret, bool IsEnabled);

/// <summary>
/// Credenciales descifradas para uso interno (NUNCA exponer en endpoints). Solo el SignIn service
/// las consume cuando hace el exchange contra Google.
/// </summary>
public sealed record GoogleAuthCredentials(string ClientId, string ClientSecret);

/// <summary>
/// Gestiona la configuracion del proveedor Google (singleton). El Client Secret se cifra con
/// ISecretProtector y solo se re-cifra si llega un valor nuevo (vacio conserva el existente).
/// </summary>
public interface IGoogleAuthConfigService
{
    Task<GoogleAuthConfigDto?> GetAsync(CancellationToken ct = default);
    Task<GoogleAuthConfigDto> SaveAsync(SaveGoogleAuthConfigRequest request, Guid actorId, string actorEmail, string? ip, CancellationToken ct = default);
    Task<GoogleAuthCredentials?> GetCredentialsAsync(CancellationToken ct = default);
}
