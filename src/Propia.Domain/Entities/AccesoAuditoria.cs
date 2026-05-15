using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Log de auditoria append-only. Spec 2.5 v1.0 tabla <c>acceso_auditoria</c>.
/// La tabla tiene trigger BEFORE UPDATE/DELETE que aborta - ningun rol puede modificar
/// ni eliminar registros (RN-14, mismo patron que <see cref="SuperAdminLog"/>).
/// </summary>
public class AccesoAuditoria : BaseEntity
{
    public Guid? UsuarioId { get; set; }
    public Guid? TenantId { get; set; }
    public TipoEventoAuditoria TipoEvento { get; set; }
    public Guid? ActorUsuarioId { get; set; }
    public Guid? EntidadAfectadaId { get; set; }
    public string? Detalle { get; set; }  // JSON serializado
    public string? IpOrigen { get; set; }
    public string? Dispositivo { get; set; }
    public CanalAcceso Canal { get; set; } = CanalAcceso.Web;
}
