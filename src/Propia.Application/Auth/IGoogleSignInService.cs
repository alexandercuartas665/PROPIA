namespace Propia.Application.Auth;

/// <summary>Identidad verificada que devuelve Google tras el intercambio del code.</summary>
public sealed record GoogleIdentity(string Subject, string Email, bool EmailVerified, string? Name, string? Picture);

/// <summary>Cliente HTTP que habla con Google para intercambiar el authorization code por la identidad.</summary>
public interface IGoogleOAuthClient
{
    Task<GoogleIdentity?> ExchangeCodeAsync(string clientId, string clientSecret, string code, string redirectUri, CancellationToken cancellationToken = default);
}

/// <summary>
/// Resultado de resolver un login con Google. Tiene tres modos:
/// 1. Login OK (UserId+AccessToken seteados): el frontend guarda JWT y navega.
/// 2. Auto-registro OK (UserId+AccessToken+OnboardingSessionId seteados): el frontend
///    guarda JWT y navega a /onboarding/continuar (saltando el OTP que Google ya cubrio).
/// 3. Error (Success=false + Error): el frontend muestra el mensaje.
/// </summary>
public sealed record GoogleSignInResult(
    bool Success,
    string? Error = null,
    string? AccessToken = null,
    DateTimeOffset? ExpiresAt = null,
    Guid? UserId = null,
    string? Email = null,
    Guid? TenantId = null,
    string? TenantNombre = null,
    Guid? OnboardingSessionId = null,
    bool AutoRegistrado = false);

public interface IGoogleSignInService
{
    /// <summary>Arma la URL de challenge hacia Google. Null si Google no esta configurado/habilitado.</summary>
    Task<string?> BuildAuthorizeUrlAsync(string redirectUri, string state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Intercambia el code y resuelve el usuario de PROPIA. Si el correo no existe, hace auto-registro
    /// (crea User+Persona+OnboardingSession) y devuelve OnboardingSessionId para que el frontend
    /// continue con el wizard 2.1 desde el paso 3.
    /// </summary>
    Task<GoogleSignInResult> ResolveAsync(string code, string redirectUri, CancellationToken cancellationToken = default);
}
