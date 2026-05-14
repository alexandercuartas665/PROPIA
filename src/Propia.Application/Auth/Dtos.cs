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
    IReadOnlyList<TenantInfo> AvailableTenants);

public record TenantInfo(
    Guid TenantId,
    string Nombre,
    string Rol);

public record SwitchTenantRequest(Guid TenantId);
