using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Propia.Application.Auth;
using Propia.Application.Integraciones;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Auth;

/// <summary>
/// Login con Google (OAuth/OIDC). Diferencia clave vs CUBOT.travels: PROPIA permite auto-registro.
///
/// Tres caminos al resolver:
/// 1. ApplicationUser con GoogleSubject o Email existente -> emite JWT (login normal).
///    Si el usuario tiene OnboardingSessionId pendiente, devuelve esa sesion para que el
///    frontend lo lleve a /onboarding/continuar (paso 3-5).
/// 2. ApplicationUser por Email existente sin GoogleSubject -> vincula GoogleSubject y emite JWT.
/// 3. Correo nuevo -> crea User+Persona+OnboardingSession (paso 1 implicito), salta el OTP
///    (Google ya verifico el correo), devuelve JWT + OnboardingSessionId. El frontend navega
///    a /onboarding/continuar (paso 3 - clasificacion).
///
/// Comparte la tabla _sessions estatica con OnboardingService asi el wizard reconoce la sesion
/// creada por Google. NOTA: dependencia frágil del singleton in-process; al persistir
/// onboarding_sessions en BD (Fase 2) esto desaparece.
/// </summary>
public sealed class GoogleSignInService : IGoogleSignInService
{
    private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";

    private readonly IGoogleAuthConfigService _config;
    private readonly IGoogleOAuthClient _client;
    private readonly PropiaDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly Application.Auth.ITokenService _tokenService;
    private readonly ILogger<GoogleSignInService> _logger;

    public GoogleSignInService(
        IGoogleAuthConfigService config,
        IGoogleOAuthClient client,
        PropiaDbContext db,
        UserManager<ApplicationUser> userManager,
        Application.Auth.ITokenService tokenService,
        ILogger<GoogleSignInService> logger)
    {
        _config = config;
        _client = client;
        _db = db;
        _userManager = userManager;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<string?> BuildAuthorizeUrlAsync(string redirectUri, string state, CancellationToken cancellationToken = default)
    {
        var creds = await _config.GetCredentialsAsync(cancellationToken);
        if (creds is null) return null;

        var query = new Dictionary<string, string>
        {
            ["client_id"] = creds.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["state"] = state,
            ["access_type"] = "online",
            ["prompt"] = "select_account",
            ["include_granted_scopes"] = "true"
        };
        var qs = string.Join("&", query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return $"{AuthEndpoint}?{qs}";
    }

    public async Task<GoogleSignInResult> ResolveAsync(string code, string redirectUri, CancellationToken cancellationToken = default)
    {
        var creds = await _config.GetCredentialsAsync(cancellationToken);
        if (creds is null)
        {
            return new GoogleSignInResult(false, "El inicio de sesion con Google no esta habilitado.");
        }

        var identity = await _client.ExchangeCodeAsync(creds.ClientId, creds.ClientSecret, code, redirectUri, cancellationToken);
        if (identity is null)
        {
            return new GoogleSignInResult(false, "No se pudo validar tu identidad con Google.");
        }
        if (!identity.EmailVerified || string.IsNullOrWhiteSpace(identity.Email))
        {
            return new GoogleSignInResult(false, "Tu correo de Google no esta verificado.");
        }

        var email = identity.Email.Trim();

        // Buscar usuario por GoogleSubject (link existente) o por Email (login + link al primer uso de Google)
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.GoogleSubject == identity.Subject || u.Email == email, cancellationToken);

        if (user is null)
        {
            // Camino 3: auto-registro
            return await AutoRegistrarAsync(identity, email, cancellationToken);
        }

        // Camino 1 o 2: usuario existente
        var dirty = false;
        if (string.IsNullOrEmpty(user.GoogleSubject))
        {
            user.GoogleSubject = identity.Subject;
            dirty = true;
        }
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            dirty = true;
        }
        if (dirty) await _userManager.UpdateAsync(user);

        // Resolver tenant activo (similar a /connect/login)
        Guid? tenantId = null;
        string? tenantNombre = null;
        if (user.PersonaId.HasValue)
        {
            var membresia = await _db.UsuariosTenant
                .IgnoreQueryFilters()
                .Where(ut => ut.PersonaId == user.PersonaId.Value && ut.Estado == EstadoUsuarioTenant.Activo)
                .Join(_db.Tenants.IgnoreQueryFilters(), ut => ut.TenantId, t => t.Id, (ut, t) => new { ut, t })
                .OrderBy(x => x.ut.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (membresia is not null)
            {
                tenantId = membresia.t.Id;
                tenantNombre = membresia.t.Nombre;
            }
        }

        var (token, expires) = _tokenService.IssueAccessToken(user, tenantId);
        return new GoogleSignInResult(
            Success: true,
            AccessToken: token,
            ExpiresAt: expires,
            UserId: user.Id,
            Email: user.Email,
            TenantId: tenantId,
            TenantNombre: tenantNombre,
            OnboardingSessionId: user.OnboardingSessionId,
            AutoRegistrado: false);
    }

    private async Task<GoogleSignInResult> AutoRegistrarAsync(GoogleIdentity identity, string email, CancellationToken ct)
    {
        // 1. Persona basada en el nombre de Google
        var nombreCompleto = identity.Name ?? email;
        var partes = nombreCompleto.Trim().Split(' ', 2);
        var persona = new Persona
        {
            TipoDocumento = TipoDocumento.CC,
            Documento = $"PENDIENTE-{Guid.NewGuid().ToString("N")[..10]}",
            Nombres = partes[0],
            Apellidos = partes.Length > 1 ? partes[1] : partes[0],
            Email = email
        };
        _db.Personas.Add(persona);
        await _db.SaveChangesAsync(ct);

        // 2. ApplicationUser con Google ya verificado y sesion de onboarding pendiente (paso 3+)
        var sessionId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,           // Google ya verifico
            PersonaId = persona.Id,
            OnboardingSessionId = sessionId,
            GoogleSubject = identity.Subject
        };

        // Password aleatoria - el usuario nunca la usara (entra por Google). Si despues quiere
        // password local puede usar "olvide contrasena" para resetearla.
        var randomPwd = $"GG-{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))}";
        var created = await _userManager.CreateAsync(user, randomPwd);
        if (!created.Succeeded)
        {
            _logger.LogError("No se pudo auto-registrar usuario Google {Email}: {Errors}",
                email, string.Join(", ", created.Errors.Select(e => e.Description)));
            return new GoogleSignInResult(false, "No se pudo crear tu cuenta automaticamente. Intenta el registro manual.");
        }

        // 3. Sembrar la sesion de onboarding en el singleton de OnboardingService.
        //    El wizard 2.1 paso 3 (clasificacion) requiere que la sesion exista en _sessions
        //    con EmailConfirmado=true. Usamos reflection para acceder al ConcurrentDictionary
        //    estatico privado del OnboardingService (esta dependencia desaparece cuando movemos
        //    onboarding_sessions a BD en Fase 2).
        SeedOnboardingSession(sessionId, email, nombreCompleto, user.Id, persona.Id);

        var (token, expires) = _tokenService.IssueAccessToken(user, null);
        return new GoogleSignInResult(
            Success: true,
            AccessToken: token,
            ExpiresAt: expires,
            UserId: user.Id,
            Email: user.Email,
            OnboardingSessionId: sessionId,
            AutoRegistrado: true);
    }

