using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Suscripcion comercial activa entre A&D GROUP y un cliente (organizacion o copropiedad).
/// REGLA: pertenece a Organizacion XOR Copropiedad (CHECK constraint en BD).
/// Entidad GLOBAL del modulo 0.2.
/// </summary>
public class Suscripcion : BaseEntity
{
    public Guid? OrganizacionId { get; set; }
    public Organizacion? Organizacion { get; set; }

    public Guid? CopropiedadId { get; set; }
    public Tenant? Copropiedad { get; set; }

    public Guid PlanId { get; set; }
    public Plan? Plan { get; set; }

    public CicloFacturacion Ciclo { get; set; } = CicloFacturacion.Mensual;
    public EstadoSuscripcion Estado { get; set; } = EstadoSuscripcion.Trial;

    /// <summary>Fecha de activacion (fin de trial si aplica) - establece el aniversario.</summary>
    public DateOnly FechaInicio { get; set; }

    /// <summary>Dia del mes en que se cobra recurrentemente (1-31). Aniversario fijo.</summary>
    public int FechaAniversario { get; set; }

    public DateOnly FechaProximoCobro { get; set; }
    public DateOnly? FechaFinTrial { get; set; }

    public Guid? MetodoPagoId { get; set; }
    public MetodoPago? MetodoPago { get; set; }

    public Guid? CuponId { get; set; }
    public Cupon? Cupon { get; set; }

    public decimal CreditoAFavor { get; set; }
}
