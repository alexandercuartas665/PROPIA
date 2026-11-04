namespace Propia.Application.InfraestructuraIa;

/// <summary>
/// Ola D (analitica de entregas): un estado de entrega reportado por el webhook del proveedor para un
/// saliente. <see cref="ExternalId"/> es el id del proveedor (key.id de Evolution / wamid de Meta) con el
/// que se correlaciona el <c>Message</c> saliente. <see cref="Status"/> ya viene NORMALIZADO a uno de
/// "sent" | "delivered" | "read" | "failed". En fallos, el codigo/texto del proveedor va en Error*.
/// </summary>
public sealed record DeliveryStatusUpdate(
    string ExternalId,
    string Status,
    string? ErrorCode = null,
    string? ErrorMessage = null);

/// <summary>
/// Aplica a los salientes del tenant los estados de entrega que llegan por el webhook de estado de cada
/// proveedor (Evolution messages.update / Meta statuses[]). Correlaciona por ExternalId y avanza el estado
/// de forma MONOTONA (sent -> delivered -> read; "failed" marca y guarda el motivo). Portado de ECOREX.
/// </summary>
public interface IDeliveryStatusService
{
    /// <summary>
    /// Aplica <paramref name="updates"/> a los salientes del tenant <paramref name="tenantId"/> (fija el tenant
    /// en contexto). Devuelve cuantos mensajes cambiaron de estado. Es idempotente: un estado igual o anterior
    /// al ya guardado no retrocede nada.
    /// </summary>
    Task<int> AplicarAsync(Guid tenantId, IReadOnlyList<DeliveryStatusUpdate> updates, CancellationToken ct = default);
}
