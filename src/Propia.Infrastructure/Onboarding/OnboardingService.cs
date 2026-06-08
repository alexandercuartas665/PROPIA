using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Propia.Application.Auth;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Application.Onboarding;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Onboarding;

/// <summary>
/// Implementacion del wizard 2.1 con estado de sesion en memoria.
/// Paso 1 envia un OTP de 6 digitos al correo (via IEmailSender) y deja la cuenta
/// con EmailConfirmed=false hasta que el usuario verifica en Paso1Confirmar (entrega JWT).
/// Paso 5 manda correo de "copropiedad activada" (best-effort, no bloquea la activacion).
/// Para Fase 2: persistir el estado en tabla onboarding_session con TTL y motor de notificaciones T.2.
/// </summary>
public class OnboardingService : IOnboardingService
{
    private const int OtpTtlMinutes = 15;

    // Estado del wizard por session - singleton del proceso (Fase 2: persistir)
    private static readonly ConcurrentDictionary<Guid, OnboardingState> _sessions = new();

    private readonly PropiaDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IEmailSender _emailSender;
    private readonly IAiAgentTemplateService _aiAgentTemplates;
    private readonly ILogger<OnboardingService> _logger;
    private readonly IHostEnvironment _env;

    public OnboardingService(
        PropiaDbContext db,
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IEmailSender emailSender,
        IAiAgentTemplateService aiAgentTemplates,
        ILogger<OnboardingService> logger,
        IHostEnvironment env)
    {
        _db = db;
        _userManager = userManager;
        _tokenService = tokenService;
        _emailSender = emailSender;
        _aiAgentTemplates = aiAgentTemplates;
        _logger = logger;
        _env = env;
    }

