using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Unidad privada de la copropiedad (apartamento, local, casa, oficina, parqueadero, etc).
/// El coeficiente de propiedad es porcentual respecto al total de la PH - se usa para
/// calcular cuotas en 2.6. La suma de coeficientes debe ser 100% (validado a nivel de UI).
/// Es TenantEntity - aislada por tenant_id.
/// </summary>
public class UnidadPrivada : TenantEntity
{
    /// <summary>Numero o codigo identificador (101, A-203, L-15, etc).</summary>
    public string Numero { get; set; } = string.Empty;
    public TipoUnidad Tipo { get; set; } = TipoUnidad.Apartamento;

    public Guid? TorreId { get; set; }
    public Torre? Torre { get; set; }

    public int? Piso { get; set; }

    /// <summary>Coeficiente de propiedad en porcentaje (Ley 675 art. 26). Suma total 100.0%.</summary>
    public decimal CoeficientePropiedad { get; set; }

    /// <summary>Area privada en metros cuadrados.</summary>
    public decimal? AreaM2 { get; set; }

    public int? Habitaciones { get; set; }
    public int? Banos { get; set; }
    public int? Parqueaderos { get; set; }

    /// <summary>Estado: habitada / desocupada / arrendada. Texto libre por ahora.</summary>
    public string? Estado { get; set; }

    public string? Observaciones { get; set; }
}
