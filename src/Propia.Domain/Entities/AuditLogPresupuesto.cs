using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Log append-only de eventos del modulo 2.6. Spec v1.0 tabla <c>audit_log_presupuesto</c>.
/// Trigger BEFORE UPDATE/DELETE aborta - inalterable.
/// </summary>
public class AuditLogPresupuesto : TenantEntity
{
    public string Entidad { get; set; } = string.Empty;       // presupuesto | rubro | liquidacion | pago | ...
    public Guid EntidadId { get; set; }
    public string Accion { get; set; } = string.Empty;        // creado | editado | aprobado | activado | cerrado | etc
    public string? ValorAnterior { get; set; }                 // JSON
    public string? ValorNuevo { get; set; }                    // JSON
    public Guid UsuarioId { get; set; }
}
