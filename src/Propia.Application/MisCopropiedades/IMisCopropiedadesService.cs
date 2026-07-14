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
/// Alta de copropiedades por parte de un cliente ya autenticado (no confundir con el
/// onboarding publico de /registro, que ademas crea la cuenta y la organizacion).
/// </summary>
public interface IMisCopropiedadesService
{
    Task<CopropiedadCreadaDto> CrearAsync(CrearCopropiedadRequest req, Guid userId, CancellationToken ct);
}
