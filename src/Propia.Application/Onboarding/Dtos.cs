using Propia.Domain.Enums;

namespace Propia.Application.Onboarding;

/// <summary>Tipo de cliente que se auto-registra en el onboarding (modulo 2.1).</summary>
public enum TipoPerfilCliente
{
    /// <summary>Empresa administradora que gestiona varias copropiedades.</summary>
    EmpresaAdministradora = 1,
    /// <summary>Administrador independiente (persona natural).</summary>
    AdministradorIndependiente = 2,
    /// <summary>Copropiedad autoadministrada por su Consejo.</summary>
    Autoadministrada = 3
}

// ----- Paso 1: Registro -----
public record RegistroRequest(string NombreCompleto, string Email, string Password);
public record RegistroResponse(Guid OnboardingSessionId, string Email);

// Verificacion email simulada en MVP (auto-verificada). En Fase 2 se envia codigo.
public record ConfirmarEmailRequest(Guid OnboardingSessionId, string? Codigo);

// ----- Paso 2: Clasificacion -----
public record ClasificacionRequest(Guid OnboardingSessionId, TipoPerfilCliente Perfil);

// ----- Paso 3: Organizacion (opcional si autoadministrada) -----
public record DatosOrganizacionRequest(
    Guid OnboardingSessionId,
    string NombreOrganizacion, string? Nit, string? Email, string? Telefono);

// ----- Paso 4: Copropiedad + Administrador Delegado -----
public record DatosCopropiedadRequest(
    Guid OnboardingSessionId,
    string NombreCopropiedad, string? Nit, string? Direccion, string? Ciudad,
    TipoCopropiedad? Tipo, Estrato? Estrato);

// ----- Paso 5: Activacion final -----
public record ActivacionRequest(Guid OnboardingSessionId, Guid? PlanId);

public record ActivacionResponse(
    string AccessToken, DateTimeOffset ExpiresAt,
    Guid UserId, string Email,
    Guid? OrganizacionId,
    Guid TenantId, string CopropiedadNombre);

// Estado del wizard - para que la UI sepa en que paso esta el usuario
public record OnboardingStatusDto(
    Guid OnboardingSessionId, int PasoActual,
    bool EmailConfirmado, TipoPerfilCliente? Perfil,
    bool TieneOrganizacion, bool TieneCopropiedad, bool Activado);
