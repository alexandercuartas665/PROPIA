using Microsoft.AspNetCore.Identity;
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
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminTenantUsuariosService(PropiaDbContext db, ITenantContext tenant, IUsuariosService usuarios,
        IRolesService roles, IDirectorioService directorio, AuditoriaAccesoContexto auditoria,
        UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
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
        var lista = await _directorio.ListarPersonasDelTenantAsync(q, ct);
        // Documento escrito con puntos, espacios o guiones ("1.020.304"): se busca tambien solo por digitos.
        var digitos = Digitos(q);
        if (digitos.Length >= 4 && digitos != (q ?? "").Trim())
            lista = lista.Concat(await _directorio.ListarPersonasDelTenantAsync(digitos, ct)).DistinctBy(p => p.Id).ToList();
        return lista;
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
        var conClave = ModoAcceso(req.Acceso) == AdminModoAcceso.ClaveTemporal;

        // Roles: la alta descarta en silencio los ids que no ve; aqui un rol de otro tenant debe fallar.
        var rolIds = await ValidarRolesAsync(req.RolIds, ct);

        // 1) Resolver la persona SIN crear nada todavia (para validar el acceso antes de tocar datos).
        Persona? existente;
        if (req.PersonaId is { } pid)
        {
            // Persona existente: debe pertenecer a ESTA copropiedad (vinculo de Directorio o de unidad).
            if (await _directorio.ObtenerPersonaAsync(pid, ct) is null)
                throw new AdminRecursoNoEncontradoException("La persona no existe en el Directorio de esta copropiedad.");
            existente = await _db.Personas.IgnoreQueryFilters().AsNoTracking().FirstAsync(p => p.Id == pid, ct);
        }
        else
        {
            existente = await BuscarPersonaAsync(req.Persona!, ct);
        }
        var advertencias = existente is not null && req.Persona is not null
            ? AdvertenciasDatosDistintos(req.Persona, existente)
            : new List<string>();

        // 2) Si ya es usuario ACTIVO aqui: alta repetida (200), sin tocar roles, cargos ni su clave.
        var utPrevio = existente is null ? null
            : await _db.UsuariosTenant.AsNoTracking().FirstOrDefaultAsync(u => u.PersonaId == existente.Id, ct);
        if (utPrevio is { Estado: EstadoUsuarioTenant.Activo })
        {
            advertencias.Add("El usuario ya tenía acceso activo en esta copropiedad: no se cambiaron sus roles, cargos ni su contraseña "
                + "(usa PUT roles / cargos o activar-con-clave-temporal).");
            LogSuperAdmin(actor, "TENANT_USUARIO_CREAR", tenantId, utPrevio.Id, $"persona {existente!.Id} ya era usuario activo (sin cambios)");
            await _db.SaveChangesAsync(ct);
            return new AdminCrearUsuarioResponse(utPrevio.Id, existente.Id, true, utPrevio.Estado, null,
                YaExistia: true, Advertencias: advertencias);
        }

        // 3) Clave temporal: correo de login y canal entregable ANTES de crear la persona o el usuario.
        if (conClave)
        {
            var email = existente is not null ? existente.Email : req.Persona?.Email;
            var tel = existente is not null ? existente.Telefono : req.Persona?.Telefono;
            await _usuarios.ValidarEntregaAccesoAsync(email, tel, req.Canal, ct);
        }

        Guid personaId;
        bool reutilizada = existente is not null;
        if (existente is not null) personaId = existente.Id;
        else personaId = await CrearPersonaAsync(req.Persona!, ct);
        // La invitacion exige vinculo de Directorio (S-02b); idempotente si ya lo tiene.
        await VinculoDirectorio.AsegurarPersonaAsync(_db, _tenant, personaId, ct);

        var cargos = (req.Cargos ?? Array.Empty<string>()).ToList();
        var usuarioTenantId = await _usuarios.CrearUsuarioEnAltaAsync(new CrearUsuarioAltaRequest(personaId, rolIds, cargos), ct);

        AdminInvitacionCreadaDto? invitacion = null;
        AccesoEntregadoDto? acceso = null;
        if (conClave)
        {
            acceso = await _usuarios.ActivarConClaveTemporalAsync(usuarioTenantId, req.Canal, ct);
        }
        else if (req.Invitar)
        {
            // Mismo rol que manda la UI: el principal del usuario (primero de la lista o el por defecto).
            var rolPrincipal = await _db.UsuariosTenant.AsNoTracking()
                .Where(u => u.Id == usuarioTenantId).Select(u => u.RolId).FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("El usuario quedo sin rol principal; no se puede invitar.");
            var inv = await _usuarios.InvitarAsync(new CrearInvitacionRequest(personaId, rolPrincipal, req.Canal), ct);
            invitacion = new AdminInvitacionCreadaDto(inv.Id, inv.ExpiraAt, inv.CanalEnvio, inv.LinkAceptacion, inv.Envio);
        }

        var estado = await _db.UsuariosTenant.AsNoTracking()
            .Where(u => u.Id == usuarioTenantId).Select(u => u.Estado).FirstAsync(ct);

        LogSuperAdmin(actor, "TENANT_USUARIO_CREAR", tenantId, usuarioTenantId,
            $"persona {personaId} ({(reutilizada ? "reutilizada" : "nueva")}), roles [{string.Join(",", rolIds)}]"
            + (invitacion is null ? "" : $", invitacion {invitacion.Id} por {invitacion.Canal}")
            + (acceso is null ? "" : $", clave temporal modo {acceso.Modo} entregada por [{string.Join(",", acceso.EntregadoPor)}]"));
        await _db.SaveChangesAsync(ct);

        return new AdminCrearUsuarioResponse(usuarioTenantId, personaId, reutilizada, estado, invitacion,
            YaExistia: utPrevio is not null,
            Advertencias: advertencias.Count > 0 ? advertencias : null,
            AccesoEntregado: acceso);
    }

    private static string ModoAcceso(string? acceso)
    {
        var a = (acceso ?? "").Trim().ToLowerInvariant();
        if (a.Length == 0 || a == AdminModoAcceso.Invitacion) return AdminModoAcceso.Invitacion;
        if (a == AdminModoAcceso.ClaveTemporal) return AdminModoAcceso.ClaveTemporal;
        throw new InvalidOperationException($"acceso debe ser \"{AdminModoAcceso.Invitacion}\" o \"{AdminModoAcceso.ClaveTemporal}\".");
    }

    /// <summary>
    /// Persona existente por documento y luego por email (globales: una persona existe una sola vez en la
    /// plataforma). Null si no existe: el llamador la crea despues de validar.
    /// </summary>
    private async Task<Persona?> BuscarPersonaAsync(AdminPersonaNuevaRequest p, CancellationToken ct)
    {
        var documento = (p.Documento ?? "").Trim();
        var email = string.IsNullOrWhiteSpace(p.Email) ? null : p.Email.Trim();

        if (documento.Length > 0)
        {
            var porDoc = await _directorio.BuscarPersonaPorDocumentoAsync(new BuscarPorDocumentoRequest(p.TipoDocumento, documento), ct);
            if (porDoc is not null)
                return await _db.Personas.IgnoreQueryFilters().AsNoTracking().FirstAsync(x => x.Id == porDoc.Id, ct);
        }
        if (email is not null)
        {
            var emailLower = email.ToLower();
            // Persona es global (sin tenant): la busqueda por email no depende del tenant fijado.
            return await _db.Personas.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(x => x.Email != null && x.Email.ToLower() == emailLower, ct);
        }
        return null;
    }

    /// <summary>Crea la persona con el mismo servicio que usa SelectorPersona (la deja vinculada al tenant).</summary>
    private async Task<Guid> CrearPersonaAsync(AdminPersonaNuevaRequest p, CancellationToken ct)
    {
        var documento = (p.Documento ?? "").Trim();
        if (documento.Length == 0)
            throw new InvalidOperationException("Para crear la persona se requiere el documento.");
        var creada = await _directorio.CrearPersonaAsync(new CrearPersonaRequest(
            p.TipoDocumento, documento, p.Nombres?.Trim() ?? "", p.Apellidos?.Trim() ?? "",
            string.IsNullOrWhiteSpace(p.Email) ? null : p.Email.Trim(),
            string.IsNullOrWhiteSpace(p.Telefono) ? null : p.Telefono.Trim(), null, null), ct);
        return creada.Id;
    }

    /// <summary>
    /// Persona reutilizada: lo enviado que difiere de lo registrado NO se sobrescribe (la persona es global);
    /// se avisa para corregirlo explicitamente con PUT personas/{id}.
    /// </summary>
    private static List<string> AdvertenciasDatosDistintos(AdminPersonaNuevaRequest enviado, Persona registrada)
    {
        var avisos = new List<string>();
        var email = (enviado.Email ?? "").Trim();
        if (email.Length > 0 && !string.Equals(email, registrada.Email?.Trim(), StringComparison.OrdinalIgnoreCase))
            avisos.Add(registrada.Email is null
                ? "La persona ya existía sin correo registrado; el correo enviado no se guardó (usa PUT personas/{id})."
                : $"El correo enviado difiere del registrado ({Propia.Application.Auth.ClaveTemporal.EnmascararCorreo(registrada.Email)}); no se cambió (usa PUT personas/{{id}}).");
        var tel = Digitos(enviado.Telefono);
        if (tel.Length > 0 && tel != Digitos(registrada.Telefono))
            avisos.Add(string.IsNullOrWhiteSpace(registrada.Telefono)
                ? "La persona ya existía sin teléfono registrado; el teléfono enviado no se guardó (usa PUT personas/{id})."
                : $"El teléfono enviado difiere del registrado (***{Digitos(registrada.Telefono)[^Math.Min(4, Digitos(registrada.Telefono).Length)..]}); no se cambió (usa PUT personas/{{id}}).");
        return avisos;
    }

    private static string Digitos(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());

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

    public async Task<AdminInvitacionCreadaDto> ReenviarInvitacionAsync(Guid tenantId, Guid invitacionId, AdminActor actor, CancellationToken ct)
    {
        await FijarTenantAsync(tenantId, ct);
        MarcarActor(actor);
        await ExigirInvitacionAsync(invitacionId, ct);
        var inv = await _usuarios.ReenviarInvitacionConEnvioAsync(invitacionId, ct)
            ?? throw new InvalidOperationException("Solo se reenvia una invitacion pendiente o expirada.");
        await LogYGuardarAsync(actor, "TENANT_INVITACION_REENVIAR", tenantId, invitacionId,
            inv.Envio is { } e ? $"correo {e.Correo}, whatsapp {e.Whatsapp}" : null, ct);
        return new AdminInvitacionCreadaDto(inv.Id, inv.ExpiraAt, inv.CanalEnvio, inv.LinkAceptacion, inv.Envio);
    }

    public async Task<AccesoEntregadoDto> ActivarConClaveTemporalAsync(Guid tenantId, Guid usuarioTenantId, AdminActivarClaveTemporalRequest req, AdminActor actor, CancellationToken ct)
    {
        await FijarTenantAsync(tenantId, ct);
        MarcarActor(actor);
        var r = await _usuarios.ActivarConClaveTemporalAsync(usuarioTenantId, req.Canal, ct)
            ?? throw new AdminRecursoNoEncontradoException("El usuario no existe en esta copropiedad.");
        await LogYGuardarAsync(actor, "TENANT_USUARIO_CLAVE_TEMPORAL", tenantId, usuarioTenantId,
            $"modo {r.Modo}, entregado por [{string.Join(",", r.EntregadoPor)}]"
            + (r.CuentaCreada ? ", cuenta creada" : "")
            + (r.Fallidos.Count > 0 ? $", fallidos [{string.Join("; ", r.Fallidos)}]" : ""), ct);
        return r;
    }

    public async Task<AdminPersonaActualizadaDto> ActualizarPersonaAsync(Guid tenantId, Guid personaId, AdminActualizarPersonaRequest req, AdminActor actor, CancellationToken ct)
    {
        await FijarTenantAsync(tenantId, ct);
        MarcarActor(actor);
        // Solo personas de ESTA copropiedad (vinculo de Directorio o de unidad); de otra -> 404.
        if (await _directorio.ObtenerPersonaAsync(personaId, ct) is null)
            throw new AdminRecursoNoEncontradoException("La persona no existe en el Directorio de esta copropiedad.");
        var p = await _db.Personas.IgnoreQueryFilters().FirstAsync(x => x.Id == personaId, ct);

        var cambios = new List<string>();
        var avisos = new List<string>();
        if (req.Nombres is not null)
        {
            var v = req.Nombres.Trim();
            if (v.Length == 0) throw new InvalidOperationException("Los nombres no pueden quedar vacios.");
            if (v != p.Nombres) { cambios.Add("nombres"); p.Nombres = v; }
        }
        if (req.Apellidos is not null)
        {
            var v = req.Apellidos.Trim();
            if (v.Length == 0) throw new InvalidOperationException("Los apellidos no pueden quedar vacios.");
            if (v != p.Apellidos) { cambios.Add("apellidos"); p.Apellidos = v; }
        }
        if (req.Telefono is not null)
        {
            var v = req.Telefono.Trim();
            var nuevo = v.Length == 0 ? null : v;   // "" = borrar (nombrado explicito)
            if (nuevo != p.Telefono) { cambios.Add("telefono"); p.Telefono = nuevo; }
        }

        var cuentaActualizada = false;
        if (req.Email is not null)
        {
            var nuevo = req.Email.Trim();
            if (nuevo.Length < 5 || !nuevo.Contains('@') || nuevo.Contains(' '))
                throw new InvalidOperationException("Correo invalido.");
            if (!string.Equals(nuevo, p.Email, StringComparison.OrdinalIgnoreCase) || nuevo != p.Email)
            {
                var lower = nuevo.ToLower();
                if (await _db.Personas.IgnoreQueryFilters().AnyAsync(x => x.Id != p.Id && x.Email != null && x.Email.ToLower() == lower, ct))
                    throw new AdminConflictoException("Ese correo ya es de otra persona en PROPIA.");
                var cuenta = await _db.Users.FirstOrDefaultAsync(u => u.PersonaId == p.Id, ct);
                var otraCuenta = await _userManager.FindByEmailAsync(nuevo);
                if (otraCuenta is not null && otraCuenta.Id != cuenta?.Id)
                    throw new AdminConflictoException("Ese correo ya es el usuario de otra cuenta de PROPIA.");

                var anterior = p.Email;
                p.Email = nuevo;
                cambios.Add($"email {Propia.Application.Auth.ClaveTemporal.EnmascararCorreo(anterior ?? "")} -> {Propia.Application.Auth.ClaveTemporal.EnmascararCorreo(nuevo)}");
                if (cuenta is not null && !string.Equals(cuenta.Email, nuevo, StringComparison.Ordinal))
                {
                    // El correo es el usuario de ingreso: Email y UserName de Identity cambian juntos (SecurityStamp
                    // nuevo: las sesiones abiertas ya no se pueden refrescar). Lo confirma el Super Admin.
                    var r1 = await _userManager.SetUserNameAsync(cuenta, nuevo);
                    var r2 = r1.Succeeded ? await _userManager.SetEmailAsync(cuenta, nuevo) : r1;
                    if (!r2.Succeeded)
                        throw new InvalidOperationException("No se pudo cambiar el correo de la cuenta: " + string.Join("; ", r2.Errors.Select(e => e.Description)));
                    cuenta.EmailConfirmed = true;
                    await _userManager.UpdateAsync(cuenta);
                    cuentaActualizada = true;
                    avisos.Add($"El correo de ingreso de la cuenta cambió: desde ahora entra con {nuevo}.");
                }
            }
        }

        var copropiedades = await ContarCopropiedadesAsync(p.Id, ct);
        if (copropiedades > 1)
            avisos.Add($"Esta persona tiene acceso en {copropiedades} copropiedades: el cambio se ve en todas.");
        if (cambios.Count == 0) avisos.Add("No había nada que cambiar.");

        await LogYGuardarAsync(actor, "TENANT_PERSONA_ACTUALIZAR", tenantId, p.Id,
            cambios.Count == 0 ? "sin cambios" : string.Join(", ", cambios) + (cuentaActualizada ? " (tambien el usuario de ingreso)" : ""), ct);
        return new AdminPersonaActualizadaDto(p.Id, p.Nombres, p.Apellidos, p.Email, p.Telefono, copropiedades, cuentaActualizada, avisos);
    }

    /// <summary>Copropiedades activas donde la persona tiene usuario (SECURITY DEFINER: ve todos los tenants).</summary>
    private async Task<int> ContarCopropiedadesAsync(Guid personaId, CancellationToken ct)
    {
        var conn = _db.Database.GetDbConnection();
        var abrir = conn.State != System.Data.ConnectionState.Open;
        if (abrir) await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM get_tenants_for_persona(@p)";
        var prm = cmd.CreateParameter(); prm.ParameterName = "p"; prm.Value = personaId; cmd.Parameters.Add(prm);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
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
