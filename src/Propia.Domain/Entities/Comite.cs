using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Comite de la copropiedad (Convivencia, Obras, Deportes, etc.). Spec 2.3 - seccion 4 Gobierno.
/// La Ley 675 de 2001 obliga el Comite de Convivencia en residenciales.
/// </summary>
public class Comite : TenantEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public DateOnly FechaConformacion { get; set; }
    public DateOnly? FechaFin { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<ComiteMiembro> Miembros { get; set; } = new List<ComiteMiembro>();
}
