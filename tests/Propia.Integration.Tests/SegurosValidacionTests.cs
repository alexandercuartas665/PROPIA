using Microsoft.EntityFrameworkCore;
using Propia.Application.Seguros;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Seguros;
using Propia.Infrastructure.Storage;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// K-09: el API aceptaba polizas imposibles. La unica validacion era "la aseguradora es
/// obligatoria", asi que una poliza con la vigencia al reves, con valor asegurado negativo o con
/// cero cuotas se guardaba sin protestar; y una reclamacion se podia abrir por cero pesos, fuera de
/// la vigencia, o cerrarse reconociendo un monto negativo.
///
/// K-07: ademas la fecha de inicio pasa a ser obligatoria, y el semaforo de una poliza lo define un
/// unico metodo (SegurosService.SemaforoPoliza). Antes habia tres formulas distintas para una
/// poliza sin fecha de inicio: la pagina la pintaba verde y el job y el dashboard, roja.
///
/// Este modulo no tenia NINGUN test (hallazgo K-06).
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SegurosValidacionTests
{
    private readonly PostgresFixture _fx;

    public SegurosValidacionTests(PostgresFixture fx) => _fx = fx;

    // Poliza valida de referencia; cada test rompe solo lo que quiere probar.
    private static CrearPolizaRequest PolizaValida(
        DateOnly? inicio = null, DateOnly? fin = null, decimal? valor = 500_000_000m,
        string? numero = null, int? cuotas = null, bool pagoMensual = true)
        => new(
            "[SELLO] Aseguradora de prueba",
            NumeroPoliza: numero,
            FechaInicio: inicio ?? new DateOnly(2026, 1, 1),
            FechaFin: fin ?? new DateOnly(2026, 12, 31),
            ValorPoliza: valor,
            FormaPagoCuotas: cuotas,
            PagoMensual: pagoMensual);

    // ----------------------------- Polizas -----------------------------

    [Fact]
    public async Task Poliza_valida_se_crea()
    {
        var tenantId = await SeedTenantAsync("[SELLO] SG valida");
        var svc = BuildService(tenantId);

        var p = await svc.CrearPolizaAsync(PolizaValida(), CancellationToken.None);

        Assert.Equal("[SELLO] Aseguradora de prueba", p.Aseguradora);
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Crear_sin_fecha_de_inicio_falla()
    {
        // K-07: sin inicio el semaforo por porcentaje no se puede calcular y cada consumidor se
        // inventaba un valor distinto.
        var tenantId = await SeedTenantAsync("[SELLO] SG sin inicio");
        var svc = BuildService(tenantId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearPolizaAsync(
                new CrearPolizaRequest("[SELLO] Aseguradora", FechaFin: new DateOnly(2026, 12, 31)),
                CancellationToken.None));

        Assert.Contains("fecha de inicio", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await svc.ListPolizasAsync(CancellationToken.None));
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Crear_con_vigencia_al_reves_falla()
    {
        var tenantId = await SeedTenantAsync("[SELLO] SG vigencia");
        var svc = BuildService(tenantId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearPolizaAsync(
                PolizaValida(inicio: new DateOnly(2026, 6, 1), fin: new DateOnly(2026, 1, 1)),
                CancellationToken.None));

        Assert.Contains("fecha de fin", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await svc.ListPolizasAsync(CancellationToken.None));
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Crear_con_valor_asegurado_negativo_falla()
    {
        var tenantId = await SeedTenantAsync("[SELLO] SG valor");
        var svc = BuildService(tenantId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearPolizaAsync(PolizaValida(valor: -5m), CancellationToken.None));

        Assert.Contains("negativo", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await svc.ListPolizasAsync(CancellationToken.None));
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Crear_sin_pago_mensual_y_con_cero_cuotas_falla()
    {
        var tenantId = await SeedTenantAsync("[SELLO] SG cuotas");
        var svc = BuildService(tenantId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearPolizaAsync(PolizaValida(pagoMensual: false, cuotas: 0), CancellationToken.None));

        Assert.Contains("cuotas", ex.Message, StringComparison.OrdinalIgnoreCase);
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Numero_de_poliza_repetido_en_la_misma_copropiedad_falla()
    {
        var tenantId = await SeedTenantAsync("[SELLO] SG numero");
        var svc = BuildService(tenantId);

        await svc.CrearPolizaAsync(PolizaValida(numero: "POL-001"), CancellationToken.None);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearPolizaAsync(PolizaValida(numero: "POL-001"), CancellationToken.None));

        Assert.Contains("POL-001", ex.Message);
        Assert.Single(await svc.ListPolizasAsync(CancellationToken.None));
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Mismo_numero_de_poliza_en_otra_copropiedad_se_permite()
    {
        // El numero identifica a la poliza DENTRO de la copropiedad, no en toda la plataforma.
        var tA = await SeedTenantAsync("[SELLO] SG numero A");
        var tB = await SeedTenantAsync("[SELLO] SG numero B");

        await BuildService(tA).CrearPolizaAsync(PolizaValida(numero: "POL-777"), CancellationToken.None);
        var enB = await BuildService(tB).CrearPolizaAsync(PolizaValida(numero: "POL-777"), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, enB.Id);
        await CleanupTenantAsync(tA);
        await CleanupTenantAsync(tB);
    }

    [Fact]
    public async Task Actualizar_quitando_la_fecha_de_inicio_falla_y_no_persiste()
    {
        var tenantId = await SeedTenantAsync("[SELLO] SG upd inicio");
        var svc = BuildService(tenantId);
        var p = await svc.CrearPolizaAsync(PolizaValida(), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ActualizarPolizaAsync(p.Id,
                new ActualizarPolizaRequest("[SELLO] Aseguradora de prueba", FechaFin: new DateOnly(2026, 12, 31)),
                CancellationToken.None));

        Assert.Contains("fecha de inicio", ex.Message, StringComparison.OrdinalIgnoreCase);

        var actual = (await svc.ListPolizasAsync(CancellationToken.None)).Single();
        Assert.Equal(new DateOnly(2026, 1, 1), actual.FechaInicio);
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Actualizar_con_vigencia_al_reves_falla_y_no_persiste()
    {
        var tenantId = await SeedTenantAsync("[SELLO] SG upd vigencia");
        var svc = BuildService(tenantId);
        var p = await svc.CrearPolizaAsync(
            PolizaValida(inicio: new DateOnly(2026, 6, 1), fin: new DateOnly(2026, 12, 31)),
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ActualizarPolizaAsync(p.Id,
                new ActualizarPolizaRequest("[SELLO] Aseguradora de prueba",
                    FechaInicio: new DateOnly(2026, 6, 1), FechaFin: new DateOnly(2026, 1, 15)),
                CancellationToken.None));

        var actual = (await svc.ListPolizasAsync(CancellationToken.None)).Single();
        Assert.Equal(new DateOnly(2026, 12, 31), actual.FechaFin);
        await CleanupTenantAsync(tenantId);
    }

    // ----------------------------- Reclamaciones -----------------------------

    [Fact]
    public async Task Reclamacion_con_monto_cero_falla()
    {
        var tenantId = await SeedTenantAsync("[SELLO] SG recl monto");
        var svc = BuildService(tenantId);
        var p = await svc.CrearPolizaAsync(PolizaValida(), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearReclamacionAsync(p.Id,
                new CrearReclamacionRequest(new DateOnly(2026, 3, 1), 0m, "[SELLO] Dano en fachada"),
                CancellationToken.None));

        Assert.Contains("mayor que cero", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await svc.ListReclamacionesAsync(p.Id, CancellationToken.None));
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Reclamacion_fuera_de_la_vigencia_falla()
    {
        // Un siniestro ocurrido fuera de la vigencia no lo cubre la poliza.
        var tenantId = await SeedTenantAsync("[SELLO] SG recl fecha");
        var svc = BuildService(tenantId);
        var p = await svc.CrearPolizaAsync(PolizaValida(), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearReclamacionAsync(p.Id,
                new CrearReclamacionRequest(new DateOnly(2027, 5, 1), 1_000_000m, "[SELLO] Dano en fachada"),
                CancellationToken.None));

        Assert.Contains("vigencia", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await svc.ListReclamacionesAsync(p.Id, CancellationToken.None));
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Reclamacion_dentro_de_la_vigencia_se_crea()
    {
        var tenantId = await SeedTenantAsync("[SELLO] SG recl ok");
        var svc = BuildService(tenantId);
        var p = await svc.CrearPolizaAsync(PolizaValida(), CancellationToken.None);

        var r = await svc.CrearReclamacionAsync(p.Id,
            new CrearReclamacionRequest(new DateOnly(2026, 3, 1), 1_000_000m, "[SELLO] Dano en fachada"),
            CancellationToken.None);

        Assert.Equal(EstadoReclamacion.Vigente, r.Estado);
        Assert.Single(await svc.ListReclamacionesAsync(p.Id, CancellationToken.None));
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Cerrar_reclamacion_con_monto_reconocido_negativo_falla()
    {
        var tenantId = await SeedTenantAsync("[SELLO] SG recl cierre");
        var svc = BuildService(tenantId);
        var p = await svc.CrearPolizaAsync(PolizaValida(), CancellationToken.None);
        var r = await svc.CrearReclamacionAsync(p.Id,
            new CrearReclamacionRequest(new DateOnly(2026, 3, 1), 1_000_000m, "[SELLO] Dano en fachada"),
            CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CerrarReclamacionAsync(r.Id, new CerrarReclamacionRequest(-1m), CancellationToken.None));

        Assert.Contains("negativo", ex.Message, StringComparison.OrdinalIgnoreCase);
        var actual = (await svc.ListReclamacionesAsync(p.Id, CancellationToken.None)).Single();
        Assert.Equal(EstadoReclamacion.Vigente, actual.Estado);
        await CleanupTenantAsync(tenantId);
    }

    // ----------------------------- K-07: un solo semaforo -----------------------------

    [Fact]
    public void Semaforo_de_poliza_sin_inicio_ya_no_depende_de_quien_pregunte()
    {
        // Antes: la pagina pasaba (inicio ?? hoy) -> pct 1 -> siempre VERDE, y el job y el dashboard
        // pasaban (inicio ?? fin) -> total 0 dias -> siempre ROJO. Con la vigencia anual asumida,
        // una poliza que vence en 30 dias esta en rojo y una que vence en 100, en verde.
        var hoy = new DateOnly(2026, 6, 1);

        Assert.Equal(SemaforoContrato.Rojo, SegurosService.SemaforoPoliza(null, hoy.AddDays(30), hoy));
        Assert.Equal(SemaforoContrato.Verde, SegurosService.SemaforoPoliza(null, hoy.AddDays(100), hoy));
        Assert.Equal(SemaforoContrato.Rojo, SegurosService.SemaforoPoliza(null, hoy.AddDays(-1), hoy));
        Assert.Equal(SemaforoContrato.Ninguno, SegurosService.SemaforoPoliza(null, null, hoy));
    }

    [Fact]
    public async Task Poliza_vieja_sin_inicio_ya_no_sale_verde_en_la_pagina()
    {
        // Repro de K-07 del lado de /seguros: una poliza que vence en 30 dias se listaba VERDE.
        var tenantId = await SeedTenantAsync("[SELLO] SG k07");
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedPolizaSinInicioAsync(tenantId, hoy.AddDays(30));

        var dto = (await BuildService(tenantId).ListPolizasAsync(CancellationToken.None)).Single();

        Assert.Null(dto.FechaInicio);
        Assert.Equal(SemaforoContrato.Rojo, dto.Semaforo);
        Assert.Equal(SegurosService.SemaforoPoliza(null, hoy.AddDays(30), hoy), dto.Semaforo);
        await CleanupTenantAsync(tenantId);
    }

    // ----------------------------- infraestructura de los tests -----------------------------

    private ISegurosService BuildService(Guid tenantId) => new SegurosService(AppDb(tenantId), new NoopBlobStorage());

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

    private PropiaDbContext OwnerDb(Guid? tenantId = null)
    {
        var tenantCtx = new TenantContext();
        if (tenantId is { } t) tenantCtx.SetTenant(t);
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.OwnerConnectionString)
            .Options;
        return new PropiaDbContext(options, tenantCtx);
    }

    private async Task<Guid> SeedTenantAsync(string nombre)
    {
        await using var ctx = OwnerDb();
        var t = new Tenant
        {
            Nombre = nombre,
            Estado = EstadoCopropiedad.Activa,
            EstadoCustodia = EstadoCustodia.SinAdmin
        };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        return t.Id;
    }

    /// <summary>Inserta una poliza SIN fecha de inicio saltandose el servicio: desde K-09 ya no se
    /// puede crear una asi, pero en la base hay filas anteriores y son las de K-07.</summary>
    private async Task SeedPolizaSinInicioAsync(Guid tenantId, DateOnly fin)
    {
        await using var ctx = OwnerDb(tenantId);
        ctx.Polizas.Add(new Poliza
        {
            Aseguradora = "[SELLO] Aseguradora sin inicio",
            FechaInicio = null,
            FechaFin = fin
        });
        await ctx.SaveChangesAsync();
    }

    private async Task CleanupTenantAsync(Guid tenantId)
    {
        await using var ctx = OwnerDb();
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM poliza_reclamaciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM poliza_campo_valores WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM polizas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }

    private sealed class NoopBlobStorage : IBlobStorage
    {
        public Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct)
            => Task.FromResult($"/mem/{key}");
        public Task DeleteAsync(string key, CancellationToken ct) => Task.CompletedTask;
        public string GetPublicUrl(string key) => $"/mem/{key}";
        public string? ResolveUrl(string? storedValueOrKey) => storedValueOrKey;
        public Task<byte[]?> DownloadAsync(string key, CancellationToken ct) => Task.FromResult<byte[]?>(null);
    }
}
