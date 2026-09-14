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
/// K-06: aislamiento por tenant de contratos y sus campos dinamicos. Con RLS + HasQueryFilter, el
/// servicio de una copropiedad no ve los contratos, definiciones de campo ni valores de otra.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ContratoTenantIsolationTests
{
    private readonly PostgresFixture _fx;

    public ContratoTenantIsolationTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Contratos_y_sus_campos_no_se_ven_entre_copropiedades()
    {
        var tA = await SeedTenantAsync("[SELLO] ISO contrato A");
        var tB = await SeedTenantAsync("[SELLO] ISO contrato B");
        var svcA = BuildService(tA);
        var svcB = BuildService(tB);

        var cA = await svcA.CrearContratoAsync(Contrato("[SELLO] Prov A", "[SELLO]-A"), CancellationToken.None);
        var campoA = await svcA.CrearContratoCampoAsync(new CrearContratoCampoRequest("[SELLO] Campo A", TipoCampoTablero.Texto, null, null), CancellationToken.None);
        await svcA.GuardarContratoCampoValorAsync(cA.Id, campoA.Id, new GuardarContratoCampoValorRequest("valorA"), CancellationToken.None);

        await svcB.CrearContratoAsync(Contrato("[SELLO] Prov B", "[SELLO]-B"), CancellationToken.None);
        await svcB.CrearContratoCampoAsync(new CrearContratoCampoRequest("[SELLO] Campo B", TipoCampoTablero.Texto, null, null), CancellationToken.None);

        // A solo ve lo suyo.
        var contratosA = await svcA.ListContratosAsync(CancellationToken.None);
        Assert.Single(contratosA);
        Assert.Equal("[SELLO] Prov A", contratosA[0].Proveedor);

        var camposA = await svcA.ListContratoCamposAsync(CancellationToken.None);
        Assert.Single(camposA);
        Assert.Equal("[SELLO] Campo A", camposA[0].Label);

        var valoresA = await svcA.ListTodosCamposValoresContratoAsync(CancellationToken.None);
        Assert.All(valoresA, v => Assert.Equal(cA.Id, v.ContratoId));

        // B no ve nada de A.
        var contratosB = await svcB.ListContratosAsync(CancellationToken.None);
        Assert.Single(contratosB);
        Assert.Equal("[SELLO] Prov B", contratosB[0].Proveedor);
        Assert.DoesNotContain(await svcB.ListContratoCamposAsync(CancellationToken.None), c => c.Label == "[SELLO] Campo A");

        await CleanupTenantAsync(tA);
        await CleanupTenantAsync(tB);
    }

    // ----------------------------- infraestructura -----------------------------

    private static CrearContratoServicioRequest Contrato(string proveedor, string numero)
        => new(TipoServicio.Aseo, proveedor, "900111333", null,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 1_000_000m, null, 30, NumeroContrato: numero);

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
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM bitacora_mi_copropiedad WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM contrato_campo_valores WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM contrato_campos WHERE tenant_id = {tenantId}");
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
