using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Bitacora de cambios sensibles de la copropiedad (spec 2.3 RN-06). Registro en lenguaje
/// natural de cambios de coeficientes, parametros financieros (moneda, dia de corte, tasa de
/// mora) y estados. Es TenantEntity - aislada por tenant_id. Solo lectura desde la UI.
/// </summary>
public class BitacoraMiCopropiedad : TenantEntity
{
    /// <summary>Categoria del cambio: Coeficiente, Finanzas, Zona, Equipo, Identidad...</summary>
    public string Categoria { get; set; } = string.Empty;

    /// <summary>Descripcion en lenguaje natural ("Cambio de moneda de COP a USD").</summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Nombre/identificacion del autor del cambio (opcional).</summary>
    public string? Autor { get; set; }
}
