using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Configuracion de siembra POR COPROPIEDAD para un rol (override tenant-scoped). Permite configurar
/// las facetas semilla y el "solo directorio" tanto de roles Base/Extendido (globales, tenant_id NULL)
/// como Personalizados, SIN filtrar la config entre copropiedades: como los Base son globales y
/// compartidos, su config de siembra no puede vivir en la fila del rol, sino aqui, atada al tenant.
/// Es la fuente de verdad de la siembra para TODOS los tipos de rol. Unico por (TenantId, RolId).
/// </summary>
public class RolSemillaTenant : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid RolId { get; set; }
    public Rol? Rol { get; set; }

    /// <summary>Facetas semilla (CSV de ints de RolUnidadPersona, ej. "1,2").</summary>
    public string? FacetasSemilla { get; set; }

    /// <summary>Si es true, los usuarios de este rol se ocultan de la lista de Usuarios.</summary>
    public bool SoloDirectorio { get; set; }
}
