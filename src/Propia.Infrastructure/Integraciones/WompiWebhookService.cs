using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Integraciones;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Integraciones;

/// <summary>
/// Procesa los webhooks de Wompi de forma idempotente. La firma del evento se valida con el secret
/// de eventos (SHA256 de los valores de signature.properties + timestamp + secret). Idempotente por
/// (transaction.id + timestamp). Concilia con la Factura del modulo 0.2 por su referencia externa.
/// Portado de CUBOT.travels y adaptado a Factura/Suscripcion de PROPIA.
/// </summary>
public sealed class WompiWebhookService : IWompiWebhookService
{
    private readonly PropiaDbContext _db;
    private readonly ISecretProtector _secretProtector;

    public WompiWebhookService(PropiaDbContext db, ISecretProtector secretProtector)
    {
        _db = db;
        _secretProtector = secretProtector;
    }

    public async Task<WompiWebhookResult> ProcessAsync(string rawJson, CancellationToken ct = default)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(rawJson); }
        catch (JsonException) { return WompiWebhookResult.Error; }

        using (doc)
        {
            var root = doc.RootElement;
            if (!root.TryGetProperty("data", out var data)
                || !data.TryGetProperty("transaction", out var tx)
                || !root.TryGetProperty("signature", out var signature)
                || !root.TryGetProperty("timestamp", out var timestampEl))
            {
                return WompiWebhookResult.Error;
            }

            var transactionId = tx.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            var status = tx.TryGetProperty("status", out var stEl) ? stEl.GetString() : null;
            var reference = tx.TryGetProperty("reference", out var refEl) ? refEl.GetString() : null;
            var timestampRaw = timestampEl.GetRawText();

            if (string.IsNullOrEmpty(transactionId)) return WompiWebhookResult.Error;

            var providerEventId = $"{transactionId}:{timestampRaw}";

            // Idempotencia: un reenvio del mismo evento no se vuelve a procesar.
            if (await _db.WompiWebhookEvents.AsNoTracking().AnyAsync(e => e.ProviderEventId == providerEventId, ct))
                return WompiWebhookResult.Duplicate;

            var config = await _db.WompiMasterConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
            string? eventsSecret = null;
            if (config?.EventsSecretEncrypted is { } enc)
            {
                try { eventsSecret = _secretProtector.Unprotect(enc); }
                catch { eventsSecret = null; }
            }
            var signatureValid = eventsSecret is not null && VerifySignature(data, signature, timestampRaw, eventsSecret);

            var record = new WompiWebhookEvent
            {
                ProviderEventId = providerEventId,
                SignatureValid = signatureValid,
                RawPayload = rawJson,
                TransactionId = transactionId,
                Reference = reference,
                ReceivedAt = DateTimeOffset.UtcNow
            };
            _db.WompiWebhookEvents.Add(record);

            if (!signatureValid)
            {
                record.ProcessingStatus = WebhookProcessingStatus.InvalidSignature;
                record.Note = "Firma invalida o sin secret de eventos configurado.";
                await _db.SaveChangesAsync(ct);
                return WompiWebhookResult.InvalidSignature;
            }

            // Conciliacion con el billing 0.2: la factura se localiza por su referencia externa
            // (lo que se envia a Wompi como reference) o por el id de transaccion ya asociado.
            Factura? factura = null;
            if (!string.IsNullOrEmpty(reference))
            {
                factura = await _db.Facturas.FirstOrDefaultAsync(
                    f => f.ReferenciaExterna == reference || f.WompiTransactionId == reference, ct);
            }
            factura ??= await _db.Facturas.FirstOrDefaultAsync(f => f.WompiTransactionId == transactionId, ct);

            if (factura is null)
            {
                record.ProcessingStatus = WebhookProcessingStatus.NoMatchingPayment;
                record.Note = "No se encontro una factura con esa referencia (cola de conciliacion).";
                record.ProcessedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
                return WompiWebhookResult.NoMatchingPayment;
            }

            var mapped = MapStatus(status);
            factura.WompiTransactionId = transactionId;
            if (mapped == EstadoFactura.Pagada)
            {
                factura.Estado = EstadoFactura.Pagada;
                factura.FechaPago = DateTimeOffset.UtcNow;
                await RenovarSuscripcionAsync(factura.SuscripcionId, ct);
            }

            record.ProcessingStatus = WebhookProcessingStatus.Processed;
            record.ProcessedAt = DateTimeOffset.UtcNow;
            record.Note = $"Factura {factura.Id} -> {factura.Estado}.";

            await _db.SaveChangesAsync(ct);
            return WompiWebhookResult.Processed;
        }
    }

    private async Task RenovarSuscripcionAsync(Guid suscripcionId, CancellationToken ct)
    {
        var sub = await _db.Suscripciones.FirstOrDefaultAsync(s => s.Id == suscripcionId, ct);
        if (sub is null) return;

        sub.Estado = EstadoSuscripcion.Activa;
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var basis = sub.FechaProximoCobro > hoy ? sub.FechaProximoCobro : hoy;
        sub.FechaProximoCobro = sub.Ciclo == CicloFacturacion.Anual ? basis.AddYears(1) : basis.AddMonths(1);
    }

    // Wompi: APPROVED/DECLINED/VOIDED/ERROR/PENDING. PROPIA solo marca Pagada/Pendiente/Anulada.
    private static EstadoFactura MapStatus(string? wompiStatus) => wompiStatus?.ToUpperInvariant() switch
    {
        "APPROVED" => EstadoFactura.Pagada,
        "VOIDED" => EstadoFactura.Anulada,
        _ => EstadoFactura.Pendiente
    };

    /// <summary>checksum = SHA256( concat(valores de signature.properties bajo "data") + timestamp + secret ).</summary>
    private static bool VerifySignature(JsonElement data, JsonElement signature, string timestampRaw, string eventsSecret)
    {
        if (!signature.TryGetProperty("checksum", out var checksumEl)
            || !signature.TryGetProperty("properties", out var propsEl)
            || propsEl.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var sb = new StringBuilder();
        foreach (var prop in propsEl.EnumerateArray())
        {
            var path = prop.GetString();
            if (path is null || !TryResolve(data, path, out var value)) return false;
            sb.Append(value);
        }
        sb.Append(timestampRaw);
        sb.Append(eventsSecret);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        var computed = Convert.ToHexString(hash);
        var expected = checksumEl.GetString();
        return expected is not null && string.Equals(computed, expected, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Resuelve una ruta tipo "transaction.amount_in_cents" relativa al objeto data.</summary>
    private static bool TryResolve(JsonElement data, string dottedPath, out string value)
    {
        value = string.Empty;
        var current = data;
        foreach (var segment in dottedPath.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                return false;
        }
        value = current.ValueKind == JsonValueKind.String ? current.GetString() ?? string.Empty : current.GetRawText();
        return true;
    }
}
