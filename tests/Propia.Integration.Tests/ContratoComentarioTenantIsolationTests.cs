using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Servicios;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Servicios;
using Propia.Infrastructure.Storage;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Hilo de actividad (chat) por-contrato: el comentario PERSISTE y se lee de vuelta, y NO se ve entre
/// copropiedades (RLS FORCE + policy). Espejo del test de comentarios de polizas.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ContratoComentarioTenantIsolationTests
{
    private readonly PostgresFixture _fx;

    public ContratoComentarioTenantIsolationTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Comentarios_de_contrato_persisten_y_no_se_ven_entre_copropiedades()
    {
        var tA = await SeedTenantAsync("[SELLO] ISO coment contrato A");
        var tB = await SeedTenantAsync("[SELLO] ISO coment contrato B");
        var svcA = BuildService(tA);
        var svcB = BuildService(tB);

        var cA = await SeedContratoAsync(tA, "[SELLO] Prov A");
        var cB = await SeedContratoAsync(tB, "[SELLO] Prov B");

        // Persistencia: el comentario se guarda (fila en BD) y se lee de vuelta, en orden por CreatedAt.
        var c1 = await svcA.AgregarComentarioContratoAsync(cA, new AgregarComentarioContratoRequest("Cargue el contrato firmado"), CancellationToken.None);
        Assert.NotEqual(Guid.Empty, c1.Id);
        Assert.Equal("Cargue el contrato firmado", c1.Texto);
        await svcA.AgregarComentarioContratoAsync(cA, new AgregarComentarioContratoRequest("Verificado por contabilidad"), CancellationToken.None);

        var comentariosA = await svcA.ListarComentariosContratoAsync(cA, CancellationToken.None);
        Assert.Equal(2, comentariosA.Count);
        Assert.Equal("Cargue el contrato firmado", comentariosA[0].Texto);

        // Aislamiento: B no ve los comentarios de A (ni por su contrato ni apuntando al id del de A).
        Assert.Empty(await svcB.ListarComentariosContratoAsync(cB, CancellationToken.None));
        Assert.Empty(await svcB.ListarComentariosContratoAsync(cA, CancellationToken.None));

        await CleanupTenantAsync(tA);
        await CleanupTenantAsync(tB);
    }

    // ----------------------------- infraestructura -----------------------------

    private IServiciosService BuildService(Guid tenantId)
    {
        var tenantCtx = new TenantContext();
        tenantCtx.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .AddInterceptors(new TenantConnectionInterceptor(tenantCtx))
            .Options;
        return new ServiciosService(new PropiaDbContext(options, tenantCtx), tenantCtx, new NoopBlobStorage(), new HttpContextAccessor());
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

    private async Task<Guid> SeedContratoAsync(Guid tenantId, string proveedor)
    {
        var tenantCtx = new TenantContext();
        tenantCtx.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .AddInterceptors(new TenantConnectionInterceptor(tenantCtx))
            .Options;
        await using var db = new PropiaDbContext(options, tenantCtx);
        var c = new ContratoServicio
        {
            TenantId = tenantId,
            Tipo = TipoServicio.Aseo,
            Proveedor = proveedor,
            FechaInicio = new DateOnly(2026, 1, 1),
            FechaFin = new DateOnly(2026, 12, 31),
            Estado = EstadoContrato.Vigente
        };
        db.ContratosServicio.Add(c);
        await db.SaveChangesAsync();
        return c.Id;
    }

    private async Task CleanupTenantAsync(Guid tenantId)
    {
        await using var ctx = OwnerDb();
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM contrato_comentarios WHERE tenant_id = {tenantId}");
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
