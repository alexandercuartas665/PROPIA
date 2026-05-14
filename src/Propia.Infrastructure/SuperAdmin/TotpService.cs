using OtpNet;
using Propia.Application.SuperAdmin;

namespace Propia.Infrastructure.SuperAdmin;

public class TotpService : ITotpService
{
    public string GenerateSecret()
    {
        // 20 bytes = 160 bits, recomendado por RFC 4226
        var bytes = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(bytes);
    }

    public string BuildOtpAuthUri(string secret, string accountEmail, string issuer)
    {
        var encIssuer = Uri.EscapeDataString(issuer);
        var encAccount = Uri.EscapeDataString(accountEmail);
        // RFC 6238 standard - SHA1, 6 digitos, 30s. Compatible con Google/Microsoft Authenticator, Authy.
        return $"otpauth://totp/{encIssuer}:{encAccount}?secret={secret}&issuer={encIssuer}&algorithm=SHA1&digits=6&period=30";
    }

    public bool VerifyCode(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code)) return false;
        try
        {
            var bytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(bytes);
            // VerificationWindow(previous: 1, future: 1) tolera +/- 30s de drift
            return totp.VerifyTotp(code.Trim(), out _, new VerificationWindow(previous: 1, future: 1));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
