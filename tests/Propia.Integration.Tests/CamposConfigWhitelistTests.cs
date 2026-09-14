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
/// Config compartida de campos por copropiedad (UnidadCamposConfig, generica por (Entidad, CampoClave)).
/// Los modulos de administracion (PQRSD, Contratos, Seguros) adoptan esta config (decision de Alex
/// 2026-09-14), asi que su 'entidad' debe estar en la whitelist EntidadesCampoConfig. Cubre: round-trip
/// de guardar+listar para pqrsd/contrato/poliza, rechazo de entidad no valida, y aislamiento por tenant.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class CamposConfigWhitelistTests
{
    private readonly PostgresFixture _fx;
    public CamposConfigWhitelistTests(PostgresFixture fx) => _fx = fx;

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
        var options = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(options, new TenantContext());
        var t = new Tenant { Nombre = nombre, Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.SinAdmin };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        return t.Id;
    }

    [Theory]
    [InlineData("pqrsd")]
    [InlineData("contrato")]
    [InlineData("poliza")]
    // Mantenimiento (YUNQUE) tambien adopta la config compartida (whitelist-yunque):
    [InlineData("programacion")]
    [InlineData("zona")]
    [InlineData("equipo")]
    public async Task Config_admite_entidades_de_modulos_admin_y_hace_round_trip(string entidad)
    {
        var tenantId = await SeedTenantAsync($"[cfg] {entidad}");
        var svc = BuildService(tenantId);

        var guardado = await svc.GuardarCampoConfigAsync(
            new GuardarUnidadCampoConfigRequest("estado", $"Alias {entidad}", null, Oculto: true, Entidad: entidad),
            CancellationToken.None);
        Assert.Equal(entidad, guardado.Entidad);

        var list = await svc.ListCamposConfigAsync(entidad, CancellationToken.None);
        var row = Assert.Single(list);
        Assert.Equal("estado", row.CampoClave);
        Assert.Equal($"Alias {entidad}", row.Alias);
        Assert.True(row.Oculto);
    }

    [Fact]
    public async Task Config_rechaza_entidad_no_valida()
    {
        var tenantId = await SeedTenantAsync("[cfg] invalida");
        var svc = BuildService(tenantId);
        await Assert.ThrowsAnyAsync<Exception>(() => svc.GuardarCampoConfigAsync(
            new GuardarUnidadCampoConfigRequest("estado", null, null, Entidad: "inexistente"), CancellationToken.None));
    }

    [Fact]
    public async Task Config_esta_aislada_por_tenant()
    {
        var tA = await SeedTenantAsync("[cfg] tenant A");
        var tB = await SeedTenantAsync("[cfg] tenant B");

        await BuildService(tA).GuardarCampoConfigAsync(
            new GuardarUnidadCampoConfigRequest("estado", "solo A", null, Entidad: "pqrsd"), CancellationToken.None);

        var enB = await BuildService(tB).ListCamposConfigAsync("pqrsd", CancellationToken.None);
        Assert.Empty(enB);
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
