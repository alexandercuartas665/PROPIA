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
    private readonly ISeedUsuarioRolService _seed;

    public RolesService(PropiaDbContext db, ITenantContext tenantContext, ISeedUsuarioRolService seed)
    {
        _db = db;
        _tenantContext = tenantContext;
        _seed = seed;
    }

    public async Task<IReadOnlyList<RolDto>> ListarRolesAsync(CancellationToken ct)
    {
        // Garantiza que existan los roles base (globales) con su matriz por defecto y que los
        // usuarios de la copropiedad activa esten linkeados a su rol (idempotente).
        await EnsureRolesBaseAsync(ct);

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

        // Config de siembra: override por copropiedad (aplica a Base/Extendido/Personalizado).
        var overrides = await _db.RolesSemillaTenant.AsNoTracking()
            .ToDictionaryAsync(x => x.RolId, x => x, ct);

        return roles.Select(r =>
        {
            overrides.TryGetValue(r.Id, out var ov);
            return new RolDto(
                r.Id, r.TenantId, r.Nombre, r.Descripcion,
                r.Tipo, r.Categoria, r.EsEliminable, r.Activo,
                conteos.GetValueOrDefault(r.Id, 0),
                ov?.FacetasSemilla, ov?.SoloDirectorio ?? false);
        }).ToList();
    }

    public async Task<RolDetalleDto?> GetRolDetalleAsync(Guid rolId, CancellationToken ct)
    {
        var r = await _db.RolesCopropiedad.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rolId, ct);
        if (r is null) return null;

        // Matriz EFECTIVA en la copropiedad activa (default global + override por tenant si el rol
        // es global). La Ficha muestra/edita la matriz de ESTA copropiedad, no la global.
        var efectiva = await MatrizEfectivaAsync(r, ct);

        var matriz = new List<PermisoMatrizDto>();
        foreach (var mod in ModuloCodigo.Todos)
        {
            foreach (var acc in Enum.GetValues<AccionPermiso>())
            {
                var (hab, niv) = efectiva.TryGetValue((mod, acc), out var v) ? v : (false, NivelDato.SinAcceso);
                matriz.Add(new PermisoMatrizDto(mod, acc, hab, niv));
            }
        }

        var cuenta = await _db.UsuariosTenant
            .CountAsync(u => u.RolId == rolId && u.Estado == EstadoUsuarioTenant.Activo, ct);

        var ov = await _db.RolesSemillaTenant.AsNoTracking().FirstOrDefaultAsync(x => x.RolId == rolId, ct);

        return new RolDetalleDto(r.Id, r.TenantId, r.Nombre, r.Descripcion,
            r.Tipo, r.Categoria, r.EsEliminable, r.Activo, cuenta, matriz,
            ov?.FacetasSemilla, ov?.SoloDirectorio ?? false);
    }

    /// <summary>
    /// Matriz EFECTIVA (solo celdas con registro) de un rol en la copropiedad ACTIVA:
    /// rol GLOBAL (TenantId null) = default de rol_permisos, sobrescrito celda a celda por el override
    /// del tenant (rol_permisos_tenant, ya filtrado por RLS/query filter); rol PERSONALIZADO
    /// (tenant-scoped) = rol_permisos directo (sin overrides). Es la clave del aislamiento por tenant.
    /// </summary>
    private async Task<Dictionary<(string Modulo, AccionPermiso Accion), (bool Habilitado, NivelDato Nivel)>> MatrizEfectivaAsync(Rol rol, CancellationToken ct)
    {
        var map = new Dictionary<(string, AccionPermiso), (bool, NivelDato)>();
        var defaults = await _db.RolPermisos.AsNoTracking().Where(p => p.RolId == rol.Id).ToListAsync(ct);
        foreach (var p in defaults) map[(p.ModuloCodigo, p.Accion)] = (p.Habilitado, p.NivelDato);
        if (rol.TenantId is null)
        {
            var ov = await _db.RolPermisosTenant.AsNoTracking().Where(p => p.RolId == rol.Id).ToListAsync(ct);
            foreach (var p in ov) map[(p.ModuloCodigo, p.Accion)] = (p.Habilitado, p.NivelDato);
        }
        return map;
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
            Categoria = req.Categoria,
            Tipo = TipoRol.Personalizado,
            EsEliminable = true,
            Activo = true,
            CopiadoDeRolId = req.CopiarDeRolId
        };
        _db.RolesCopropiedad.Add(rol);
        await _db.SaveChangesAsync(ct);

        if (req.CopiarDeRolId.HasValue)
        {
            // Duplicar copia la matriz EFECTIVA del origen en esta copropiedad (default global +
            // override del tenant si el origen es global), no solo el default global (RP-02).
            var origen = await _db.RolesCopropiedad.AsNoTracking().FirstOrDefaultAsync(x => x.Id == req.CopiarDeRolId.Value, ct);
            if (origen is not null)
            {
                var efectiva = await MatrizEfectivaAsync(origen, ct);
                foreach (var kv in efectiva)
                {
                    _db.RolPermisos.Add(new RolPermiso
                    {
                        RolId = rol.Id,
                        ModuloCodigo = kv.Key.Modulo,
                        Accion = kv.Key.Accion,
                        Habilitado = kv.Value.Habilitado,
                        NivelDato = kv.Value.Nivel
                    });
                }
                await _db.SaveChangesAsync(ct);
            }
        }

        return new RolDto(rol.Id, rol.TenantId, rol.Nombre, rol.Descripcion,
            rol.Tipo, rol.Categoria, rol.EsEliminable, rol.Activo, 0);
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
        // La categoria de los roles GLOBALES (base/extendido) es fija (definida en la semilla) para no
        // mutarla entre copropiedades; solo los personalizados (tenant-scoped) la editan.
        if (rol.TenantId is not null) rol.Categoria = req.Categoria;

        // Config de siembra: se guarda en el OVERRIDE por copropiedad (RolSemillaTenant), asi
        // aplica a cualquier tipo de rol (incluidos Base/Extendido globales) sin filtrarse entre
        // copropiedades. Requiere tenant activo.
        var tenantId = _tenantContext.CurrentTenantId
            ?? throw new InvalidOperationException("Sin tenant activo.");

        var facetasNuevas = ParseFacetas(req.FacetasSemilla).Distinct().Where(n => n >= 1 && n <= 5).ToList();
        var facetasCsv = facetasNuevas.Count == 0 ? null : string.Join(",", facetasNuevas);

        var ov = await _db.RolesSemillaTenant.FirstOrDefaultAsync(x => x.RolId == rolId, ct);
        if (ov is null)
        {
            ov = new RolSemillaTenant { TenantId = tenantId, RolId = rolId };
            _db.RolesSemillaTenant.Add(ov);
        }
        ov.FacetasSemilla = facetasCsv;
        ov.SoloDirectorio = req.SoloDirectorio;

        // Exclusividad: una faceta pertenece a un solo rol. Se la quito a los demas overrides del tenant.
        if (facetasNuevas.Count > 0)
        {
            var otros = await _db.RolesSemillaTenant
                .Where(x => x.RolId != rolId && x.FacetasSemilla != null)
                .ToListAsync(ct);
            foreach (var o in otros)
            {
                var restantes = ParseFacetas(o.FacetasSemilla).Where(f => !facetasNuevas.Contains(f)).ToList();
                o.FacetasSemilla = restantes.Count == 0 ? null : string.Join(",", restantes);
            }
        }

        await _db.SaveChangesAsync(ct);

        // Backfill retroactivo inmediato de las facetas sembradas.
        if (facetasNuevas.Count > 0)
            await _seed.BackfillFacetasAsync(facetasNuevas, ct);

        return true;
    }

    private static IEnumerable<int> ParseFacetas(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) yield break;
        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (int.TryParse(part, out var n)) yield return n;
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

        if (rol.TenantId is null)
        {
            // Rol GLOBAL (base/extendido): el cambio se guarda como OVERRIDE de la copropiedad activa
            // (rol_permisos_tenant, RLS). NO muta el default global ni afecta a otras copropiedades
            // (RP-04/RN-07). TenantId lo asigna SaveChanges (TenantEntity); se fija explicito por claridad.
            var tenantId = _tenantContext.CurrentTenantId
                ?? throw new InvalidOperationException("Sin tenant activo.");
            var ov = await _db.RolPermisosTenant
                .FirstOrDefaultAsync(p => p.RolId == rolId && p.ModuloCodigo == req.ModuloCodigo && p.Accion == req.Accion, ct);
            if (ov is null)
            {
                _db.RolPermisosTenant.Add(new RolPermisoTenant
                {
                    TenantId = tenantId,
                    RolId = rolId,
                    ModuloCodigo = req.ModuloCodigo,
                    Accion = req.Accion,
                    Habilitado = req.Habilitado,
                    NivelDato = req.NivelDato
                });
            }
            else
            {
                ov.Habilitado = req.Habilitado;
                ov.NivelDato = req.NivelDato;
            }
        }
        else
        {
            // Rol PERSONALIZADO (tenant-scoped): matriz directa en rol_permisos (aislada por su RolId).
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

    public async Task<IReadOnlyList<PermisoMatrizDto>> GetPermisosEfectivosAsync(Guid personaId, CancellationToken ct, Guid? rolActivoId = null)
    {
        var tenantId = _tenantContext.CurrentTenantId;
        if (tenantId is null) return Array.Empty<PermisoMatrizDto>();

        var ut = await _db.UsuariosTenant
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.PersonaId == personaId && u.Estado == EstadoUsuarioTenant.Activo, ct);
        if (ut is null) return Array.Empty<PermisoMatrizDto>();

        var rolId = await ResolverRolActivoIdAsync(ut, rolActivoId, ct);
        if (rolId is null) return Array.Empty<PermisoMatrizDto>();

        var rol = await _db.RolesCopropiedad.AsNoTracking().FirstOrDefaultAsync(r => r.Id == rolId, ct);
        if (rol is null) return Array.Empty<PermisoMatrizDto>();

        // Permisos EFECTIVOS = matriz efectiva (default global + override del tenant para roles
        // globales), quedandonos con las celdas habilitadas. Asi el [RequierePermiso] respeta el
        // override por copropiedad sin mutar el default global.
        var efectiva = await MatrizEfectivaAsync(rol, ct);
        return efectiva
            .Where(kv => kv.Value.Habilitado)
            .Select(kv => new PermisoMatrizDto(kv.Key.Modulo, kv.Key.Accion, true, kv.Value.Nivel))
            .ToList();
    }

    public async Task<string?> GetRolActorAsync(Guid personaId, CancellationToken ct, Guid? rolActivoId = null)
    {
        // El query filter / RLS limitan a la copropiedad activa.
        var ut = await _db.UsuariosTenant
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.PersonaId == personaId && u.Estado == EstadoUsuarioTenant.Activo, ct);
        if (ut is null) return null;

        var rolId = await ResolverRolActivoIdAsync(ut, rolActivoId, ct);
        if (rolId is null) return ut.Rol;  // sin rol activo valido -> nombre del principal (compat)

        return await _db.RolesCopropiedad.AsNoTracking()
            .Where(r => r.Id == rolId).Select(r => r.Nombre).FirstOrDefaultAsync(ct) ?? ut.Rol;
    }

    /// <summary>
    /// Resuelve el RolId con el que autorizar (2.5 v2.0 rol activo por sesion):
    /// - Si <paramref name="rolActivoId"/> viene y pertenece al usuario en esta copropiedad (via
    ///   usuario_tenant_roles, o es el rol principal) -> ese rol.
    /// - Si viene pero NO le pertenece -> null (deniega; no se cae al principal para no escalar).
    /// - Si no viene -> el rol principal del vinculo (compat tokens viejos / usuarios mono-rol).
    /// </summary>
    private async Task<Guid?> ResolverRolActivoIdAsync(UsuarioTenant ut, Guid? rolActivoId, CancellationToken ct)
    {
        if (rolActivoId is not Guid pedido) return ut.RolId;
        if (ut.RolId == pedido) return pedido;
        var pertenece = await _db.UsuarioTenantRoles.AsNoTracking()
            .AnyAsync(utr => utr.UsuarioTenantId == ut.Id && utr.RolId == pedido, ct);
        return pertenece ? pedido : (Guid?)null;
    }

    // Nombres canonicos del catalogo de roles base (sembrado por la migracion
    // AddUsuariosRolesModulo25). Mapea nombres legacy de seeds antiguos.
    private static string CanonRol(string rol) => rol switch
    {
        "Consejo" => "Consejero",
        "Personal Interno" => "Operario",
        _ => rol
    };

    public async Task EnsureRolesBaseAsync(CancellationToken ct)
    {
        // El catalogo de roles base/extendidos es global (tenant_id NULL) y lo siembra la
        // migracion. Aqui SOLO linkeamos los usuarios de la copropiedad activa a su rol por
        // nombre (idempotente) - no creamos roles para no duplicar.
        var sinRol = await _db.UsuariosTenant.Where(u => u.RolId == null).ToListAsync(ct);
        if (sinRol.Count == 0) return;

        var roles = await _db.RolesCopropiedad.Where(r => r.Tipo == TipoRol.Base).ToListAsync(ct);
        var linked = false;
        foreach (var ut in sinRol)
        {
            var rol = roles.FirstOrDefault(x => x.Nombre == CanonRol(ut.Rol));
            if (rol != null) { ut.RolId = rol.Id; linked = true; }
        }
        if (linked) await _db.SaveChangesAsync(ct);
    }
}
