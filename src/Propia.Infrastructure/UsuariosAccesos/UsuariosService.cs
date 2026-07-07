using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Propia.Application.Auth;
using Propia.Application.Common;
using Propia.Infrastructure.Auth;
using Propia.Application.UsuariosAccesos;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.UsuariosAccesos;

/// <summary>
/// Servicio del modulo 2.5 - bandeja de usuarios, invitaciones, cambio de rol, revocacion.
/// Spec v1.0. Auditoria se registra automaticamente al final de cada operacion.
/// </summary>
public class UsuariosService : IUsuariosService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly JwtSettings _jwt;

    public UsuariosService(
        PropiaDbContext db,
        ITenantContext tenantContext,
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IOptions<JwtSettings> jwt)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userManager = userManager;
        _tokenService = tokenService;
        _jwt = jwt.Value;
    }

    // ===================== Bandeja =====================

    public async Task<IReadOnlyList<UsuarioListaDto>> ListarUsuariosAsync(EstadoUsuarioTenant? estado, string? query, CancellationToken ct)
    {
        var q = _db.UsuariosTenant
            .AsNoTracking()
            .Include(u => u.Persona)
            .Include(u => u.RolNavigation)
            .AsQueryable();

        if (estado.HasValue) q = q.Where(u => u.Estado == estado.Value);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var qq = query.Trim().ToLower();
            q = q.Where(u =>
                u.Persona!.Nombres.ToLower().Contains(qq) ||
                u.Persona.Apellidos.ToLower().Contains(qq) ||
                u.Persona.Documento.Contains(qq) ||
                (u.Persona.Email != null && u.Persona.Email.ToLower().Contains(qq)));
        }

        var lista = await q
            .OrderBy(u => u.Persona!.Apellidos).ThenBy(u => u.Persona!.Nombres)
            .ToListAsync(ct);

        // Para cada uno verifico si tiene ApplicationUser (cuenta real para login)
        var emails = lista.Where(u => u.Persona?.Email is not null).Select(u => u.Persona!.Email!).Distinct().ToList();
        var emailsConCuenta = await _db.Users
            .Where(au => emails.Contains(au.Email!))
            .Select(au => au.Email!)
            .ToListAsync(ct);
        var setCuentas = new HashSet<string>(emailsConCuenta, StringComparer.OrdinalIgnoreCase);

        // Etiquetas asignadas por usuario (2.5)
        var utIds = lista.Select(u => u.Id).ToList();
        var etiquetasPorUsuario = (await _db.UsuarioTenantEtiquetas.AsNoTracking()
                .Where(ute => utIds.Contains(ute.UsuarioTenantId))
                .Join(_db.EtiquetasUsuario, ute => ute.EtiquetaId, e => e.Id,
                      (ute, e) => new { ute.UsuarioTenantId, e })
                .ToListAsync(ct))
            .GroupBy(x => x.UsuarioTenantId)
            .ToDictionary(g => g.Key,
                g => (IReadOnlyList<EtiquetaUsuarioDto>)g.Select(x => new EtiquetaUsuarioDto(x.e.Id, x.e.Nombre, x.e.Color, x.e.Activo)).ToList());

        return lista.Select(u => new UsuarioListaDto(
            u.Id, u.PersonaId,
            $"{u.Persona!.Nombres} {u.Persona.Apellidos}",
            u.Persona.Documento,
            u.Persona.Email, u.Persona.Telefono,
            u.RolId, u.RolNavigation?.Nombre ?? u.Rol,
            u.Estado, u.UltimoAcceso, u.FechaInvitacion,
            u.Persona.Email is not null && setCuentas.Contains(u.Persona.Email),
            etiquetasPorUsuario.TryGetValue(u.Id, out var ets) ? ets : new List<EtiquetaUsuarioDto>()
        )).ToList();
    }

    // ===================== Etiquetas de usuario (2.5) =====================

    public async Task<IReadOnlyList<EtiquetaUsuarioDto>> ListarEtiquetasAsync(CancellationToken ct)
        => await _db.EtiquetasUsuario.AsNoTracking()
            .OrderBy(e => e.Nombre)
            .Select(e => new EtiquetaUsuarioDto(e.Id, e.Nombre, e.Color, e.Activo))
            .ToListAsync(ct);

    public async Task<EtiquetaUsuarioDto> CrearEtiquetaAsync(CrearEtiquetaUsuarioRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("El nombre de la etiqueta es obligatorio.");
        var e = new EtiquetaUsuario
        {
            Nombre = req.Nombre.Trim(),
            Color = string.IsNullOrWhiteSpace(req.Color) ? null : req.Color.Trim(),
            Activo = true
        };
        _db.EtiquetasUsuario.Add(e);
        await _db.SaveChangesAsync(ct);
        return new EtiquetaUsuarioDto(e.Id, e.Nombre, e.Color, e.Activo);
    }

    public async Task<bool> ActualizarEtiquetaAsync(Guid etiquetaId, ActualizarEtiquetaUsuarioRequest req, CancellationToken ct)
    {
        var e = await _db.EtiquetasUsuario.FirstOrDefaultAsync(x => x.Id == etiquetaId, ct);
        if (e is null) return false;
        e.Nombre = req.Nombre.Trim();
        e.Color = string.IsNullOrWhiteSpace(req.Color) ? null : req.Color.Trim();
        e.Activo = req.Activo;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarEtiquetaAsync(Guid etiquetaId, CancellationToken ct)
    {
        var e = await _db.EtiquetasUsuario.FirstOrDefaultAsync(x => x.Id == etiquetaId, ct);
        if (e is null) return false;
        var asigs = await _db.UsuarioTenantEtiquetas.Where(a => a.EtiquetaId == etiquetaId).ToListAsync(ct);
        _db.UsuarioTenantEtiquetas.RemoveRange(asigs);
        _db.EtiquetasUsuario.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> AsignarEtiquetaAsync(Guid usuarioTenantId, Guid etiquetaId, CancellationToken ct)
    {
        if (await _db.UsuarioTenantEtiquetas.AnyAsync(a => a.UsuarioTenantId == usuarioTenantId && a.EtiquetaId == etiquetaId, ct))
            return true; // idempotente
        var utOk = await _db.UsuariosTenant.AnyAsync(u => u.Id == usuarioTenantId, ct);
        var etOk = await _db.EtiquetasUsuario.AnyAsync(e => e.Id == etiquetaId, ct);
        if (!utOk || !etOk) return false;
        _db.UsuarioTenantEtiquetas.Add(new UsuarioTenantEtiqueta { UsuarioTenantId = usuarioTenantId, EtiquetaId = etiquetaId });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> QuitarEtiquetaAsync(Guid usuarioTenantId, Guid etiquetaId, CancellationToken ct)
    {
        var a = await _db.UsuarioTenantEtiquetas.FirstOrDefaultAsync(x => x.UsuarioTenantId == usuarioTenantId && x.EtiquetaId == etiquetaId, ct);
        if (a is null) return false;
        _db.UsuarioTenantEtiquetas.Remove(a);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<UsuarioDetalleDto?> GetUsuarioDetalleAsync(Guid usuarioTenantId, CancellationToken ct)
    {
        var ut = await _db.UsuariosTenant
            .AsNoTracking()
            .Include(u => u.Persona)
            .Include(u => u.RolNavigation)
            .FirstOrDefaultAsync(u => u.Id == usuarioTenantId, ct);
        if (ut is null) return null;

        var appUser = ut.Persona?.Email is not null
            ? await _userManager.FindByEmailAsync(ut.Persona.Email)
            : null;

        var sesiones = appUser is null ? new List<UsuarioSesionDto>() :
            await _db.UsuarioSesiones
                .AsNoTracking()
                .Where(s => s.UsuarioId == appUser.Id && s.Activa)
                .OrderByDescending(s => s.UltimoUsoAt)
                .Select(s => new UsuarioSesionDto(s.Id, s.Dispositivo, s.IpOrigen, s.CanalAuth, s.UltimoUsoAt, s.ExpiraAt))
                .ToListAsync(ct);

        var metodos = appUser is null ? new List<AuthMetodoDto>() :
            await _db.UsuarioAuthMetodos
                .AsNoTracking()
                .Where(m => m.UsuarioId == appUser.Id)
                .Select(m => new AuthMetodoDto(m.Tipo, m.Activo))
                .ToListAsync(ct);

        // Si existe cuenta pero no se han registrado metodos, asumimos EmailPassword
        if (appUser is not null && metodos.Count == 0)
            metodos = new List<AuthMetodoDto> { new(TipoAuthMetodo.EmailPassword, true) };

        return new UsuarioDetalleDto(
            ut.Id, ut.PersonaId,
            $"{ut.Persona!.Nombres} {ut.Persona.Apellidos}",
            ut.Persona.Documento,
            ut.Persona.Email, ut.Persona.Telefono,
            ut.RolId, ut.RolNavigation?.Nombre ?? ut.Rol,
            ut.Estado, ut.UltimoAcceso, ut.FechaInvitacion,
            ut.FechaActivacion, ut.FechaRevocacion, ut.MotivoRevocacion,
            appUser is not null,
            sesiones, metodos);
    }

    // ===================== Invitaciones =====================

    public async Task<InvitacionDto> InvitarAsync(CrearInvitacionRequest req, CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentTenantId
            ?? throw new InvalidOperationException("Sin tenant activo.");

        var persona = await _db.Personas.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == req.PersonaId, ct)
            ?? throw new InvalidOperationException("Persona no existe en el Directorio. Crea primero su identidad (RN-02).");

        var rol = await _db.RolesCopropiedad.FirstOrDefaultAsync(r => r.Id == req.RolId, ct)
            ?? throw new InvalidOperationException("Rol no encontrado.");

        // Cancelo invitaciones pendientes previas para esta persona
        var pendientes = await _db.UsuarioInvitaciones
            .Where(i => i.PersonaId == req.PersonaId && i.Estado == EstadoInvitacion.Pendiente)
            .ToListAsync(ct);
        foreach (var p in pendientes)
        {
            p.Estado = EstadoInvitacion.Cancelada;
            p.CanceladaAt = DateTimeOffset.UtcNow;
        }

        var token = GenerarTokenSeguro();
        var inv = new UsuarioInvitacion
        {
            PersonaId = req.PersonaId,
            RolId = req.RolId,
            Token = token,
            Estado = EstadoInvitacion.Pendiente,
            ExpiraAt = DateTimeOffset.UtcNow.AddHours(72),  // RN-11: 72h vigencia
            CanalEnvio = req.Canal
        };
        _db.UsuarioInvitaciones.Add(inv);
        await _db.SaveChangesAsync(ct);

        await RegistrarAuditoriaAsync(TipoEventoAuditoria.InvitacionEnviada, tenantId, inv.Id, persona.Email, ct);

        return ArmarInvitacionDto(inv, persona, rol);
    }

    public async Task<bool> ReenviarInvitacionAsync(Guid invitacionId, CancellationToken ct)
    {
        var inv = await _db.UsuarioInvitaciones.FirstOrDefaultAsync(i => i.Id == invitacionId, ct);
        if (inv is null) return false;
        if (inv.Estado != EstadoInvitacion.Pendiente && inv.Estado != EstadoInvitacion.Expirada) return false;

        // Regenero el token y la vigencia (RN-11: el admin puede regenerar)
        inv.Token = GenerarTokenSeguro();
        inv.ExpiraAt = DateTimeOffset.UtcNow.AddHours(72);
        inv.Estado = EstadoInvitacion.Pendiente;
        await _db.SaveChangesAsync(ct);

        await RegistrarAuditoriaAsync(TipoEventoAuditoria.InvitacionEnviada, _tenantContext.CurrentTenantId, inv.Id, "reenvio", ct);
        return true;
    }

    public async Task<bool> CancelarInvitacionAsync(Guid invitacionId, CancellationToken ct)
    {
        var inv = await _db.UsuarioInvitaciones.FirstOrDefaultAsync(i => i.Id == invitacionId, ct);
        if (inv is null) return false;
        inv.Estado = EstadoInvitacion.Cancelada;
        inv.CanceladaAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await RegistrarAuditoriaAsync(TipoEventoAuditoria.InvitacionCancelada, _tenantContext.CurrentTenantId, invitacionId, null, ct);
        return true;
    }

    public async Task<IReadOnlyList<InvitacionDto>> ListarPendientesAsync(CancellationToken ct)
    {
        var lista = await _db.UsuarioInvitaciones
            .AsNoTracking()
            .Include(i => i.Persona)
            .Include(i => i.Rol)
            .Where(i => i.Estado == EstadoInvitacion.Pendiente)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);
        return lista.Select(i => ArmarInvitacionDto(i, i.Persona!, i.Rol!)).ToList();
    }

    public async Task<InvitacionPublicaDto?> ConsultarInvitacionAsync(string token, CancellationToken ct)
    {
        // Endpoint publico sin tenant en JWT. RLS bloquea SELECT cuando current_tenant_id() es NULL.
        // Usamos la funcion SECURITY DEFINER get_invitacion_publica(token) que ejecuta como owner.
        var conn = _db.Database.GetDbConnection();
        var opened = conn.State != System.Data.ConnectionState.Open;
        if (opened) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM get_invitacion_publica(@t)";
            var p = cmd.CreateParameter(); p.ParameterName = "t"; p.Value = token; cmd.Parameters.Add(p);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;

            var estado = (EstadoInvitacion)reader.GetInt32(reader.GetOrdinal("estado"));
            var expiraAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("expira_at"));
            var nombres = reader.GetString(reader.GetOrdinal("persona_nombres"));
            var apellidos = reader.GetString(reader.GetOrdinal("persona_apellidos"));
            var email = reader.IsDBNull(reader.GetOrdinal("persona_email")) ? "" : reader.GetString(reader.GetOrdinal("persona_email"));
            var rolNombre = reader.GetString(reader.GetOrdinal("rol_nombre"));
            var copNombre = reader.GetString(reader.GetOrdinal("copropiedad_nombre"));

            return new InvitacionPublicaDto(
                copNombre,
                $"{nombres} {apellidos}",
                email,
                rolNombre,
                estado == EstadoInvitacion.Pendiente,
                expiraAt < DateTimeOffset.UtcNow);
        }
        finally { /* no cerramos: reusable */ }
    }

    public async Task<AceptarInvitacionResponse> AceptarInvitacionAsync(AceptarInvitacionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(req.Token)) throw new InvalidOperationException("Token requerido.");
        if (req.Password != req.ConfirmPassword) throw new InvalidOperationException("Las contrasenas no coinciden.");
        if ((req.Password ?? "").Length < 10) throw new InvalidOperationException("La contrasena debe tener al menos 10 caracteres.");

        // Endpoint publico: leemos la invitacion via SECURITY DEFINER y luego activamos el tenant
        // para hacer todas las escrituras RLS-compliant en la misma conexion.
        Guid invId, tenantIdInv, personaId, rolId;
        EstadoInvitacion estadoInv;
        DateTimeOffset expiraAt, invCreatedAt;

        var conn = _db.Database.GetDbConnection();
        var opened = conn.State != System.Data.ConnectionState.Open;
        if (opened) await conn.OpenAsync(ct);

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT id, tenant_id, persona_id, rol_id, estado, expira_at, created_at FROM get_invitacion_publica(@t)";
            var p = cmd.CreateParameter(); p.ParameterName = "t"; p.Value = req.Token; cmd.Parameters.Add(p);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (!await rd.ReadAsync(ct))
                throw new InvalidOperationException("Invitacion no encontrada o ya utilizada.");
            invId = rd.GetGuid(0);
            tenantIdInv = rd.GetGuid(1);
            personaId = rd.GetGuid(2);
            rolId = rd.GetGuid(3);
            estadoInv = (EstadoInvitacion)rd.GetInt32(4);
            expiraAt = rd.GetFieldValue<DateTimeOffset>(5);
            invCreatedAt = rd.GetFieldValue<DateTimeOffset>(6);
        }

        if (estadoInv != EstadoInvitacion.Pendiente)
            throw new InvalidOperationException($"Invitacion en estado {estadoInv}, no se puede aceptar.");
        if (expiraAt < DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Invitacion expirada. Pide al administrador que regenere el link.");

        // Activamos tenant en la sesion para que RLS WITH CHECK permita los UPDATE/INSERT
        await using (var cmdSet = conn.CreateCommand())
        {
            cmdSet.CommandText = "SELECT set_config('app.tenant_id', @tid, false)";
            var p = cmdSet.CreateParameter(); p.ParameterName = "tid"; p.Value = tenantIdInv.ToString(); cmdSet.Parameters.Add(p);
            await cmdSet.ExecuteNonQueryAsync(ct);
        }
        _tenantContext.SetTenant(tenantIdInv);

        // Releemos los entities ahora con tenant activo
        var inv = await _db.UsuarioInvitaciones.FirstAsync(i => i.Id == invId, ct);
        inv.Persona = await _db.Personas.IgnoreQueryFilters().FirstAsync(p => p.Id == personaId, ct);
        inv.Rol = await _db.RolesCopropiedad.FirstAsync(r => r.Id == rolId, ct);

        var email = inv.Persona!.Email
            ?? throw new InvalidOperationException("La persona no tiene email registrado. Pide al admin que lo agregue.");

        // Crea ApplicationUser si no existe (idempotente)
        var appUser = await _userManager.FindByEmailAsync(email);
        if (appUser is null)
        {
            appUser = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                PersonaId = inv.Persona.Id
            };
            var result = await _userManager.CreateAsync(appUser, req.Password);
            if (!result.Succeeded)
                throw new InvalidOperationException("No se pudo crear la cuenta: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }
        else if (appUser.PersonaId is null)
        {
            appUser.PersonaId = inv.Persona.Id;
            await _userManager.UpdateAsync(appUser);
        }

        // Crea o reactiva UsuarioTenant
        var ut = await _db.UsuariosTenant.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.TenantId == inv.TenantId && u.PersonaId == inv.PersonaId, ct);
        if (ut is null)
        {
            ut = new UsuarioTenant
            {
                TenantId = inv.TenantId,
                PersonaId = inv.PersonaId,
                RolId = inv.RolId,
                Rol = inv.Rol!.Nombre,
                Estado = EstadoUsuarioTenant.Activo,
                FechaActivacion = DateTimeOffset.UtcNow,
                FechaInvitacion = inv.CreatedAt
            };
            _db.UsuariosTenant.Add(ut);
        }
        else
        {
            ut.RolId = inv.RolId;
            ut.Rol = inv.Rol!.Nombre;
            ut.Estado = EstadoUsuarioTenant.Activo;
            ut.FechaActivacion = DateTimeOffset.UtcNow;
            ut.MotivoRevocacion = null;
            ut.FechaRevocacion = null;
        }

        inv.Estado = EstadoInvitacion.Aceptada;
        inv.AceptadaAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Genera JWT para login inmediato
        var (token, _) = _tokenService.IssueAccessToken(appUser, inv.TenantId);
        var tenantNombre = (await _db.Tenants.FirstOrDefaultAsync(t => t.Id == inv.TenantId, ct))?.Nombre ?? "";

        await RegistrarAuditoriaAsync(TipoEventoAuditoria.InvitacionAceptada, inv.TenantId, inv.Id, email, ct);
        await RegistrarAuditoriaAsync(TipoEventoAuditoria.AccesoOtorgado, inv.TenantId, ut.Id, email, ct);

        return new AceptarInvitacionResponse(true, email, token,
            inv.TenantId, tenantNombre,
            $"Bienvenido a {tenantNombre}. Tu acceso esta activo.");
    }

    // ===================== Cambio de rol / revocar =====================

    public async Task<bool> CambiarRolAsync(Guid usuarioTenantId, CambiarRolUsuarioRequest req, CancellationToken ct)
    {
        var ut = await _db.UsuariosTenant.FirstOrDefaultAsync(u => u.Id == usuarioTenantId, ct);
        if (ut is null) return false;
        var rol = await _db.RolesCopropiedad.FirstOrDefaultAsync(r => r.Id == req.RolId, ct)
            ?? throw new InvalidOperationException("Rol no encontrado.");

        // RN-validacion: no quitar Administrador al ultimo activo
        if (ut.Rol == "Administrador" && rol.Nombre != "Administrador")
        {
            var adminsActivos = await _db.UsuariosTenant
                .CountAsync(u => u.Rol == "Administrador" && u.Estado == EstadoUsuarioTenant.Activo, ct);
            if (adminsActivos <= 1)
                throw new InvalidOperationException("No puedes quitar el rol Administrador al ultimo activo de la copropiedad.");
        }

        ut.RolId = rol.Id;
        ut.Rol = rol.Nombre;
        await _db.SaveChangesAsync(ct);
        await RegistrarAuditoriaAsync(TipoEventoAuditoria.RolCambiado, ut.TenantId, ut.Id, rol.Nombre, ct);
        return true;
    }

    public async Task<bool> RevocarAccesoAsync(Guid usuarioTenantId, RevocarAccesoRequest req, CancellationToken ct)
    {
        var ut = await _db.UsuariosTenant.FirstOrDefaultAsync(u => u.Id == usuarioTenantId, ct);
        if (ut is null) return false;

        if (ut.Rol == "Administrador")
        {
            var adminsActivos = await _db.UsuariosTenant
                .CountAsync(u => u.Rol == "Administrador" && u.Estado == EstadoUsuarioTenant.Activo && u.Id != usuarioTenantId, ct);
            if (adminsActivos == 0)
                throw new InvalidOperationException("No puedes revocar el acceso al ultimo Administrador activo.");
        }

        ut.Estado = EstadoUsuarioTenant.Inactivo;
        ut.FechaRevocacion = DateTimeOffset.UtcNow;
        ut.MotivoRevocacion = req.Motivo;
        await _db.SaveChangesAsync(ct);

        // RN-08: revocacion en tiempo real - cerrar sesiones del usuario en este tenant
        var persona = await _db.Personas.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == ut.PersonaId, ct);
        if (persona?.Email is not null)
        {
            var appUser = await _userManager.FindByEmailAsync(persona.Email);
            if (appUser is not null)
            {
                var sesiones = await _db.UsuarioSesiones
                    .Where(s => s.UsuarioId == appUser.Id && s.TenantId == ut.TenantId && s.Activa)
                    .ToListAsync(ct);
                foreach (var s in sesiones) s.Activa = false;
                await _db.SaveChangesAsync(ct);
            }
        }

        await RegistrarAuditoriaAsync(TipoEventoAuditoria.AccesoRevocado, ut.TenantId, ut.Id, req.Motivo, ct);
        return true;
    }

    public async Task<bool> ReactivarUsuarioAsync(Guid usuarioTenantId, CancellationToken ct)
    {
        var ut = await _db.UsuariosTenant.FirstOrDefaultAsync(u => u.Id == usuarioTenantId, ct);
        if (ut is null) return false;
        ut.Estado = EstadoUsuarioTenant.Activo;
        ut.MotivoRevocacion = null;
        ut.FechaRevocacion = null;
        await _db.SaveChangesAsync(ct);
        await RegistrarAuditoriaAsync(TipoEventoAuditoria.AccesoOtorgado, ut.TenantId, ut.Id, "reactivacion", ct);
        return true;
    }

    // ===================== Auditoria =====================

    public async Task<IReadOnlyList<AuditoriaEntradaDto>> ListarAuditoriaAsync(int limit, CancellationToken ct)
    {
        limit = limit <= 0 ? 50 : Math.Min(limit, 500);
        var lista = await _db.AccesoAuditorias
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        var actorIds = lista.Where(a => a.ActorUsuarioId.HasValue).Select(a => a.ActorUsuarioId!.Value).Distinct().ToList();
        var actores = await _db.Users
            .Where(u => actorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? "(sin email)", ct);

        return lista.Select(a => new AuditoriaEntradaDto(
            a.Id, a.CreatedAt, a.TipoEvento,
            a.ActorUsuarioId,
            a.ActorUsuarioId.HasValue && actores.TryGetValue(a.ActorUsuarioId.Value, out var nom) ? nom : null,
            a.EntidadAfectadaId, a.IpOrigen, a.Dispositivo, a.Detalle
        )).ToList();
    }

    // ===================== Helpers =====================

    private InvitacionDto ArmarInvitacionDto(UsuarioInvitacion inv, Persona p, Rol r)
        => new(inv.Id, inv.PersonaId, $"{p.Nombres} {p.Apellidos}", p.Documento,
            inv.RolId, r.Nombre, inv.Token, inv.Estado, inv.ExpiraAt, inv.CanalEnvio,
            $"/invitacion/{inv.Token}");

    private static string GenerarTokenSeguro()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToHexString(bytes).ToLowerInvariant();  // 96 chars hex
    }

    private async Task RegistrarAuditoriaAsync(
        TipoEventoAuditoria tipo, Guid? tenantId, Guid? entidadAfectada, string? detalle, CancellationToken ct)
    {
        _db.AccesoAuditorias.Add(new AccesoAuditoria
        {
            TenantId = tenantId,
            TipoEvento = tipo,
            EntidadAfectadaId = entidadAfectada,
            Detalle = detalle,
            Canal = CanalAcceso.Web
        });
        await _db.SaveChangesAsync(ct);
    }
}
