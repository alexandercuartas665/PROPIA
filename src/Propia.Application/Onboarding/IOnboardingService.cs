namespace Propia.Application.Onboarding;

/// <summary>
/// Servicio del modulo 2.1 Onboarding y Activacion - wizard de 5 pasos.
/// Cada paso construye la sesion de onboarding hasta crear:
/// ApplicationUser + Persona + Organizacion (opcional) + Tenant + UsuarioTenant + Suscripcion (Plan).
/// </summary>
public interface IOnboardingService
{
    Task<RegistroResponse> Paso1RegistrarAsync(RegistroRequest req, CancellationToken ct);
    Task<bool> Paso1ConfirmarEmailAsync(ConfirmarEmailRequest req, CancellationToken ct);
    Task<OnboardingStatusDto?> Paso2ClasificarAsync(ClasificacionRequest req, CancellationToken ct);
    Task<OnboardingStatusDto?> Paso3OrganizacionAsync(DatosOrganizacionRequest req, CancellationToken ct);
    Task<OnboardingStatusDto?> Paso4CopropiedadAsync(DatosCopropiedadRequest req, CancellationToken ct);
    Task<ActivacionResponse?> Paso5ActivarAsync(ActivacionRequest req, CancellationToken ct);
    Task<OnboardingStatusDto?> GetStatusAsync(Guid sessionId, CancellationToken ct);
}
