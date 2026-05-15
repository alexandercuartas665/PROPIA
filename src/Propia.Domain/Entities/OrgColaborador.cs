using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Colaborador vinculado al equipo de una organizacion. Spec 1.3 v1.0 tabla org_colaborador.
/// Entidad GLOBAL (sin tenant_id). Una persona puede ser colaboradora de N organizaciones
/// (una sola fila por (OrganizacionId, PersonaId) - RN-01 identidad unica).
/// Al desactivar, se conserva el registro y el historial - RN-05.
/// </summary>
public class OrgColaborador : BaseEntity
{
    public Guid OrganizacionId { get; set; }
    public Organizacion? Organizacion { get; set; }

    public Guid PersonaId { get; set; }
    public Persona? Persona { get; set; }

    public Guid CargoId { get; set; }
    public OrgCargo? Cargo { get; set; }

    public EstadoColaborador Estado { get; set; } = EstadoColaborador.Pendiente;

    public DateOnly FechaVinculacion { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly? FechaDesvinculacion { get; set; }

    /// <summary>Usuario (Director) que envio la invitacion. FK opcional para auditoria.</summary>
    public Guid? InvitadoPor { get; set; }

    /// <summary>Notas o tags reservados para IA en fase posterior (spec 1.3 nota dev 11).</summary>
    public string? NotasIa { get; set; }

    public ICollection<OrgColaboradorPermiso> PermisosIndividuales { get; set; } = new List<OrgColaboradorPermiso>();
    public ICollection<OrgColaboradorCopropiedad> Asignaciones { get; set; } = new List<OrgColaboradorCopropiedad>();
    public ICollection<OrgColaboradorHistorial> Historial { get; set; } = new List<OrgColaboradorHistorial>();
}
