using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Directorio;
using Propia.Application.SuperAdmin;
using Propia.Application.UsuariosAccesos;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Directorio;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.SuperAdmin;

/// <summary>
/// Usuarios de CUALQUIER copropiedad desde la API del Super Admin (JWT de Super Admin o token pak_).
/// Patron de AdminAgentService: fija el tenant de la ruta en EF (ITenantContext) Y en la RLS de Postgres
/// (app.tenant_id), y delega en IUsuariosService / IRolesService / IDirectorioService, que comparten este
/// scope. No usa IgnoreQueryFilters para saltar tenants (la RLS lo impediria igual). Lo unico propio de
/// aqui son las validaciones fail-closed que la UI no necesita (rol / persona / usuario de OTRO tenant ->
/// 404) y la auditoria con el Super Admin como actor ("via API").
/// </summary>
public sealed class AdminTenantUsuariosService : IAdminTenantUsuariosService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IUsuariosService _usuarios;
    private readonly IRolesService _roles;
    private readonly IDirectorioService _directorio;
    private readonly AuditoriaAccesoContexto _auditoria;

    public AdminTenantUsuariosService(PropiaDbContext db, ITenantContext tenant, IUsuariosService usuarios,
        IRolesService roles, IDirectorioService directorio, AuditoriaAccesoContexto auditoria)
    {
        _db = db;
        _tenant = tenant;
        _usuarios = usuarios;
        _roles = roles;
        _directorio = directorio;
        _auditoria = auditoria;
    }

    // ===================== Lectura =====================

    public async Task<IReadOnlyList<UsuarioListaDto>> ListarUsuariosAsync(Guid tenantId, EstadoUsuarioTenant? estado, string? q, CancellationToken ct)
    {
        await FijarTenantAsync(tenantId, ct);
        return await _usuarios.ListarUsuariosAsync(estado, q, ct);
    }

    public async Task<UsuarioDetalleDto> ObtenerUsuarioAsync(Guid tenantId, Guid usuarioTenantId, CancellationToken ct)
    {
        await FijarTenantAsync(tenantId, ct);
        return await _usuarios.GetUsuarioDetalleAsync(usuarioTenantId, ct)
            ?? throw new AdminRecursoNoEncontradoException("El usuario no existe en esta copropiedad.");
    }

    public async Task<IReadOnlyList<AdminRolTenantDto>> ListarRolesAsync(Guid tenantId, CancellationToken ct)
    {
        await FijarTenantAsync(tenantId, ct);
        var roles = await _roles.ListarRolesAsync(ct);
        return roles.Select(r => new AdminRolTenantDto(r.Id, r.Nombre, r.Tipo, r.Activo, r.SoloDirectorio)).ToList();
    }

    public async Task<IReadOnlyList<PersonaResumenDto>> BuscarPersonasAsync(Guid tenantId, string? q, CancellationToken ct)
    {
        await FijarTenantAsync(tenantId, ct);
        return await _directorio.ListarPersonasDelTenantAsync(q, ct);
    }

    // ===================== Alta =====================

    public async Task<AdminCrearUsuarioResponse> CrearUsuarioAsync(Guid tenantId, AdminCrearUsuarioRequest req, AdminActor actor, CancellationToken ct)
    {
        await FijarTenantAsync(tenantId, ct);
        MarcarActor(actor);

        if (req.PersonaId is null && req.Persona is null)
            throw new InvalidOperationException("Envia personaId (persona existente) o persona (datos para crearla).");
        if (req.PersonaId is not null && req.Persona is not null)
            throw new InvalidOperationException("Envia personaId o persona, no ambos.");

        // Roles: la alta descarta en silencio los ids que no ve; aqui un rol de otro tenant debe fallar.
        var rolIds = await ValidarRolesAsync(req.RolIds, ct);

        Guid personaId;
        bool reutilizada;
        if (req.PersonaId is { } pid)
        {
            // Persona existente: debe pertenecer a ESTA copropiedad (vinculo de Directorio o de unidad).
            if (await _directorio.ObtenerPersonaAsync(pid, ct) is null)
                throw new AdminRecursoNoEncontradoException("La persona no existe en el Directorio de esta copropiedad.");
            personaId = pid;
            reutilizada = true;
        }
        else
        {
            (personaId, reutilizada) = await ResolverOCrearPersonaAsync(req.Persona!, ct);
        }
        // La invitacion exige vinculo de Directorio (S-02b); idempotente si ya lo tiene.
        await VinculoDirectorio.AsegurarPersonaAsync(_db, _tenant, personaId, ct);

        var cargos = (req.Cargos ?? Array.Empty<string>()).ToList();
        var usuarioTenantId = await _usuarios.CrearUsuarioEnAltaAsync(new CrearUsuarioAltaRequest(personaId, rolIds, cargos), ct);

        AdminInvitacionCreadaDto? invitacion = null;
        if (req.Invitar)
        {
            // Mismo rol que manda la UI: el principal del usuario (primero de la lista o el por defecto).
            var rolPrincipal = await _db.UsuariosTenant.AsNoTracking()
                .Where(u => u.Id == usuarioTenantId).Select(u => u.RolId).FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("El usuario quedo sin rol principal; no se puede invitar.");
            var inv = await _usuarios.InvitarAsync(new CrearInvitacionRequest(personaId, rolPrincipal, req.Canal), ct);
            invitacion = new AdminInvitacionCreadaDto(inv.Id, inv.ExpiraAt, inv.CanalEnvio, inv.LinkAceptacion);
        }

        var estado = await _db.UsuariosTenant.AsNoTracking()
            .Where(u => u.Id == usuarioTenantId).Select(u => u.Estado).FirstAsync(ct);

        LogSuperAdmin(actor, "TENANT_USUARIO_CREAR", tenantId, usuarioTenantId,
            $"persona {personaId} ({(reutilizada ? "reutilizada" : "nueva")}), roles [{string.Join(",", rolIds)}]"
            + (invitacion is null ? "" : $", invitacion {invitacion.Id} por {invitacion.Canal}"));
        await _db.SaveChangesAsync(ct);

        return new AdminCrearUsuarioResponse(usuarioTenantId, personaId, reutilizada, estado, invitacion);
    }

    /// <summary>
    /// Persona por documento y luego por email (globales: una persona existe una sola vez en la plataforma).
    /// Si existe se reutiliza (y el llamador asegura su vinculo con esta copropiedad); si no, se crea con el
    /// mismo servicio que usa SelectorPersona (que ya la deja vinculada al tenant).
    /// </summary>
    private async Task<(Guid PersonaId, bool Reutilizada)> ResolverOCrearPersonaAsync(AdminPersonaNuevaRequest p, CancellationToken ct)
    {
        var documento = (p.Documento ?? "").Trim();
        var email = string.IsNullOrWhiteSpace(p.Email) ? null : p.Email.Trim();

        if (documento.Length > 0)
        {
            var porDoc = await _directorio.BuscarPersonaPorDocumentoAsync(new BuscarPorDocumentoRequest(p.TipoDocumento, documento), ct);
            if (porDoc is not null) return (porDoc.Id, true);
        }
        if (email is not null)
        {
            var emailLower = email.ToLower();
            // Persona es global (sin tenant): la busqueda por email no depende del tenant fijado.
            var porEmail = await _db.Personas.AsNoTracking()
                .Where(x => x.Email != null && x.Email.ToLower() == emailLower)
                .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
            if (porEmail is { } id) return (id, true);
        }

        if (documento.Length == 0)
            throw new InvalidOperationException("Para crear la persona se requiere el documento.");
        var creada = await _directorio.CrearPersonaAsync(new CrearPersonaRequest(
            p.TipoDocumento, documento, p.Nombres?.Trim() ?? "", p.Apellidos?.Trim() ?? "",
            email, string.IsNullOrWhiteSpace(p.Telefono) ? null : p.Telefono.Trim(), null, null), ct);
        return (creada.Id, false);
    }

    // ===================== Gestion =====================

    public async Task ActualizarRolesAsync(Guid tenantId, Guid usuarioTenantId, ActualizarRolesUsuarioRequest req, AdminActor actor, CancellationToken ct)
    {
        await FijarTenantAsync(tenantId, ct);
        MarcarActor(actor);
        var rolIds = await ValidarRolesAsync(req.RolIds, ct);
        if (!await _usuarios.ActualizarRolesAsync(usuarioTenantId, new ActualizarRolesUsuarioRequest(rolIds), ct))
            throw new AdminRecursoNoEncontradoException("El usuario no existe en esta copropiedad.");
        await LogYGuardarAsync(actor, "TENANT_USUARIO_ROLES", tenantId, usuarioTenantId, $"roles [{string.Join(",", rolIds)}]", ct);
    }

    public async Task ActualizarCargosAsync(Guid tenantId, Guid usuarioTenantId, ActualizarCargosUsuarioRequest req, AdminActor actor, CancellationToken ct)
    {
        await FijarTenantAsync(tenantId, ct);
        MarcarActor(actor);
        if (!await _usuarios.ActualizarCargosAsync(usuarioTenantId, new ActualizarCargosUsuarioRequest(req.Cargos ?? Array.Empty<string>()), ct))
            throw new AdminRecursoNoEncontradoException("El usuario no existe en esta copropiedad.");
        await LogYGuardarAsync(actor, "TENANT_USUARIO_CARGOS", tenantId, usuarioTenantId, $"cargos [{string.Join(", ", req.Cargos ?? Array.Empty<string>())}]", ct);
    }

    public async Task RevocarAsync(Guid tenantId, Guid usuarioTenantId, RevocarAccesoRequest req, AdminActor actor, CancellationToken ct)
    {
        await FijarTenantAsync(tenantId, ct);
        MarcarActor(actor);
        if (!await _usuarios.RevocarAccesoAsync(usuarioTenantId, req, ct))
            throw new AdminRecursoNoEncontradoException("El usuario no existe en esta copropiedad.");
        await LogYGuardarAsync(actor, "TENANT_USUARIO_REVOCAR", tenantId, usuarioTenantId, $"motivo: {req.Motivo}", ct);
    }

    public async Task ReactivarAsync(Guid tenantId, Guid usuarioTenantId, AdminActor actor, CancellationToken ct)
    {
        await FijarTenantAsync(tenantId, ct);
        MarcarActor(actor);
        if (!await _usuarios.ReactivarUsuarioAsync(usuarioTenantId, ct))
            throw new AdminRecursoNoEncontradoException("El usuario no existe en esta copropiedad o no esta revocado.");
        await LogYGuardarAsync(actor, "TENANT_USUARIO_REACTIVAR", tenantId, usuarioTenantId, null, ct);
    }

    public async Task ReenviarInvitacionAsync(Guid tenantId, Guid invitacionId, AdminActor actor, CancellationToken ct)
    {
        await FijarTenantAsync(tenantId, ct);
        MarcarActor(actor);
        await ExigirInvitacionAsync(invitacionId, ct);
        if (!await _usuarios.ReenviarInvitacionAsync(invitacionId, ct))
            throw new InvalidOperationException("Solo se reenvia una invitacion pendiente o expirada.");
        await LogYGuardarAsync(actor, "TENANT_INVITACION_REENVIAR", tenantId, invitacionId, null, ct);
    }

    public async Task CancelarInvitacionAsync(Guid tenantId, Guid invitacionId, AdminActor actor, CancellationToken ct)
    {
        await FijarTenantAsync(tenantId, ct);
        MarcarActor(actor);
        await ExigirInvitacionAsync(invitacionId, ct);
        if (!await _usuarios.CancelarInvitacionAsync(invitacionId, ct))
            throw new AdminRecursoNoEncontradoException("La invitacion no existe en esta copropiedad.");
        await LogYGuardarAsync(actor, "TENANT_INVITACION_CANCELAR", tenantId, invitacionId, null, ct);
    }

    // ===================== Helpers =====================

    /// <summary>
    /// Copropiedad existente y activa (si no, 404). Fija el tenant en EF y en la RLS (app.tenant_id),
    /// igual que TenantMiddleware: el token de Super Admin no trae tenant y la RLS quedo en '' (fail-closed).
    /// </summary>
    private async Task FijarTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var activa = await _db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == tenantId && t.Estado == EstadoCopropiedad.Activa, ct);
        if (!activa) throw new AdminRecursoNoEncontradoException("La copropiedad no existe o no esta activa.");
        _tenant.SetTenant(tenantId);
        await _db.Database.ExecuteSqlRawAsync("SELECT set_config('app.tenant_id', {0}, false)", new object[] { tenantId.ToString() }, ct);
    }

    /// <summary>Los eventos que IUsuariosService registre en acceso_auditorias quedan con el Super Admin como actor.</summary>
    private void MarcarActor(AdminActor actor)
    {
        _auditoria.ActorUsuarioId = actor.Id == Guid.Empty ? null : actor.Id;
        _auditoria.Canal = CanalAcceso.Api;
        _auditoria.Marca = $"[via API Super Admin {actor.Email}]";
        _auditoria.Ip = Cortar(actor.Ip, 45);
    }

    /// <summary>
    /// Todos los rolIds deben verse en ESTA copropiedad (base globales + propios del tenant, por query filter y
    /// RLS) y estar activos. Un rol de otro tenant no se ve -> 404 (no se cuela ni cae al rol por defecto).
    /// </summary>
    private async Task<List<Guid>> ValidarRolesAsync(IReadOnlyList<Guid>? rolIds, CancellationToken ct)
    {
        var pedidos = (rolIds ?? Array.Empty<Guid>()).Distinct().ToList();
        if (pedidos.Count == 0) return pedidos;
        var visibles = await _db.RolesCopropiedad.AsNoTracking()
            .Where(r => pedidos.Contains(r.Id)).Select(r => new { r.Id, r.Nombre, r.Activo }).ToListAsync(ct);
        var faltan = pedidos.Where(id => visibles.All(v => v.Id != id)).ToList();
        if (faltan.Count > 0)
            throw new AdminRecursoNoEncontradoException($"Rol no encontrado en esta copropiedad: {string.Join(", ", faltan)}.");
        var inactivo = visibles.FirstOrDefault(v => !v.Activo);
        if (inactivo is not null)
            throw new InvalidOperationException($"El rol \"{inactivo.Nombre}\" no esta activo en esta copropiedad.");
        return pedidos;
    }

    private async Task ExigirInvitacionAsync(Guid invitacionId, CancellationToken ct)
    {
        if (!await _db.UsuarioInvitaciones.AsNoTracking().AnyAsync(i => i.Id == invitacionId, ct))
            throw new AdminRecursoNoEncontradoException("La invitacion no existe en esta copropiedad.");
    }

    private void LogSuperAdmin(AdminActor actor, string accion, Guid tenantId, Guid entidadId, string? detalle)
    {
        var justificacion = string.IsNullOrWhiteSpace(detalle) ? "via API" : $"via API: {detalle}";
        _db.SuperAdminLogs.Add(new SuperAdminLog
        {
            ActorId = actor.Id,
            ActorEmail = Cortar(actor.Email, 200)!,
            Accion = accion,
            EntidadAfectada = $"Tenant:{tenantId}/{entidadId}",
            Justificacion = Cortar(justificacion, 1000),
            Ip = Cortar(actor.Ip, 45),
            UserAgent = Cortar(actor.UserAgent, 500)
        });
    }

    private static string? Cortar(string? s, int max) => s is { } v && v.Length > max ? v[..max] : s;

    private async Task LogYGuardarAsync(AdminActor actor, string accion, Guid tenantId, Guid entidadId, string? detalle, CancellationToken ct)
    {
        LogSuperAdmin(actor, accion, tenantId, entidadId, detalle);
        await _db.SaveChangesAsync(ct);
    }
}
