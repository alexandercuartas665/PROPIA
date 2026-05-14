using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Cupon de descuento que el cliente activa al ingresar un codigo.
/// MVP: solo el modelo de datos. La aplicacion automatica del descuento queda
/// para Fase 2 del modulo 0.2 (junto con motor de prorrateo y reintentos).
/// </summary>
public class Cupon : BaseEntity
{
    public string Codigo { get; set; } = string.Empty;
    public TipoCupon Tipo { get; set; }

    /// <summary>Significado depende del Tipo: % o COP o cantidad de meses.</summary>
    public decimal Valor { get; set; }

    public AplicacionCupon Aplicacion { get; set; } = AplicacionCupon.UnaVez;
    public int? MesesAplicacion { get; set; }

    public DateOnly VigenciaDesde { get; set; }
    public DateOnly? VigenciaHasta { get; set; }
    public int? UsosMaximos { get; set; }
    public int UsosActuales { get; set; }

    /// <summary>JSON array de plan_id que aceptan este cupon. null = todos.</summary>
    public string? PlanesAplicables { get; set; }

    public bool Acumulable { get; set; }
    public bool Activo { get; set; } = true;
}
