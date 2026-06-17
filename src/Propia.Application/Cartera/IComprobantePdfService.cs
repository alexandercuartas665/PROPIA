namespace Propia.Application.Cartera;

/// <summary>
/// Genera un comprobante PDF a partir de un pago registrado.
/// Implementacion en Infrastructure usando QuestPDF.
/// </summary>
public interface IComprobantePdfService
{
    /// <summary>
    /// Devuelve el PDF como arreglo de bytes y un nombre sugerido de archivo.
    /// Si el pago no existe, devuelve null.
    /// </summary>
    Task<(byte[] Pdf, string FileName)?> GenerarComprobantePagoAsync(Guid pagoId, CancellationToken ct);
}
