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
    /// Nombre legacy del rol (Administrador, Consejero, Residente...). Mantenido como
    /// snapshot textual para compatibilidad. La fuente real es <see cref="RolId"/>.
    /// </summary>
    public string Rol { get; set; } = "Residente";

    /// <summary>Rol asignado en esta copropiedad (FK a Rol, spec 2.5).</summary>
    public Guid? RolId { get; set; }
    public Rol? RolNavigation { get; set; }

    public EstadoUsuarioTenant Estado { get; set; } = EstadoUsuarioTenant.Pendiente;
    public DateTimeOffset? UltimoAcceso { get; set; }
    public DateTimeOffset? FechaInvitacion { get; set; }
    public DateTimeOffset? FechaActivacion { get; set; }

    /// <summary>Motivo de revocacion cuando Estado == Inactiva (spec 2.5 HU-02).</summary>
    public string? MotivoRevocacion { get; set; }
    public DateTimeOffset? FechaRevocacion { get; set; }
}
