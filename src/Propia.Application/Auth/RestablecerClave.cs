namespace Propia.Application.Auth;

/// <summary>
/// Restablecimiento de contrasena (2.5). Dos caminos:
/// - El administrador de la copropiedad pone una contrasena TEMPORAL; el usuario queda marcado
///   "debe cambiar la clave" (claim de Identity <see cref="ClaimTipo"/>) y su JWT lleva
///   <see cref="JwtClaim"/>: el API solo le deja usar /connect/* hasta que la cambie.
/// - "Olvide mi contrasena": enlace de un solo uso por correo (token de Identity, 24 h).
/// La cuenta (ApplicationUser) es UNA para todas las copropiedades: si el usuario tambien esta en
/// otra copropiedad (o es de la consola A&D), el administrador no puede ponerle una temporal; en su
/// lugar se le envia el enlace al correo, para que nadie tome una cuenta de otra copropiedad.
/// </summary>
public static class ClaveTemporal
{
    /// <summary>Claim de Identity (tabla asp_net_user_claims) que marca "debe cambiar la clave".</summary>
    public const string ClaimTipo = "propia:debe_cambiar_clave";

    /// <summary>Claim del JWT que el middleware del API revisa para bloquear todo salvo /connect/*.</summary>
    public const string JwtClaim = "pwd_chg";

    /// <summary>Error que devuelve el API (403) mientras el usuario no cambie la clave temporal.</summary>
    public const string ErrorDebeCambiar = "debe_cambiar_clave";

    public const int LongitudMinima = 10;

    /// <summary>Valida la politica de Identity (10+, una mayuscula, un numero) con mensaje en espanol.</summary>
    public static string? ValidarPolitica(string? clave)
    {
        if (string.IsNullOrEmpty(clave) || clave.Length < LongitudMinima)
            return $"La contraseña debe tener al menos {LongitudMinima} caracteres.";
        if (!clave.Any(char.IsUpper)) return "La contraseña debe incluir al menos una mayúscula.";
        if (!clave.Any(char.IsDigit)) return "La contraseña debe incluir al menos un número.";
        return null;
    }

    /// <summary>12 caracteres sin ambiguos (0/O, 1/l/I), con mayuscula, minuscula y numero.</summary>
    public static string Generar()
    {
        const string may = "ABCDEFGHJKLMNPQRSTUVWXYZ", min = "abcdefghijkmnopqrstuvwxyz", num = "23456789";
        const string todos = may + min + num;
        Func<int, int> rng = System.Security.Cryptography.RandomNumberGenerator.GetInt32;
        var c = new char[12];
        c[0] = may[rng(may.Length)];
        c[1] = min[rng(min.Length)];
        c[2] = num[rng(num.Length)];
        for (var i = 3; i < c.Length; i++) c[i] = todos[rng(todos.Length)];
        for (var i = c.Length - 1; i > 0; i--) { var j = rng(i + 1); (c[i], c[j]) = (c[j], c[i]); }
        return new string(c);
    }

    /// <summary>"alex@gmail.com" -> "al***@gmail.com".</summary>
    public static string EnmascararCorreo(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0) return "***";
        return email[..Math.Min(2, at)] + "***" + email[at..];
    }
}

/// <summary>
/// Admin de la copropiedad: restablece la clave de un usuario. Clave vacia = la genera el sistema.
/// BaseUrl = origen del Web (solo se usa en Development para el enlace del correo).
/// </summary>
public record RestablecerClaveUsuarioRequest(string? ClaveTemporal, string? BaseUrl = null);

/// <summary>
/// Resultado. Modo "temporal": se fijo la clave (ClaveTemporal la trae para mostrarla UNA vez al
/// admin). Modo "enlace": el usuario esta en otra copropiedad o es de A&D; se le envio el enlace al
/// correo (CorreoEnmascarado) y la clave no cambio.
/// </summary>
public record RestablecerClaveUsuarioResponse(string Modo, string? ClaveTemporal, string? CorreoEnmascarado, string? Motivo);

/// <summary>"Olvide mi contrasena" (anonimo). BaseUrl = origen del Web; solo se usa en Development.</summary>
public record SolicitarRecuperacionRequest(string Email, string? BaseUrl);

/// <summary>Fija la nueva clave con el token del enlace del correo (anonimo).</summary>
public record RestablecerConTokenRequest(string Email, string Token, string NuevaClave, string ConfirmarClave);

/// <summary>Cambio obligatorio tras una clave temporal (JWT con pwd_chg; no pide la clave actual).</summary>
public record CambiarClaveObligatoriaRequest(string NuevaClave, string ConfirmarClave);
