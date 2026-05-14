namespace Propia.Application.SuperAdmin;

/// <summary>
/// Servicio de TOTP (Time-based One-Time Password, RFC 6238) para MFA del SuperAdmin.
/// Standard de 6 digitos, ventana de 30 segundos, hashing SHA-1 (compatibilidad con
/// todas las apps autenticadoras populares).
/// </summary>
public interface ITotpService
{
    /// <summary>Genera un nuevo secret base32 de 160 bits.</summary>
    string GenerateSecret();

    /// <summary>
    /// Construye el URI otpauth:// para que la app autenticadora lo lea desde un QR.
    /// Formato: otpauth://totp/{issuer}:{account}?secret={secret}&issuer={issuer}&algorithm=SHA1&digits=6&period=30
    /// </summary>
    string BuildOtpAuthUri(string secret, string accountEmail, string issuer);

    /// <summary>
    /// Valida un codigo TOTP. Acepta ventana de +/-1 paso (90s totales) para tolerar
    /// clock skew entre cliente y servidor.
    /// </summary>
    bool VerifyCode(string secret, string code);
}
