using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Ola D (analitica de entregas): aplica a los salientes del tenant los estados de entrega que reportan los
/// webhooks de estado (Evolution messages.update / Meta statuses[]). El tenant viene fijado por la URL del
/// webhook, asi que se opera con SetTenant + el filtro de RLS normal (no IgnoreQueryFilters). Portado de ECOREX.
/// </summary>
public sealed class DeliveryStatusService : IDeliveryStatusService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;

    public DeliveryStatusService(PropiaDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    // Avance monotono: read > delivered > sent. "failed" no entra aqui (se maneja aparte, siempre marca).
    private static int Rank(string? status) => (status ?? "").ToLowerInvariant() switch
    {
        "sent" => 1,
        "delivered" => 2,
        "read" => 3,
        _ => 0
    };

    public async Task<int> AplicarAsync(Guid tenantId, IReadOnlyList<DeliveryStatusUpdate> updates, CancellationToken ct = default)
    {
        if (updates is null || updates.Count == 0) { return 0; }
        _tenant.SetTenant(tenantId);

        // Ids del lote (unicos, no vacios). Correlacionamos por ExternalId contra los salientes del tenant.
        var ids = updates
            .Where(u => !string.IsNullOrWhiteSpace(u.ExternalId))
            .Select(u => u.ExternalId)
            .Distinct()
            .ToList();
        if (ids.Count == 0) { return 0; }

        // Salientes tracked (los vamos a modificar). El filtro de RLS ya acota al tenant fijado.
        var mensajes = await _db.Messages
            .Where(m => m.Direction == MessageDirection.Outbound && m.ExternalId != null && ids.Contains(m.ExternalId))
            .ToListAsync(ct);
        if (mensajes.Count == 0) { return 0; }

        var porId = mensajes
            .GroupBy(m => m.ExternalId!)
            .ToDictionary(g => g.Key, g => g.First());

        var cambiados = 0;
        foreach (var u in updates)
        {
            if (string.IsNullOrWhiteSpace(u.ExternalId)) { continue; }
            if (!porId.TryGetValue(u.ExternalId, out var msg)) { continue; }

            var estado = (u.Status ?? "").Trim().ToLowerInvariant();
            if (estado == "failed")
            {
                // "failed" siempre marca (salvo que ya estuviera failed con el mismo motivo). Guarda el motivo del proveedor.
                var motivo = FormatearMotivo(u.ErrorCode, u.ErrorMessage);
                if (msg.DeliveryStatus == "failed" && msg.DeliveryError == motivo) { continue; }
                msg.DeliveryStatus = "failed";
                msg.DeliveryError = motivo;
                cambiados++;
            }
            else if (Rank(estado) > 0 && Rank(estado) > Rank(msg.DeliveryStatus))
            {
                // Avance monotono: nunca retrocede read -> delivered -> sent. No toca los "failed".
                if (msg.DeliveryStatus == "failed") { continue; }
                msg.DeliveryStatus = estado;
                cambiados++;
            }
        }

        if (cambiados > 0) { await _db.SaveChangesAsync(ct); }
        return cambiados;
    }

    // Mismo formato que ECOREX: "codigo X - texto" / "codigo X" / "texto"; null si el proveedor no dio detalle.
    private static string? FormatearMotivo(string? code, string? message)
        => !string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(message) ? $"codigo {code} - {message}"
            : !string.IsNullOrWhiteSpace(code) ? $"codigo {code}"
            : !string.IsNullOrWhiteSpace(message) ? message
            : null;
}
