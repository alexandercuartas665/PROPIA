using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Persona juridica. Entidad GLOBAL de la plataforma - identidad unica por NIT.
/// Una empresa existe UNA sola vez en la plataforma. Puede ser proveedor de N copropiedades.
/// Spec: modulo 2.4 Directorio (entidad Empresa) v1.0.
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

    // Modulo 2.4 (spec v1.0)
    public string? TipoEmpresa { get; set; }
    public string? SectorEconomico { get; set; }
    public string? RegimenTributario { get; set; }
    public string? SitioWeb { get; set; }
    public string? LogoUrl { get; set; }
    public Guid? RepresentanteLegalPersonaId { get; set; }
    public Persona? RepresentanteLegal { get; set; }
    public bool PerfilIncompleto { get; set; } = true;
    public EstadoDirectorio EstadoDirectorio { get; set; } = EstadoDirectorio.Activo;
}
