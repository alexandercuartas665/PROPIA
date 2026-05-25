namespace Propia.Application.Common;

/// <summary>
/// Cifra/descifra secretos en reposo (API keys de pasarela, IA, SMTP, OAuth, etc.).
/// La implementacion usa ASP.NET Core Data Protection. Solo los valores cifrados se persisten;
/// el valor en claro nunca se guarda ni se loggea. Portado del proyecto hermano CUBOT.travels.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
