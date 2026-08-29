namespace Propia.Application.Auth;

public record LoginRequest(string Email, string Password);

public record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    Guid UserId,
    string Email,
    Guid? ActiveTenantId,
    IReadOnlyList<TenantInfo> AvailableTenants);

public record MeResponse(
    Guid UserId,
    string Email,
    Guid? PersonaId,
    string? PersonaNombres,
    string? PersonaApellidos,
    Guid? ActiveTenantId,
    IReadOnlyList<TenantInfo> AvailableTenants,
    string? PersonaFotoUrl = null,
    string? PersonaFirmaUrl = null);

public record TenantInfo(
    Guid TenantId,
    string Nombre,
    string Rol,
    string? LogoUrl = null);

public record SwitchTenantRequest(Guid TenantId);

/// <summary>
/// Respuesta del login UNIFICADO (/connect/login). Un solo punto de entrada para todos los
/// usuarios; el campo Kind dice si la sesion es de plataforma ("superadmin") o de copropiedad
/// ("client"), para que el app decida que mostrar. Si Kind="superadmin" y RequiresMfa=true, el
/// cliente debe pedir el codigo TOTP y llamar a /connect/login/mfa con el MfaTicket.
/// </summary>
public record UnifiedLoginResponse(
    string Kind,
    bool RequiresMfa,
    string? AccessToken,
    DateTimeOffset? ExpiresAt,
    string? Email,
    string? MfaTicket,
    Guid? ActiveTenantId,
    IReadOnlyList<TenantInfo> AvailableTenants);
