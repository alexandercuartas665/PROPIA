using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Propia.Application.Catalogos;
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
/// Lector de buzon: buzones por copropiedad (crear/editar en merge sin exponer la clave, activar exige clave, cuenta
/// unica, temas que atiende, eliminar deja los correos), clasificacion con el Coordinador contra los temas del buzon
/// (lista "correo.tema" con temas propios; un tema que el buzon no atiende queda como Error reclasificable),
/// descartar/restaurar y aislamiento por copropiedad (HasQueryFilter + RLS FORCE contra PostgreSQL real con
/// propia_app). El IMAP real no se prueba aqui (se verifica en runtime con una cuenta de prueba).
/// </summary>
[Collection(nameof(PostgresCollection))]
public class CorreosRecibidosFlowTests
{
    private readonly PostgresFixture _fx;
    private static readonly CancellationToken Ct = CancellationToken.None;

    public CorreosRecibidosFlowTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Buzones_merge_no_expone_la_clave_activar_exige_clave_y_cuenta_unica()
    {
        var t = await SeedTenantAsync("[COR] buzones");
        try
        {
            var svc = Build(t, new FakeIa(null));
            Assert.Empty(await svc.ListarBuzonesAsync(Ct));

            // Sin clave no se puede activar.
            var (sinClave, err) = await svc.CrearBuzonAsync(new GuardarBuzonRequest(Cuenta: "buzon@ejemplo.com", Activo: true), Ct);
            Assert.Null(sinClave);
            Assert.NotNull(err);

            var (b, err2) = await svc.CrearBuzonAsync(new GuardarBuzonRequest(Nombre: "Administraci\u00f3n",
                Cuenta: "buzon@ejemplo.com", Password: "abcd efgh ijkl mnop", Activo: false), Ct);
            Assert.Null(err2);
            Assert.True(b!.TienePassword);
            Assert.False(b.Activo);
            Assert.Equal("imap.gmail.com", b.ImapHost);
            Assert.Equal(993, b.ImapPort);
            Assert.Null(b.Temas);   // todos

            // MERGE: activar y marcar temas no borra la cuenta ni la clave.
            var (b2, _) = await svc.GuardarBuzonAsync(b.Id, new GuardarBuzonRequest(Activo: true, Temas: new() { "pqrsd" }), Ct);
            Assert.True(b2!.Activo);
            Assert.NotNull(b2.LecturaDesde);
            Assert.Equal("buzon@ejemplo.com", b2.Cuenta);
            Assert.True(b2.TienePassword);
            Assert.Equal(new[] { "pqrsd" }, b2.Temas);
            var (b3, _) = await svc.GuardarBuzonAsync(b.Id, new GuardarBuzonRequest(TodosLosTemas: true), Ct);
            Assert.Null(b3!.Temas);

            // Otra cuenta igual (sin importar mayusculas) se rechaza; una distinta es otro buzon.
            Assert.NotNull((await svc.CrearBuzonAsync(new GuardarBuzonRequest(Cuenta: "BUZON@ejemplo.com", Password: "x"), Ct)).Error);
            var (otro, _) = await svc.CrearBuzonAsync(new GuardarBuzonRequest(Cuenta: "contabilidad@ejemplo.com", Password: "x"), Ct);
            Assert.Equal("contabilidad", otro!.Nombre);   // sin nombre: la parte local de la cuenta
            Assert.Equal(2, (await svc.ListarBuzonesAsync(Ct)).Count);

            await using (var db = AppDb(t))
            {
                var fila = await db.BuzonesCorreo.AsNoTracking().SingleAsync(x => x.Id == b.Id);
                Assert.Equal("enc:abcdefghijklmnop", fila.PasswordCifrada);   // cifrada y sin espacios
            }

            // Eliminar el buzon deja sus correos en la bandeja, sin buzon.
            var idCorreo = await SeedCorreoAsync(t, "[COR] Correo del buzon", b.Id);
            Assert.True(await svc.EliminarBuzonAsync(b.Id, Ct));
            Assert.Null((await svc.ObtenerAsync(idCorreo, Ct))!.BuzonId);
            Assert.Single(await svc.ListarBuzonesAsync(Ct));
        }
        finally { await CleanupTenantAsync(t); }
    }

