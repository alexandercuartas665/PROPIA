using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Snapshot inmutable de una liquidacion mensual. Spec 2.6 v1.0 tabla <c>liquidaciones</c>.
/// RN-04: no se recalculan liquidaciones pasadas. RN-05: la reliquidacion es siempre manual.
/// </summary>
public class Liquidacion : TenantEntity
{
    public Guid PresupuestoId { get; set; }
    public Presupuesto? Presupuesto { get; set; }

    public DateOnly Periodo { get; set; }  // Primer dia del mes liquidado
    public EstadoLiquidacion Estado { get; set; } = EstadoLiquidacion.Emitida;
    public decimal MontoTotal { get; set; }
    public string SnapshotCalculo { get; set; } = "{}";  // JSON inmutable con coeficientes + bases
    public Guid EmitidaPor { get; set; }

    public ICollection<LiquidacionUnidad> Detalle { get; set; } = new List<LiquidacionUnidad>();
}
