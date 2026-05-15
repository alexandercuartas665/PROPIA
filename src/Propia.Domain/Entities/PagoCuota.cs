using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Pago recibido contra una liquidacion u otra cuota. Spec 2.6 v1.0 tabla <c>pagos</c>.
/// El dinero va directo a la cuenta bancaria de la copropiedad (RN-08, PROPIA no intermedia).
/// Pagos manuales quedan marcados con <see cref="EsManual"/> + trazabilidad (RN-10).
/// </summary>
public class PagoCuota : TenantEntity
{
    public Guid UnidadPrivadaId { get; set; }
    public UnidadPrivada? UnidadPrivada { get; set; }

    public Guid? PersonaId { get; set; }
    public Guid? LiquidacionUnidadId { get; set; }
    public LiquidacionUnidad? LiquidacionUnidad { get; set; }
    public Guid? CuotaExtraordinariaId { get; set; }
    public CuotaExtraordinaria? CuotaExtraordinaria { get; set; }

    public TipoPago Tipo { get; set; }
    public CanalPago Canal { get; set; }
    public decimal Monto { get; set; }
    public string? ReferenciaExterna { get; set; }  // ID de transaccion en Wompi
    public DateTimeOffset? FechaPago { get; set; }
    public EstadoPago Estado { get; set; } = EstadoPago.Pendiente;
    public bool EsManual { get; set; }
    public Guid? RegistradoPor { get; set; }  // Solo si es manual
    public string? Notas { get; set; }
}
