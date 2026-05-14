using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Persona juridica. Entidad GLOBAL de la plataforma - identidad unica por NIT.
/// Una empresa existe UNA sola vez en la plataforma. Puede ser proveedor de N copropiedades.
/// Spec: modulo 2.4 Directorio (entidad Empresa).
/// El vinculo Empresa <-> Copropiedad se modela en un modulo posterior (EmpresaTenant).
/// </summary>
public class Empresa : BaseEntity
{
    public string Nit { get; set; } = string.Empty;     // Sin digito de verificacion - el DV se guarda aparte
    public string? DigitoVerificacion { get; set; }
    public string RazonSocial { get; set; } = string.Empty;
    public string? NombreComercial { get; set; }
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
}
