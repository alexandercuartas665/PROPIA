namespace Propia.Application.Auth;

/// <summary>
/// Servicio de orquestacion del login y selector de tenant.
/// Si el usuario tiene 1 sola copropiedad vinculada, se selecciona automaticamente
/// como ActiveTenantId. Si tiene varias, queda null y el cliente debe llamar
/// switch-tenant para elegir cual (modelo tipo Slack).
/// </summary>
public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<MeResponse?> GetMeAsync(Guid userId, Guid? activeTenantId, CancellationToken ct);
    Task<LoginResponse?> SwitchTenantAsync(Guid userId, Guid newTenantId, CancellationToken ct);
    /// <summary>
    /// Sliding refresh: recibe un JWT que puede estar recien expirado (dentro de RefreshSlidingHours)
    /// y emite uno nuevo con la misma identidad y tenant. Devuelve null si firma invalida o vencido fuera de ventana.
    /// </summary>
    Task<LoginResponse?> RefreshAsync(string rawJwt, CancellationToken ct);
}
