using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Matriz de permisos por rol, modulo y accion. Spec 2.5 v1.0 tabla <c>rol_permiso</c>.
/// Unica fuente de verdad de "que puede hacer cada rol" (Notas dev: la evaluacion
/// de permisos siempre se ejecuta en backend).
/// </summary>
public class RolPermiso : BaseEntity
{
    public Guid RolId { get; set; }
    public Rol? Rol { get; set; }

    /// <summary>Codigo del modulo (ver <see cref="ModuloCodigo"/>).</summary>
    public string ModuloCodigo { get; set; } = string.Empty;
    public AccionPermiso Accion { get; set; }
    public bool Habilitado { get; set; }
    public NivelDato NivelDato { get; set; } = NivelDato.SinAcceso;
}
