using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Bandeja humana de conversaciones (Oleada IA #3). Permite al operador ver, responder, archivar
/// y bloquear conversaciones del agente IA. Portado de CUBOT.travels (ChatService) con alcance
/// reducido: sin multimedia inline, sin SignalR todavia, sin templates.
/// </summary>
public sealed class ConversacionService : IConversacionService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IWhatsAppConnectorService _connector;
    private readonly IListaNegraService _listaNegra;

    public ConversacionService(
        PropiaDbContext db,
        ITenantContext tenant,
        IWhatsAppConnectorService connector,
        IListaNegraService listaNegra)
    {
        _db = db;
        _tenant = tenant;
        _connector = connector;
        _listaNegra = listaNegra;
    }

    public async Task<IReadOnlyList<ConversacionDto>> ListarActivasAsync(string? buscar, CancellationToken ct = default)
    {
        var q = _db.Conversations.AsNoTracking()
            .Where(c => c.ArchivedAt == null && c.LastMessageAt != null);

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var b = buscar.Trim().ToLower();
            q = q.Where(c =>
                c.ContactPhone.ToLower().Contains(b) ||
                (c.ContactName != null && c.ContactName.ToLower().Contains(b)));
        }

        return await BuildDtoQuery(q.OrderByDescending(c => c.LastMessageAt)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ConversacionDto>> ListarArchivadasAsync(CancellationToken ct = default)
    {
        var q = _db.Conversations.AsNoTracking()
            .Where(c => c.ArchivedAt != null)
            .OrderByDescending(c => c.ArchivedAt);
        return await BuildDtoQuery(q).ToListAsync(ct);
    }

    public async Task<ConversacionDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        return await BuildDtoQuery(_db.Conversations.AsNoTracking().Where(c => c.Id == id))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<MensajeDto>> ListarMensajesAsync(Guid conversationId, CancellationToken ct = default)
    {
        return await _db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.SentAt)
            .Select(m => new MensajeDto(
                m.Id, m.ConversationId, m.Direction.ToString(),
                m.Body, m.MessageType, m.SentAt, m.SentByName,
                m.MediaType.ToString(), m.MediaUrl, m.MediaMimeType, m.ExternalId))
            .ToListAsync(ct);
    }

    public async Task<MensajeDto?> EnviarTextoAsync(Guid conversationId, EnviarMensajeRequest req, CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is not Guid tenantId) return null;
        if (string.IsNullOrWhiteSpace(req.Body)) return null;

        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId, ct);
        if (conv is null) return null;

        // Envio efectivo via la linea WhatsApp (si esta vinculada). Si no, guardamos local
        // marcando como pendiente (sin ExternalId). Cuando el conector exponga MessageId
        // se podra reconciliar la idempotencia con el webhook entrante.
        if (conv.WhatsAppLineId.HasValue)
        {
            try
            {
                await _connector.SendTestAsync(conv.WhatsAppLineId.Value, conv.ContactPhone, req.Body.Trim(), ct);
            }
            catch { /* el mensaje queda guardado localmente aunque el envio falle */ }
        }

        var now = DateTimeOffset.UtcNow;
        var msg = new Message
        {
            TenantId = tenantId,
            ConversationId = conversationId,
            Direction = MessageDirection.Outbound,
            ExternalId = null,
            Body = req.Body.Trim(),
            MessageType = "text",
            SentAt = now,
            MediaType = MessageMediaType.None,
            SentByName = "Operador" // TODO: traer nombre real del usuario logueado cuando se exponga
        };
        _db.Messages.Add(msg);

        conv.LastMessageAt = now;
        if (conv.ArchivedAt.HasValue) conv.ArchivedAt = null; // si respondemos a una archivada, vuelve a activa
        await _db.SaveChangesAsync(ct);

        return new MensajeDto(
            msg.Id, msg.ConversationId, msg.Direction.ToString(),
            msg.Body, msg.MessageType, msg.SentAt, msg.SentByName,
            msg.MediaType.ToString(), msg.MediaUrl, msg.MediaMimeType, msg.ExternalId);
    }

    public async Task<bool> ArchivarAsync(Guid id, bool archivar, CancellationToken ct = default)
    {
        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (conv is null) return false;
        conv.ArchivedAt = archivar ? DateTimeOffset.UtcNow : (DateTimeOffset?)null;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ReiniciarContextoAgenteAsync(Guid id, CancellationToken ct = default)
    {
        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (conv is null) return false;
        conv.AgentContextResetAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> BloquearContactoAsync(Guid id, CancellationToken ct = default)
    {
        var conv = await _db.Conversations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (conv is null) return false;
        await _listaNegra.AgregarAsync(new AgregarNumeroBloqueadoRequest(conv.ContactPhone, "Bloqueado desde conversaciones"), ct);
        await ArchivarAsync(id, true, ct);
        return true;
    }

    /// <summary>Helper: proyeccion comun con join a WhatsAppLine + ultimo mensaje (subconsulta).</summary>
    private IQueryable<ConversacionDto> BuildDtoQuery(IQueryable<Conversation> q)
    {
        return from c in q
               join wl in _db.WhatsAppLines.AsNoTracking() on c.WhatsAppLineId equals wl.Id into wlj
               from wl in wlj.DefaultIfEmpty()
               select new ConversacionDto(
                   c.Id,
                   c.ContactPhone,
                   c.ContactName,
                   c.WhatsAppLineId,
                   wl != null ? wl.InstanceName : null,
                   c.PersonaId,
                   c.LastMessageAt,
                   c.ArchivedAt,
                   _db.Messages.Where(m => m.ConversationId == c.Id)
                       .OrderByDescending(m => m.SentAt)
                       .Select(m => m.Body).FirstOrDefault());
    }
}
