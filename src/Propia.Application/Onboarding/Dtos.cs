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
/// <summary>
/// Respuesta de Paso 1: el usuario YA quedo creado en BD pero con EmailConfirmed=false.
/// El sistema envia un OTP de 6 digitos al correo y el frontend muestra un input para
/// ingresarlo. El JWT solo se emite en Paso1ConfirmarEmail tras verificar el OTP.
/// </summary>
public record RegistroResponse(
    Guid OnboardingSessionId, Guid UserId, string Email);

/// <summary>Pide verificar el OTP enviado al correo del usuario.</summary>
public record ConfirmarEmailRequest(Guid OnboardingSessionId, string Codigo);

/// <summary>
/// Respuesta de Paso1Confirmar: OTP verificado, se emite JWT sin tenant
/// para que el frontend continue con los pasos 3-5 del wizard autenticado.
/// </summary>
public record ConfirmarEmailResponse(
    Guid OnboardingSessionId, Guid UserId, string Email,
    string AccessToken, DateTimeOffset ExpiresAt);

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

/// <summary>
/// Sesion de onboarding pendiente del usuario autenticado. Si OnboardingSessionId es null,
/// significa que el usuario YA completo el wizard (no debe redirigir).
/// </summary>
public record MiSesionPendienteDto(Guid? OnboardingSessionId, int PasoActual, bool Completado);
