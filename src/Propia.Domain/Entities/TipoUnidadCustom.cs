using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Tipo de unidad personalizado por copropiedad. Spec 2.3 v1.0 - seccion Distribucion.
/// El catalogo base (TipoUnidad enum) es global; estos son extensiones que solo viven
/// dentro de UN tenant. Ejemplos: "Duplex", "Estudio loft", "Suite ejecutiva".
/// </summary>
public class TipoUnidadCustom : TenantEntity
{
    public string Nombre { get; set; } = string.Empty;
    public bool PagaAdministracionPorDefecto { get; set; } = true;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
}
