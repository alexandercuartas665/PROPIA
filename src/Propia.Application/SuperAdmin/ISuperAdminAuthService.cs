namespace Propia.Application.SuperAdmin;

/// <summary>
/// Autenticacion EXCLUSIVA para personal de A&D GROUP en el Super Admin Console (modulo 0.1).
/// SEPARADA de la auth de clientes (Propia.Application.Auth) - usa la tabla super_admin_usuarios
/// y emite un JWT con claim `is_super_admin=true`. NO comparte sesion ni usuarios con la
/// plataforma cliente.
/// </summary>
public interface ISuperAdminAuthService
{
    /// <summary>
    /// Primer paso del login: valida credenciales.
    /// - Si el user NO tiene MFA configurado: devuelve JWT completo (compatibilidad con seed founder).
    /// - Si el user YA configuro MFA: devuelve MfaTicket para el segundo paso.
    /// - Si credenciales invalidas: null.
    /// </summary>
    Task<SuperAdminLoginResponse?> LoginAsync(SuperAdminLoginRequest request, string? ip, CancellationToken ct);

    /// <summary>
    /// Segundo paso del login MFA: valida ticket + codigo TOTP, emite JWT final.
    /// Retorna null si el ticket es invalido, expirado, o el codigo TOTP no coincide.
    /// </summary>
    Task<SuperAdminLoginResponse?> VerifyMfaLoginAsync(VerifyMfaLoginRequest request, string? ip, CancellationToken ct);

    /// <summary>
    /// Genera un secret TOTP para el usuario autenticado y retorna el URI otpauth://
    /// para que la app autenticadora lo escanee como QR.
    /// El secret se guarda en MfaSecret pero MfaConfigurado queda en false hasta que
    /// el usuario verifique un codigo via VerifyEnrollMfaAsync.
    /// </summary>
    Task<MfaEnrollResponse> EnrollMfaAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Confirma que el usuario configuro correctamente la app autenticadora.
    /// Si el codigo coincide con el secret almacenado, marca MfaConfigurado = true.
    /// </summary>
    Task<bool> VerifyEnrollMfaAsync(Guid userId, VerifyMfaEnrollRequest request, string? ip, CancellationToken ct);
}
