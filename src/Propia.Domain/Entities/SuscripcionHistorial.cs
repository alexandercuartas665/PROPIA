using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Historial APPEND-ONLY de todos los eventos relacionados con una suscripcion:
/// activaciones, upgrades, downgrades, suspensiones, reactivaciones, cancelaciones,
/// cambios de metodo de pago, ajustes manuales del Super Admin, creditos aplicados.
///
/// La inmutabilidad se refuerza con trigger PostgreSQL (similar a super_admin_logs).
/// Sin UPDATE, sin DELETE - solo INSERT.
/// </summary>
public class SuscripcionHistorial : BaseEntity
{
    public Guid SuscripcionId { get; set; }
    public Suscripcion? Suscripcion { get; set; }

    public TipoEventoSuscripcion Tipo { get; set; }
    public OrigenEventoSuscripcion Origen { get; set; } = OrigenEventoSuscripcion.Sistema;

    /// <summary>SuperAdminUsuario o ApplicationUser que disparo el evento. null si fue Sistema.</summary>
    public Guid? ActorId { get; set; }

    public Guid? PlanAnteriorId { get; set; }
    public Guid? PlanNuevoId { get; set; }
    public string? EstadoAnterior { get; set; }
    public string? EstadoNuevo { get; set; }
    public decimal? MontoProrrateo { get; set; }
    public decimal? CreditoGenerado { get; set; }
    public string? Notas { get; set; }
}
