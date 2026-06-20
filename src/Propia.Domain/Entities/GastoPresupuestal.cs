using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Movimiento de gasto contra un rubro del presupuesto (2.6 tab Ejecucion).
/// Comprometido = reservado/contratado; Ejecutado = efectivamente pagado.
/// Disponible por rubro = MontoAnual - Comprometido - Ejecutado. No reemplaza la contabilidad.
/// </summary>
public class GastoPresupuestal : TenantEntity
{
    public Guid PresupuestoId { get; set; }
    public Presupuesto? Presupuesto { get; set; }

    public Guid RubroId { get; set; }
    public PresupuestoRubro? Rubro { get; set; }

    public TipoGasto Tipo { get; set; } = TipoGasto.Ejecutado;
    public decimal Monto { get; set; }
    public string? Descripcion { get; set; }
    public DateOnly Fecha { get; set; }
    public string? SoporteUrl { get; set; }

    public Guid? CreadoPorUsuarioId { get; set; }
}
