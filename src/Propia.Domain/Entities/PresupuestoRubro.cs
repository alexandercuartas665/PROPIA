using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Rubro presupuestal. Spec 2.6 v1.0 tabla <c>presupuesto_rubros</c>.
/// El Fondo de Imprevistos es obligatorio y no eliminable (RN-02).
/// La base de liquidacion se configura por rubro (RN-14).
/// </summary>
public class PresupuestoRubro : TenantEntity
{
    public Guid PresupuestoId { get; set; }
    public Presupuesto? Presupuesto { get; set; }

    public string Codigo { get; set; } = string.Empty;  // Codigo del catalogo o custom
    public string Nombre { get; set; } = string.Empty;
    public decimal MontoAnual { get; set; }
    public BaseLiquidacion BaseLiquidacion { get; set; } = BaseLiquidacion.Coeficiente;
    public bool EsFondoImprevistos { get; set; }
    public bool EsObligatorio { get; set; }
    public bool Activo { get; set; } = true;
    public int Orden { get; set; }
    public string? NotasInternas { get; set; }
}
