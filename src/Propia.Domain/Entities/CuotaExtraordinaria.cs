using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Cuota extraordinaria. Spec 2.6 v1.0 tabla <c>cuotas_extraordinarias</c>.
/// Independiente del presupuesto ordinario (RN-06). Requiere soporte de aprobacion (RN-07).
/// </summary>
public class CuotaExtraordinaria : TenantEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string Proposito { get; set; } = string.Empty;
    public decimal MontoTotal { get; set; }
    public FormaRecaudo FormaRecaudo { get; set; }
    public int? NumeroCuotas { get; set; }
    public BaseLiquidacion BaseLiquidacion { get; set; } = BaseLiquidacion.Coeficiente;
    public Guid? ProyectoId { get; set; }  // Modulo 2.10

    public EstadoCuotaExtraordinaria Estado { get; set; } = EstadoCuotaExtraordinaria.PendienteAprobacion;
    public TipoAprobacion? AprobacionTipo { get; set; }
    public string? AprobacionActaUrl { get; set; }
    public Guid? AsambleaId { get; set; }

    public DateOnly? FechaInicioRecaudo { get; set; }
    public DateOnly? FechaFinRecaudo { get; set; }
    public Guid CreadaPorPersonaId { get; set; }
}
