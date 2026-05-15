using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Estado configurable por copropiedad para tareas. Spec 2.10 v1.0 seccion 4.
/// Los 6 estados base se siembran al primer acceso al modulo por tenant.
/// Estados terminales (EsTerminal = true) no se pueden renombrar ni eliminar.
/// </summary>
public class TareaEstado : TenantEntity
{
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Color hexadecimal para chips en UI. Opcional.</summary>
    public string? Color { get; set; }

    public int Orden { get; set; }

    /// <summary>True = Completada o Cancelada. No editables ni eliminables.</summary>
    public bool EsTerminal { get; set; }

    public bool EsBase { get; set; }

    public bool Activo { get; set; } = true;
}
