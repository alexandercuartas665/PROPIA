using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Propia.Application.Common;

namespace Propia.Infrastructure.Security;

/// <summary>
/// Cifra secretos en reposo usando ASP.NET Core Data Protection.
/// Las llaves de Data Protection deben persistirse fuera del proceso en produccion
/// (volumen o BD) para que los secretos sigan siendo descifrables tras un redeploy.
/// Ver DependencyInjection.AddInfrastructure (AddDataProtection).
/// </summary>
public sealed class SecretProtector : ISecretProtector
{
    private readonly IDataProtector _protector;
    private readonly ILogger<SecretProtector> _log;

    public SecretProtector(IDataProtectionProvider provider, ILogger<SecretProtector> log)
    {
        _protector = provider.CreateProtector("Propia.PlatformSecrets.v1");
        _log = log;
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    // Los consumidores atrapan la excepcion y muestran "cifrada con una version anterior" sin detalle.
    // Aqui se registra la causa real (que llave falta) para poder diagnosticarlo en prod sin adivinar.
    // Nunca se loguea el secreto ni el texto cifrado.
    public string Unprotect(string ciphertext)
    {
        try { return _protector.Unprotect(ciphertext); }
        catch (CryptographicException ex)
        {
            _log.LogError(ex,
                "No se pudo descifrar un secreto (llave de cifrado {KeyId}). Si esa llave no esta en data_protection_keys, " +
                "el secreto se cifro con una llave que ya no existe: hay que volver a guardarlo.", LlaveDe(ciphertext));
            throw;
        }
    }

    // Formato de Data Protection: base64url; bytes 0-3 = magic header, 4-19 = id (Guid) de la llave usada.
    private static string LlaveDe(string ciphertext)
    {
        try
        {
            var b = WebEncoders.Base64UrlDecode(ciphertext);
            return b.Length >= 20 ? new Guid(b.AsSpan(4, 16)).ToString() : "desconocida";
        }
        catch { return "desconocida"; }
    }
}
