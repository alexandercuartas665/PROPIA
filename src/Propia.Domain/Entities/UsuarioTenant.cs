using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Vinculo entre una Persona (identidad global) y un Tenant (Copropiedad).
/// Representa el ACCESO de una persona a una copropiedad y su ROL en ella.
/// Una misma Persona puede tener N UsuarioTenant (uno por cada copropiedad donde tiene acceso).
/// Spec: modulo 2.5 Usuarios, Roles y Accesos.
/// Es TenantEntity - se aisla via RLS por tenant_id.
/// </summary>
public class UsuarioTenant : TenantEntity
{
    public Guid PersonaId { get; set; }
    public Persona? Persona { get; set; }

    /// <summary>
    /// Identificador del Rol asignado dentro de la copropiedad (referencia futura a tabla Roles).
    /// Por ahora se almacena como string del nombre del rol (Administrador, Consejero, Residente, etc.).
    /// Cuando se implemente la tabla Roles configurable (paso 2.5 completo), se migrara a RolId.
    /// </summary>
    public string Rol { get; set; } = "Residente";

    public EstadoUsuarioTenant Estado { get; set; } = EstadoUsuarioTenant.Pendiente;
    public DateTimeOffset? UltimoAcceso { get; set; }
    public DateTimeOffset? FechaInvitacion { get; set; }
    public DateTimeOffset? FechaActivacion { get; set; }
}
