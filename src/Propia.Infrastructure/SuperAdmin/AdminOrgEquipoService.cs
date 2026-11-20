using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.EquipoOrg;
using Propia.Application.SuperAdmin;
using Propia.Application.UsuariosAccesos;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Directorio;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.SuperAdmin;

/// <summary>
/// Equipo de CUALQUIER organizacion desde la API del Super Admin. Fija la organizacion de la ruta en
/// IEquipoOrgService (que en la consola de la org la saca del JWT) y reutiliza su logica: cargos, alta,
/// historial, asignaciones. Las tablas de Capa 1 (org_*) no tienen RLS; lo que si la tiene (usuarios_tenant,
/// roles propios, directorio) se toca fijando cada copropiedad en EF y en app.tenant_id, una por una.
/// </summary>
public sealed class AdminOrgEquipoService : IAdminOrgEquipoService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IEquipoOrgService _equipo;
    private readonly IUsuariosService _usuarios;

    public AdminOrgEquipoService(PropiaDbContext db, ITenantContext tenant, IEquipoOrgService equipo, IUsuariosService usuarios)
    {
        _db = db;
        _tenant = tenant;
        _equipo = equipo;
        _usuarios = usuarios;
    }

    public async Task<IReadOnlyList<ColaboradorListaDto>> ListarEquipoAsync(Guid orgId, EstadoColaborador? estado, string? q, CancellationToken ct)
    {
        await FijarOrganizacionAsync(orgId, ct);
        return await _equipo.ListarColaboradoresAsync(estado, q, ct);
    }

    public async Task<IReadOnlyList<CargoDto>> ListarCargosAsync(Guid orgId, CancellationToken ct)
    {
        await FijarOrganizacionAsync(orgId, ct);
        return await _equipo.ListarCargosAsync(ct);
    }

    public async Task<AdminColaboradorResultadoDto> AgregarAsync(Guid orgId, AdminAgregarColaboradorRequest req, AdminActor actor, CancellationToken ct)
    {
        await FijarOrganizacionAsync(orgId, ct);
        // Siembra perezosa del catalogo de cargos de la org y validacion del cargo pedido.
        var cargos = await _equipo.ListarCargosAsync(ct);
        if (cargos.All(c => c.Id != req.CargoId))
            throw new AdminRecursoNoEncontradoException("El cargo no existe en esta organizacion.");

        var asignaciones = (req.Asignaciones ?? Array.Empty<AdminAsignacionColaboradorRequest>()).DistinctBy(a => a.TenantId).ToList();
        // Copropiedades de la org y activas, ANTES de crear nada.
        foreach (var a in asignaciones)
        {
            var ok = await _db.Tenants.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(t => t.Id == a.TenantId && t.OrganizacionId == orgId && t.Estado == EstadoCopropiedad.Activa, ct);
            if (!ok) throw new AdminRecursoNoEncontradoException($"La copropiedad {a.TenantId} no existe, no esta activa o no es de esta organizacion.");
        }

        var (persona, advertencias) = await ResolverPersonaAsync(req, ct);

        // Idempotente: si ya es colaborador de la org se devuelve el existente.
        var existente = persona is null ? null
            : await _db.OrgColaboradores.AsNoTracking().FirstOrDefaultAsync(c => c.OrganizacionId == orgId && c.PersonaId == persona.Id, ct);
        Guid colaboradorId;
        if (existente is not null)
        {
            colaboradorId = existente.Id;
            if (existente.Estado == EstadoColaborador.Inactivo)
                advertencias.Add("El colaborador esta inactivo en la organizacion: usa PUT equipo/{id}/reactivar.");
            if (existente.CargoId != req.CargoId)
                advertencias.Add("Ya era colaborador con otro cargo; no se cambio (usa PUT equipo/{id}/cargo).");
        }
        else
        {
            var p = req.Persona;
            var creado = await _equipo.AgregarColaboradorAsync(new AgregarColaboradorRequest(
                persona?.Id,
                persona is null ? p?.TipoDocumento : null,
                persona is null ? p?.Documento?.Trim() : null,
                persona is null ? p?.Nombres?.Trim() : null,
                persona is null ? p?.Apellidos?.Trim() : null,
                persona is null ? p?.Email?.Trim() : null,
                persona is null ? p?.Telefono?.Trim() : null,
                req.CargoId, null, false, null), ct);
            colaboradorId = creado.Id;
        }
        var personaId = persona?.Id ?? await _db.OrgColaboradores.AsNoTracking()
            .Where(c => c.Id == colaboradorId).Select(c => c.PersonaId).FirstAsync(ct);

        var resultados = new List<AdminAsignacionResultadoDto>();
        foreach (var a in asignaciones)
            resultados.Add(await AsignarAsync(orgId, colaboradorId, personaId, a, advertencias, ct));

        _db.SuperAdminLogs.Add(Log(actor, existente is null ? "ORG_EQUIPO_AGREGAR" : "ORG_EQUIPO_AGREGAR_EXISTENTE", orgId, colaboradorId,
            $"persona {personaId}, cargo {req.CargoId}, copropiedades [{string.Join(",", asignaciones.Select(a => a.TenantId))}]"));
        await _db.SaveChangesAsync(ct);

        var detalle = await _equipo.GetColaboradorAsync(colaboradorId, ct)
            ?? throw new InvalidOperationException("No se pudo leer el colaborador.");
        return new AdminColaboradorResultadoDto(detalle, existente is not null, resultados, advertencias.Count > 0 ? advertencias : null);
    }

    /// <summary>
    /// Asignacion de Capa 1 (org_colaborador_copropiedades) + el UsuarioTenant que da el acceso real (la entrada
    /// a una copropiedad sale de usuarios_tenant). Activo si la persona ya tiene cuenta; si no, Pendiente.
    /// </summary>
    private async Task<AdminAsignacionResultadoDto> AsignarAsync(Guid orgId, Guid colaboradorId, Guid personaId,
        AdminAsignacionColaboradorRequest a, List<string> advertencias, CancellationToken ct)
    {
        await FijarTenantAsync(a.TenantId, ct);
        // El rol debe verse en ESA copropiedad (base global o propio del tenant) y estar activo.
        var rol = await _db.RolesCopropiedad.AsNoTracking().FirstOrDefaultAsync(r => r.Id == a.RolId, ct)
            ?? throw new AdminRecursoNoEncontradoException($"Rol no encontrado en la copropiedad {a.TenantId}.");
        if (!rol.Activo) throw new InvalidOperationException($"El rol \"{rol.Nombre}\" no esta activo en la copropiedad {a.TenantId}.");

        var yaAsignado = await _db.OrgColaboradorCopropiedades.AsNoTracking()
            .AnyAsync(x => x.ColaboradorId == colaboradorId && x.TenantId == a.TenantId && x.Activo, ct);
        if (!yaAsignado)
            await _equipo.AgregarAsignacionAsync(colaboradorId, new AgregarAsignacionRequest(a.TenantId, a.RolId), ct);

        await VinculoDirectorio.AsegurarPersonaAsync(_db, _tenant, personaId, ct);
        var tieneCuenta = await _db.Users.AnyAsync(u => u.PersonaId == personaId, ct);
        var ut = await _db.UsuariosTenant.FirstOrDefaultAsync(u => u.PersonaId == personaId, ct);
        var creado = false;
        if (ut is null)
        {
            var utId = await _usuarios.CrearUsuarioEnAltaAsync(new CrearUsuarioAltaRequest(personaId, new[] { a.RolId }, Array.Empty<string>()), ct);
            ut = await _db.UsuariosTenant.FirstAsync(u => u.Id == utId, ct);
            creado = true;
        }
        else
        {
            var tieneRol = await _db.UsuarioTenantRoles.AnyAsync(r => r.UsuarioTenantId == ut.Id && r.RolId == a.RolId, ct);
            if (!tieneRol)
                _db.UsuarioTenantRoles.Add(new UsuarioTenantRol { TenantId = a.TenantId, UsuarioTenantId = ut.Id, RolId = a.RolId });
            if (ut.Estado == EstadoUsuarioTenant.Inactivo)
                advertencias.Add($"El usuario estaba revocado en la copropiedad {a.TenantId}; se reactivo por la asignacion.");
        }
        if (ut.Estado != EstadoUsuarioTenant.Activo)
        {
            ut.Estado = tieneCuenta ? EstadoUsuarioTenant.Activo : EstadoUsuarioTenant.Pendiente;
            if (tieneCuenta) ut.FechaActivacion = DateTimeOffset.UtcNow;
            ut.FechaRevocacion = null;
            ut.MotivoRevocacion = null;
        }
        await _db.SaveChangesAsync(ct);
        if (!tieneCuenta)
            advertencias.Add($"Sin cuenta todavia: en la copropiedad {a.TenantId} queda Pendiente; usa activar-con-clave-temporal o una invitacion.");
        return new AdminAsignacionResultadoDto(a.TenantId, ut.Id, ut.Estado, creado);
    }

    public async Task CambiarCargoAsync(Guid orgId, Guid colaboradorId, AdminCambiarCargoRequest req, AdminActor actor, CancellationToken ct)
    {
        await FijarOrganizacionAsync(orgId, ct);
        if (!await _equipo.CambiarCargoAsync(colaboradorId, new CambiarCargoRequest(req.CargoId, req.ResetearPermisos), ct))
            throw new AdminRecursoNoEncontradoException("El colaborador no existe en esta organizacion.");
        await LogYGuardarAsync(actor, "ORG_EQUIPO_CARGO", orgId, colaboradorId, $"cargo {req.CargoId}", ct);
    }

    public async Task DesactivarAsync(Guid orgId, Guid colaboradorId, AdminDesactivarColaboradorRequest req, AdminActor actor, CancellationToken ct)
    {
        await FijarOrganizacionAsync(orgId, ct);
        if (!await _equipo.DesactivarColaboradorAsync(colaboradorId, new DesactivarColaboradorRequest(req.Motivo, null), ct))
            throw new AdminRecursoNoEncontradoException("El colaborador no existe en esta organizacion.");
        await LogYGuardarAsync(actor, "ORG_EQUIPO_DESACTIVAR", orgId, colaboradorId, req.Motivo, ct);
    }

    public async Task ReactivarAsync(Guid orgId, Guid colaboradorId, AdminActor actor, CancellationToken ct)
    {
        await FijarOrganizacionAsync(orgId, ct);
        if (!await _equipo.ReactivarColaboradorAsync(colaboradorId, ct))
            throw new AdminRecursoNoEncontradoException("El colaborador no existe en esta organizacion o no esta inactivo.");
        await LogYGuardarAsync(actor, "ORG_EQUIPO_REACTIVAR", orgId, colaboradorId, null, ct);
    }

    // ===================== Helpers =====================

    /// <summary>
    /// Persona global por personaId, tipo+documento o datos (documento y luego correo). Null = no existe y se
    /// creara con los datos de req.Persona. Lo enviado que difiere de lo registrado no se sobrescribe: se avisa.
    /// </summary>
    private async Task<(Persona? Persona, List<string> Advertencias)> ResolverPersonaAsync(AdminAgregarColaboradorRequest req, CancellationToken ct)
    {
        var avisos = new List<string>();
        if (req.PersonaId is { } pid)
        {
            var p = await _db.Personas.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(x => x.Id == pid, ct)
                ?? throw new AdminRecursoNoEncontradoException("La persona no existe.");
            return (p, avisos);
        }

        var tipo = req.TipoDocumento ?? req.Persona?.TipoDocumento;
        var doc = (req.Documento ?? req.Persona?.Documento ?? "").Trim();
        Persona? encontrada = null;
        if (tipo is { } t && doc.Length > 0)
            encontrada = await _db.Personas.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(x => x.TipoDocumento == t && x.Documento == doc, ct);
        var email = (req.Persona?.Email ?? "").Trim();
        if (encontrada is null && email.Length > 0)
        {
            var lower = email.ToLower();
            encontrada = await _db.Personas.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(x => x.Email != null && x.Email.ToLower() == lower, ct);
        }

        if (encontrada is null)
        {
            if (req.Persona is null)
                throw new AdminRecursoNoEncontradoException("No existe una persona con ese documento. Envia persona {...} para crearla.");
            return (null, avisos);
        }
        if (email.Length > 0 && !string.Equals(email, encontrada.Email, StringComparison.OrdinalIgnoreCase))
            avisos.Add(encontrada.Email is null
                ? "La persona ya existia sin correo; el correo enviado no se guardo (usa PUT admin/tenants/{t}/personas/{id})."
                : $"El correo enviado difiere del registrado ({Propia.Application.Auth.ClaveTemporal.EnmascararCorreo(encontrada.Email)}); no se cambio.");
        return (encontrada, avisos);
    }

    private async Task FijarOrganizacionAsync(Guid orgId, CancellationToken ct)
    {
        if (!await _db.Organizaciones.AsNoTracking().AnyAsync(o => o.Id == orgId, ct))
            throw new AdminRecursoNoEncontradoException("La organizacion no existe.");
        _equipo.FijarOrganizacion(orgId);
    }

    /// <summary>Fija la copropiedad en EF y en la RLS (app.tenant_id), como TenantMiddleware.</summary>
    private async Task FijarTenantAsync(Guid tenantId, CancellationToken ct)
    {
        _tenant.SetTenant(tenantId);
        await _db.Database.ExecuteSqlRawAsync("SELECT set_config('app.tenant_id', {0}, false)", new object[] { tenantId.ToString() }, ct);
    }

    private static SuperAdminLog Log(AdminActor actor, string accion, Guid orgId, Guid entidadId, string? detalle) => new()
    {
        ActorId = actor.Id,
        ActorEmail = Cortar(actor.Email, 200)!,
        Accion = accion,
        EntidadAfectada = $"Organizacion:{orgId}/{entidadId}",
        Justificacion = Cortar(string.IsNullOrWhiteSpace(detalle) ? "via API" : $"via API: {detalle}", 1000),
        Ip = Cortar(actor.Ip, 45),
        UserAgent = Cortar(actor.UserAgent, 500)
    };

    private static string? Cortar(string? s, int max) => s is { } v && v.Length > max ? v[..max] : s;

    private async Task LogYGuardarAsync(AdminActor actor, string accion, Guid orgId, Guid entidadId, string? detalle, CancellationToken ct)
    {
        _db.SuperAdminLogs.Add(Log(actor, accion, orgId, entidadId, detalle));
        await _db.SaveChangesAsync(ct);
    }
}
