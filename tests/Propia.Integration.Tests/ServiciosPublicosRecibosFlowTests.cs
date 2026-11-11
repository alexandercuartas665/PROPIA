using Microsoft.EntityFrameworkCore;
using Propia.Application.MiCopropiedad;
using Propia.Application.ServiciosPublicos;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.MiCopropiedad;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.ServiciosPublicos;
using Propia.Infrastructure.Storage;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Servicios publicos (2.17) con recibos leidos por IA: la referencia del contrato es unica y se compara
/// normalizada; evaluar NO guarda; confirmar guarda con Origen = IA y desglose, reemplaza el recibo del mismo
/// periodo y alerta por topes sin duplicar; "Es nuestro" crea el servicio; nada cruza entre copropiedades.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ServiciosPublicosRecibosFlowTests
{
    private readonly PostgresFixture _fx;
    public ServiciosPublicosRecibosFlowTests(PostgresFixture fx) => _fx = fx;

    private static ReciboLeidoRequest Recibo(string referencia, decimal valor, decimal? consumo = null, int mes = 10)
        => new(referencia, "EPM", "890904996-1", "Conjunto de prueba", null, "energia",
            new DateOnly(2026, mes - 1, 17), new DateOnly(2026, mes, 16), new DateOnly(2026, mes, 20), new DateOnly(2026, mes + 1, 15),
            10000m, 10000m + (consumo ?? 0), consumo, "kWh", valor, 0m, valor,
            new[] { new ConceptoReciboDto("Consumo de energía", valor * 0.9m), new ConceptoReciboDto("Alumbrado público", valor * 0.1m) },
            "tenants/x/extracciones/r.pdf", "Factura.pdf");

    [Fact]
    public async Task Referencia_unica_y_comparada_normalizada()
    {
        var t = await SeedTenantAsync("[SPX] referencia");
        var svc = Build(t);
        var c = await svc.CrearCuentaAsync(new CrearCuentaServicioRequest(TipoServicioPublico.EnergiaElectrica, "Energía", "EPM", "9001234-5", null, null, 25), default);
        Assert.Equal("kWh", c.UnidadMedida);   // unidad por defecto del tipo

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearCuentaAsync(new CrearCuentaServicioRequest(TipoServicioPublico.Gas, "Otro", "Vanti", "9001.234 5", null, null, 25), default));
        var otra = await svc.CrearCuentaAsync(new CrearCuentaServicioRequest(TipoServicioPublico.Gas, "Gas", "Vanti", "777", null, null, 25), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ActualizarCuentaAsync(otra.Id, new ActualizarCuentaServicioRequest(NumeroCuenta: "90012345"), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ActualizarCuentaAsync(otra.Id, new ActualizarCuentaServicioRequest(DiaPago: 32), default));

        // PUT MERGE: solo lo enviado; Limpiar borra un opcional.
        var r = await svc.ActualizarCuentaAsync(c.Id, new ActualizarCuentaServicioRequest(ValorMax: 4_200_000m, DiaPago: 15), default);
        Assert.Equal("EPM", r!.Prestador);
        Assert.Equal(4_200_000m, r.ValorMax);
        r = await svc.ActualizarCuentaAsync(c.Id, new ActualizarCuentaServicioRequest(Limpiar: new[] { "valorMax" }), default);
        Assert.Null(r!.ValorMax);
        Assert.Equal(15, r.DiaPago);

        await CleanupTenantAsync(t);
    }

    [Fact]
    public async Task Evaluar_no_guarda_y_confirmar_reemplaza_el_periodo_y_alerta_una_vez()
    {
        var t = await SeedTenantAsync("[SPX] recibos");
        var svc = Build(t);
        var c = await svc.CrearCuentaAsync(new CrearCuentaServicioRequest(TipoServicioPublico.EnergiaElectrica, "Energía zonas comunes", "EPM",
            "1029384756", null, "kWh", 25, PeriodicidadServicio.Mensual, 15, 3_200_000m, 4_200_000m, 3600m, 4800m), default);

        // Coincide (aunque el recibo traiga la referencia con espacios) y sale de los topes.
        var ev = await svc.EvaluarReciboAsync(Recibo("1029 384 756", 5_297_300m, 5952m), default);
        Assert.Equal(c.Id, ev.CuentaId);
        Assert.Equal(RangoRecibo.Alto, ev.Rango);
        Assert.Equal("oct 2026", ev.PeriodoLabel);
        Assert.False(ev.Duplicado);
        Assert.True(ev.CuadraDesglose);
        Assert.Contains("El valor $5.297.300 supera el máximo de $4.200.000 (+26.1%).", ev.Motivos);
        await using (var ctx = OwnerDb()) Assert.Equal(0, await ctx.RegistrosConsumoServicio.IgnoreQueryFilters().CountAsync(x => x.TenantId == t));

        var reg = await svc.ConfirmarReciboAsync(Recibo("1029384756", 5_297_300m, 5952m) with { CuentaId = c.Id }, default);
        Assert.Equal(OrigenRegistroConsumo.IA, reg.Origen);
        Assert.Equal(2, reg.Conceptos!.Count);
        Assert.Equal(new DateOnly(2026, 11, 15), reg.FechaLimitePago);
        Assert.Equal("Factura.pdf", reg.ArchivoNombre);

        // Mismo periodo: evaluar avisa duplicado; confirmar reemplaza (un solo registro) y NO duplica la alerta.
        var ev2 = await svc.EvaluarReciboAsync(Recibo("1029384756", 5_400_000m, 6000m), default);
        Assert.True(ev2.Duplicado);
        await svc.ConfirmarReciboAsync(Recibo("1029384756", 5_400_000m, 6000m), default);
        await using (var ctx = OwnerDb())
        {
            var regs = await ctx.RegistrosConsumoServicio.IgnoreQueryFilters().Where(x => x.TenantId == t).ToListAsync();
            Assert.Single(regs);
            Assert.Equal(5_400_000m, regs[0].Valor);
            Assert.Equal(2, await ctx.RegistrosConsumoConceptos.IgnoreQueryFilters().CountAsync(x => x.TenantId == t));
            var alertas = await ctx.AlertasCopropiedad.IgnoreQueryFilters().Where(a => a.TenantId == t && a.ModuloOrigenCodigo == "servicios-publicos").ToListAsync();
            Assert.Single(alertas);
            Assert.Equal("Recibo fuera de tope: Energía zonas comunes", alertas[0].Titulo);
            Assert.Equal(SeveridadAlerta.Advertencia, alertas[0].Severidad);
        }

        // +50% sobre el maximo -> Critica (otro periodo, otra alerta).
        await svc.ConfirmarReciboAsync(Recibo("1029384756", 6_400_000m, 4000m, mes: 11), default);
        await using (var ctx = OwnerDb())
            Assert.Contains(await ctx.AlertasCopropiedad.IgnoreQueryFilters().Where(a => a.TenantId == t).ToListAsync(), a => a.Severidad == SeveridadAlerta.Critica);

        var lista = await svc.ListCuentasAsync(default);
        Assert.Equal(RangoRecibo.Alto, lista.Single().Rango);
        Assert.NotNull(lista.Single().ProximoPago);

        await CleanupTenantAsync(t);
    }

    [Fact]
    public async Task Recibo_ajeno_o_de_cuenta_inactiva_no_se_registra_y_es_nuestro_lo_agrega()
    {
        var t = await SeedTenantAsync("[SPX] ajeno");
        var svc = Build(t);
        var inactiva = await svc.CrearCuentaAsync(new CrearCuentaServicioRequest(TipoServicioPublico.Acueducto, "Vieja", "EPM", "5566", null, null, 25), default);
        await svc.ActualizarCuentaAsync(inactiva.Id, new ActualizarCuentaServicioRequest(Activa: false), default);

        var ev = await svc.EvaluarReciboAsync(Recibo("5566", 100_000m), default);
        Assert.Null(ev.CuentaId);
        Assert.True(ev.CuentaInactiva);
        var ajeno = await svc.EvaluarReciboAsync(Recibo("4455667788", 3_800_000m), default);
        Assert.Null(ajeno.CuentaId);
        Assert.False(ajeno.CuentaInactiva);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ConfirmarReciboAsync(Recibo("4455667788", 3_800_000m), default));

        var agregado = await svc.AgregarServicioDesdeReciboAsync(Recibo("4455667788", 3_800_000m), default);
        Assert.NotNull(agregado.CuentaId);
        Assert.Equal(RangoRecibo.Sin, agregado.Rango);   // sin topes todavia
        var nueva = (await svc.ListCuentasAsync(default)).Single(x => x.Id == agregado.CuentaId);
        Assert.Equal("Energía eléctrica - 4455667788", nueva.Alias);
        Assert.Equal(TipoServicioPublico.EnergiaElectrica, nueva.Tipo);
        Assert.Equal(15, nueva.DiaPago);   // dia de la fecha limite del recibo

        await CleanupTenantAsync(t);
    }

    [Fact]
    public async Task Servicios_recibos_y_campos_no_cruzan_entre_copropiedades()
    {
        var tA = await SeedTenantAsync("[SPX] ISO A");
        var tB = await SeedTenantAsync("[SPX] ISO B");
        var a = Build(tA); var b = Build(tB);
        var cA = await a.CrearCuentaAsync(new CrearCuentaServicioRequest(TipoServicioPublico.Gas, "Gas A", "Vanti", "7788123", null, null, 25), default);
        // La MISMA referencia en otra copropiedad es valida (unica por tenant) y el recibo de A no es de B.
        var cB = await b.CrearCuentaAsync(new CrearCuentaServicioRequest(TipoServicioPublico.Gas, "Gas B", "Vanti", "7788123", null, null, 25), default);
        Assert.Equal(cB.Id, (await b.EvaluarReciboAsync(Recibo("7788123", 500_000m), default)).CuentaId);
        Assert.Equal(cA.Id, (await a.EvaluarReciboAsync(Recibo("7788123", 500_000m), default)).CuentaId);

        Assert.Null(await b.GetCuentaAsync(cA.Id, default));
        Assert.Null(await b.ActualizarCuentaAsync(cA.Id, new ActualizarCuentaServicioRequest(Alias: "Hackeada"), default));
        Assert.False(await b.EliminarCuentaAsync(cA.Id, default));

        var campo = await a.CrearCampoAsync(new CrearCampoDefinicionRequest("Medidor", TipoCampoTablero.Texto, null, null), default);
        Assert.True(await a.SetValorAsync(cA.Id, campo.Id, "M-1", default));
        Assert.Empty(await b.ListarCamposAsync(default));
        Assert.Empty(await b.ListarValoresAsync(default));
        Assert.False(await b.SetValorAsync(cA.Id, campo.Id, "x", default));

        await CleanupTenantAsync(tA);
        await CleanupTenantAsync(tB);
    }

    [Fact]
    public async Task El_contrato_se_relaciona_con_un_servicio_publico_de_la_copropiedad()
    {
        var tA = await SeedTenantAsync("[SPX] contrato A");
        var tB = await SeedTenantAsync("[SPX] contrato B");
        var cuentaA = await Build(tA).CrearCuentaAsync(new CrearCuentaServicioRequest(TipoServicioPublico.Internet, "Internet", "Claro", "3345221", null, null, 25), default);
        var cuentaB = await Build(tB).CrearCuentaAsync(new CrearCuentaServicioRequest(TipoServicioPublico.Internet, "Internet B", "Claro", "1", null, null, 25), default);

        var mi = BuildMiCopropiedad(tA);
        var ctr = await mi.CrearContratoAsync(new CrearContratoServicioRequest(TipoServicio.Otros, "Claro", null, null,
            new DateOnly(2026, 1, 1), null, 189_900m, null, CuentaServicioPublicoId: cuentaA.Id), default);
        Assert.Equal(cuentaA.Id, ctr.CuentaServicioPublicoId);

        // Un servicio de OTRA copropiedad se ignora (queda sin vinculo).
        var ctr2 = await mi.CrearContratoAsync(new CrearContratoServicioRequest(TipoServicio.Otros, "Claro 2", null, null,
            new DateOnly(2026, 1, 1), null, null, null, CuentaServicioPublicoId: cuentaB.Id), default);
        Assert.Null(ctr2.CuentaServicioPublicoId);

        // Borrar el servicio deja el contrato sin vinculo (SetNull), no lo borra.
        Assert.True(await Build(tA).EliminarCuentaAsync(cuentaA.Id, default));
        await using (var ctx = OwnerDb())
        {
            var c = await ctx.ContratosServicio.IgnoreQueryFilters().SingleAsync(x => x.Id == ctr.Id);
            Assert.Null(c.CuentaServicioPublicoId);
        }

        await CleanupTenantAsync(tA);
        await CleanupTenantAsync(tB);
    }

    // ---------- helpers ----------
    private IServiciosPublicosService Build(Guid tenantId)
    {
        var tenantCtx = new TenantContext();
        tenantCtx.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .AddInterceptors(new TenantConnectionInterceptor(tenantCtx))
            .Options;
        return new ServiciosPublicosService(new PropiaDbContext(options, tenantCtx), tenantCtx, new NoopBlobStorage());
    }
    private IMiCopropiedadService BuildMiCopropiedad(Guid tenantId)
    {
        var tenantCtx = new TenantContext();
        tenantCtx.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .AddInterceptors(new TenantConnectionInterceptor(tenantCtx))
            .Options;
        var db = new PropiaDbContext(options, tenantCtx);
        var directorio = new Propia.Infrastructure.Directorio.DirectorioService(db, tenantCtx, new NoopBlobStorage());
        return new MiCopropiedadService(db, tenantCtx, new NoopBlobStorage(), new StubSeedUsuarioRolService(), directorio);
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
    private async Task CleanupTenantAsync(Guid tenantId)
    {
        await using var ctx = OwnerDb();
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM alertas_copropiedad WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM contratos_servicio WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM servicio_publico_campo_valores WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM servicio_publico_campo_definiciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM registros_consumo_conceptos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM registros_consumo_servicio WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM cuentas_servicio_publico WHERE tenant_id = {tenantId}");
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
