using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Propia.Application.SuperAdmin;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.SuperAdmin;

public class SuperAdminService : ISuperAdminService
{
    private readonly PropiaDbContext _db;
    private readonly IPasswordHasher<SuperAdminUsuario> _hasher;

    public SuperAdminService(PropiaDbContext db, IPasswordHasher<SuperAdminUsuario> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    // ---------------------------------- Organizaciones ----------------------------------

    public async Task<IReadOnlyList<OrganizacionDto>> ListOrganizacionesAsync(CancellationToken ct)
    {
        return await _db.Organizaciones
            .AsNoTracking()
            .OrderBy(o => o.Nombre)
            .Select(o => new OrganizacionDto(
                o.Id, o.Nombre, o.Tipo, o.Nit, o.Email,
                o.Copropiedades.Count, o.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<OrganizacionDto> CrearOrganizacionAsync(CrearOrganizacionRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct)
    {
        var org = new Organizacion
        {
            Nombre = req.Nombre,
            Tipo = req.Tipo,
            Nit = req.Nit,
            Email = req.Email,
            Telefono = req.Telefono,
            FechaActivacion = DateTimeOffset.UtcNow
        };
        _db.Organizaciones.Add(org);
        _db.SuperAdminLogs.Add(NewLog(actorId, actorEmail, "CREATE_ORGANIZACION", $"Organizacion:{org.Id}", $"Nombre={req.Nombre}", ip));
        await _db.SaveChangesAsync(ct);
        return new OrganizacionDto(org.Id, org.Nombre, org.Tipo, org.Nit, org.Email, 0, org.CreatedAt);
    }

    // ---------------------------------- Tenants ----------------------------------

    public async Task<IReadOnlyList<TenantDto>> ListTenantsAsync(CancellationToken ct)
    {
        return await _db.Tenants
            .AsNoTracking()
            .Include(t => t.Organizacion)
            .OrderBy(t => t.Nombre)
            .Select(t => new TenantDto(
                t.Id, t.Nombre, t.Nit, t.CodigoPropia, t.Estado, t.EstadoCustodia,
                t.OrganizacionId, t.Organizacion != null ? t.Organizacion.Nombre : null,
                t.FechaActivacion))
            .ToListAsync(ct);
    }

    public async Task<TenantDto> CrearTenantAsync(CrearTenantRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct)
    {
        var tenant = new Tenant
        {
            Nombre = req.Nombre,
            Nit = req.Nit,
            Direccion = req.Direccion,
            CodigoPropia = req.CodigoPropia,
            OrganizacionId = req.OrganizacionId,
            Estado = EstadoCopropiedad.Activa,
            EstadoCustodia = req.OrganizacionId.HasValue ? EstadoCustodia.ConAdmin : EstadoCustodia.SinAdmin,
            FechaActivacion = DateTimeOffset.UtcNow
        };
        _db.Tenants.Add(tenant);
        _db.SuperAdminLogs.Add(NewLog(actorId, actorEmail, "CREATE_TENANT", $"Tenant:{tenant.Id}", $"Nombre={req.Nombre}, OrgId={req.OrganizacionId}", ip));
        await _db.SaveChangesAsync(ct);

        var orgNombre = tenant.OrganizacionId.HasValue
            ? await _db.Organizaciones.Where(o => o.Id == tenant.OrganizacionId).Select(o => o.Nombre).FirstOrDefaultAsync(ct)
            : null;
        return new TenantDto(tenant.Id, tenant.Nombre, tenant.Nit, tenant.CodigoPropia, tenant.Estado, tenant.EstadoCustodia, tenant.OrganizacionId, orgNombre, tenant.FechaActivacion);
    }

    public async Task<TenantDto?> CambiarEstadoTenantAsync(Guid tenantId, CambiarEstadoTenantRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct)
    {
        var tenant = await _db.Tenants.Include(t => t.Organizacion).FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return null;

        if (string.IsNullOrWhiteSpace(req.Justificacion))
            throw new InvalidOperationException("Justificacion obligatoria al cambiar estado de un tenant.");

        var estadoAnterior = tenant.Estado;
        tenant.Estado = req.NuevoEstado;
        if (req.NuevoEstado == EstadoCopropiedad.EnCancelacion || req.NuevoEstado == EstadoCopropiedad.CanceladoArchivado)
            tenant.FechaCancelacion = DateTimeOffset.UtcNow;

        _db.SuperAdminLogs.Add(NewLog(actorId, actorEmail, "CHANGE_TENANT_STATE",
            $"Tenant:{tenant.Id}",
            $"De {estadoAnterior} a {req.NuevoEstado}. Justificacion: {req.Justificacion}",
            ip));

        await _db.SaveChangesAsync(ct);
        return new TenantDto(tenant.Id, tenant.Nombre, tenant.Nit, tenant.CodigoPropia, tenant.Estado, tenant.EstadoCustodia, tenant.OrganizacionId, tenant.Organizacion?.Nombre, tenant.FechaActivacion);
    }

    // ---------------------------------- Equipo A&D GROUP ----------------------------------

    public async Task<IReadOnlyList<SuperAdminUsuarioDto>> ListEquipoAsync(CancellationToken ct)
    {
        return await _db.SuperAdminUsuarios
            .AsNoTracking()
            .OrderBy(u => u.Email)
            .Select(u => new SuperAdminUsuarioDto(u.Id, u.Email, u.Rol, u.Activo, u.UltimoAcceso))
            .ToListAsync(ct);
    }

    public async Task<SuperAdminUsuarioDto> CrearMiembroEquipoAsync(CrearSuperAdminUsuarioRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct)
    {
        var existe = await _db.SuperAdminUsuarios.AnyAsync(u => u.Email == req.Email, ct);
        if (existe) throw new InvalidOperationException($"Ya existe un miembro del equipo con email {req.Email}.");

        var user = new SuperAdminUsuario
        {
            Email = req.Email,
            Rol = req.Rol,
            Activo = true
        };
        user.PasswordHash = _hasher.HashPassword(user, req.Password);
        _db.SuperAdminUsuarios.Add(user);
        _db.SuperAdminLogs.Add(NewLog(actorId, actorEmail, "CREATE_SUPER_ADMIN_USER",
            $"SuperAdminUsuario:{user.Id}",
            $"Email={req.Email}, Rol={req.Rol}", ip));
        await _db.SaveChangesAsync(ct);
        return new SuperAdminUsuarioDto(user.Id, user.Email, user.Rol, user.Activo, null);
    }

    public async Task<bool> DesactivarMiembroEquipoAsync(Guid usuarioId, Guid actorId, string actorEmail, string? ip, CancellationToken ct)
    {
        var target = await _db.SuperAdminUsuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct);
        if (target is null || !target.Activo) return false;

        // REGLA CRITICA spec 0.1: siempre debe quedar al menos 1 SuperAdmin activo
        if (target.Rol == RolSuperAdmin.SuperAdmin)
        {
            var superAdminsActivos = await _db.SuperAdminUsuarios
                .CountAsync(u => u.Rol == RolSuperAdmin.SuperAdmin && u.Activo, ct);
            if (superAdminsActivos <= 1)
                throw new InvalidOperationException(
                    "No se puede desactivar el ultimo SuperAdmin activo del sistema. Crea otro SuperAdmin antes.");
        }

        target.Activo = false;
        _db.SuperAdminLogs.Add(NewLog(actorId, actorEmail, "DEACTIVATE_SUPER_ADMIN_USER",
            $"SuperAdminUsuario:{target.Id}",
            $"Email={target.Email}, Rol={target.Rol}", ip));
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ---------------------------------- Logs ----------------------------------

    public async Task<IReadOnlyList<SuperAdminLogDto>> ListLogsAsync(int take, CancellationToken ct)
    {
        return await _db.SuperAdminLogs
            .AsNoTracking()
            .OrderByDescending(l => l.CreatedAt)
            .Take(take)
            .Select(l => new SuperAdminLogDto(l.Id, l.ActorEmail, l.Accion, l.EntidadAfectada, l.Justificacion, l.Ip, l.CreatedAt))
            .ToListAsync(ct);
    }

    // ---------------------------------- Helpers ----------------------------------

    private static SuperAdminLog NewLog(Guid actorId, string actorEmail, string accion, string? entidad, string? justificacion, string? ip) =>
        new()
        {
            ActorId = actorId,
            ActorEmail = actorEmail,
            Accion = accion,
            EntidadAfectada = entidad,
            Justificacion = justificacion,
            Ip = ip
        };
}
