using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Api.Controllers;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.InfraestructuraIa;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Ola D (analitica de entregas). Cubre:
///  - Los parsers de estado (Evolution messages.update, Meta statuses[]) normalizan los acks a
///    sent/delivered/read/failed y extraen el motivo de Meta.
///  - DeliveryStatusService aplica los estados a los salientes correlacionando por ExternalId, avanza
///    de forma MONOTONA (no retrocede read->delivered->sent), marca "failed" con motivo, y solo toca
///    salientes (no entrantes).
///  - El resumen de entregas (ConversacionService) cuenta por estado y lista los fallos con su motivo.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class EntregasFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;

    public EntregasFlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    // ---------- Parsers (puros, sin BD) ----------

    [Fact]
    public void EvolutionStatusParser_normaliza_acks()
    {
        // messages.update con forma anidada (key.id + update.status) y string DELIVERY_ACK.
        var json = JsonDocument.Parse("""
        { "event": "messages.update", "instance": "propia_x_y",
          "data": { "key": { "id": "EVO-A", "fromMe": true }, "update": { "status": "DELIVERY_ACK" } } }
        """);
        var r = EvolutionStatusParser.ParseStatuses(json.RootElement);
        Assert.Single(r);
        Assert.Equal("EVO-A", r[0].ExternalId);
        Assert.Equal("delivered", r[0].Status);

        // Forma plana (keyId + status numerico 4 = READ) en un array.
        var json2 = JsonDocument.Parse("""
        { "event": "messages.update", "data": [ { "keyId": "EVO-B", "status": 4 } ] }
        """);
        var r2 = EvolutionStatusParser.ParseStatuses(json2.RootElement);
        Assert.Single(r2);
        Assert.Equal("EVO-B", r2[0].ExternalId);
        Assert.Equal("read", r2[0].Status);

        // ERROR -> failed.
        var json3 = JsonDocument.Parse("""{ "event": "messages.update", "data": { "keyId": "EVO-C", "status": "ERROR" } }""");
        var r3 = EvolutionStatusParser.ParseStatuses(json3.RootElement);
        Assert.Equal("failed", Assert.Single(r3).Status);

        // Un entrante (messages.upsert) no produce estados.
        var upsert = JsonDocument.Parse("""{ "event": "messages.upsert", "data": { "key": { "id": "X" } } }""");
        Assert.Empty(EvolutionStatusParser.ParseStatuses(upsert.RootElement));
    }

    [Fact]
    public void MetaWebhookParser_ParseStatuses_extrae_estado_y_motivo()
    {
        var json = JsonDocument.Parse("""
        { "entry": [ { "changes": [ { "value": {
            "metadata": { "phone_number_id": "123" },
            "statuses": [
              { "id": "WAMID-OK", "status": "read", "recipient_id": "573001" },
              { "id": "WAMID-FAIL", "status": "failed", "errors": [
                  { "code": 131026, "title": "Message undeliverable", "error_data": { "details": "detalle" } } ] }
            ] } } ] } ] }
        """);
        var r = MetaWebhookParser.ParseStatuses(json.RootElement);
        Assert.Equal(2, r.Count);
        var ok = r.First(x => x.ExternalId == "WAMID-OK");
        Assert.Equal("read", ok.Status);
        var fail = r.First(x => x.ExternalId == "WAMID-FAIL");
        Assert.Equal("failed", fail.Status);
        Assert.Equal("131026", fail.ErrorCode);
        Assert.Equal("Message undeliverable", fail.ErrorMessage);

        // Un entrante (messages[]) no produce estados.
        var inbound = JsonDocument.Parse("""
        { "entry": [ { "changes": [ { "value": { "messages": [ { "id": "m1" } ] } } ] } ] }
        """);
        Assert.Empty(MetaWebhookParser.ParseStatuses(inbound.RootElement));
    }

    // ---------- Servicio de aplicacion (BD) ----------

    [Fact]
    public async Task DeliveryStatusService_avanza_monotono_marca_fallo_y_resume()
    {
        var tenantId = await SeedAsync();

        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddScoped<ITenantContext, TenantContext>();
        sc.AddScoped<TenantConnectionInterceptor>();
        sc.AddDbContext<PropiaDbContext>((sp, o) =>
            o.UseNpgsql(_fx.AppConnectionString).AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>()));
        sc.AddScoped<IDeliveryStatusService, DeliveryStatusService>();
        sc.AddScoped<IConversacionService>(sp => new ConversacionService(
            sp.GetRequiredService<PropiaDbContext>(),
            sp.GetRequiredService<ITenantContext>(),
            new FakeConnector(), new FakeListaNegra(), new FakeBlob()));
        var services = sc.BuildServiceProvider();

        // 1) "EVO-1" sent -> delivered; "EVO-2" sent -> failed con motivo.
        using (var scope = services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<IDeliveryStatusService>();
            var n = await svc.AplicarAsync(tenantId, new[]
            {
                new DeliveryStatusUpdate("EVO-1", "delivered"),
                new DeliveryStatusUpdate("EVO-2", "failed", "131026", "Message undeliverable")
            });
            Assert.Equal(2, n);
        }
        Assert.Equal("delivered", await EstadoAsync("EVO-1"));
        Assert.Equal("failed", await EstadoAsync("EVO-2"));
        Assert.Equal("codigo 131026 - Message undeliverable", await MotivoAsync("EVO-2"));

        // 2) Monotonia: un "sent" atrasado NO retrocede el "delivered"; "read" si avanza.
        using (var scope = services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<IDeliveryStatusService>();
            await svc.AplicarAsync(tenantId, new[] { new DeliveryStatusUpdate("EVO-1", "sent") });
            Assert.Equal("delivered", await EstadoAsync("EVO-1"));
            await svc.AplicarAsync(tenantId, new[] { new DeliveryStatusUpdate("EVO-1", "read") });
        }
        Assert.Equal("read", await EstadoAsync("EVO-1"));

        // 3) Un estado para un entrante no lo toca (solo salientes).
        using (var scope = services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<IDeliveryStatusService>();
            var n = await svc.AplicarAsync(tenantId, new[] { new DeliveryStatusUpdate("IN-1", "read") });
            Assert.Equal(0, n);
        }
        Assert.Null(await EstadoAsync("IN-1"));

        // 4) Resumen: 1 leido, 1 fallido (y el fallo aparece con su motivo).
        using (var scope = services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
            var conv = scope.ServiceProvider.GetRequiredService<IConversacionService>();
            var resumen = await conv.ObtenerResumenEntregasAsync();
            Assert.Equal(0, resumen.Aceptados);
            Assert.Equal(0, resumen.Entregados);
            Assert.Equal(1, resumen.Leidos);
            Assert.Equal(1, resumen.Fallidos);
            var fallo = Assert.Single(resumen.FallosRecientes);
            Assert.Equal("codigo 131026 - Message undeliverable", fallo.DeliveryError);
        }

        await CleanAsync(tenantId);
    }

    // ----- helpers -----

    private async Task<Guid> SeedAsync()
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var t = new Tenant { Nombre = "[ENTREGAS] test", Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.ConAdmin };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();

        var conv = new Conversation { TenantId = t.Id, ContactPhone = "573001112233", ContactName = "Residente Prueba", LastMessageAt = DateTimeOffset.UtcNow };
        ctx.Conversations.Add(conv);
        await ctx.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        ctx.Messages.AddRange(
            new Message { TenantId = t.Id, ConversationId = conv.Id, Direction = MessageDirection.Outbound, ExternalId = "EVO-1", Body = "Hola 1", MessageType = "text", SentAt = now, DeliveryStatus = "sent" },
            new Message { TenantId = t.Id, ConversationId = conv.Id, Direction = MessageDirection.Outbound, ExternalId = "EVO-2", Body = "Hola 2", MessageType = "text", SentAt = now, DeliveryStatus = "sent" },
            new Message { TenantId = t.Id, ConversationId = conv.Id, Direction = MessageDirection.Inbound, ExternalId = "IN-1", Body = "entrante", MessageType = "text", SentAt = now });
        await ctx.SaveChangesAsync();
        return t.Id;
    }

    private async Task<string?> EstadoAsync(string externalId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        return await ctx.Messages.Where(m => m.ExternalId == externalId).Select(m => m.DeliveryStatus).FirstOrDefaultAsync();
    }

    private async Task<string?> MotivoAsync(string externalId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        return await ctx.Messages.Where(m => m.ExternalId == externalId).Select(m => m.DeliveryError).FirstOrDefaultAsync();
    }

    private async Task CleanAsync(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM messages WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM conversations WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }

    // Stubs minimos para construir ConversacionService (no se usan en el resumen de entregas).
    private sealed class FakeConnector : IWhatsAppConnectorService
    {
        public Task<bool> MasterReadyAsync(CancellationToken ct = default) => Task.FromResult(false);
        public Task<LineConnectResult> ConnectLineAsync(Guid lineId, CancellationToken ct = default) => Task.FromResult(new LineConnectResult(false, null, null));
        public Task<WhatsAppLineDto?> RefreshAsync(Guid lineId, CancellationToken ct = default) => Task.FromResult<WhatsAppLineDto?>(null);
        public Task<bool> DisconnectAsync(Guid lineId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> DeleteLineAsync(Guid lineId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<LineSendResult> SendTestAsync(Guid lineId, string phone, string text, CancellationToken ct = default) => Task.FromResult(new LineSendResult(true, null));
        public Task<LineSendResult> SendMediaAsync(Guid lineId, string phone, string imageUrl, string? caption, CancellationToken ct = default) => Task.FromResult(new LineSendResult(true, null));
        public Task<LineSendResult> SendTemplateAsync(Guid lineId, string phone, string templateName, string languageCode, IReadOnlyList<string> bodyParams, string fallbackText, CancellationToken ct = default) => Task.FromResult(new LineSendResult(true, null));
        public Task<LineSendResult> SendAttachmentAsync(Guid lineId, string phone, AgentResourceType kind, string mediaUrl, string? caption, string? fileName, CancellationToken ct = default) => Task.FromResult(new LineSendResult(true, null));
        public Task<LineSendResult> SendLocationAsync(Guid lineId, string phone, double latitude, double longitude, string? name, string? address, CancellationToken ct = default) => Task.FromResult(new LineSendResult(true, null));
        public Task<LineSendResult> SendReactionAsync(Guid lineId, string phone, string externalMessageId, string emoji, CancellationToken ct = default) => Task.FromResult(new LineSendResult(true, null));
        public Task<(string MimeType, string Base64)?> FetchInboundImageAsync(Guid lineId, string externalMessageId, CancellationToken ct = default) => Task.FromResult<(string, string)?>(null);
        public Task<(string? MimeType, string Base64)?> FetchInboundMediaAsync(Guid lineId, string externalMessageId, CancellationToken ct = default) => Task.FromResult<(string?, string)?>(null);
    }

    private sealed class FakeListaNegra : IListaNegraService
    {
        public Task<bool> EstaBloqueadoAsync(string telefono, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<NumeroEnListaNegraDto>> ListarAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<NumeroEnListaNegraDto>>(Array.Empty<NumeroEnListaNegraDto>());
        public Task<NumeroEnListaNegraDto?> AgregarAsync(AgregarNumeroBloqueadoRequest req, CancellationToken ct = default) => Task.FromResult<NumeroEnListaNegraDto?>(null);
        public Task<bool> EliminarAsync(Guid id, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FakeBlob : Propia.Infrastructure.Storage.IBlobStorage
    {
        public Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct) => Task.FromResult("/mem/" + key);
        public Task DeleteAsync(string key, CancellationToken ct) => Task.CompletedTask;
        public string GetPublicUrl(string key) => "/mem/" + key;
        public string? ResolveUrl(string? storedValueOrKey) => storedValueOrKey;
        public Task<byte[]?> DownloadAsync(string key, CancellationToken ct) => Task.FromResult<byte[]?>(null);
    }
}
