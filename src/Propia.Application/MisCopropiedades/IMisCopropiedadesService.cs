using Propia.Domain.Enums;

namespace Propia.Application.MisCopropiedades;

/// <summary>
/// Datos minimos para dar de alta una copropiedad desde el selector (wizard "Nueva copropiedad").
/// Solo identidad: el resto (distribucion, zonas, equipos...) se completa despues en Mi Copropiedad.
/// </summary>
public record CrearCopropiedadRequest(
    string Nombre,
    string? Nit,
    string? DigitoVerificacion,
    TipoCopropiedad? Tipo,
    Estrato? Estrato,
    string? Direccion,
    string? Departamento,
    string? Ciudad);

public record CopropiedadCreadaDto(Guid TenantId, string Nombre);

/// <summary>
/// Alta desde el onboarding de bienvenida (/bienvenida): el usuario puede NO tener tenant activo
/// ni organizacion todavia. Si no administra ninguna copropiedad, se crea la organizacion segun
/// su perfil (empresa administradora o independiente/autoadministrada).
/// </summary>
public record CrearPrimeraCopropiedadRequest(
    bool EsEmpresa,
    string? EmpresaNombre,
    string? EmpresaNit,
    string Nombre,
    string? Nit,
    string? DigitoVerificacion,
    TipoCopropiedad? Tipo,
    Estrato? Estrato,
    string? Direccion,
    string? Departamento,
    string? Ciudad,
    string? Telefono,
    string? Email,
    string? Descripcion);

/// <summary>
/// Alta de copropiedades por parte de un cliente ya autenticado (no confundir con el
/// onboarding publico de /registro, que ademas crea la cuenta y la organizacion).
/// </summary>
public interface IMisCopropiedadesService
{
    Task<CopropiedadCreadaDto> CrearAsync(CrearCopropiedadRequest req, Guid userId, CancellationToken ct);

    /// <summary>Alta desde /bienvenida: tolera sesion SIN tenant activo y crea organizacion si hace falta.</summary>
    Task<CopropiedadCreadaDto> CrearPrimeraAsync(CrearPrimeraCopropiedadRequest req, Guid userId, CancellationToken ct);
}
