namespace Propia.Application.SuperAdmin;

/// <summary>
/// Autenticacion EXCLUSIVA para personal de A&D GROUP en el Super Admin Console (modulo 0.1).
/// SEPARADA de la auth de clientes (Propia.Application.Auth) - usa la tabla super_admin_usuarios
/// y emite un JWT con claim `is_super_admin=true`. NO comparte sesion ni usuarios con la
/// plataforma cliente.
/// </summary>
public interface ISuperAdminAuthService
{
    Task<SuperAdminLoginResponse?> LoginAsync(SuperAdminLoginRequest request, string? ip, CancellationToken ct);
}
