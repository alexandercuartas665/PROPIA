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

    /// <summary>
    /// Facetas de la distribucion (RolUnidadPersona) para las que este rol es la "semilla":
    /// al asignar una persona a esa faceta en una unidad, se le crea automaticamente
    /// usuario+directorio con este rol. CSV de valores int del enum RolUnidadPersona
    /// (ej. "1,2" = Propietario,Residente). Exclusiva: una faceta pertenece a un solo rol.
    /// Solo aplica a roles Personalizados (tenant-scoped) para no filtrar config entre tenants.
    /// </summary>
    public string? FacetasSemilla { get; set; }

    /// <summary>
    /// Si es true, los usuarios con este rol se ocultan de la lista de Usuarios y se
    /// gestionan solo desde el Directorio (para no llenar Usuarios de propietarios/residentes).
    /// </summary>
    public bool SoloDirectorio { get; set; }
}
