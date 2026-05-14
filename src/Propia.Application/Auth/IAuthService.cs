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
}
