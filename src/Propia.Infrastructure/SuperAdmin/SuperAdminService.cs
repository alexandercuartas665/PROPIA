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
    private readonly UserManager<ApplicationUser> _userManager;

    public SuperAdminService(PropiaDbContext db, IPasswordHasher<SuperAdminUsuario> hasher, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _hasher = hasher;
        _userManager = userManager;
    }

    // ---------------------------------- Organizaciones ----------------------------------

    public async Task<IReadOnlyList<OrganizacionDto>> ListOrganizacionesAsync(CancellationToken ct)
    {
        return await _db.Organizaciones
            .AsNoTracking()
            .OrderBy(o => o.Nombre)
            .Select(o => new OrganizacionDto(
                o.Id, o.Nombre, o.Tipo, o.Nit, o.Email,
                o.Copropiedades.Count, o.CreatedAt, o.Estado))
            .ToListAsync(ct);
    }

    public async Task<OrganizacionDto> CrearOrganizacionAsync(CrearOrganizacionRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct)
    {
        var nombre = (req.Nombre ?? "").Trim();
        if (nombre.Length == 0)
            throw new InvalidOperationException("El nombre de la organizacion es obligatorio.");

        var nit = string.IsNullOrWhiteSpace(req.Nit) ? null : req.Nit.Trim();
        if (nit is not null && await _db.Organizaciones.AnyAsync(o => o.Nit == nit, ct))
            throw new InvalidOperationException($"Ya existe una organizacion con el NIT {nit}.");

        var org = new Organizacion
        {
            Nombre = nombre,
            Tipo = req.Tipo,
            Nit = nit,
            Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim(),
            Telefono = string.IsNullOrWhiteSpace(req.Telefono) ? null : req.Telefono.Trim(),
            Estado = EstadoOrganizacion.Activa,
            FechaActivacion = DateTimeOffset.UtcNow
        };
        _db.Organizaciones.Add(org);
        _db.SuperAdminLogs.Add(NewLog(actorId, actorEmail, "CREATE_ORGANIZACION", $"Organizacion:{org.Id}", $"Nombre={nombre}", ip));
        await _db.SaveChangesAsync(ct);
        return new OrganizacionDto(org.Id, org.Nombre, org.Tipo, org.Nit, org.Email, 0, org.CreatedAt, org.Estado);
    }

    public async Task<OrganizacionDto?> CambiarEstadoOrganizacionAsync(Guid orgId, CambiarEstadoOrganizacionRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct)
    {
        var org = await _db.Organizaciones.FirstOrDefaultAsync(o => o.Id == orgId, ct);
        if (org is null) return null;

        org.Estado = req.Estado;
        _db.SuperAdminLogs.Add(NewLog(actorId, actorEmail, "CHANGE_ESTADO_ORGANIZACION", $"Organizacion:{org.Id}", $"Estado={req.Estado}", ip));
        await _db.SaveChangesAsync(ct);

        var count = await _db.Tenants.CountAsync(t => t.OrganizacionId == org.Id, ct);
        return new OrganizacionDto(org.Id, org.Nombre, org.Tipo, org.Nit, org.Email, count, org.CreatedAt, org.Estado);
    }

    public async Task<AdminOrganizacionDto> CrearAdminOrganizacionAsync(Guid orgId, CrearAdminOrganizacionRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct)
    {
        var org = await _db.Organizaciones.FirstOrDefaultAsync(o => o.Id == orgId, ct)
            ?? throw new InvalidOperationException("La organizacion no existe.");

        var nombres = (req.Nombres ?? "").Trim();
        var apellidos = (req.Apellidos ?? "").Trim();
        var documento = (req.Documento ?? "").Trim();
        var email = (req.Email ?? "").Trim();
        if (nombres.Length == 0 || apellidos.Length == 0) throw new InvalidOperationException("Nombres y apellidos son obligatorios.");
        if (documento.Length == 0) throw new InvalidOperationException("El documento es obligatorio.");
        if (email.Length == 0 || !email.Contains('@')) throw new InvalidOperationException("Correo invalido.");
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8) throw new InvalidOperationException("La clave debe tener al menos 8 caracteres.");

        if (await _db.Users.AnyAsync(u => u.Email == email, ct)) throw new InvalidOperationException($"Ya existe un usuario con el correo {email}.");
        if (await _db.Personas.AnyAsync(p => p.TipoDocumento == req.TipoDocumento && p.Documento == documento, ct))
            throw new InvalidOperationException($"Ya existe una persona con el documento {documento}.");

        // 1) Persona (global)
        var persona = new Persona
        {
            TipoDocumento = req.TipoDocumento,
            Documento = documento,
            Nombres = nombres,
            Apellidos = apellidos,
            Email = email,
            PerfilIncompleto = false
        };
        _db.Personas.Add(persona);
        await _db.SaveChangesAsync(ct);

        // 2) ApplicationUser (Identity)
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            PersonaId = persona.Id
        };
        var created = await _userManager.CreateAsync(user, req.Password);
        if (!created.Succeeded)
        {
            _db.Personas.Remove(persona);
            await _db.SaveChangesAsync(ct);
            throw new InvalidOperationException("No se pudo crear el usuario: " + string.Join("; ", created.Errors.Select(e => e.Description)));
        }

        // 3) OrgColaborador con cargo Director (acceso Capa 1). Best-effort: no aborta si falla.
        try
        {
            var directorCargoId = await AsegurarCargoDirectorAsync(orgId, ct);
            if (directorCargoId is { } cargoId)
            {
                _db.OrgColaboradores.Add(new OrgColaborador
                {
                    OrganizacionId = orgId,
                    PersonaId = persona.Id,
                    CargoId = cargoId,
                    Estado = EstadoColaborador.Activo,
                    FechaVinculacion = DateOnly.FromDateTime(DateTime.UtcNow)
                });
                await _db.SaveChangesAsync(ct);
            }
        }
        catch { /* el acceso operativo real lo dan los usuarios_tenant de abajo */ }

        // 4) UsuarioTenant (Administrador) en cada copropiedad de la org, via SQL con set_config (RLS).
        var copropiedades = await _db.Tenants.Where(t => t.OrganizacionId == orgId).Select(t => t.Id).ToListAsync(ct);
        var asignadas = 0;
        if (copropiedades.Count > 0)
        {
            var conn = _db.Database.GetDbConnection();
            var opened = conn.State != System.Data.ConnectionState.Open;
            if (opened) await conn.OpenAsync(ct);
            try
            {
                foreach (var tid in copropiedades)
                {
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT set_config('app.tenant_id', @tid::text, false);
                        INSERT INTO usuarios_tenant (id, tenant_id, persona_id, rol, estado, fecha_activacion, created_at)
                        VALUES (@id, @tid, @pid, 'Administrador', 1, now(), now());";
                    AddParam(cmd, "@tid", tid);
                    AddParam(cmd, "@id", Guid.NewGuid());
                    AddParam(cmd, "@pid", persona.Id);
                    await cmd.ExecuteNonQueryAsync(ct);
                    asignadas++;
                }
            }
            finally { if (opened) await conn.CloseAsync(); }
        }

        _db.SuperAdminLogs.Add(NewLog(actorId, actorEmail, "CREATE_ADMIN_ORGANIZACION", $"Organizacion:{orgId}", $"Email={email}, Copropiedades={asignadas}", ip));
        await _db.SaveChangesAsync(ct);

        return new AdminOrganizacionDto(user.Id, persona.Id, $"{nombres} {apellidos}".Trim(), email, asignadas);
    }

    // Devuelve el cargo "Director" (admin) de la org; crea el catalogo base o el cargo si faltan.
    private async Task<Guid?> AsegurarCargoDirectorAsync(Guid orgId, CancellationToken ct)
    {
        var existing = await _db.OrgCargos
            .Where(c => c.OrganizacionId == orgId && c.Nombre == CargoCatalogoBase.Director)
            .Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);
        if (existing is not null) return existing;

        var hayCargos = await _db.OrgCargos.AnyAsync(c => c.OrganizacionId == orgId, ct);
        if (!hayCargos)
        {
            foreach (var (nombre, permisos) in CargoCatalogoBase.PermisosPorDefecto)
            {
                var cargo = new OrgCargo { OrganizacionId = orgId, Nombre = nombre, EsDefault = true, Activo = true };
                _db.OrgCargos.Add(cargo);
                foreach (var (modulo, nivel) in permisos)
                    _db.OrgCargoPermisos.Add(new OrgCargoPermiso { Cargo = cargo, Modulo = modulo, Nivel = nivel });
            }
            await _db.SaveChangesAsync(ct);
            return await _db.OrgCargos
                .Where(c => c.OrganizacionId == orgId && c.Nombre == CargoCatalogoBase.Director)
                .Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);
        }

        var dir = new OrgCargo { OrganizacionId = orgId, Nombre = CargoCatalogoBase.Director, EsDefault = true, Activo = true };
        _db.OrgCargos.Add(dir);
        foreach (var m in Enum.GetValues<ModuloCapa1>())
            _db.OrgCargoPermisos.Add(new OrgCargoPermiso { Cargo = dir, Modulo = m, Nivel = NivelPermisoCapa1.Completo });
        await _db.SaveChangesAsync(ct);
        return dir.Id;
    }

    private static void AddParam(System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
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
                t.FechaActivacion, t.CodigoCorto))
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
        return new TenantDto(tenant.Id, tenant.Nombre, tenant.Nit, tenant.CodigoPropia, tenant.Estado, tenant.EstadoCustodia, tenant.OrganizacionId, orgNombre, tenant.FechaActivacion, tenant.CodigoCorto);
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
        return new TenantDto(tenant.Id, tenant.Nombre, tenant.Nit, tenant.CodigoPropia, tenant.Estado, tenant.EstadoCustodia, tenant.OrganizacionId, tenant.Organizacion?.Nombre, tenant.FechaActivacion, tenant.CodigoCorto);
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
