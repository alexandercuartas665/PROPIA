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
/// Webhook de entrada de YCloud (/webhooks/ycloud/{tenantId}): parser de eventos v2 (entrante de texto, respuesta
/// de boton de plantilla, estados de entrega), firma "YCloud-Signature: t=..,s=.." con el secreto de la linea
/// (valida, alterada, vencida, sin secreto) y resolucion de la linea YCloud por el numero destino, aislada por tenant.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class YCloudWebhookTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;
    private readonly IngestFalso _ingest = new();

    public YCloudWebhookTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddScoped<ITenantContext, TenantContext>();
        sc.AddSingleton<ISecretProtector, SecretoPlano>();
        sc.AddSingleton<IChatIngestService>(_ingest);
        sc.AddScoped<TenantConnectionInterceptor>();
        sc.AddDbContext<PropiaDbContext>((sp, o) => o.UseNpgsql(_fx.AppConnectionString).AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>()));
        sc.AddScoped<IYCloudWebhookService, YCloudWebhookService>();
        sc.AddScoped<IWhatsAppLineService, WhatsAppLineService>();
        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed class SecretoPlano : ISecretProtector
    {
        public string Protect(string plain) => "enc:" + plain;
        public string Unprotect(string cipher) => cipher.StartsWith("enc:") ? cipher[4..] : throw new InvalidOperationException("llave anterior");
    }

    private sealed class IngestFalso : IChatIngestService
    {
        public List<(Guid Tenant, IngestMessageRequest Payload)> Recibidos { get; } = new();
        public Task<ChatIngestResult> IngestTrustedAsync(Guid tenantId, IngestMessageRequest payload, CancellationToken ct = default)
        {
            Recibidos.Add((tenantId, payload));
            return Task.FromResult(ChatIngestResult.Accepted);
        }
    }

    private const string Entrante = """
        {"id":"evt_1","type":"whatsapp.inbound_message.received","apiVersion":"v2","createTime":"2026-11-12T20:00:00.000Z",
         "whatsappInboundMessage":{"id":"63f5d1","wamid":"wamid.HBgM","wabaId":"123","from":"+57 300 111 2222",
          "customerProfile":{"name":"Maria Residente"},"to":"+57 320 555 0000","sendTime":"2026-11-12T20:00:00.000Z",
          "type":"text","text":{"body":"Hola, necesito ayuda"}}}
        """;

    [Fact]
    public void Parser_entrante_boton_de_plantilla_y_estados()
    {
        using var d1 = JsonDocument.Parse(Entrante);
        var e = YCloudWebhookParser.ParseEntrante(d1.RootElement)!;
        Assert.Equal("573205550000", e.NumeroDestino);
        Assert.Equal(("wamid.HBgM", "573001112222", "Maria Residente", "Hola, necesito ayuda"),
            (e.Payload.ExternalId, e.Payload.ContactPhone, e.Payload.ContactName, e.Payload.Body));
        Assert.Equal(new DateTimeOffset(2026, 11, 12, 20, 0, 0, TimeSpan.Zero), e.Payload.SentAt);
        Assert.Empty(YCloudWebhookParser.ParseEstados(d1.RootElement));

        // Respuesta a un boton de respuesta rapida de una plantilla (asi llegara el "Si, autorizo" de porteria).
        using var d2 = JsonDocument.Parse("""
            {"type":"whatsapp.inbound_message.received","whatsappInboundMessage":{"id":"x2","from":"+573001112222","to":"+573205550000",
             "type":"button","button":{"payload":"SI","text":"Si, autorizo"}}}
            """);
        var b = YCloudWebhookParser.ParseEntrante(d2.RootElement)!;
        Assert.Equal(("x2", "Si, autorizo"), (b.Payload.ExternalId, b.Payload.Body));

        // Estado de entrega: se emite por wamid y por id de YCloud (el envio guarda cualquiera de los dos).
        using var d3 = JsonDocument.Parse("""
            {"type":"whatsapp.message.updated","whatsappMessage":{"id":"yc_9","wamid":"wamid.9","status":"FAILED","errorCode":"131026","errorMessage":"Message undeliverable"}}
            """);
        var st = YCloudWebhookParser.ParseEstados(d3.RootElement);
        Assert.Equal(new[] { "wamid.9", "yc_9" }, st.Select(x => x.ExternalId).ToArray());
        Assert.All(st, x => Assert.Equal(("failed", "131026", "Message undeliverable"), (x.Status, x.ErrorCode, x.ErrorMessage)));
        Assert.Null(YCloudWebhookParser.ParseEntrante(d3.RootElement));

        using var d4 = JsonDocument.Parse("""{"type":"whatsapp.business_account.updated"}""");
        Assert.Null(YCloudWebhookParser.ParseEntrante(d4.RootElement));
        Assert.Empty(YCloudWebhookParser.ParseEstados(d4.RootElement));
    }

    [Fact]
    public async Task Firma_y_resolucion_de_la_linea_por_numero_destino_aislada_por_tenant()
    {
        var tA = await SeedTenantAsync("YCloud A");
        var tB = await SeedTenantAsync("YCloud B");
        await SeedLineaAsync(tA, "ycloud-a", "573205550000", "whsec_aaa");
        await SeedLineaAsync(tA, "ycloud-sin", "573209990000", null);
        await SeedLineaAsync(tB, "ycloud-b", "573205550000", null);   // mismo numero en otra copropiedad, sin secreto

        using var scope = _services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IYCloudWebhookService>();
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        string Header(string secreto, string ts, string cuerpo) => $"t={ts},s={YCloudWebhookService.Firmar(secreto, ts, cuerpo)}";

        Assert.Equal(FirmaYCloud.Valida, await svc.VerificarFirmaAsync(tA, Entrante, Header("whsec_aaa", t, Entrante)));
        Assert.Equal(FirmaYCloud.Valida, await svc.VerificarFirmaAsync(tA, Entrante, $" s={YCloudWebhookService.Firmar("whsec_aaa", t, Entrante)} , t={t} "));
        Assert.Equal(FirmaYCloud.Invalida, await svc.VerificarFirmaAsync(tA, Entrante, Header("otro", t, Entrante)));
        Assert.Equal(FirmaYCloud.Invalida, await svc.VerificarFirmaAsync(tA, Entrante.Replace("ayuda", "plata"), Header("whsec_aaa", t, Entrante)));
        var viejo = DateTimeOffset.UtcNow.AddMinutes(-30).ToUnixTimeSeconds().ToString();
        Assert.Equal(FirmaYCloud.Invalida, await svc.VerificarFirmaAsync(tA, Entrante, Header("whsec_aaa", viejo, Entrante)));
        Assert.Equal(FirmaYCloud.Invalida, await svc.VerificarFirmaAsync(tA, Entrante, null));
        // El secreto de A no sirve para B (y B no tiene secreto propio).
        Assert.Equal(FirmaYCloud.SinSecreto, await svc.VerificarFirmaAsync(tB, Entrante, Header("whsec_aaa", t, Entrante)));

        using var doc = JsonDocument.Parse(Entrante);
        var e = YCloudWebhookParser.ParseEntrante(doc.RootElement)!;
        _ingest.Recibidos.Clear();
        Assert.Equal(ChatIngestResult.Accepted, await svc.IngestAsync(tA, e.NumeroDestino, e.Payload));
        var r = Assert.Single(_ingest.Recibidos);
        Assert.Equal((tA, "ycloud-a"), (r.Tenant, r.Payload.InstanceName));
        Assert.Equal(ChatIngestResult.LineNotFound, await svc.IngestAsync(tA, "573000000000", e.Payload));

        // La misma URL de otra copropiedad resuelve SU linea, nunca la de A.
        using (var scopeB = _services.CreateScope())
        {
            var svcB = scopeB.ServiceProvider.GetRequiredService<IYCloudWebhookService>();
            Assert.Equal(ChatIngestResult.Accepted, await svcB.IngestAsync(tB, e.NumeroDestino, e.Payload));
        }
        Assert.Equal((tB, "ycloud-b"), (_ingest.Recibidos[^1].Tenant, _ingest.Recibidos[^1].Payload.InstanceName));

        await CleanAsync(tA); await CleanAsync(tB);
    }

    [Fact]
    public async Task Url_universal_enruta_por_numero_cuenta_compartida_y_propia_y_numero_unico()
    {
        // Cuenta YCloud compartida por dos copropiedades (mismo secreto, numeros distintos) + una con cuenta propia.
        var tA = await SeedTenantAsync("YCloud U-A");
        var tB = await SeedTenantAsync("YCloud U-B");
        var tC = await SeedTenantAsync("YCloud U-C");
        var tD = await SeedTenantAsync("YCloud U-D");
        await SeedLineaAsync(tA, "u-a", "573201110001", "whsec_cuenta_admin");
        await SeedLineaAsync(tB, "u-b", "573201110002", "whsec_cuenta_admin");
        await SeedLineaAsync(tC, "u-c", "573201110003", "whsec_propia_c");
        await SeedLineaAsync(tD, "u-d", "573201110004", null);

        using var scope = _services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IYCloudWebhookService>();
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        string Cuerpo(string to) => Entrante.Replace("+57 320 555 0000", to);
        string Header(string secreto, string cuerpo) => $"t={t},s={YCloudWebhookService.Firmar(secreto, t, cuerpo)}";

        // El numero de la linea sale del evento: "to" de un entrante, "from" de un estado.
        using (var d = JsonDocument.Parse(Cuerpo("+57 320 111 0002")))
            Assert.Equal("573201110002", YCloudWebhookParser.NumeroLinea(d.RootElement));
        using (var d = JsonDocument.Parse("{\"type\":\"whatsapp.message.updated\",\"whatsappMessage\":{\"id\":\"x\",\"from\":\"+573201110003\",\"to\":\"+573001112222\",\"status\":\"read\"}}"))
            Assert.Equal("573201110003", YCloudWebhookParser.NumeroLinea(d.RootElement));
        using (var d = JsonDocument.Parse("{\"type\":\"whatsapp.template.reviewed\",\"whatsappTemplate\":{\"name\":\"x\"}}"))
            Assert.Null(YCloudWebhookParser.NumeroLinea(d.RootElement));

        var cb = Cuerpo("+573201110002");
        Assert.Equal((tB, FirmaYCloud.Valida), await svc.ResolverPorNumeroAsync("573201110002", cb, Header("whsec_cuenta_admin", cb)));
        var ca = Cuerpo("+573201110001");
        Assert.Equal((tA, FirmaYCloud.Valida), await svc.ResolverPorNumeroAsync("573201110001", ca, Header("whsec_cuenta_admin", ca)));
        var cc = Cuerpo("+573201110003");
        Assert.Equal((tC, FirmaYCloud.Valida), await svc.ResolverPorNumeroAsync("+57 320 111 0003", cc, Header("whsec_propia_c", cc)));
        // El secreto de la cuenta compartida no abre la linea de C (otra cuenta).
        Assert.Equal(((Guid?)null, FirmaYCloud.Invalida), await svc.ResolverPorNumeroAsync("573201110003", cc, Header("whsec_cuenta_admin", cc)));
        Assert.Equal(((Guid?)null, FirmaYCloud.NumeroDesconocido), await svc.ResolverPorNumeroAsync("573209999999", cc, Header("whsec_propia_c", cc)));
        Assert.Equal(((Guid?)tD, FirmaYCloud.SinSecreto), await svc.ResolverPorNumeroAsync("573201110004", cc, Header("x", cc)));

        // Ruteado a B: la ingesta va a la linea de B, no a la de A.
        _ingest.Recibidos.Clear();
        using (var d = JsonDocument.Parse(cb))
        {
            var e = YCloudWebhookParser.ParseEntrante(d.RootElement)!;
            Assert.Equal(ChatIngestResult.Accepted, await svc.IngestAsync(tB, e.NumeroDestino, e.Payload));
        }
        Assert.Equal((tB, "u-b"), (_ingest.Recibidos.Single().Tenant, _ingest.Recibidos.Single().Payload.InstanceName));

        // Un numero no puede quedar en dos lineas YCloud: la URL universal enruta por el.
        using (var sc2 = _services.CreateScope())
        {
            sc2.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tC);
            var lines = sc2.ServiceProvider.GetRequiredService<IWhatsAppLineService>();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => lines.CreateAsync(new CreateWhatsAppLineRequest(
                "duplicada", null, WhatsAppProvider.YCloud, YCloudApiKey: "k", YCloudFromNumber: "+57 320 111 0001")));
            Assert.Contains("ya est", ex.Message);
            Assert.NotNull(await lines.CreateAsync(new CreateWhatsAppLineRequest("nueva", null, WhatsAppProvider.YCloud, YCloudApiKey: "k", YCloudFromNumber: "573201110099")));
        }

        foreach (var x in new[] { tA, tB, tC, tD }) await CleanAsync(x);
    }

    private async Task<Guid> SeedTenantAsync(string nombre)
    {
        await using var ctx = Owner();
        var t = new Tenant { Nombre = nombre, Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.ConAdmin };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        return t.Id;
    }

    private async Task SeedLineaAsync(Guid tenantId, string nombre, string numero, string? secreto)
    {
        await using var ctx = Owner();
        ctx.WhatsAppLines.Add(new WhatsAppLine
        {
            TenantId = tenantId,
            InstanceName = nombre,
            Provider = WhatsAppProvider.YCloud,
            YCloudFromNumber = numero,
            YCloudApiKeyEncrypted = "enc:key",
            YCloudWebhookSecretEncrypted = secreto is null ? null : "enc:" + secreto
        });
        await ctx.SaveChangesAsync();
    }

    private async Task CleanAsync(Guid tenantId)
    {
        await using var ctx = Owner();
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM whats_app_lines WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }

    private PropiaDbContext Owner() => new(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options, new TenantContext());
}
