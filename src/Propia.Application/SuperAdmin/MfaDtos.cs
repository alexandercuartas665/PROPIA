namespace Propia.Application.SuperAdmin;

/// <summary>
/// DTOs del flujo MFA TOTP para SuperAdmin (modulo 0.1 - regla critica de seguridad).
/// </summary>

// ----- Login con MFA -----
/// <summary>
/// Respuesta del login cuando el usuario tiene MFA configurado.
/// El cliente debe pedir codigo TOTP y llamar a /admin/mfa/verify-login con MfaTicket + Code.
/// El MfaTicket es un token temporal corto (5 min) que NO autoriza ninguna accion.
/// </summary>
public record MfaChallengeResponse(string MfaTicket, DateTimeOffset TicketExpiresAt);

public record VerifyMfaLoginRequest(string MfaTicket, string Code);

// ----- Enroll de MFA (usuario YA autenticado configura su MFA por primera vez) -----
/// <summary>
/// Respuesta del enroll: secret base32 + URI estilo otpauth:// para que la app autenticadora
/// (Google Authenticator, Microsoft Authenticator, Authy, etc.) genere los codigos.
/// El cliente muestra el URI como QR code y/o el secret como texto fallback.
/// El secret NO se persiste como MfaConfigurado hasta que el usuario verifique un codigo.
/// </summary>
public record MfaEnrollResponse(string Secret, string OtpAuthUri);

public record VerifyMfaEnrollRequest(string Code);
