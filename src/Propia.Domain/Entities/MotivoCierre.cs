using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Motivo de cierre configurable por copropiedad. Se usa al cerrar una tarjeta (tarea o PQRSD):
/// el usuario elige un motivo y este ya trae su clasificacion (cierre correcto / via interna
/// agotada / perdida). Catalogos SEPARADOS por modulo via <see cref="Modulo"/>.
/// </summary>
public class MotivoCierre : TenantEntity
{
    /// <summary>"tareas" o "pqrsd": separa los catalogos por modulo.</summary>
    public string Modulo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Clasificacion incorporada del motivo (para reportes y semantica de cierre).</summary>
    public ClasificacionCierre Clasificacion { get; set; } = ClasificacionCierre.CierreCorrecto;

    /// <summary>Sembrado por defecto por el sistema (no se puede borrar, solo desactivar).</summary>
    public bool EsBase { get; set; }

    public bool Activo { get; set; } = true;
    public int Orden { get; set; }
}