    [Fact]
    public async Task Reclasificar_usa_los_temas_del_buzon_incluidos_los_propios()
    {
        var t = await SeedTenantAsync("[COR] clasificar");
        try
        {
            var opciones = new FakeOpciones();
            var ia = new FakeIa("{\"tema\":\"c:asc\",\"confianza\":0.9,\"resumen\":\"Reporte del ascensor\",\"accion_sugerida\":\"Crear tarea\",\"datos\":{\"torre\":\"2\"}}");
            var svc = Build(t, ia, opciones);
            var (b, _) = await svc.CrearBuzonAsync(new GuardarBuzonRequest(Cuenta: "porteria@ejemplo.com", Password: "x",
                Temas: new() { "c:asc" }), Ct);
            var id = await SeedCorreoAsync(t, "[COR] Ascensor torre 2 detenido", b!.Id);

            var r = await svc.ReclasificarAsync(id, Ct);
            Assert.Equal("c:asc", r!.TemaClave);
            Assert.Equal(EstadoCorreoRecibido.Clasificado, r.Estado);
            // El prompt solo ofrece los temas del buzon (+ otro e irrelevante), con la descripcion del tema propio.
            Assert.Contains("\"c:asc\": Ascensores. Fallas y mantenimiento", ia.UltimoPrompt);
            Assert.Contains("\"otro\"", ia.UltimoPrompt);
            Assert.DoesNotContain("\"recibo_servicio\"", ia.UltimoPrompt);
            var det = await svc.ObtenerAsync(id, Ct);
            Assert.Equal("Reporte del ascensor", det!.Resumen);
            Assert.Equal("porteria", det.BuzonNombre);
            Assert.NotNull(det.ClasificadoEn);

            // Un tema que el buzon no atiende (o que no existe) no se inventa: queda en Error.
            var fuera = Build(t, new FakeIa("{\"tema\":\"recibo_servicio\"}"), opciones);
            r = await fuera.ReclasificarAsync(id, Ct);
            Assert.Equal(EstadoCorreoRecibido.Error, r!.Estado);
            Assert.False(string.IsNullOrWhiteSpace(r.Error));

            // Descartar y restaurar (vuelve a Clasificado porque ya se habia clasificado una vez).
            Assert.Equal(EstadoCorreoRecibido.Descartado, (await svc.DescartarAsync(id, true, Ct))!.Estado);
            Assert.Equal(EstadoCorreoRecibido.Clasificado, (await svc.DescartarAsync(id, false, Ct))!.Estado);
        }
        finally { await CleanupTenantAsync(t); }
    }

    [Fact]
    public async Task Correos_y_buzones_no_se_ven_entre_copropiedades()
    {
        var tA = await SeedTenantAsync("[COR] ISO A");
        var tB = await SeedTenantAsync("[COR] ISO B");
        try
        {
            var ia = new FakeIa("{\"tema\":\"pqrsd\",\"confianza\":0.8}");
            var svcA = Build(tA, ia);
            var svcB = Build(tB, ia);
            var idA = await SeedCorreoAsync(tA, "[COR] Queja por ruido");
            var (bA, _) = await svcA.CrearBuzonAsync(new GuardarBuzonRequest(Cuenta: "a@ejemplo.com", Password: "x"), Ct);

            Assert.Single(await svcA.ListarAsync(Ct));
            Assert.Empty(await svcB.ListarAsync(Ct));
            Assert.Null(await svcB.ObtenerAsync(idA, Ct));
            Assert.Null(await svcB.ReclasificarAsync(idA, Ct));
            Assert.Null(await svcB.DescartarAsync(idA, true, Ct));
            Assert.Empty(await svcB.ListarBuzonesAsync(Ct));
            Assert.Null((await svcB.GuardarBuzonAsync(bA!.Id, new GuardarBuzonRequest(Nombre: "robado"), Ct)).Buzon);
            Assert.False(await svcB.EliminarBuzonAsync(bA.Id, Ct));

            // La misma cuenta y el mismo Message-ID pueden existir en otra copropiedad (indices unicos por tenant).
            Assert.NotNull((await svcB.CrearBuzonAsync(new GuardarBuzonRequest(Cuenta: "a@ejemplo.com", Password: "x"), Ct)).Buzon);
            await SeedCorreoAsync(tB, "[COR] Queja por ruido");
            Assert.Single(await svcB.ListarAsync(Ct));
            Assert.Equal(EstadoCorreoRecibido.Nuevo, (await svcA.ObtenerAsync(idA, Ct))!.Estado);
            Assert.Equal("a", (await svcA.ListarBuzonesAsync(Ct)).Single().Nombre);
        }
        finally { await CleanupTenantAsync(tA); await CleanupTenantAsync(tB); }
    }

