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
/// K-04: la bitacora de contratos era generica ("Contrato con 'X' actualizado."), no decia QUE cambio,
/// y el cambio de etapa (drag and drop) y el borrado no dejaban rastro. Ahora el update registra el
/// diff campo -> antes -> despues, y crear/mover-etapa/eliminar dejan una linea con los datos clave.
///
/// (El "quien"/Autor queda para SOLICITUD_SELLO_03: necesita user-context en el constructor del
/// servicio, que es archivo compartido.)
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ContratoBitacoraTests
{
    private readonly PostgresFixture _fx;

    public ContratoBitacoraTests(PostgresFixture fx) => _fx = fx;

    private static CrearContratoServicioRequest Contrato(string? numero = "[SELLO]-CT-100")
        => new(
            TipoServicio.Aseo, "[SELLO] Proveedor bitacora", "900111333", null,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 1_000_000m, null, 30,
            NumeroContrato: numero);

    private async Task<string> UltimaBitacoraAsync(Guid tenantId, Guid contratoId)
    {
        var entradas = await BuildService(tenantId).ListBitacoraEntidadAsync(contratoId, 20, CancellationToken.None);
        return entradas.Count == 0 ? "" : entradas[0].Descripcion;   // orden: mas reciente primero
    }

    [Fact]
    public async Task Crear_registra_la_bitacora_con_numero_y_vigencia()
    {
        var tenantId = await SeedTenantAsync("[SELLO] BIT crear");
        var c = await BuildService(tenantId).CrearContratoAsync(Contrato(), CancellationToken.None);

        var linea = await UltimaBitacoraAsync(tenantId, c.Id);
        Assert.Contains("[SELLO]-CT-100", linea);
        Assert.Contains("01/01/2026", linea);
        Assert.Contains("31/12/2026", linea);
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Actualizar_registra_el_diff_de_los_campos_cambiados()
    {
        var tenantId = await SeedTenantAsync("[SELLO] BIT diff");
        var svc = BuildService(tenantId);
        var c = await svc.CrearContratoAsync(Contrato(), CancellationToken.None);

        await svc.ActualizarContratoAsync(c.Id,
            new ActualizarContratoRequest(EstadoContrato.Vigente, 30, ValorMensual: 2_500_000m, FechaFin: new DateOnly(2027, 6, 30)),
            CancellationToken.None);

        var linea = await UltimaBitacoraAsync(tenantId, c.Id);
        Assert.Contains("valor mensual", linea, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1,000,000", linea);   // antes
        Assert.Contains("2,500,000", linea);   // despues
        Assert.Contains("fin:", linea, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("30/06/2027", linea);
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Actualizar_sin_cambios_lo_dice_explicitamente()
    {
        var tenantId = await SeedTenantAsync("[SELLO] BIT sin cambios");
        var svc = BuildService(tenantId);
        var c = await svc.CrearContratoAsync(Contrato(), CancellationToken.None);
        var creado = (await svc.ListContratosAsync(CancellationToken.None)).Single();

        // Mismo estado, sin tocar ningun campo de datos.
        await svc.ActualizarContratoAsync(c.Id,
            new ActualizarContratoRequest(creado.Estado, 30), CancellationToken.None);

        var linea = await UltimaBitacoraAsync(tenantId, c.Id);
        Assert.Contains("sin cambios de datos", linea, StringComparison.OrdinalIgnoreCase);
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Cambiar_de_etapa_registra_el_movimiento()
    {
        var tenantId = await SeedTenantAsync("[SELLO] BIT etapa");
        var svc = BuildService(tenantId);
        var c = await svc.CrearContratoAsync(Contrato(), CancellationToken.None);
        // ListContratos siembra las etapas base de la copropiedad.
        await svc.ListContratosAsync(CancellationToken.None);
        var etapaId = await PrimeraEtapaIdAsync(tenantId);

        await svc.CambiarEtapaContratoAsync(c.Id, new CambiarEtapaContratoRequest(etapaId), CancellationToken.None);

        var linea = await UltimaBitacoraAsync(tenantId, c.Id);
        Assert.Contains("movido de etapa", linea, StringComparison.OrdinalIgnoreCase);
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Eliminar_registra_el_borrado()
    {
        var tenantId = await SeedTenantAsync("[SELLO] BIT eliminar");
        var svc = BuildService(tenantId);
        var c = await svc.CrearContratoAsync(Contrato(), CancellationToken.None);

        await svc.EliminarContratoAsync(c.Id, CancellationToken.None);

        var linea = await UltimaBitacoraAsync(tenantId, c.Id);
        Assert.Contains("eliminado", linea, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[SELLO]-CT-100", linea);
        await CleanupTenantAsync(tenantId);
    }

    // ----------------------------- infraestructura -----------------------------

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
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.OwnerConnectionString)
            .Options;
        return new PropiaDbContext(options, new TenantContext());
    }

    private async Task<Guid> PrimeraEtapaIdAsync(Guid tenantId)
    {
        await using var ctx = OwnerDb();
        return await ctx.ContratoEtapas.IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.TenantId == tenantId).OrderBy(e => e.Orden).Select(e => e.Id).FirstAsync();
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
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM contratos_servicio WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM contrato_etapas WHERE tenant_id = {tenantId}");
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
