using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Cada intento de cobrar una factura via pasarela (Wompi o manual).
/// El primer intento es el del dia de aniversario, los siguientes son reintentos
/// configurados en billing_config (por defecto en dias 1, 3, 7).
/// </summary>
public class IntentoCobro : BaseEntity
{
    public Guid FacturaId { get; set; }
    public Factura? Factura { get; set; }

    public Guid SuscripcionId { get; set; }
    public Suscripcion? Suscripcion { get; set; }

    /// <summary>1 = primer intento (cobro inicial), 2..N = reintentos.</summary>
    public int NumeroIntento { get; set; }
    public DateTimeOffset FechaIntento { get; set; }
    public ResultadoIntentoCobro Resultado { get; set; } = ResultadoIntentoCobro.Pendiente;
    public string? CodigoError { get; set; }
    public string? DescripcionError { get; set; }

    /// <summary>Respuesta JSON completa de Wompi - para auditoria y debug.</summary>
    public string? WompiResponse { get; set; }
}
