using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Torre, bloque o edificio dentro de una copropiedad. Agrupa unidades privadas.
/// Si la copropiedad es una sola edificacion, se crea una sola torre con el mismo nombre.
/// Es TenantEntity - aislada por tenant_id.
/// </summary>
public class Torre : TenantEntity
{
    public string Nombre { get; set; } = string.Empty;
    public int? CantidadPisos { get; set; }
    public string? Descripcion { get; set; }
}
