namespace Propia.Application.Auth;

/// <summary>
/// Servicio de orquestacion del login y selector de tenant.
/// Si el usuario tiene 1 sola copropiedad vinculada, se selecciona automaticamente
/// como ActiveTenantId. Si tiene varias, queda null y el cliente debe llamar
/// switch-tenant para elegir cual (modelo tipo Slack).
/// </summary>
public interface IAuthService
{
    /// <summary>Login de copropiedad. `ip`/`userAgent` se guardan en el registro de ingresos (auditoria).</summary>
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct, string? ip = null, string? userAgent = null);
    Task<MeResponse?> GetMeAsync(Guid userId, Guid? activeTenantId, CancellationToken ct, Guid? activeRolId = null);
    Task<LoginResponse?> SwitchTenantAsync(Guid userId, Guid newTenantId, CancellationToken ct);

    /// <summary>
    /// 2.5 v2.0 multi-rol: fija el rol ACTIVO de la sesion. Reemite el JWT (mismo tenant) con el claim
    /// rol_id tras validar que <paramref name="rolId"/> es uno de los roles del usuario en la copropiedad
    /// activa. Corre en contexto autenticado con tenant (RLS); devuelve null si el rol no le pertenece.
    /// </summary>
    Task<LoginResponse?> SwitchRolAsync(Guid userId, Guid activeTenantId, Guid rolId, CancellationToken ct);
    /// <summary>
    /// Sliding refresh: recibe un JWT que puede estar recien expirado (dentro de RefreshSlidingHours)
    /// y emite uno nuevo con la misma identidad y tenant. Devuelve null si firma invalida o vencido fuera de ventana.
    /// </summary>
    Task<LoginResponse?> RefreshAsync(string rawJwt, CancellationToken ct);

    /// <summary>
    /// Cambia la contrasena del usuario autenticado validando la actual.
    /// Devuelve (ok, error). error trae el motivo legible si ok=false.
    /// </summary>
    Task<(bool Ok, string? Error)> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct);

    /// <summary>
    /// "Olvide mi contrasena": si el correo es de una cuenta confirmada, le envia un enlace de un solo
    /// uso (token de Identity, 24 h). No revela si la cuenta existe (siempre termina igual).
    /// </summary>
    Task SolicitarRecuperacionAsync(string email, string? baseUrl, CancellationToken ct);

    /// <summary>Envia el enlace de restablecimiento a la cuenta dada (lo usa el admin de la copropiedad). True si salio.</summary>
    Task<bool> EnviarEnlaceRestablecerAsync(Propia.Domain.Entities.ApplicationUser user, string? baseUrl, CancellationToken ct);

    /// <summary>Fija la nueva clave con el token del enlace. Quita la marca de clave temporal y el bloqueo.</summary>
    Task<(bool Ok, string? Error)> RestablecerConTokenAsync(RestablecerConTokenRequest req, CancellationToken ct);

    /// <summary>Cambio obligatorio tras una clave temporal: solo si el usuario sigue marcado.</summary>
    Task<(bool Ok, string? Error)> CambiarClaveObligatoriaAsync(Guid userId, CambiarClaveObligatoriaRequest req, CancellationToken ct);
}
