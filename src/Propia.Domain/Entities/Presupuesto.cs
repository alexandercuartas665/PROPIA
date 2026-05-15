using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Presupuesto anual de la copropiedad. Spec 2.6 v1.0 tabla <c>presupuestos</c>.
/// Solo puede existir una vigencia en estado EnEjecucion por copropiedad (RN-01).
/// </summary>
public class Presupuesto : TenantEntity
{
    public string Nombre { get; set; } = string.Empty;
    public DateOnly VigenciaInicio { get; set; }
    public DateOnly VigenciaFin { get; set; }
    public EstadoPresupuesto Estado { get; set; } = EstadoPresupuesto.Borrador;

    public decimal MontoTotal { get; set; }  // Calculado de la suma de rubros activos
    public TipoAprobacion? AprobacionTipo { get; set; }
    public string? AprobacionActaUrl { get; set; }
    public DateOnly? AprobacionFecha { get; set; }
    public Guid? AprobacionUsuarioId { get; set; }
    public Guid? AsambleaId { get; set; }
    public string? Notas { get; set; }

    public ICollection<PresupuestoRubro> Rubros { get; set; } = new List<PresupuestoRubro>();
}
