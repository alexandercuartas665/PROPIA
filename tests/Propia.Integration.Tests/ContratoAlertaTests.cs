using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Jobs;
using Propia.Infrastructure.MiCopropiedad;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// K-08: la alerta de vencimiento de un contrato se perdia al prorrogar la fecha sin pasar por verde.
/// El job solo reinicia el contador (AlertaVencimientoPctNotificado) cuando el contrato vuelve a verde,
/// y ActualizarContratoAsync no lo tocaba: un contrato en rojo que se prorroga a amarillo nunca volvia
/// a avisar. Ahora, si cambia la vigencia, el update reinicia el contador.
///
/// K-12: el job de vencimiento emitia las alertas con un codigo de modulo equivocado ("2.5" para los
/// contratos, "seguros" para las polizas). El modulo real es 2.3 (Mi Copropiedad). Este test corre el
/// job en la base aislada de Testcontainers y comprueba el codigo de la alerta creada.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ContratoAlertaTests
{
    private readonly PostgresFixture _fx;

    public ContratoAlertaTests(PostgresFixture fx) => _fx = fx;

    private static CrearContratoServicioRequest Contrato(DateOnly inicio, DateOnly fin)
        => new(
            TipoServicio.Seguridad, "[SELLO] Proveedor alerta", "900111333", null,
            inicio, fin, 1_000_000m, null, 30);

    [Fact]
    public async Task Prorrogar_un_contrato_resetea_la_alerta_de_vencimiento()
    {
        // K-08: contrato en rojo con la alerta del 10% ya notificada. Al prorrogar la fecha de fin, el
        // contador debe volver a null para que el job pueda avisar en el nuevo umbral.
        var tenantId = await SeedTenantAsync("[SELLO] CP alerta reset");
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var c = await BuildService(tenantId).CrearContratoAsync(Contrato(hoy.AddDays(-350), hoy.AddDays(5)), CancellationToken.None);
        await SetPctNotificadoAsync(c.Id, 10);

        // Servicio nuevo = contexto nuevo (como un request distinto): asi ve el pct=10 del paso previo
        // en vez de quedarse con el valor que dejo trackeado la creacion.
        await BuildService(tenantId).ActualizarContratoAsync(c.Id,
            new ActualizarContratoRequest(EstadoContrato.Vigente, 30, FechaFin: hoy.AddDays(120)),
            CancellationToken.None);

        Assert.Null(await GetPctNotificadoAsync(c.Id));
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Actualizar_sin_cambiar_la_vigencia_conserva_la_alerta()
    {
        // El reset es SOLO cuando cambia la vigencia: un cambio de estado u observaciones no debe
        // reabrir la alerta (si no, avisaria en cada guardado).
        var tenantId = await SeedTenantAsync("[SELLO] CP alerta conserva");
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var c = await BuildService(tenantId).CrearContratoAsync(Contrato(hoy.AddDays(-350), hoy.AddDays(5)), CancellationToken.None);
        await SetPctNotificadoAsync(c.Id, 10);

        await BuildService(tenantId).ActualizarContratoAsync(c.Id,
            new ActualizarContratoRequest(EstadoContrato.Vigente, 30, Observaciones: "[SELLO] sin tocar fechas"),
            CancellationToken.None);

        Assert.Equal(10, await GetPctNotificadoAsync(c.Id));
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task El_job_crea_la_alerta_de_contrato_con_el_codigo_de_modulo_2_3()
    {
        // K-12: antes salia "2.5". Contrato en amarillo (15% restante) -> el job debe crear una
        // AlertaCopropiedad con ModuloOrigenCodigo "2.3".
        var tenantId = await SeedTenantAsync("[SELLO] CP job codigo");
        var svc = BuildService(tenantId);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        await svc.CrearContratoAsync(Contrato(hoy.AddDays(-85), hoy.AddDays(15)), CancellationToken.None);

        await RunJobAsync();

        var alerta = await GetAlertaAsync(tenantId, TipoAlertaDashboard.ContratoPorVencer);
        Assert.NotNull(alerta);
        Assert.Equal("2.3", alerta!.ModuloOrigenCodigo);
        await CleanupTenantAsync(tenantId);
    }

    // ----------------------------- infraestructura -----------------------------

    private IMiCopropiedadService BuildService(Guid tenantId)
    {
        var db = AppDb(tenantId);
        var directorio = new Propia.Infrastructure.Directorio.DirectorioService(db, TenantCtx(tenantId), new NoopBlobStorage());
        return new MiCopropiedadService(db, TenantCtx(tenantId), new NoopBlobStorage(), new StubSeedUsuarioRolService(), directorio);
    }

    private static TenantContext TenantCtx(Guid tenantId)
    {
        var c = new TenantContext();
        c.SetTenant(tenantId);
        return c;
    }

    private PropiaDbContext AppDb(Guid tenantId)
    {
        var tenantCtx = TenantCtx(tenantId);
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .AddInterceptors(new TenantConnectionInterceptor(tenantCtx))
            .Options;
        return new PropiaDbContext(options, tenantCtx);
    }

    private PropiaDbContext OwnerDb()
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.OwnerConnectionString)
            .Options;
        return new PropiaDbContext(options, new TenantContext());
    }

    /// <summary>Corre el job completo. En Testcontainers la base solo tiene los tenants que sembro el
    /// test, asi que iterar "todos los tenants" es seguro.</summary>
    private async Task RunJobAsync()
    {
        // El job fija el tenant por iteracion; se le pasa un contexto sin tenant fijado de entrada.
        var tenantCtx = new TenantContext();
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .AddInterceptors(new TenantConnectionInterceptor(tenantCtx))
            .Options;
        await using var db = new PropiaDbContext(options, tenantCtx);
        var job = new ContratosVencimientoJob(db, tenantCtx, NullLogger<ContratosVencimientoJob>.Instance);
        await job.EjecutarAsync(CancellationToken.None);
    }

    private async Task SetPctNotificadoAsync(Guid contratoId, int pct)
    {
        await using var ctx = OwnerDb();
        await ctx.Database.ExecuteSqlAsync(
            $"UPDATE contratos_servicio SET alerta_vencimiento_pct_notificado = {pct} WHERE id = {contratoId}");
    }

    private async Task<int?> GetPctNotificadoAsync(Guid contratoId)
    {
        await using var ctx = OwnerDb();
        var c = await ctx.ContratosServicio.IgnoreQueryFilters().AsNoTracking()
            .FirstAsync(x => x.Id == contratoId);
        return c.AlertaVencimientoPctNotificado;
    }

    private async Task<AlertaCopropiedad?> GetAlertaAsync(Guid tenantId, TipoAlertaDashboard tipo)
    {
        await using var ctx = OwnerDb();
        return await ctx.AlertasCopropiedad.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Tipo == tipo);
    }

    private async Task<Guid> SeedTenantAsync(string nombre)
    {
        await using var ctx = OwnerDb();
        var t = new Tenant { Nombre = nombre, Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.SinAdmin };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        return t.Id;
    }

    private async Task CleanupTenantAsync(Guid tenantId)
    {
        await using var ctx = OwnerDb();
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM alertas_copropiedad WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM bitacora_mi_copropiedad WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM contratos_servicio WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }

    private sealed class NoopBlobStorage : IBlobStorage
    {
        public Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct) => Task.FromResult($"/mem/{key}");
        public Task DeleteAsync(string key, CancellationToken ct) => Task.CompletedTask;
        public string GetPublicUrl(string key) => $"/mem/{key}";
        public string? ResolveUrl(string? storedValueOrKey) => storedValueOrKey;
        public Task<byte[]?> DownloadAsync(string key, CancellationToken ct) => Task.FromResult<byte[]?>(null);
    }
}
