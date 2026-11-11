using Microsoft.EntityFrameworkCore;
using Propia.Application.Plantillas;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Plantillas;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Configuracion > Plantillas de documentos (catalogo central, tabla plantillas_documento con RLS):
/// la copropiedad nace con las plantillas semilla de PQRSD (categoria "pqrsd"), el PUT es MERGE, la
/// categoria se valida y NADA se ve ni se toca entre copropiedades.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class PlantillasDocumentoFlowTests
{
    private readonly PostgresFixture _fx;

    public PlantillasDocumentoFlowTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Nace_con_las_semillas_de_pqrsd_y_filtra_por_categoria()
    {
        var t = await SeedTenantAsync("[PLT] semilla");
        var svc = Build(t);

        int semillas;
        await using (var ctx = OwnerDb()) semillas = await ctx.PqrsdPlantillasSemilla.CountAsync(s => s.Activa);

        var todas = await svc.ListarAsync(null, CancellationToken.None);
        Assert.Equal(semillas, todas.Count);
        Assert.All(todas, p => Assert.Equal(PlantillasCatalogo.Pqrsd, p.Categoria));

        // Idempotente: listar otra vez no duplica.
        Assert.Equal(semillas, (await svc.ListarAsync(null, CancellationToken.None)).Count);
        Assert.Equal(semillas, (await svc.ListarAsync("pqrsd", CancellationToken.None)).Count);
        Assert.Empty(await svc.ListarAsync("cartera", CancellationToken.None));

        await CleanupTenantAsync(t);
    }

    [Fact]
    public async Task Put_es_merge_y_valida_nombre_y_categoria()
    {
        var t = await SeedTenantAsync("[PLT] merge");
        var svc = Build(t);

        var p = await svc.CrearAsync(new GuardarPlantillaDocumentoRequest("Paz y salvo", "cartera", "<p>{deuda.total}</p>"), CancellationToken.None);
        Assert.Equal("cartera", p.Categoria);

        // Solo nombre: categoria y cuerpo se conservan.
        var r1 = await svc.ActualizarAsync(p.Id, new GuardarPlantillaDocumentoRequest("Paz y salvo 2026", null, null), CancellationToken.None);
        Assert.NotNull(r1);
        Assert.Equal("Paz y salvo 2026", r1!.Nombre);
        Assert.Equal("cartera", r1.Categoria);
        Assert.Equal("<p>{deuda.total}</p>", r1.CuerpoHtml);

        // Solo categoria.
        var r2 = await svc.ActualizarAsync(p.Id, new GuardarPlantillaDocumentoRequest(null, "asambleas", null), CancellationToken.None);
        Assert.Equal("asambleas", r2!.Categoria);
        Assert.Equal("Paz y salvo 2026", r2.Nombre);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ActualizarAsync(p.Id, new GuardarPlantillaDocumentoRequest(null, "inexistente", null), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ActualizarAsync(p.Id, new GuardarPlantillaDocumentoRequest("  ", null, null), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearAsync(new GuardarPlantillaDocumentoRequest("", "pqrsd", ""), CancellationToken.None));

        await CleanupTenantAsync(t);
    }

    [Fact]
    public async Task Las_plantillas_no_se_ven_ni_se_tocan_entre_copropiedades()
    {
        var tA = await SeedTenantAsync("[PLT] ISO A");
        var tB = await SeedTenantAsync("[PLT] ISO B");
        var svcA = Build(tA);
        var svcB = Build(tB);

        var pA = await svcA.CrearAsync(new GuardarPlantillaDocumentoRequest("Solo de A", "certificados", "<p>A</p>"), CancellationToken.None);

        Assert.DoesNotContain(await svcB.ListarAsync(null, CancellationToken.None), x => x.Id == pA.Id);
        Assert.Empty(await svcB.ListarAsync("certificados", CancellationToken.None));
        Assert.Null(await svcB.ObtenerAsync(pA.Id, CancellationToken.None));
        Assert.Null(await svcB.ActualizarAsync(pA.Id, new GuardarPlantillaDocumentoRequest("Hackeada", null, null), CancellationToken.None));
        Assert.False(await svcB.EliminarAsync(pA.Id, CancellationToken.None));

        // Sigue intacta para A.
        var deA = await svcA.ObtenerAsync(pA.Id, CancellationToken.None);
        Assert.Equal("Solo de A", deA!.Nombre);
        Assert.True(await svcA.EliminarAsync(pA.Id, CancellationToken.None));

        await CleanupTenantAsync(tA);
        await CleanupTenantAsync(tB);
    }

    // ----------------------------- infraestructura -----------------------------

    private IPlantillasDocumentoService Build(Guid tenantId)
    {
        var tenantCtx = new TenantContext();
        tenantCtx.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .AddInterceptors(new TenantConnectionInterceptor(tenantCtx))
            .Options;
        return new PlantillasDocumentoService(new PropiaDbContext(options, tenantCtx), tenantCtx);
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
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM plantillas_documento WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }
}
