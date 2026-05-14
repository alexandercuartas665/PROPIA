using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Factura emitida a un cliente por un periodo de suscripcion.
/// PROPIA NO almacena PDFs - solo metadatos (numero, CUFE de DIAN, referencia, estado).
/// El PDF lo custodia el sistema contable externo (Siigo/Alegra).
/// Retencion: 5 anios desde fecha de emision.
/// </summary>
public class Factura : BaseEntity
{
    public Guid SuscripcionId { get; set; }
    public Suscripcion? Suscripcion { get; set; }

    /// <summary>Numero de factura asignado por el sistema contable (Siigo/Alegra).</summary>
    public string? NumeroFactura { get; set; }

    /// <summary>Codigo unico de facturacion electronica (DIAN).</summary>
    public string? Cufe { get; set; }

    /// <summary>ID interno de la factura en Siigo/Alegra.</summary>
    public string? ReferenciaExterna { get; set; }

    public DateOnly PeriodoDesde { get; set; }
    public DateOnly PeriodoHasta { get; set; }

    public decimal Subtotal { get; set; }
    public decimal Descuento { get; set; }
    public decimal ImpuestoPct { get; set; }
    public decimal ImpuestoValor { get; set; }
    public decimal Total { get; set; }

    public EstadoFactura Estado { get; set; } = EstadoFactura.Pendiente;
    public DateTimeOffset FechaEmision { get; set; }
    public DateOnly FechaVencimiento { get; set; }
    public DateTimeOffset? FechaPago { get; set; }

    public Guid? MetodoPagoId { get; set; }
    public MetodoPago? MetodoPago { get; set; }

    /// <summary>ID de transaccion en Wompi cuando el pago se procesa via pasarela.</summary>
    public string? WompiTransactionId { get; set; }
}
