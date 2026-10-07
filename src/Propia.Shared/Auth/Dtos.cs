namespace Propia.Shared.Auth;

/// <summary>
/// DTOs compartidos API <-> Web para los endpoints /connect/*.
/// Los DTOs internos del API (Propia.Application/Auth/Dtos.cs) son distintos -
/// estos son los que viajan por HTTP entre la UI Blazor y el API REST.
/// </summary>
public record LoginRequestDto(string Email, string Password);

public record LoginResponseDto(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    Guid UserId,
    string Email,
    Guid? ActiveTenantId,
    IReadOnlyList<TenantInfoDto> AvailableTenants,
    Guid? ActiveRolId = null,
    IReadOnlyList<RolInfoDto>? AvailableRoles = null);

public record MeResponseDto(
    Guid UserId,
    string Email,
    Guid? PersonaId,
    string? PersonaNombres,
    string? PersonaApellidos,
    Guid? ActiveTenantId,
    IReadOnlyList<TenantInfoDto> AvailableTenants,
    Guid? ActiveRolId = null,
    IReadOnlyList<RolInfoDto>? AvailableRoles = null);

public record TenantInfoDto(Guid TenantId, string Nombre, string Rol, string? LogoUrl = null, string? CodigoCorto = null);

/// <summary>Un rol del usuario en la copropiedad activa (2.5 v2.0 multi-rol / rol activo por sesion).</summary>
public record RolInfoDto(Guid RolId, string Nombre);

public record SwitchTenantRequestDto(Guid TenantId);

public record SwitchRolRequestDto(Guid RolId);