    // ---------- Paso 1: Registro (crea User+Persona, envia OTP, NO emite JWT todavia) ----------
    public async Task<RegistroResponse> Paso1RegistrarAsync(RegistroRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.NombreCompleto))
            throw new InvalidOperationException("Nombre completo obligatorio.");
        if (string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains('@'))
            throw new InvalidOperationException("Email invalido.");
        if (req.Password.Length < 10)
            throw new InvalidOperationException("Password minimo 10 caracteres.");

        var existe = await _userManager.FindByEmailAsync(req.Email);
        if (existe is not null) throw new InvalidOperationException("Ya existe un usuario con ese email.");

        // 1. Crear Persona (perfil humano) - documento sintetico hasta capturar el real
        var partes = req.NombreCompleto.Trim().Split(' ', 2);
        var nombres = partes[0];
        var apellidos = partes.Length > 1 ? partes[1] : nombres;
        var persona = new Persona
        {
            TipoDocumento = TipoDocumento.CC,
            Documento = $"PENDIENTE-{Guid.NewGuid().ToString("N")[..10]}",
            Nombres = nombres,
            Apellidos = apellidos,
            Email = req.Email
        };
        _db.Personas.Add(persona);
        await _db.SaveChangesAsync(ct);

        // 2. Crear ApplicationUser CON EmailConfirmed=false. El usuario no podra hacer login
        //    en /connect/login mientras el correo no este verificado.
        var sessionId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = req.Email,
            Email = req.Email,
            EmailConfirmed = false,
            PersonaId = persona.Id,
            OnboardingSessionId = sessionId
        };
        var created = await _userManager.CreateAsync(user, req.Password);
        if (!created.Succeeded)
            throw new InvalidOperationException("No se pudo crear el usuario: " + string.Join(", ", created.Errors.Select(e => e.Description)));

        // 3. Generar OTP de 6 digitos + guardar hash en estado (no en BD por MVP)
        var otp = GenerateOtp6();
        var otpHash = HashOtp(otp, sessionId);

        _sessions[sessionId] = new OnboardingState
        {
            Email = req.Email,
            Password = req.Password,
            NombreCompleto = req.NombreCompleto,
            UserId = user.Id,
            PersonaId = persona.Id,
            EmailConfirmado = false,
            OtpHash = otpHash,
            OtpExpiresAt = DateTimeOffset.UtcNow.AddMinutes(OtpTtlMinutes),
            PasoActual = 1
        };

        // 4. Enviar correo de bienvenida + OTP. Si SMTP falla, el usuario igualmente queda
        //    creado y puede reintentar pidiendo reenvio (Fase 2). Logueamos la falla.
        try
        {
            var (subject, body) = OnboardingEmailTemplates.BienvenidaConOtp(req.NombreCompleto, otp);
            var result = await _emailSender.SendAsync(req.Email, subject, body, ct);
            if (!result.Success)
            {
                _logger.LogWarning("OTP onboarding NO enviado a {Email}: {Error}", req.Email, result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo enviando OTP de onboarding a {Email}", req.Email);
        }

        // [DEV-ONLY] En entorno Development logueamos el OTP en plano para poder
        // probar el wizard sin depender de SMTP real. El guard IsDevelopment evita
        // que el OTP termine en logs de produccion.
        if (_env.IsDevelopment())
        {
            _logger.LogWarning("[DEV-ONLY] OTP onboarding {Email} -> {Otp}", req.Email, otp);
        }

        return new RegistroResponse(sessionId, user.Id, user.Email!);
    }

    // ---------- Paso 1.5: Confirmar email con OTP -> emite JWT ----------
    public async Task<ConfirmarEmailResponse?> Paso1ConfirmarEmailAsync(ConfirmarEmailRequest req, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(req.OnboardingSessionId, out var st)) return null;
        if (string.IsNullOrWhiteSpace(req.Codigo))
            throw new InvalidOperationException("Codigo obligatorio.");
        if (st.OtpExpiresAt is null || st.OtpExpiresAt.Value < DateTimeOffset.UtcNow)
            throw new InvalidOperationException("El codigo expiro. Solicita uno nuevo.");
        if (string.IsNullOrEmpty(st.OtpHash))
            throw new InvalidOperationException("No hay codigo pendiente en esta sesion.");

        var hash = HashOtp(req.Codigo.Trim(), req.OnboardingSessionId);
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(hash), Encoding.UTF8.GetBytes(st.OtpHash)))
        {
            throw new InvalidOperationException("Codigo invalido.");
        }

        if (st.UserId is not Guid uid) throw new InvalidOperationException("La sesion no tiene usuario.");
        var user = await _userManager.FindByIdAsync(uid.ToString())
            ?? throw new InvalidOperationException("Usuario no encontrado.");

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);
        }
        st.EmailConfirmado = true;
        st.OtpHash = null;
        st.OtpExpiresAt = null;

        var (token, expires) = _tokenService.IssueAccessToken(user, null);
        return new ConfirmarEmailResponse(req.OnboardingSessionId, user.Id, user.Email!, token, expires);
    }

    private static string GenerateOtp6()
    {
        var n = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return n.ToString("D6");
    }

    private static string HashOtp(string otp, Guid sessionId)
    {
        // Hash con session_id como sal para que el mismo OTP en otra sesion no matchee.
        var bytes = Encoding.UTF8.GetBytes($"{sessionId:N}|{otp}");
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    // ---------- Paso 2: Clasificacion ----------
    public Task<OnboardingStatusDto?> Paso2ClasificarAsync(ClasificacionRequest req, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(req.OnboardingSessionId, out var st)) return Task.FromResult<OnboardingStatusDto?>(null);
        if (!st.EmailConfirmado) throw new InvalidOperationException("Email aun no confirmado.");
        st.Perfil = req.Perfil;
        st.PasoActual = Math.Max(st.PasoActual, 2);
        return Task.FromResult<OnboardingStatusDto?>(BuildStatus(req.OnboardingSessionId, st));
    }

    // ---------- Paso 3: Organizacion (opcional) ----------
    public Task<OnboardingStatusDto?> Paso3OrganizacionAsync(DatosOrganizacionRequest req, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(req.OnboardingSessionId, out var st)) return Task.FromResult<OnboardingStatusDto?>(null);
        if (st.Perfil is null) throw new InvalidOperationException("Falta seleccionar perfil (paso 2).");
        if (string.IsNullOrWhiteSpace(req.NombreOrganizacion))
            throw new InvalidOperationException("Nombre de la organizacion obligatorio.");

        st.NombreOrganizacion = req.NombreOrganizacion;
        st.NitOrganizacion = req.Nit;
        st.EmailOrganizacion = req.Email;
        st.TelefonoOrganizacion = req.Telefono;
        st.PasoActual = Math.Max(st.PasoActual, 3);
        return Task.FromResult<OnboardingStatusDto?>(BuildStatus(req.OnboardingSessionId, st));
    }

    // ---------- Paso 4: Copropiedad ----------
    public Task<OnboardingStatusDto?> Paso4CopropiedadAsync(DatosCopropiedadRequest req, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(req.OnboardingSessionId, out var st)) return Task.FromResult<OnboardingStatusDto?>(null);
        if (string.IsNullOrWhiteSpace(req.NombreCopropiedad))
            throw new InvalidOperationException("Nombre de la copropiedad obligatorio.");

        st.NombreCopropiedad = req.NombreCopropiedad;
        st.NitCopropiedad = req.Nit;
        st.DireccionCopropiedad = req.Direccion;
        st.CiudadCopropiedad = req.Ciudad;
        st.TipoCopropiedad = req.Tipo;
        st.EstratoCopropiedad = req.Estrato;
        st.PasoActual = Math.Max(st.PasoActual, 4);
        return Task.FromResult<OnboardingStatusDto?>(BuildStatus(req.OnboardingSessionId, st));
    }

    // ---------- Paso 5: Activacion (crea Org+Tenant+Suscripcion, User/Persona ya existen del Paso 1) ----------
    public async Task<ActivacionResponse?> Paso5ActivarAsync(ActivacionRequest req, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(req.OnboardingSessionId, out var st)) return null;
        if (st.Activado) throw new InvalidOperationException("Esta sesion ya fue activada.");
        if (!st.EmailConfirmado) throw new InvalidOperationException("Email no confirmado.");
        if (string.IsNullOrWhiteSpace(st.NombreCopropiedad))
            throw new InvalidOperationException("Falta nombre de la copropiedad (paso 4).");
        if (st.UserId is not Guid userId || st.PersonaId is not Guid personaId)
            throw new InvalidOperationException("La sesion no tiene usuario asociado (Paso 1 incompleto).");

        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("Usuario no encontrado para esta sesion.");
        var persona = await _db.Personas.FirstOrDefaultAsync(p => p.Id == personaId, ct)
            ?? throw new InvalidOperationException("Persona no encontrada para esta sesion.");

        // 2. Crear Organizacion (si aplica)
        Guid? orgId = null;
        if (!string.IsNullOrWhiteSpace(st.NombreOrganizacion) && st.Perfil != TipoPerfilCliente.Autoadministrada)
        {
            var org = new Organizacion
            {
                Nombre = st.NombreOrganizacion,
                Tipo = st.Perfil == TipoPerfilCliente.EmpresaAdministradora
                    ? TipoOrganizacion.Administradora
                    : TipoOrganizacion.Administradora,
                Nit = st.NitOrganizacion,
                Email = st.EmailOrganizacion,
                Telefono = st.TelefonoOrganizacion,
                FechaActivacion = DateTimeOffset.UtcNow
            };
            _db.Organizaciones.Add(org);
            await _db.SaveChangesAsync(ct);
            orgId = org.Id;
        }

        // 3. Crear Tenant (Copropiedad)
        var tenant = new Tenant
        {
            Nombre = st.NombreCopropiedad!,
            Nit = st.NitCopropiedad,
            Direccion = st.DireccionCopropiedad,
            Ciudad = st.CiudadCopropiedad,
            TipoCopropiedad = st.TipoCopropiedad,
            Estrato = st.EstratoCopropiedad,
            OrganizacionId = orgId,
            Estado = EstadoCopropiedad.Activa,
            EstadoCustodia = orgId.HasValue ? EstadoCustodia.ConAdmin : EstadoCustodia.SinAdmin,
            FechaActivacion = DateTimeOffset.UtcNow
        };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(ct);

        // 4. Vincular UsuarioTenant (la persona es administradora de su copropiedad)
        // RLS bloquea INSERT si app.tenant_id no esta seteado. Abrimos conexion
        // explicitamente y ejecutamos set_config + INSERT en el mismo cmd para
        // garantizar misma sesion PostgreSQL.
        var conn = _db.Database.GetDbConnection();
        var opened = conn.State != System.Data.ConnectionState.Open;
        if (opened) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT set_config('app.tenant_id', '{tenant.Id}', false);
                INSERT INTO usuarios_tenant (id, tenant_id, persona_id, rol, estado, fecha_activacion, created_at)
                VALUES ('{Guid.NewGuid()}', '{tenant.Id}', '{persona.Id}', 'Administrador', 1, now(), now());";
            await cmd.ExecuteNonQueryAsync(ct);

            // Desplegar plantillas de agente IA con la misma sesion SQL (set_config sigue activo).
            // Best-effort: si el deploy falla, la activacion sigue exitosa - el tenant simplemente
            // tendra que crear sus agentes a mano en /agentes-ia.
            try
            {
                var copiados = await _aiAgentTemplates.DeployToTenantAsync(
                    tenant.Id, tenant.Nombre, st.NombreOrganizacion, ct);
                if (copiados > 0)
                {
                    _logger.LogInformation("Desplegados {N} agentes-plantilla al tenant {TenantId} ({Nombre})",
                        copiados, tenant.Id, tenant.Nombre);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo desplegando plantillas de agente al tenant {TenantId}", tenant.Id);
            }
        }
        finally
        {
            if (opened) await conn.CloseAsync();
        }

        // 5. Crear Suscripcion en plan seleccionado (si se proveyo PlanId)
        if (req.PlanId.HasValue)
        {
            var plan = await _db.Planes.FirstOrDefaultAsync(p => p.Id == req.PlanId, ct);
            if (plan is not null && plan.Estado == EstadoPlan.Activo)
            {
                var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
                DateOnly? finTrial = plan.DiasTrial > 0 ? hoy.AddDays(plan.DiasTrial) : null;
                var s = new Suscripcion
                {
                    OrganizacionId = orgId,
                    CopropiedadId = orgId.HasValue ? null : tenant.Id,
                    PlanId = plan.Id,
                    Ciclo = CicloFacturacion.Mensual,
                    Estado = plan.DiasTrial > 0 ? EstadoSuscripcion.Trial : EstadoSuscripcion.Activa,
                    FechaInicio = hoy,
                    FechaAniversario = hoy.Day,
                    FechaProximoCobro = finTrial ?? hoy.AddMonths(1),
                    FechaFinTrial = finTrial
                };
                _db.Suscripciones.Add(s);
                _db.SuscripcionHistorial.Add(new SuscripcionHistorial
                {
                    SuscripcionId = s.Id,
                    Tipo = TipoEventoSuscripcion.Activacion,
                    Origen = OrigenEventoSuscripcion.Cliente,
                    PlanNuevoId = plan.Id,
                    EstadoNuevo = s.Estado.ToString(),
                    Notas = $"Activacion via onboarding self-service (modulo 2.1)"
                });
                await _db.SaveChangesAsync(ct);
            }
        }

        st.Activado = true;
        st.PasoActual = 5;

        // 6. Marcar onboarding como completado en el usuario (limpia OnboardingSessionId)
        user.OnboardingSessionId = null;
        await _userManager.UpdateAsync(user);

        // 7. Correo de activacion (best-effort, no bloquea la activacion)
        try
        {
            var (subject, body) = OnboardingEmailTemplates.CopropiedadActivada(
                st.NombreCompleto ?? user.Email ?? "",
                tenant.Nombre);
            var emailResult = await _emailSender.SendAsync(user.Email!, subject, body, ct);
            if (!emailResult.Success)
            {
                _logger.LogWarning("Correo de activacion no enviado a {Email}: {Error}", user.Email, emailResult.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo enviando correo de activacion a {Email}", user.Email);
        }

        // 8. Emitir JWT con tenant activo
        var (token, expires) = _tokenService.IssueAccessToken(user, tenant.Id);
        return new ActivacionResponse(token, expires, user.Id, user.Email!, orgId, tenant.Id, tenant.Nombre);
    }

    public async Task<MiSesionPendienteDto> GetMiSesionPendienteAsync(Guid userId, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.OnboardingSessionId is null)
        {
            return new MiSesionPendienteDto(null, 0, Completado: true);
        }
        var sid = user.OnboardingSessionId.Value;
        var paso = _sessions.TryGetValue(sid, out var st) ? st.PasoActual : 1;
        return new MiSesionPendienteDto(sid, paso, Completado: false);
    }

    public Task<OnboardingStatusDto?> GetStatusAsync(Guid sessionId, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(sessionId, out var st)) return Task.FromResult<OnboardingStatusDto?>(null);
        return Task.FromResult<OnboardingStatusDto?>(BuildStatus(sessionId, st));
    }

    private static OnboardingStatusDto BuildStatus(Guid id, OnboardingState st) =>
        new(id, st.PasoActual, st.EmailConfirmado, st.Perfil,
            !string.IsNullOrWhiteSpace(st.NombreOrganizacion),
            !string.IsNullOrWhiteSpace(st.NombreCopropiedad),
            st.Activado);

    private class OnboardingState
    {
        // Paso 1 (ya persistido en BD: User+Persona)
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? NombreCompleto { get; set; }
        public Guid? UserId { get; set; }
        public Guid? PersonaId { get; set; }
        public bool EmailConfirmado { get; set; }
        public string? OtpHash { get; set; }
        public DateTimeOffset? OtpExpiresAt { get; set; }
        // Paso 2
        public TipoPerfilCliente? Perfil { get; set; }
        // Paso 3
        public string? NombreOrganizacion { get; set; }
        public string? NitOrganizacion { get; set; }
        public string? EmailOrganizacion { get; set; }
        public string? TelefonoOrganizacion { get; set; }
        // Paso 4
        public string? NombreCopropiedad { get; set; }
        public string? NitCopropiedad { get; set; }
        public string? DireccionCopropiedad { get; set; }
        public string? CiudadCopropiedad { get; set; }
        public TipoCopropiedad? TipoCopropiedad { get; set; }
        public Estrato? EstratoCopropiedad { get; set; }
        // Estado
        public int PasoActual { get; set; } = 1;
        public bool Activado { get; set; }
    }
}
