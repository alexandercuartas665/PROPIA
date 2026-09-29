using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Override POR COPROPIEDAD de una celda de la matriz de permisos de un rol GLOBAL (Base/Extendido,
/// <c>Rol.TenantId == NULL</c>). Spec 2.5 v2.0 RP-04/RN-07: la config de roles es independiente por
/// copropiedad. Los roles base son definiciones globales compartidas con una matriz por DEFECTO en
/// <c>rol_permisos</c>; cuando una copropiedad edita la matriz de un rol base, el cambio se guarda
/// aqui (tenant-scoped) y NO afecta a las demas. La matriz EFECTIVA de un rol global en un tenant =
/// override si existe, si no el default global. Los roles Personalizados (tenant-scoped) siguen
/// usando <c>rol_permisos</c> directo (aislados por su RolId). Gemelo de <see cref="RolSemillaTenant"/>,
/// que ya hace lo mismo para la config de siembra.
/// </summary>
public class RolPermisoTenant : TenantEntity
{
    public Guid RolId { get; set; }
    public Rol? Rol { get; set; }

    /// <summary>Codigo del modulo (ver <see cref="ModuloCodigo"/>).</summary>
    public string ModuloCodigo { get; set; } = string.Empty;
    public AccionPermiso Accion { get; set; }
    public bool Habilitado { get; set; }
    public NivelDato NivelDato { get; set; } = NivelDato.SinAcceso;
}
