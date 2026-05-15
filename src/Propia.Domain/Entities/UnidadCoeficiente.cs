using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Valor de un coeficiente especifico para una unidad privada. Modela la
/// relacion M-N entre Unidad y TipoCoeficiente con su valor.
/// </summary>
public class UnidadCoeficiente : TenantEntity
{
    public Guid UnidadId { get; set; }
    public UnidadPrivada? Unidad { get; set; }

    public Guid TipoCoeficienteId { get; set; }
    public TipoCoeficiente? TipoCoeficiente { get; set; }

    public decimal Valor { get; set; }  // En porcentaje (0..100)
}
