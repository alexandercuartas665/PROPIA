using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Liquidacion por unidad privada (renglon del snapshot). Spec 2.6 v1.0 tabla
/// <c>liquidaciones_unidades</c>. Contiene el desglose JSON por rubro.
/// </summary>
public class LiquidacionUnidad : TenantEntity
{
    public Guid LiquidacionId { get; set; }
    public Liquidacion? Liquidacion { get; set; }

    public Guid UnidadPrivadaId { get; set; }
    public UnidadPrivada? UnidadPrivada { get; set; }

    public Guid? PersonaId { get; set; }  // Propietario/residente al momento de la liquidacion
    public decimal Monto { get; set; }
    public string Desglose { get; set; } = "[]";  // JSON con monto por rubro
    public EstadoPagoLiquidacion EstadoPago { get; set; } = EstadoPagoLiquidacion.Pendiente;
}
