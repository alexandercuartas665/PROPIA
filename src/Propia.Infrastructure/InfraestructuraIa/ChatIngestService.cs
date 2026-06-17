using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Ingesta de mensajes entrantes desde el webhook publico de Evolution.
/// - Bypassa el query filter de RLS porque opera con TenantId explicito desde la URL.
/// - Idempotente por (TenantId, ExternalId).
/// - Si el contacto esta en lista negra, rechaza con Blocked (el dispatcher tampoco respondera).
/// - Crea/obtiene Conversation, persiste Message, actualiza LastMessageAt, auto-desarchiva.
/// Portado de CUBOT.travels (ChatIngestService).
/// </summary>
public sealed class ChatIngestService : IChatIngestService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IListaNegraService _listaNegra;
    private readonly IChatBroadcaster? _broadcaster;

    public ChatIngestService(PropiaDbContext db, ITenantContext tenant, IListaNegraService listaNegra, IChatBroadcaster? broadcaster = null)
    {
        _db = db;
        _tenant = tenant;
        _listaNegra = listaNegra;
        _broadcaster = broadcaster;
    }

    public async Task<ChatIngestResult> IngestTrustedAsync(Guid tenantId, IngestMessageRequest payload, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(payload.ExternalId)) return ChatIngestResult.InvalidPayload;
        if (string.IsNullOrWhiteSpace(payload.ContactPhone)) return ChatIngestResult.InvalidPayload;

        var phone = new string(payload.ContactPhone.Where(char.IsDigit).ToArray());
        if (phone.Length < 7) return ChatIngestResult.InvalidPayload;

        // Setear tenant en contexto para que el query filter no descarte resultados.
        _tenant.SetTenant(tenantId);

        // Idempotencia: ya existe ese mensaje en este tenant?
        var yaExiste = await _db.Messages.AsNoTracking()
            .AnyAsync(m => m.TenantId == tenantId && m.ExternalId == payload.ExternalId, ct);
        if (yaExiste) return ChatIngestResult.Duplicate;

        // Lista negra: si el contacto esta bloqueado, descartamos el mensaje sin persistir
        // (el operador puede ver el evento en logs si decide, pero la conversacion no se infla).
        if (await _listaNegra.EstaBloqueadoAsync(phone, ct))
        {
            return ChatIngestResult.Blocked;
        }

        // Resolver linea por InstanceName (opcional). Si no se pasa o no se encuentra, la conversacion
        // queda sin linea (puede vincularse luego en la UI o cuando se responda).
        Guid? lineId = null;
        if (!string.IsNullOrWhiteSpace(payload.InstanceName))
        {
            lineId = await _db.WhatsAppLines.AsNoTracking()
                .Where(l => l.TenantId == tenantId && l.InstanceName == payload.InstanceName)
                .Select(l => (Guid?)l.Id)
                .FirstOrDefaultAsync(ct);
        }

        // Obtener o crear conversacion por (TenantId, ContactPhone).
        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ContactPhone == phone, ct);
        var now = payload.SentAt ?? DateTimeOffset.UtcNow;
        if (conv is null)
        {
            conv = new Conversation
            {
                TenantId = tenantId,
                ContactPhone = phone,
                ContactName = string.IsNullOrWhiteSpace(payload.ContactName) ? null : payload.ContactName.Trim(),
                WhatsAppLineId = lineId,
                LastMessageAt = now
            };
            _db.Conversations.Add(conv);
        }
        else
        {
            // Actualizar nombre si vino uno nuevo y la conv no tenia.
            if (string.IsNullOrWhiteSpace(conv.ContactName) && !string.IsNullOrWhiteSpace(payload.ContactName))
            {
                conv.ContactName = payload.ContactName.Trim();
            }
            // Auto-desarchivar al recibir un entrante (CUBOT pattern).
            if (conv.ArchivedAt.HasValue) conv.ArchivedAt = null;
            // LastMessageAt solo avanza hacia adelante.
            if (now > (conv.LastMessageAt ?? DateTimeOffset.MinValue)) conv.LastMessageAt = now;
            if (lineId.HasValue && conv.WhatsAppLineId is null) conv.WhatsAppLineId = lineId;
        }

        // Validar tipo de media.
        var mediaType = (payload.MessageType?.ToLowerInvariant()) switch
        {
            "image" => MessageMediaType.Image,
            "video" => MessageMediaType.Video,
            "audio" => MessageMediaType.Audio,
            "document" => MessageMediaType.Document,
            "location" => MessageMediaType.Location,
            _ => MessageMediaType.None
        };

        var msg = new Message
        {
            TenantId = tenantId,
            Conversation = conv,
            Direction = MessageDirection.Inbound,
            ExternalId = payload.ExternalId,
            Body = payload.Body ?? string.Empty,
            MessageType = string.IsNullOrWhiteSpace(payload.MessageType) ? "text" : payload.MessageType,
            SentAt = now,
            MediaType = mediaType
        };
        _db.Messages.Add(msg);

        await _db.SaveChangesAsync(ct);

        if (_broadcaster is not null)
        {
            try
            {
                var dto = new MensajeDto(
                    msg.Id, msg.ConversationId, msg.Direction.ToString(),
                    msg.Body, msg.MessageType, msg.SentAt, msg.SentByName,
                    msg.MediaType.ToString(), msg.MediaUrl, msg.MediaMimeType, msg.ExternalId);
                await _broadcaster.NotifyMessageAddedAsync(tenantId, conv.Id, dto, ct);
            }
            catch { /* webhook no debe fallar si SignalR esta caido */ }
        }
        return ChatIngestResult.Accepted;
    }
}
