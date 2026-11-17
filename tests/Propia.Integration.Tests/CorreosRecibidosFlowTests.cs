using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Application.Integraciones;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Integraciones;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Lector de buzon (Ola 1): config de lectura (merge, sin exponer la clave, activar exige cuenta), clasificacion con el
/// Coordinador de correo (respuesta valida -> tema; invalida -> Error reclasificable), descartar/restaurar y aislamiento
/// por copropiedad (HasQueryFilter + RLS FORCE contra PostgreSQL real con propia_app). El IMAP real no se prueba aqui
/// (se verifica en runtime con una cuenta de prueba).
/// </summary>
[Collection(nameof(PostgresCollection))]
public class CorreosRecibidosFlowTests
{
    private readonly PostgresFixture _fx;
    private static readonly CancellationToken Ct = CancellationToken.None;

    public CorreosRecibidosFlowTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Config_merge_no_expone_la_clave_y_activar_exige_cuenta()
    {
        var t = await SeedTenantAsync("[COR] config");
        try
        {
            var svc = Build(t, new FakeIa(null));
            Assert.False((await svc.GetConfigAsync(Ct)).LecturaActiva);

            // Sin cuenta no se puede activar.
            Assert.NotNull(await svc.GuardarConfigAsync(new GuardarCorreoLecturaRequest(LecturaActiva: true), Ct));

            Assert.Null(await svc.GuardarConfigAsync(new GuardarCorreoLecturaRequest(Cuenta: "buzon@ejemplo.com", Password: "abcd efgh ijkl mnop"), Ct));
            var cfg = await svc.GetConfigAsync(Ct);
            Assert.Equal("buzon@ejemplo.com", cfg.Cuenta);
            Assert.True(cfg.TienePassword);
            Assert.Equal("imap.gmail.com", cfg.ImapHost);
            Assert.Equal(993, cfg.ImapPort);

            Assert.Null(await svc.GuardarConfigAsync(new GuardarCorreoLecturaRequest(LecturaActiva: true), Ct));
            cfg = await svc.GetConfigAsync(Ct);
            Assert.True(cfg.LecturaActiva);
            Assert.NotNull(cfg.LecturaDesde);
            Assert.Equal("buzon@ejemplo.com", cfg.Cuenta);   // MERGE: activar no borra la cuenta

            await using var db = AppDb(t);
            var fila = await db.TenantEmailConfigs.AsNoTracking().SingleAsync();
            Assert.Equal("enc:abcdefghijklmnop", fila.SmtpPasswordEncrypted);   // cifrada y sin espacios
            Assert.Equal("buzon@ejemplo.com", fila.FromEmail);
        }
        finally { await CleanupTenantAsync(t); }
    }

    [Fact]
    public async Task Reclasificar_aplica_el_tema_del_coordinador_o_deja_error()
    {
        var t = await SeedTenantAsync("[COR] clasificar");
        try
        {
            var id = await SeedCorreoAsync(t, "Su factura EPM de octubre");
            var ok = Build(t, new FakeIa("{\"tema\":\"recibo_servicio\",\"confianza\":0.9,\"resumen\":\"Factura EPM\",\"accion_sugerida\":\"Registrar\",\"datos\":{\"servicio\":\"energia\"}}"));
            var r = await ok.ReclasificarAsync(id, Ct);
            Assert.Equal(TemaCorreo.ReciboServicio, r!.Tema);
            Assert.Equal(EstadoCorreoRecibido.Clasificado, r.Estado);
            var det = await ok.ObtenerAsync(id, Ct);
            Assert.Equal("Factura EPM", det!.Resumen);
            Assert.Contains("energia", det.DatosJson);
            Assert.NotNull(det.ClasificadoEn);

            var malo = Build(t, new FakeIa("no entiendo"));
            r = await malo.ReclasificarAsync(id, Ct);
            Assert.Equal(EstadoCorreoRecibido.Error, r!.Estado);
            Assert.False(string.IsNullOrWhiteSpace(r.Error));

            // Descartar y restaurar (vuelve a Clasificado porque ya se habia clasificado una vez).
            Assert.Equal(EstadoCorreoRecibido.Descartado, (await ok.DescartarAsync(id, true, Ct))!.Estado);
            Assert.Equal(EstadoCorreoRecibido.Clasificado, (await ok.DescartarAsync(id, false, Ct))!.Estado);
        }
        finally { await CleanupTenantAsync(t); }
    }

