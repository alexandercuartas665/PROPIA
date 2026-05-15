using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Registro de gasto ejecutado contra un rubro presupuestal. Spec 2.6 v1.0 tabla
/// <c>ejecucion_presupuestal</c>. Seguimiento operativo paralelo (no contable).
/// </summary>
public class EjecucionPresupuestal : TenantEntity
{
    public Guid PresupuestoRubroId { get; set; }
    public PresupuestoRubro? PresupuestoRubro { get; set; }

    public DateOnly Fecha { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string? SoporteUrl { get; set; }
    public Guid? TareaId { get; set; }  // Modulo 2.10
    public Guid RegistradoPor { get; set; }
}
