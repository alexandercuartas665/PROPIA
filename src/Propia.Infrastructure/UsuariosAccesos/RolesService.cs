using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.UsuariosAccesos;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.UsuariosAccesos;

/// <summary>
/// Servicio de roles y matriz de permisos (spec 2.5 v1.0). La fuente de verdad de
/// permisos es <c>rol_permisos</c>: los modulos consultan via <see cref="GetPermisosEfectivosAsync"/>.
/// </summary>
public class RolesService : IRolesService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;

    public RolesService(PropiaDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<RolDto>> ListarRolesAsync(CancellationToken ct)
    {
        var roles = await _db.RolesCopropiedad
            .AsNoTracking()
            .OrderBy(r => r.Tipo).ThenBy(r => r.Nombre)
            .ToListAsync(ct);

        // Conteo de usuarios activos por rol
        var conteos = await _db.UsuariosTenant
            .Where(u => u.RolId != null && u.Estado == EstadoUsuarioTenant.Activo)
            .GroupBy(u => u.RolId)
            .Select(g => new { RolId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RolId!.Value, x => x.Count, ct);

        return roles.Select(r => new RolDto(
            r.Id, r.TenantId, r.Nombre, r.Descripcion,
            r.Tipo, r.EsEliminable, r.Activo,
            conteos.GetValueOrDefault(r.Id, 0))).ToList();
    }

    public async Task<RolDetalleDto?> GetRolDetalleAsync(Guid rolId, CancellationToken ct)
    {
        var r = await _db.RolesCopropiedad.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rolId, ct);
        if (r is null) return null;

        var permisos = await _db.RolPermisos
            .AsNoTracking()
            .Where(p => p.RolId == rolId)
            .Select(p => new PermisoMatrizDto(p.ModuloCodigo, p.Accion, p.Habilitado, p.NivelDato))
            .ToListAsync(ct);

        // Completar la matriz: para cada (modulo, accion) que no tenga registro, devolver SinAcceso
        var matriz = new List<PermisoMatrizDto>();
        foreach (var mod in ModuloCodigo.Todos)
        {
            foreach (var acc in Enum.GetValues<AccionPermiso>())
            {
                var existente = permisos.FirstOrDefault(p => p.ModuloCodigo == mod && p.Accion == acc);
                matriz.Add(existente ?? new PermisoMatrizDto(mod, acc, false, NivelDato.SinAcceso));
            }
        }

        var cuenta = await _db.UsuariosTenant
            .CountAsync(u => u.RolId == rolId && u.Estado == EstadoUsuarioTenant.Activo, ct);

        return new RolDetalleDto(r.Id, r.TenantId, r.Nombre, r.Descripcion,
            r.Tipo, r.EsEliminable, r.Activo, cuenta, matriz);
    }

    public async Task<RolDto> CrearRolAsync(CrearRolRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre obligatorio.");
        var tenantId = _tenantContext.CurrentTenantId
            ?? throw new InvalidOperationException("Sin tenant activo.");
        var nombre = req.Nombre.Trim();
        if (await _db.RolesCopropiedad.AnyAsync(r => r.Nombre == nombre, ct))
            throw new InvalidOperationException($"Ya existe un rol '{nombre}' en esta copropiedad.");

        var rol = new Rol
        {
            TenantId = tenantId,
            Nombre = nombre,
            Descripcion = req.Descripcion,
            Tipo = TipoRol.Personalizado,
            EsEliminable = true,
            Activo = true,
            CopiadoDeRolId = req.CopiarDeRolId
        };
        _db.RolesCopropiedad.Add(rol);
        await _db.SaveChangesAsync(ct);

        if (req.CopiarDeRolId.HasValue)
        {
            var permisosOrigen = await _db.RolPermisos
                .Where(p => p.RolId == req.CopiarDeRolId.Value)
                .ToListAsync(ct);
            foreach (var po in permisosOrigen)
            {
                _db.RolPermisos.Add(new RolPermiso
                {
                    RolId = rol.Id,
                    ModuloCodigo = po.ModuloCodigo,
                    Accion = po.Accion,
                    Habilitado = po.Habilitado,
                    NivelDato = po.NivelDato
                });
            }
            await _db.SaveChangesAsync(ct);
        }

        return new RolDto(rol.Id, rol.TenantId, rol.Nombre, rol.Descripcion,
            rol.Tipo, rol.EsEliminable, rol.Activo, 0);
    }

    public async Task<bool> ActualizarRolAsync(Guid rolId, ActualizarRolRequest req, CancellationToken ct)
    {
        var rol = await _db.RolesCopropiedad.FirstOrDefaultAsync(r => r.Id == rolId, ct);
        if (rol is null) return false;
        if (rol.Tipo == TipoRol.Base && rol.Nombre != req.Nombre)
            throw new InvalidOperationException("Los roles base no se pueden renombrar (RN-03).");
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre obligatorio.");

        rol.Nombre = req.Nombre.Trim();
        rol.Descripcion = req.Descripcion;
        rol.Activo = req.Activo;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarRolAsync(Guid rolId, CancellationToken ct)
    {
        var rol = await _db.RolesCopropiedad.FirstOrDefaultAsync(r => r.Id == rolId, ct);
        if (rol is null) return false;
        if (!rol.EsEliminable) throw new InvalidOperationException("Este rol no es eliminable. Solo puedes inactivarlo.");

        // RN-15: no se puede eliminar si tiene usuarios activos
        var enUso = await _db.UsuariosTenant
            .AnyAsync(u => u.RolId == rolId && u.Estado == EstadoUsuarioTenant.Activo, ct);
        if (enUso)
            throw new InvalidOperationException("El rol tiene usuarios activos asignados. Reasignalos antes de eliminar (RN-15).");

        _db.RolesCopropiedad.Remove(rol);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ActualizarPermisoAsync(Guid rolId, ActualizarPermisoRequest req, CancellationToken ct)
    {
        var rol = await _db.RolesCopropiedad.FirstOrDefaultAsync(r => r.Id == rolId, ct);
        if (rol is null) return false;

        // Administrador: el permiso "Gestionar usuarios y roles" siempre habilitado (Notas dev spec 2.5)
        if (rol.Nombre == "Administrador" && req.ModuloCodigo == ModuloCodigo.UsuariosAccesos && !req.Habilitado)
            throw new InvalidOperationException("El Administrador siempre conserva acceso a Usuarios y Accesos (regla de seguridad).");

        var existente = await _db.RolPermisos
            .FirstOrDefaultAsync(p => p.RolId == rolId && p.ModuloCodigo == req.ModuloCodigo && p.Accion == req.Accion, ct);
        if (existente is null)
        {
            _db.RolPermisos.Add(new RolPermiso
            {
                RolId = rolId,
                ModuloCodigo = req.ModuloCodigo,
                Accion = req.Accion,
                Habilitado = req.Habilitado,
                NivelDato = req.NivelDato
            });
        }
        else
        {
            existente.Habilitado = req.Habilitado;
            existente.NivelDato = req.NivelDato;
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ActivarRolExtendidoAsync(Guid rolId, CancellationToken ct)
    {
        var rol = await _db.RolesCopropiedad.FirstOrDefaultAsync(r => r.Id == rolId, ct);
        if (rol is null) return false;
        if (rol.Tipo != TipoRol.Extendido) throw new InvalidOperationException("Solo roles extendidos se activan/desactivan.");
        rol.Activo = !rol.Activo;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<PermisoMatrizDto>> GetPermisosEfectivosAsync(Guid personaId, CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentTenantId;
        if (tenantId is null) return Array.Empty<PermisoMatrizDto>();

        var ut = await _db.UsuariosTenant
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.PersonaId == personaId && u.Estado == EstadoUsuarioTenant.Activo, ct);
        if (ut?.RolId is null) return Array.Empty<PermisoMatrizDto>();

        return await _db.RolPermisos
            .AsNoTracking()
            .Where(p => p.RolId == ut.RolId && p.Habilitado)
            .Select(p => new PermisoMatrizDto(p.ModuloCodigo, p.Accion, p.Habilitado, p.NivelDato))
            .ToListAsync(ct);
    }
}
