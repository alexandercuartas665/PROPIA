using System.Collections.Concurrent;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Auth;
using Propia.Application.Onboarding;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Onboarding;

/// <summary>
/// Implementacion del wizard 2.1 con estado de sesion en memoria.
/// Para Fase 2: persistir el estado en tabla onboarding_session con TTL y
/// confirmacion real de email via T.2 Motor de Notificaciones.
/// En MVP: estado en memoria singleton + email auto-confirmado.
/// </summary>
public class OnboardingService : IOnboardingService
{
    // Estado del wizard por session - singleton del proceso (Fase 2: persistir)
    private static readonly ConcurrentDictionary<Guid, OnboardingState> _sessions = new();

    private readonly PropiaDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;

    public OnboardingService(PropiaDbContext db, UserManager<ApplicationUser> userManager, ITokenService tokenService)
    {
        _db = db;
        _userManager = userManager;
        _tokenService = tokenService;
    }

    // ---------- Paso 1: Registro ----------
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

        var sessionId = Guid.NewGuid();
        _sessions[sessionId] = new OnboardingState
        {
            Email = req.Email,
            Password = req.Password,
            NombreCompleto = req.NombreCompleto,
            PasoActual = 1
        };
        return new RegistroResponse(sessionId, req.Email);
    }

    // ---------- Paso 1.5: Confirmar email ----------
    public Task<bool> Paso1ConfirmarEmailAsync(ConfirmarEmailRequest req, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(req.OnboardingSessionId, out var st)) return Task.FromResult(false);
        // MVP: auto-confirmacion (cualquier codigo o null es valido). Fase 2: validar OTP real.
        st.EmailConfirmado = true;
        return Task.FromResult(true);
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

    // ---------- Paso 5: Activacion (crea todo en BD) ----------
    public async Task<ActivacionResponse?> Paso5ActivarAsync(ActivacionRequest req, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(req.OnboardingSessionId, out var st)) return null;
        if (st.Activado) throw new InvalidOperationException("Esta sesion ya fue activada.");
        if (!st.EmailConfirmado) throw new InvalidOperationException("Email no confirmado.");
        if (string.IsNullOrWhiteSpace(st.NombreCopropiedad))
            throw new InvalidOperationException("Falta nombre de la copropiedad (paso 4).");

        // 1. Crear ApplicationUser
        var partes = st.NombreCompleto!.Trim().Split(' ', 2);
        var nombres = partes[0];
        var apellidos = partes.Length > 1 ? partes[1] : nombres;

        // Generar documento sintetico para la Persona (Fase 2: capturarlo en el wizard).
        // Se identifica por email - se reemplaza cuando el usuario complete su perfil.
        var documento = $"PENDIENTE-{Guid.NewGuid().ToString("N")[..10]}";

        var persona = new Persona
        {
            TipoDocumento = TipoDocumento.CC,
            Documento = documento,
            Nombres = nombres,
            Apellidos = apellidos,
            Email = st.Email
        };
        _db.Personas.Add(persona);
        await _db.SaveChangesAsync(ct);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = st.Email,
            Email = st.Email,
            EmailConfirmed = true,
            PersonaId = persona.Id
        };
        var created = await _userManager.CreateAsync(user, st.Password!);
        if (!created.Succeeded)
            throw new InvalidOperationException("No se pudo crear el usuario: " + string.Join(", ", created.Errors.Select(e => e.Description)));

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

        // 6. Emitir JWT con tenant activo
        var (token, expires) = _tokenService.IssueAccessToken(user, tenant.Id);
        return new ActivacionResponse(token, expires, user.Id, user.Email!, orgId, tenant.Id, tenant.Nombre);
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
        // Paso 1
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? NombreCompleto { get; set; }
        public bool EmailConfirmado { get; set; }
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
