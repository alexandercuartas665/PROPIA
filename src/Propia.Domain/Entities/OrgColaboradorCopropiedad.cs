using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Asignacion de un colaborador a una copropiedad con un rol especifico de Capa 2.
/// Spec 1.3 v1.0 tabla org_colaborador_copropiedad.
/// El RolCapa2Id apunta a la tabla `rol` del modulo 2.5 - el rol debe pertenecer a esa
/// copropiedad o ser un rol base de plataforma (TenantId = null).
/// Una asignacion activa por (ColaboradorId, TenantId) - RN-08.
/// </summary>
public class OrgColaboradorCopropiedad : BaseEntity
{
    public Guid ColaboradorId { get; set; }
    public OrgColaborador? Colaborador { get; set; }

    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public Guid RolCapa2Id { get; set; }
    public Rol? RolCapa2 { get; set; }

    public DateOnly FechaDesde { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly? FechaHasta { get; set; }

    public bool Activo { get; set; } = true;
}
