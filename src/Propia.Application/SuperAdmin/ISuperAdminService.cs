using Propia.Domain.Enums;

namespace Propia.Application.SuperAdmin;

/// <summary>
/// Operaciones de la Consola de A&D GROUP (modulo 0.1).
/// CADA accion escribe un registro en super_admin_logs (tabla append-only con trigger).
/// El llamador (AdminController) garantiza que solo SuperAdmin con claim valido accede.
/// </summary>
public interface ISuperAdminService
{
    // Organizaciones
    Task<IReadOnlyList<OrganizacionDto>> ListOrganizacionesAsync(CancellationToken ct);
    Task<OrganizacionDto> CrearOrganizacionAsync(CrearOrganizacionRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct);

    // Tenants (Copropiedades)
    Task<IReadOnlyList<TenantDto>> ListTenantsAsync(CancellationToken ct);
    Task<TenantDto> CrearTenantAsync(CrearTenantRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct);
    Task<TenantDto?> CambiarEstadoTenantAsync(Guid tenantId, CambiarEstadoTenantRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct);

    // Equipo A&D GROUP
    Task<IReadOnlyList<SuperAdminUsuarioDto>> ListEquipoAsync(CancellationToken ct);
    Task<SuperAdminUsuarioDto> CrearMiembroEquipoAsync(CrearSuperAdminUsuarioRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct);

    /// <summary>
    /// Desactiva un miembro del equipo. REGLA CRITICA: debe quedar al menos 1 SuperAdmin
    /// activo en el sistema. Si se intenta desactivar el ultimo, lanza excepcion.
    /// </summary>
    Task<bool> DesactivarMiembroEquipoAsync(Guid usuarioId, Guid actorId, string actorEmail, string? ip, CancellationToken ct);

    // Logs (lectura)
    Task<IReadOnlyList<SuperAdminLogDto>> ListLogsAsync(int take, CancellationToken ct);
}
