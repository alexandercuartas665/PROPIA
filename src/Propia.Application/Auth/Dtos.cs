namespace Propia.Application.Auth;

public record LoginRequest(string Email, string Password);

public record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    Guid UserId,
    string Email,
    Guid? ActiveTenantId,
    IReadOnlyList<TenantInfo> AvailableTenants,
    // 2.5 v2.0 multi-rol: rol activo por sesion. ActiveRolId null = aun sin elegir (el cliente debe
    // elegir si AvailableRoles tiene mas de uno). AvailableRoles solo se llena en contexto con tenant
    // (p.ej. /connect/me), no en el login inicial (RLS no deja leerlos sin tenant activo).
    Guid? ActiveRolId = null,
    IReadOnlyList<RolInfo>? AvailableRoles = null);

public record MeResponse(
    Guid UserId,
    string Email,
    Guid? PersonaId,
    string? PersonaNombres,
    string? PersonaApellidos,
    Guid? ActiveTenantId,
    IReadOnlyList<TenantInfo> AvailableTenants,
    string? PersonaFotoUrl = null,
    string? PersonaFirmaUrl = null,
    // Rol activo por sesion (del claim rol_id) + roles que el usuario tiene en la copropiedad activa.
    Guid? ActiveRolId = null,
    IReadOnlyList<RolInfo>? AvailableRoles = null);

public record TenantInfo(
    Guid TenantId,
    string Nombre,
    string Rol,
    string? LogoUrl = null,
    string? CodigoCorto = null);

/// <summary>Un rol que el usuario tiene asignado en la copropiedad activa (2.5 v2.0 multi-rol).</summary>
public record RolInfo(Guid RolId, string Nombre);

public record SwitchTenantRequest(Guid TenantId);

/// <summary>Elige el rol ACTIVO para la sesion (reemite el JWT con el claim rol_id).</summary>
public record SwitchRolRequest(Guid RolId);

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
