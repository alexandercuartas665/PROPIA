using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Auth;
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

    private readonly Propia.Application.UsuariosAccesos.INotificadorAcceso? _notificador;
    private readonly Microsoft.Extensions.Configuration.IConfiguration? _config;

    public SuperAdminService(PropiaDbContext db, IPasswordHasher<SuperAdminUsuario> hasher, UserManager<ApplicationUser> userManager,
        Propia.Application.UsuariosAccesos.INotificadorAcceso? notificador = null,
        Microsoft.Extensions.Configuration.IConfiguration? config = null)
    {
        _notificador = notificador;
        _config = config;
        _db = db;
        _hasher = hasher;
        _userManager = userManager;
    }

    // ---------------------------------- Organizaciones ----------------------------------

    public async Task<IReadOnlyList<OrganizacionDto>> ListOrganizacionesAsync(CancellationToken ct)
    {
        // Trae override + plan de la suscripcion activa (mas reciente no cancelada/archivada). El limite
        // efectivo se calcula en memoria: override de la org ?? limite del plan ?? (sin suscripcion -> 1).
        var rows = await _db.Organizaciones
            .AsNoTracking()
            .OrderBy(o => o.Nombre)
            .Select(o => new
            {
                o.Id,
                o.Nombre,
                o.Tipo,
                o.Nit,
                o.Email,
                Count = o.Copropiedades.Count,
                o.CreatedAt,
                o.Estado,
                Override = o.LimiteCopropiedades,
                Sub = _db.Suscripciones
                    .Where(s => s.OrganizacionId == o.Id
                                && s.Estado != EstadoSuscripcion.Cancelada && s.Estado != EstadoSuscripcion.Archivada)
                    .OrderByDescending(s => s.FechaInicio)
                    .Select(s => new { s.Plan!.Nombre, s.Plan!.LimiteCopropiedades })
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return rows.Select(r =>
        {
            int? limitePlan = r.Sub == null ? null : r.Sub.LimiteCopropiedades;
            int? efectivo = r.Override ?? (r.Sub != null ? limitePlan : 1);
            return new OrganizacionDto(r.Id, r.Nombre, r.Tipo, r.Nit, r.Email, r.Count, r.CreatedAt, r.Estado,
                r.Override, r.Sub?.Nombre, limitePlan, efectivo);
        }).ToList();
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

    public async Task<OrganizacionDto?> CambiarLimiteCopropiedadesAsync(Guid orgId, CambiarLimiteOrganizacionRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct)
    {
        var org = await _db.Organizaciones.FirstOrDefaultAsync(o => o.Id == orgId, ct);
        if (org is null) return null;

        if (req.Limite is int n && n < 0)
            throw new InvalidOperationException("El limite de copropiedades no puede ser negativo. Deja vacio para usar el del plan.");

        org.LimiteCopropiedades = req.Limite;
        _db.SuperAdminLogs.Add(NewLog(actorId, actorEmail, "CHANGE_LIMITE_ORGANIZACION", $"Organizacion:{org.Id}",
            $"Limite={(req.Limite.HasValue ? req.Limite.Value.ToString() : "plan")}", ip));
        await _db.SaveChangesAsync(ct);

        var count = await _db.Tenants.CountAsync(t => t.OrganizacionId == org.Id, ct);
        var sub = await _db.Suscripciones.AsNoTracking()
            .Where(s => s.OrganizacionId == org.Id
                        && s.Estado != EstadoSuscripcion.Cancelada && s.Estado != EstadoSuscripcion.Archivada)
            .OrderByDescending(s => s.FechaInicio)
            .Select(s => new { s.Plan!.Nombre, s.Plan!.LimiteCopropiedades })
            .FirstOrDefaultAsync(ct);
        int? limitePlan = sub == null ? null : sub.LimiteCopropiedades;
        int? efectivo = org.LimiteCopropiedades ?? (sub != null ? limitePlan : 1);
        return new OrganizacionDto(org.Id, org.Nombre, org.Tipo, org.Nit, org.Email, count, org.CreatedAt, org.Estado,
            org.LimiteCopropiedades, sub?.Nombre, limitePlan, efectivo);
    }

    public async Task<AdminOrganizacionDto> CrearAdminOrganizacionAsync(Guid orgId, CrearAdminOrganizacionRequest req, Guid actorId, string actorEmail, string? ip, CancellationToken ct)
    {
        var org = await _db.Organizaciones.FirstOrDefaultAsync(o => o.Id == orgId, ct)
            ?? throw new InvalidOperationException("La organizacion no existe.");

        var nombres = (req.Nombres ?? "").Trim();
        var apellidos = (req.Apellidos ?? "").Trim();
        var documento = (req.Documento ?? "").Trim();
        var email = (req.Email ?? "").Trim();
        var conClave = !string.IsNullOrWhiteSpace(req.Password);
        if (nombres.Length == 0 || apellidos.Length == 0) throw new InvalidOperationException("Nombres y apellidos son obligatorios.");
        if (documento.Length == 0) throw new InvalidOperationException("El documento es obligatorio.");
        if (email.Length == 0 || !email.Contains('@')) throw new InvalidOperationException("Correo invalido.");
        if (conClave && req.Password!.Length < 8) throw new InvalidOperationException("La clave debe tener al menos 8 caracteres.");
        var advertencias = new List<string>();

        // 1) Persona (global): se REUTILIZA si ya existe por documento o por correo (no se duplica ni falla).
        var emailLower = email.ToLower();
        var persona = await _db.Personas.FirstOrDefaultAsync(p => p.TipoDocumento == req.TipoDocumento && p.Documento == documento, ct)
            ?? await _db.Personas.FirstOrDefaultAsync(p => p.Email != null && p.Email.ToLower() == emailLower, ct);
        var personaReutilizada = persona is not null;
        if (persona is null)
        {
            persona = new Persona
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
        }
        else if (!string.Equals(persona.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            advertencias.Add(persona.Email is null
                ? "La persona ya existía sin correo; el correo enviado no se guardó."
                : $"El correo enviado difiere del registrado ({ClaveTemporal.EnmascararCorreo(persona.Email)}); se usa el registrado.");
        }
        var emailLogin = persona.Email ?? email;

        // 2) ApplicationUser (Identity): una persona = una cuenta. Si ya la tiene, se conserva (y su clave).
        string? claveTemporal = null;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.PersonaId == persona.Id, ct);
        var cuentaCreada = false;
        if (user is null)
        {
            if (await _userManager.FindByEmailAsync(emailLogin) is not null)
                throw new InvalidOperationException($"Ya existe una cuenta con el correo {emailLogin} que no es de esta persona.");
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = emailLogin,
                Email = emailLogin,
                EmailConfirmed = true,
                PersonaId = persona.Id
            };
            claveTemporal = conClave ? null : ClaveTemporal.Generar();
            var created = await _userManager.CreateAsync(user, conClave ? req.Password! : claveTemporal!);
            if (!created.Succeeded)
            {
                if (!personaReutilizada) { _db.Personas.Remove(persona); await _db.SaveChangesAsync(ct); }
                throw new InvalidOperationException("No se pudo crear el usuario: " + string.Join("; ", created.Errors.Select(e => e.Description)));
            }
            if (claveTemporal is not null)
                await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim(ClaveTemporal.ClaimTipo, "1"));
            cuentaCreada = true;
        }
        else if (conClave)
        {
            advertencias.Add("La persona ya tenía cuenta: no se cambió su contraseña (usa restablecer contraseña si la necesita).");
        }

        // 3) OrgColaborador con cargo Director (acceso Capa 1). Best-effort: no aborta si falla.
        try
        {
            var directorCargoId = await AsegurarCargoDirectorAsync(orgId, ct);
            var yaColaborador = await _db.OrgColaboradores.AnyAsync(c => c.OrganizacionId == orgId && c.PersonaId == persona.Id, ct);
            if (directorCargoId is { } cargoId && !yaColaborador)
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
                    // Persona existente: si ya es usuario de esa copropiedad no se duplica (unico tenant+persona);
                    // si estaba Pendiente (sin cuenta) queda Activo, porque ahora ya tiene cuenta. No toca roles.
                    cmd.CommandText = @"
                        SELECT set_config('app.tenant_id', @tid::text, false);
                        INSERT INTO usuarios_tenant (id, tenant_id, persona_id, rol, estado, fecha_activacion, created_at)
                        VALUES (@id, @tid, @pid, 'Administrador', 1, now(), now())
                        ON CONFLICT (tenant_id, persona_id) DO UPDATE SET estado = 1, fecha_activacion = now()
                        WHERE usuarios_tenant.estado = 2;";
                    AddParam(cmd, "@tid", tid);
                    AddParam(cmd, "@id", Guid.NewGuid());
                    AddParam(cmd, "@pid", persona.Id);
                    if (await cmd.ExecuteNonQueryAsync(ct) > 0) asignadas++;
                }
            }
            finally { if (opened) await conn.CloseAsync(); }
        }

        // 5) Cuenta nueva sin clave: la temporal se ENTREGA al correo (nunca viaja en la respuesta).
        Propia.Application.UsuariosAccesos.AccesoEntregadoDto? acceso = null;
        if (claveTemporal is not null)
        {
            var envio = _notificador is null
                ? Propia.Application.UsuariosAccesos.EnvioCanalesDto.SinNotificador
                : await _notificador.EnviarAccesoTemporalAsync(new Propia.Application.UsuariosAccesos.AccesoTemporalMensaje(
                    $"{persona.Nombres} {persona.Apellidos}".Trim(), emailLogin, persona.Telefono, org.Nombre, claveTemporal,
                    ConstruirLink("/login")), Domain.Enums.CanalEnvioInvitacion.Email, ct);
            var ok = envio.Correo == Propia.Application.UsuariosAccesos.EstadoEnvioCanal.Enviado;
            acceso = new Propia.Application.UsuariosAccesos.AccesoEntregadoDto("temporal",
                ok ? new[] { "correo" } : Array.Empty<string>(), ClaveTemporal.EnmascararCorreo(emailLogin),
                ok ? Array.Empty<string>() : new[] { $"correo: {envio.DetalleCorreo}" },
                ok ? null : "No se pudo entregar la contraseña temporal: usa restablecer contraseña o activar-con-clave-temporal.",
                CuentaCreada: true);
        }

        _db.SuperAdminLogs.Add(NewLog(actorId, actorEmail, "CREATE_ADMIN_ORGANIZACION", $"Organizacion:{orgId}",
            $"Email={emailLogin}, Copropiedades={asignadas}, persona {(personaReutilizada ? "reutilizada" : "nueva")}, "
            + (cuentaCreada ? (claveTemporal is null ? "cuenta con clave dada" : $"cuenta con clave temporal entregada por [{string.Join(",", acceso!.EntregadoPor)}]") : "cuenta existente"), ip));
        await _db.SaveChangesAsync(ct);

        return new AdminOrganizacionDto(user.Id, persona.Id, $"{persona.Nombres} {persona.Apellidos}".Trim(), emailLogin, asignadas,
            personaReutilizada, acceso, advertencias.Count > 0 ? advertencias : null);
    }

    private string ConstruirLink(string relativo)
    {
        var baseUrl = (_config?["App:PublicBaseUrl"] ?? "").Trim().TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl) ? relativo : baseUrl + relativo;
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

    public async Task<IReadOnlyList<LoginEventoDto>> ListLoginEventsByOrgAsync(Guid orgId, int take, CancellationToken ct)
    {
        var rows = await _db.LoginAuditEvents.AsNoTracking()
            .Where(e => e.OrganizacionId == orgId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(take)
            .Select(e => new { e.Id, e.CreatedAt, e.Email, e.PersonaId, e.TenantId, e.Exito, e.Motivo, e.Ip, e.UserAgent })
            .ToListAsync(ct);

        // Nombres (persona + copropiedad) resueltos en un segundo paso (tablas globales, sin RLS).
        var personaIds = rows.Where(r => r.PersonaId != null).Select(r => r.PersonaId!.Value).Distinct().ToList();
        var tenantIds = rows.Where(r => r.TenantId != null).Select(r => r.TenantId!.Value).Distinct().ToList();
        var personas = await _db.Personas.AsNoTracking().Where(p => personaIds.Contains(p.Id))
            .Select(p => new { p.Id, Nombre = (p.Nombres + " " + p.Apellidos).Trim() })
            .ToDictionaryAsync(x => x.Id, x => x.Nombre, ct);
        var tenants = await _db.Tenants.AsNoTracking().Where(t => tenantIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Nombre })
            .ToDictionaryAsync(x => x.Id, x => x.Nombre, ct);

        return rows.Select(r => new LoginEventoDto(
            r.Id, r.CreatedAt, r.Email,
            r.PersonaId is Guid pid && personas.TryGetValue(pid, out var pn) ? pn : null,
            r.TenantId,
            r.TenantId is Guid tid && tenants.TryGetValue(tid, out var tn) ? tn : null,
            r.Exito, r.Motivo, r.Ip, r.UserAgent)).ToList();
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
