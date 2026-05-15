using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Rol del sistema. Spec 2.5 v1.0 tabla <c>rol</c>.
///
/// Estrategia de visibilidad:
///  - <c>Tipo == Sistema</c> o <c>Tipo == Base</c>: <c>TenantId = NULL</c>, visibles para todos los tenants.
///  - <c>Tipo == Extendido</c>: <c>TenantId = NULL</c>, predefinidos por PROPIA, activables por la copropiedad.
///  - <c>Tipo == Personalizado</c>: <c>TenantId NOT NULL</c>, creados por la copropiedad.
///
/// Los Base no se pueden eliminar ni renombrar (RN-03). Solo se edita su matriz de permisos.
/// Los Extendidos eliminados pueden restaurarse desde el catalogo base (RN-05).
/// </summary>
public class Rol : BaseEntity
{
    public Guid? TenantId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public TipoRol Tipo { get; set; }
    public bool EsEliminable { get; set; } = true;
    public bool Activo { get; set; } = true;

    /// <summary>FK al rol origen si fue copiado (informativo, no propagado).</summary>
    public Guid? CopiadoDeRolId { get; set; }
    public Rol? CopiadoDeRol { get; set; }
}