    [Fact]
    public async Task Correos_y_config_no_se_ven_entre_copropiedades()
    {
        var tA = await SeedTenantAsync("[COR] ISO A");
        var tB = await SeedTenantAsync("[COR] ISO B");
        try
        {
            var ia = new FakeIa("{\"tema\":\"pqrsd\",\"confianza\":0.8}");
            var svcA = Build(tA, ia);
            var svcB = Build(tB, ia);
            var idA = await SeedCorreoAsync(tA, "[COR] Queja por ruido");
            Assert.Null(await svcA.GuardarConfigAsync(new GuardarCorreoLecturaRequest(Cuenta: "a@ejemplo.com", Password: "x"), Ct));

            Assert.Single(await svcA.ListarAsync(Ct));
            Assert.Empty(await svcB.ListarAsync(Ct));
            Assert.Null(await svcB.ObtenerAsync(idA, Ct));
            Assert.Null(await svcB.ReclasificarAsync(idA, Ct));
            Assert.Null(await svcB.DescartarAsync(idA, true, Ct));
            Assert.Null((await svcB.GetConfigAsync(Ct)).Cuenta);

            // El mismo Message-ID puede existir en otra copropiedad (indice unico por tenant).
            await SeedCorreoAsync(tB, "[COR] Queja por ruido");
            Assert.Single(await svcB.ListarAsync(Ct));
            Assert.Equal(EstadoCorreoRecibido.Nuevo, (await svcA.ObtenerAsync(idA, Ct))!.Estado);
        }
        finally { await CleanupTenantAsync(tA); await CleanupTenantAsync(tB); }
    }

    // ----------------------------- infraestructura -----------------------------

    private sealed class FakeIa : IAiInferenceService
    {
        private readonly string? _respuesta;
        public FakeIa(string? respuesta) => _respuesta = respuesta;
        public Task<AiChatResult> TestChatAsync(Guid agentId, IReadOnlyList<AiChatTurn> turns, string? systemPromptOverride = null,
            string? bearerToken = null, string? contactPhone = null, Guid? conversationId = null, CancellationToken ct = default)
            => Task.FromResult(_respuesta is null ? new AiChatResult(false, null, "sin IA") : new AiChatResult(true, _respuesta, null));
        public Task<string?> DescribeInboundMediaAsync(Guid agentId, string mimeType, string base64, string instruction, CancellationToken ct = default)
            => Task.FromResult<string?>(null);
    }

    private sealed class FakeAgentes : IAgentesSistema
    {
        public Task<AgenteSistema?> AsegurarAsync(string clave, CancellationToken ct = default)
            => Task.FromResult<AgenteSistema?>(new AgenteSistema(Guid.NewGuid(), true));
    }

    private sealed class FakeSecret : ISecretProtector
    {
        public string Protect(string plaintext) => "enc:" + plaintext;
        public string Unprotect(string ciphertext) => ciphertext[4..];
    }

    private sealed class FakeBlob : IBlobStorage
    {
        public Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct) => Task.FromResult("/mem/" + key);
        public Task DeleteAsync(string key, CancellationToken ct) => Task.CompletedTask;
        public string GetPublicUrl(string key) => "/mem/" + key;
        public string? ResolveUrl(string? storedValueOrKey) => storedValueOrKey is null ? null : "/mem/" + storedValueOrKey;
        public Task<byte[]?> DownloadAsync(string key, CancellationToken ct) => Task.FromResult<byte[]?>(null);
    }

    private ICorreoEntranteService Build(Guid tenantId, IAiInferenceService ia)
    {
        var tenantCtx = new TenantContext();
        tenantCtx.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .AddInterceptors(new TenantConnectionInterceptor(tenantCtx))
            .Options;
        var db = new PropiaDbContext(options, tenantCtx);
        return new CorreoEntranteService(db, tenantCtx, new FakeSecret(), new FakeAgentes(), ia, new FakeBlob(),
            NullLogger<CorreoEntranteService>.Instance);
    }

    private PropiaDbContext AppDb(Guid tenantId)
    {
        var tenantCtx = new TenantContext();
        tenantCtx.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .AddInterceptors(new TenantConnectionInterceptor(tenantCtx))
            .Options;
        return new PropiaDbContext(options, tenantCtx);
    }

    private PropiaDbContext OwnerDb()
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        return new PropiaDbContext(options, new TenantContext());
    }

    private async Task<Guid> SeedTenantAsync(string nombre)
    {
        await using var ctx = OwnerDb();
        var t = new Tenant { Nombre = nombre, Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.SinAdmin };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        return t.Id;
    }

    private async Task<Guid> SeedCorreoAsync(Guid tenantId, string asunto)
    {
        await using var db = AppDb(tenantId);
        var c = new CorreoRecibido
        {
            MessageId = "<" + asunto.GetHashCode().ToString("x") + "@ejemplo.com>",
            ImapUid = 1,
            RemitenteCorreo = "vecino@ejemplo.com",
            Asunto = asunto,
            FechaCorreo = DateTimeOffset.UtcNow,
            Cuerpo = "Buenas tardes, escribo por...",
        };
        db.CorreosRecibidos.Add(c);
        await db.SaveChangesAsync();
        return c.Id;
    }

    private async Task CleanupTenantAsync(Guid tenantId)
    {
        await using var ctx = OwnerDb();
        await ctx.Database.OpenConnectionAsync();
        await ctx.Database.ExecuteSqlRawAsync($"SELECT set_config('app.tenant_id', '{tenantId}', false)");
        foreach (var tabla in new[] { "correos_recibidos", "tenant_email_configs" })
            await ctx.Database.ExecuteSqlRawAsync($"DELETE FROM {tabla} WHERE tenant_id = '{tenantId}'");
        await ctx.Database.ExecuteSqlRawAsync($"DELETE FROM tenants WHERE id = '{tenantId}'");
    }
}
