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
/// K-06: aislamiento por tenant de polizas y sus campos dinamicos. El servicio de una copropiedad no ve
/// las polizas, definiciones ni valores de otra (RLS + HasQueryFilter).
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SegurosTenantIsolationTests
{
    private readonly PostgresFixture _fx;

    public SegurosTenantIsolationTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Polizas_y_sus_campos_no_se_ven_entre_copropiedades()
    {
        var tA = await SeedTenantAsync("[SELLO] ISO poliza A");
        var tB = await SeedTenantAsync("[SELLO] ISO poliza B");
        var svcA = BuildService(tA);
        var svcB = BuildService(tB);

        var pA = await svcA.CrearPolizaAsync(Poliza("[SELLO] Aseg A", "[SELLO]-PA"), CancellationToken.None);
        var campoA = await svcA.CrearCampoAsync(new CrearPolizaCampoRequest("[SELLO] Campo A", TipoCampoTablero.Texto, null, null), CancellationToken.None);
        await svcA.GuardarCampoValorAsync(pA.Id, campoA.Id, new GuardarPolizaCampoValorRequest("valorA"), CancellationToken.None);

        await svcB.CrearPolizaAsync(Poliza("[SELLO] Aseg B", "[SELLO]-PB"), CancellationToken.None);
        await svcB.CrearCampoAsync(new CrearPolizaCampoRequest("[SELLO] Campo B", TipoCampoTablero.Texto, null, null), CancellationToken.None);

        var polizasA = await svcA.ListPolizasAsync(CancellationToken.None);
        Assert.Single(polizasA);
        Assert.Equal("[SELLO] Aseg A", polizasA[0].Aseguradora);

        var camposA = await svcA.ListCamposAsync(CancellationToken.None);
        Assert.Single(camposA);
        Assert.Equal("[SELLO] Campo A", camposA[0].Label);

        var valoresA = await svcA.ListTodosCamposValoresPolizaAsync(CancellationToken.None);
        Assert.All(valoresA, v => Assert.Equal(pA.Id, v.PolizaId));

        var polizasB = await svcB.ListPolizasAsync(CancellationToken.None);
        Assert.Single(polizasB);
        Assert.Equal("[SELLO] Aseg B", polizasB[0].Aseguradora);
        Assert.DoesNotContain(await svcB.ListCamposAsync(CancellationToken.None), c => c.Label == "[SELLO] Campo A");

        await CleanupTenantAsync(tA);
        await CleanupTenantAsync(tB);
    }

    /// <summary>
    /// Chat de reclamaciones (por-poliza): el comentario PERSISTE y se lee de vuelta, NO se ve entre
    /// copropiedades (RLS FORCE + policy), y el CRUD de siniestros sigue intacto (el chat no lo toca).
    /// </summary>
    [Fact]
    public async Task Comentarios_de_reclamaciones_persisten_no_se_ven_entre_copropiedades_y_no_tocan_el_CRUD()
    {
        var tA = await SeedTenantAsync("[ATLAS] ISO coment A");
        var tB = await SeedTenantAsync("[ATLAS] ISO coment B");
        var svcA = BuildService(tA);
        var svcB = BuildService(tB);

        var pA = await svcA.CrearPolizaAsync(Poliza("[ATLAS] Aseg A", "[ATLAS]-CA"), CancellationToken.None);
        var pB = await svcB.CrearPolizaAsync(Poliza("[ATLAS] Aseg B", "[ATLAS]-CB"), CancellationToken.None);

        // Persistencia: el comentario se guarda (fila en BD) y se lee de vuelta.
        var c1 = await svcA.AgregarComentarioAsync(pA.Id, new AgregarComentarioPolizaRequest("Revisando el siniestro"), CancellationToken.None);
        Assert.NotEqual(Guid.Empty, c1.Id);
        Assert.Equal("Revisando el siniestro", c1.Texto);
        await svcA.AgregarComentarioAsync(pA.Id, new AgregarComentarioPolizaRequest("Agendo visita"), CancellationToken.None);

        var comentariosA = await svcA.ListComentariosAsync(pA.Id, CancellationToken.None);
        Assert.Equal(2, comentariosA.Count);
        Assert.Equal("Revisando el siniestro", comentariosA[0].Texto); // orden por CreatedAt

        // Aislamiento: B no ve los comentarios de A (ni por su poliza ni apuntando al id de la de A).
        Assert.Empty(await svcB.ListComentariosAsync(pB.Id, CancellationToken.None));
        Assert.Empty(await svcB.ListComentariosAsync(pA.Id, CancellationToken.None));

        // CRUD de siniestros intacto: el chat no lo altera.
        await svcA.CrearReclamacionAsync(pA.Id, new CrearReclamacionRequest(new DateOnly(2026, 6, 1), 500m, "Siniestro X"), CancellationToken.None);
        var reclA = await svcA.ListReclamacionesAsync(pA.Id, CancellationToken.None);
        Assert.Single(reclA);
        Assert.Equal(500m, reclA[0].MontoReclamado);

        await CleanupTenantAsync(tA);
        await CleanupTenantAsync(tB);
    }

    // ----------------------------- infraestructura -----------------------------

    private static CrearPolizaRequest Poliza(string aseguradora, string numero)
        => new(aseguradora, NumeroPoliza: numero,
            FechaInicio: new DateOnly(2026, 1, 1), FechaFin: new DateOnly(2026, 12, 31), ValorPoliza: 100m);

    private ISegurosService BuildService(Guid tenantId)
    {
        var tenantCtx = new TenantContext();
        tenantCtx.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .AddInterceptors(new TenantConnectionInterceptor(tenantCtx))
            .Options;
        return new SegurosService(new PropiaDbContext(options, tenantCtx), new NoopBlobStorage(), new Microsoft.AspNetCore.Http.HttpContextAccessor());
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
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM poliza_campo_valores WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM poliza_campos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM poliza_reclamacion_comentarios WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM poliza_reclamaciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM polizas WHERE tenant_id = {tenantId}");
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
