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

    [Fact]
    public async Task Campos_propios_crud_valores_cascada_y_aislamiento()
    {
        var tA = await SeedTenantAsync("[PLT] campos A");
        var tB = await SeedTenantAsync("[PLT] campos B");
        var svcA = Build(tA);
        var svcB = Build(tB);
        var ct = CancellationToken.None;

        var p = await svcA.CrearAsync(new GuardarPlantillaDocumentoRequest("Citacion", "asambleas", "<p>x</p>"), ct);
        var def = await svcA.CrearCampoAsync(new Propia.Application.MiCopropiedad.CrearCampoDefinicionRequest("Responsable", TipoCampoTablero.Texto), ct);
        // Mismo nombre (sin importar mayusculas) devuelve el existente; Formula cae a Texto (sin motor de calculo).
        Assert.Equal(def.Id, (await svcA.CrearCampoAsync(new Propia.Application.MiCopropiedad.CrearCampoDefinicionRequest("responsable"), ct)).Id);
        var fx = await svcA.CrearCampoAsync(new Propia.Application.MiCopropiedad.CrearCampoDefinicionRequest("Calc", TipoCampoTablero.Formula, "{}"), ct);
        Assert.Equal(TipoCampoTablero.Texto, fx.Tipo);

        Assert.True(await svcA.SetValorAsync(p.Id, def.Id, " Ana ", ct));
        Assert.Contains(await svcA.ListarValoresAsync(ct), v => v.PlantillaId == p.Id && v.DefinicionId == def.Id && v.Valor == "Ana");
        Assert.True(await svcA.SetValorAsync(p.Id, def.Id, "Luis", ct));     // upsert, no duplica
        Assert.Single(await svcA.ListarValoresAsync(ct));

        // B no ve ni toca nada de A.
        Assert.Empty(await svcB.ListarCamposAsync(ct));
        Assert.Empty(await svcB.ListarValoresAsync(ct));
        Assert.False(await svcB.SetValorAsync(p.Id, def.Id, "Hack", ct));
        Assert.False(await svcB.EliminarCampoAsync(def.Id, ct));
        Assert.False(await svcB.ActualizarCampoAsync(def.Id, new Propia.Application.MiCopropiedad.ActualizarCampoDefinicionRequest("X", TipoCampoTablero.Texto, null, 1), ct));
        Assert.Equal("Luis", (await svcA.ListarValoresAsync(ct)).Single().Valor);

        // Borrar la plantilla borra sus valores (cascada); borrar el campo deja el catalogo sin el.
        Assert.True(await svcA.EliminarAsync(p.Id, ct));
        Assert.Empty(await svcA.ListarValoresAsync(ct));
        Assert.True(await svcA.EliminarCampoAsync(def.Id, ct));
        Assert.DoesNotContain(await svcA.ListarCamposAsync(ct), d => d.Id == def.Id);

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
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM plantilla_campo_valores WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM plantilla_campo_definiciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM plantillas_documento WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }
}
