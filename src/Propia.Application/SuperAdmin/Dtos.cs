using Propia.Domain.Enums;

namespace Propia.Application.SuperAdmin;

// ----- Auth -----
public record SuperAdminLoginRequest(string Email, string Password);

/// <summary>
/// Respuesta unificada del login del SuperAdmin Console.
/// - Si RequiresMfa = false: AccessToken + datos del usuario (login completo).
/// - Si RequiresMfa = true: solo MfaTicket - el cliente debe pedir codigo y llamar
///   a /admin/mfa/verify-login para obtener el JWT real.
/// </summary>
public record SuperAdminLoginResponse(
    bool RequiresMfa,
    string? AccessToken,
    DateTimeOffset? ExpiresAt,
    Guid? UserId,
    string? Email,
    RolSuperAdmin? Rol,
    string? MfaTicket,
    DateTimeOffset? MfaTicketExpiresAt);

// ----- Organizaciones -----
public record OrganizacionDto(Guid Id, string Nombre, TipoOrganizacion Tipo, string? Nit, string? Email, int CopropiedadesCount, DateTimeOffset CreatedAt, EstadoOrganizacion Estado = EstadoOrganizacion.Activa);

public record CambiarEstadoOrganizacionRequest(EstadoOrganizacion Estado);

/// <summary>Crea el usuario administrador de una organizacion (login + acceso a sus copropiedades).</summary>
public record CrearAdminOrganizacionRequest(
    string Nombres, string Apellidos, string Documento, string Email, string Password,
    TipoDocumento TipoDocumento = TipoDocumento.CC);

public record AdminOrganizacionDto(Guid UsuarioId, Guid PersonaId, string Nombre, string Email, int CopropiedadesAsignadas);
public record CrearOrganizacionRequest(string Nombre, TipoOrganizacion Tipo, string? Nit, string? Email, string? Telefono);

// ----- Tenants (Copropiedades) -----
public record TenantDto(Guid Id, string Nombre, string? Nit, string? CodigoPropia, EstadoCopropiedad Estado, EstadoCustodia EstadoCustodia, Guid? OrganizacionId, string? OrganizacionNombre, DateTimeOffset? FechaActivacion, string? CodigoCorto = null);
public record CrearTenantRequest(string Nombre, string? Nit, string? Direccion, string? CodigoPropia, Guid? OrganizacionId);
public record CambiarEstadoTenantRequest(EstadoCopropiedad NuevoEstado, string Justificacion);

// ----- Equipo A&D GROUP -----
public record SuperAdminUsuarioDto(Guid Id, string Email, RolSuperAdmin Rol, bool Activo, DateTimeOffset? UltimoAcceso);
public record CrearSuperAdminUsuarioRequest(string Email, string Password, RolSuperAdmin Rol);

// ----- Logs -----
public record SuperAdminLogDto(Guid Id, string ActorEmail, string Accion, string? EntidadAfectada, string? Justificacion, string? Ip, DateTimeOffset CreatedAt);