    // ----------------------------- infraestructura -----------------------------

    private sealed class FakeIa : IAiInferenceService
    {
        private readonly string? _respuesta;
        public string UltimoPrompt { get; private set; } = "";
        public FakeIa(string? respuesta) => _respuesta = respuesta;
        public Task<AiChatResult> TestChatAsync(Guid agentId, IReadOnlyList<AiChatTurn> turns, string? systemPromptOverride = null,
            string? bearerToken = null, string? contactPhone = null, Guid? conversationId = null, CancellationToken ct = default)
        {
            UltimoPrompt = turns.LastOrDefault()?.Text ?? "";
            return Task.FromResult(_respuesta is null ? new AiChatResult(false, null, "sin IA") : new AiChatResult(true, _respuesta, null));
        }
        public Task<string?> DescribeInboundMediaAsync(Guid agentId, string mimeType, string base64, string instruction, CancellationToken ct = default)
            => Task.FromResult<string?>(null);
    }

    /// <summary>Lista "correo.tema" de la copropiedad: los 5 base + un tema propio.</summary>
    private sealed class FakeOpciones : IOpcionesEfectivas
    {
        public Task<IReadOnlyList<OpcionEfectivaDto>> OpcionesAsync(string lista, bool incluirOcultas = false, CancellationToken ct = default)
        {
            var l = CoordinadorCorreoAgente.TemasBase
                .Select((x, i) => new OpcionEfectivaDto(x.Clave, x.Label, i, x.Color, true, true, "{\"descripcion\":\"" + x.Clave + "\"}"))
                .Append(new OpcionEfectivaDto("c:asc", "Ascensores", 9, null, true, false, "{\"descripcion\":\"Fallas y mantenimiento de ascensores.\"}"))
                .ToList();
            return Task.FromResult<IReadOnlyList<OpcionEfectivaDto>>(l);
        }
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

    private ICorreoEntranteService Build(Guid tenantId, IAiInferenceService ia, IOpcionesEfectivas? opciones = null)
    {
        var tenantCtx = new TenantContext();
        tenantCtx.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .AddInterceptors(new TenantConnectionInterceptor(tenantCtx))
            .Options;
        var db = new PropiaDbContext(options, tenantCtx);
        return new CorreoEntranteService(db, tenantCtx, new FakeSecret(), new FakeAgentes(), ia, new FakeBlob(),
            opciones ?? new FakeOpciones(), NullLogger<CorreoEntranteService>.Instance);
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

    private async Task<Guid> SeedCorreoAsync(Guid tenantId, string asunto, Guid? buzonId = null)
    {
        await using var db = AppDb(tenantId);
        var c = new CorreoRecibido
        {
            BuzonId = buzonId,
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
        foreach (var tabla in new[] { "correos_recibidos", "buzones_correo", "tenant_email_configs" })
            await ctx.Database.ExecuteSqlRawAsync($"DELETE FROM {tabla} WHERE tenant_id = '{tenantId}'");
        await ctx.Database.ExecuteSqlRawAsync($"DELETE FROM tenants WHERE id = '{tenantId}'");
    }
}
