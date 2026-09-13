using Microsoft.EntityFrameworkCore;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.MiCopropiedad;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// K-01: el API aceptaba contratos imposibles. Antes de este cambio la unica validacion era
/// "proveedor obligatorio", asi que un contrato con la fecha de fin anterior a la de inicio, con
/// valor mensual negativo o con un NIT lleno de letras se guardaba con 201 y quedaba en la base.
///
/// Estos tests fijan las reglas: cada caso debe fallar con InvalidOperationException y un mensaje
/// en espanol (el controller lo mapea a 400). El ultimo cubre el caso del MERGE, que es el que se
/// escapa facil: al actualizar se valida el ESTADO RESULTANTE, no el request suelto.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ContratoValidacionTests
{
    private readonly PostgresFixture _fx;

    public ContratoValidacionTests(PostgresFixture fx) => _fx = fx;

    // Contrato valido de referencia; cada test cambia solo lo que quiere romper.
    private static CrearContratoServicioRequest ContratoValido(
        DateOnly? inicio = null, DateOnly? fin = null, decimal? valorMensual = 1_000_000m,
        string? nit = "900111333", int dias = 30, string? numero = null,
        decimal? valorTotal = null, int? cuotas = null, bool pagoMensual = true)
        => new(
            TipoServicio.Aseo, "[SELLO] Proveedor de prueba", nit, null,
            inicio ?? new DateOnly(2026, 1, 1), fin ?? new DateOnly(2026, 12, 31),
            valorMensual, null, dias,
            NumeroContrato: numero, ValorTotal: valorTotal, FormaPagoCuotas: cuotas, PagoMensual: pagoMensual);

    [Fact]
    public async Task Contrato_valido_se_crea()
    {
        var tenantId = await SeedTenantAsync("[SELLO] CP valido");
        var svc = BuildService(tenantId);

        var c = await svc.CrearContratoAsync(ContratoValido(), CancellationToken.None);

        Assert.Equal("[SELLO] Proveedor de prueba", c.Proveedor);
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Crear_con_fecha_fin_anterior_al_inicio_falla()
    {
        var tenantId = await SeedTenantAsync("[SELLO] CP fechas");
        var svc = BuildService(tenantId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearContratoAsync(
                ContratoValido(inicio: new DateOnly(2026, 6, 1), fin: new DateOnly(2026, 1, 1)),
                CancellationToken.None));

        Assert.Contains("fecha de fin", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await svc.ListContratosAsync(CancellationToken.None));
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Crear_con_valor_mensual_negativo_falla()
    {
        var tenantId = await SeedTenantAsync("[SELLO] CP valor");
        var svc = BuildService(tenantId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearContratoAsync(ContratoValido(valorMensual: -5m), CancellationToken.None));

        Assert.Contains("negativo", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await svc.ListContratosAsync(CancellationToken.None));
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Crear_con_nit_no_numerico_falla()
    {
        var tenantId = await SeedTenantAsync("[SELLO] CP nit");
        var svc = BuildService(tenantId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearContratoAsync(ContratoValido(nit: "abc!!"), CancellationToken.None));

        Assert.Contains("NIT", ex.Message, StringComparison.OrdinalIgnoreCase);
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Nit_con_puntos_y_guion_se_acepta()
    {
        // Misma tolerancia que el Directorio: los separadores no son un error de captura.
        var tenantId = await SeedTenantAsync("[SELLO] CP nit ok");
        var svc = BuildService(tenantId);

        var c = await svc.CrearContratoAsync(ContratoValido(nit: "900.111.333-1"), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, c.Id);
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Crear_sin_pago_mensual_y_con_cero_cuotas_falla()
    {
        var tenantId = await SeedTenantAsync("[SELLO] CP cuotas");
        var svc = BuildService(tenantId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearContratoAsync(
                ContratoValido(pagoMensual: false, cuotas: 0, valorTotal: 5_000_000m),
                CancellationToken.None));

        Assert.Contains("cuotas", ex.Message, StringComparison.OrdinalIgnoreCase);
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Crear_con_dias_de_alerta_fuera_de_rango_falla()
    {
        var tenantId = await SeedTenantAsync("[SELLO] CP dias");
        var svc = BuildService(tenantId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearContratoAsync(ContratoValido(dias: 900), CancellationToken.None));

        Assert.Contains("anticipacion", ex.Message, StringComparison.OrdinalIgnoreCase);
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Numero_de_contrato_repetido_en_la_misma_copropiedad_falla()
    {
        var tenantId = await SeedTenantAsync("[SELLO] CP numero");
        var svc = BuildService(tenantId);

        await svc.CrearContratoAsync(ContratoValido(numero: "CT-001"), CancellationToken.None);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearContratoAsync(ContratoValido(numero: "CT-001"), CancellationToken.None));

        Assert.Contains("CT-001", ex.Message);
        Assert.Single(await svc.ListContratosAsync(CancellationToken.None));
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Mismo_numero_en_otra_copropiedad_se_permite()
    {
        // El numero identifica al contrato DENTRO de la copropiedad, no en toda la plataforma.
        var tA = await SeedTenantAsync("[SELLO] CP numero A");
        var tB = await SeedTenantAsync("[SELLO] CP numero B");

        await BuildService(tA).CrearContratoAsync(ContratoValido(numero: "CT-777"), CancellationToken.None);
        var enB = await BuildService(tB).CrearContratoAsync(ContratoValido(numero: "CT-777"), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, enB.Id);
        await CleanupTenantAsync(tA);
        await CleanupTenantAsync(tB);
    }

    [Fact]
    public async Task Actualizar_con_fecha_fin_anterior_al_inicio_guardado_falla()
    {
        // El caso que se escapa: el PUT es MERGE y la fecha de fin llega SOLA. Si se validara solo
        // el request, no habria con que compararla y pasaria.
        var tenantId = await SeedTenantAsync("[SELLO] CP merge");
        var svc = BuildService(tenantId);
        var c = await svc.CrearContratoAsync(
            ContratoValido(inicio: new DateOnly(2026, 6, 1), fin: new DateOnly(2026, 12, 31)),
            CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ActualizarContratoAsync(c.Id,
                new ActualizarContratoRequest(EstadoContrato.Vigente, 30, FechaFin: new DateOnly(2026, 1, 15)),
                CancellationToken.None));

        Assert.Contains("fecha de fin", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Y no se guardo: la fecha de fin sigue siendo la original.
        var actual = (await svc.ListContratosAsync(CancellationToken.None)).Single();
        Assert.Equal(new DateOnly(2026, 12, 31), actual.FechaFin);
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Actualizar_con_valor_mensual_negativo_falla()
    {
        var tenantId = await SeedTenantAsync("[SELLO] CP upd valor");
        var svc = BuildService(tenantId);
        var c = await svc.CrearContratoAsync(ContratoValido(), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ActualizarContratoAsync(c.Id,
                new ActualizarContratoRequest(EstadoContrato.Vigente, 30, ValorMensual: -1m),
                CancellationToken.None));

        var actual = (await svc.ListContratosAsync(CancellationToken.None)).Single();
        Assert.Equal(1_000_000m, actual.ValorMensual);
        await CleanupTenantAsync(tenantId);
    }

    // ----------------------------- infraestructura de los tests -----------------------------

    private IMiCopropiedadService BuildService(Guid tenantId)
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

    private async Task<Guid> SeedTenantAsync(string nombre)
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.OwnerConnectionString)
            .Options;
        await using var ctx = new PropiaDbContext(options, new TenantContext());
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

    private async Task CleanupTenantAsync(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.OwnerConnectionString)
            .Options;
        await using var ctx = new PropiaDbContext(options, new TenantContext());
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM bitacora_mi_copropiedad WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM contratos_servicio WHERE tenant_id = {tenantId}");
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