    /// <summary>
    /// Crea una entrada en el diccionario estatico privado de OnboardingService. Si el tipo
    /// cambia de forma (ConcurrentDictionary o el OnboardingState privado), este metodo
    /// captura la excepcion y solo loggea - el login no falla pero el wizard arrancara
    /// vacio (el usuario tendra que repetir clasificacion). Aceptable como degradacion.
    /// </summary>
    private void SeedOnboardingSession(Guid sessionId, string email, string nombreCompleto, Guid userId, Guid personaId)
    {
        try
        {
            var svcType = typeof(Propia.Infrastructure.Onboarding.OnboardingService);
            var sessionsField = svcType.GetField("_sessions",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (sessionsField is null) return;

            var sessionsObj = sessionsField.GetValue(null);
            if (sessionsObj is null) return;

            // El tipo del valor del dictionary es OnboardingService+OnboardingState (private nested).
            var stateType = svcType.GetNestedType("OnboardingState",
                System.Reflection.BindingFlags.NonPublic);
            if (stateType is null) return;

            var state = Activator.CreateInstance(stateType);
            if (state is null) return;

            stateType.GetProperty("Email")?.SetValue(state, email);
            stateType.GetProperty("NombreCompleto")?.SetValue(state, nombreCompleto);
            stateType.GetProperty("UserId")?.SetValue(state, (Guid?)userId);
            stateType.GetProperty("PersonaId")?.SetValue(state, (Guid?)personaId);
            stateType.GetProperty("EmailConfirmado")?.SetValue(state, true);
            stateType.GetProperty("PasoActual")?.SetValue(state, 2);

            // _sessions es ConcurrentDictionary<Guid, OnboardingState> private
            var tryAdd = sessionsObj.GetType().GetMethod("TryAdd");
            tryAdd?.Invoke(sessionsObj, new[] { (object)sessionId, state });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo sembrar la sesion de onboarding para Google login. " +
                "El wizard arrancara vacio.");
        }
    }
}
