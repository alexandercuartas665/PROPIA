using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Tenant == Copropiedad (Propiedad Horizontal). Es la entidad raiz de Capa 2.
/// Toda informacion operativa pertenece a un Tenant - el Tenant es duenio de la data.
/// El campo TenantId de todas las TenantEntity apunta al Id de un Tenant.
/// Spec: modulo 2.3 Mi Copropiedad + modulo 0.1 Super Admin Console (estados y custodia).
/// </summary>
public class Tenant : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string? Nit { get; set; }
    public string? DigitoVerificacion { get; set; }
    public string? Direccion { get; set; }
    public string? Ciudad { get; set; }
    public string? Departamento { get; set; }
    public string? CodigoPropia { get; set; }  // Codigo legible asignado por la plataforma

    // Datos para el modulo 2.3 Mi Copropiedad - seccion Identidad
    public TipoCopropiedad? TipoCopropiedad { get; set; }
    public Estrato? Estrato { get; set; }
    public string? FotoFachadaUrl { get; set; }
    public string? LogoUrl { get; set; }
    public string? Descripcion { get; set; }

    public EstadoCopropiedad Estado { get; set; } = EstadoCopropiedad.Activa;
    public EstadoCustodia EstadoCustodia { get; set; } = EstadoCustodia.SinAdmin;

    public DateTimeOffset? FechaActivacion { get; set; }
    public DateTimeOffset? FechaCancelacion { get; set; }

    // Organizacion actual a cargo (puede ser null cuando el estado es SinAdmin)
    public Guid? OrganizacionId { get; set; }
    public Organizacion? Organizacion { get; set; }

    // Navegacion inversa
    public ICollection<UsuarioTenant> Usuarios { get; set; } = new List<UsuarioTenant>();
}
