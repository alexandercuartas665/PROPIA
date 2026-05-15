using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Cargo configurable dentro de una organizacion administradora. Spec 1.3 v1.0 tabla org_cargo.
/// Entidad GLOBAL (sin tenant_id) - vive a nivel Organizacion (Capa 1).
/// Los cargos por defecto se siembran al activar la organizacion y son editables.
/// RN-06: un cargo con colaboradores activos no puede eliminarse, solo renombrarse o reasignar.
/// RN-07: el nombre del cargo es unico dentro de la organizacion.
/// </summary>
public class OrgCargo : BaseEntity
{
    public Guid OrganizacionId { get; set; }
    public Organizacion? Organizacion { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    /// <summary>True si el cargo viene del catalogo base (Director, Coordinador, etc).</summary>
    public bool EsDefault { get; set; }

    public bool Activo { get; set; } = true;

    public ICollection<OrgCargoPermiso> Permisos { get; set; } = new List<OrgCargoPermiso>();
    public ICollection<OrgColaborador> Colaboradores { get; set; } = new List<OrgColaborador>();
}
