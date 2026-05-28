using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Credenciales de "Iniciar sesion con Google" (OAuth/OIDC). GLOBAL y singleton, configurable
/// por el Super Admin. El Client Secret se guarda cifrado (ISecretProtector) y nunca se expone
/// en claro. Google actua como proveedor de identidad; PROPIA decide el acceso. Portado de
/// CUBOT.travels con la diferencia de que aqui SI hacemos auto-registro (un correo de Google
/// que no existe inicia el wizard 2.1 desde el paso 3, saltando el OTP que Google ya cubrio).
/// </summary>
public class GoogleAuthConfig : BaseEntity
{
    /// <summary>Client ID OAuth (termina en .apps.googleusercontent.com). No es secreto.</summary>
    public string? ClientId { get; set; }

    /// <summary>Client Secret cifrado en reposo.</summary>
    public string? ClientSecretEncrypted { get; set; }

    /// <summary>Si el login con Google esta habilitado en la pantalla de ingreso.</summary>
    public bool IsEnabled { get; set; }
}
