using Microsoft.AspNetCore.DataProtection;
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

    public SecretProtector(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector("Propia.PlatformSecrets.v1");

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
