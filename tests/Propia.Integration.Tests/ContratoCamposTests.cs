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
/// Selector de Campos (Fase 1, PREPARACION) - Contratos. Cubre: (1) el catalogo de campos de sistema
/// ContratoCamposSistema esta bien formado y reusa la misma forma que UnidadCampoSistema; (2) los metodos
/// de la forma estandar (ListTodosCamposValoresContratoAsync / ListCamposDinContratoAsync) devuelven lo
/// mismo que el flujo actual de campos EAV de contrato.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ContratoCamposTests
{
    private readonly PostgresFixture _fx;

    public ContratoCamposTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public void Catalogo_de_campos_de_sistema_esta_bien_formado()
    {
        var todos = ContratoCamposSistema.Todos;
        Assert.NotEmpty(todos);
        // Claves unicas.
        Assert.Equal(todos.Count, todos.Select(c => c.Clave).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        // Forma canonica: record propio con Tipo; contratos no tiene Excel (Encabezado/Ayuda null,
        // SiempreEnPlantilla false).
        Assert.All(todos, c => Assert.IsType<ContratoCampoSistema>(c));
        Assert.All(todos, c => Assert.Null(c.Encabezado));
        Assert.All(todos, c => Assert.False(c.SiempreEnPlantilla));
        // El contratista es fijo (no se puede ocultar); las listas son Tipo Seleccion.
        Assert.True(ContratoCamposSistema.Por("proveedor")!.Fija);
        Assert.Equal(TipoCampoTablero.Seleccion, ContratoCamposSistema.Por("tipoContrato")!.Tipo);
        // Las Seleccion de contrato salen de enums del dominio: lista fija, no editable.
        Assert.All(todos.Where(c => c.Tipo == TipoCampoTablero.Seleccion), c => Assert.False(c.OpcionesEditables));
        // Hay columnas visibles por defecto (las de la tabla de /contratos).
        Assert.Contains("proveedor", ContratoCamposSistema.ClavesVisiblesPorDefecto);
        Assert.Equal(TipoCampoTablero.Moneda, ContratoCamposSistema.Por("valorMensual")!.Tipo);
        // Los campos de lista traen su semilla de etiquetas.
        Assert.NotEmpty(ContratoCamposSistema.TiposContratoSemilla);
        Assert.NotEmpty(ContratoCamposSistema.CategoriasSemilla);
    }

    [Fact]
    public async Task Metodos_estandar_de_valores_devuelven_el_campo_capturado()
    {
        var tenantId = await SeedTenantAsync("[SELLO] Campos contrato");
        var svc = BuildService(tenantId);
        var c = await svc.CrearContratoAsync(
            new CrearContratoServicioRequest(TipoServicio.Aseo, "[SELLO] Proveedor campos", "900111333", null,
                new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 1_000_000m, null, 30),
            CancellationToken.None);
        var campo = await svc.CrearContratoCampoAsync(
            new CrearContratoCampoRequest("[SELLO] Placa", TipoCampoTablero.Texto, null, null), CancellationToken.None);
        await svc.GuardarContratoCampoValorAsync(c.Id, campo.Id, new GuardarContratoCampoValorRequest("ABC-123"), CancellationToken.None);

        // campos-din de ese contrato: la definicion con su valor.
        var din = await svc.ListCamposDinContratoAsync(c.Id, CancellationToken.None);
        var fila = Assert.Single(din);
        Assert.Equal(campo.Id, fila.DefinicionId);
        Assert.Equal("[SELLO] Placa", fila.Label);
        Assert.Equal("ABC-123", fila.Valor);

        // campos-valores plano: incluye el valor capturado.
        var flat = await svc.ListTodosCamposValoresContratoAsync(CancellationToken.None);
        Assert.Contains(flat, v => v.ContratoId == c.Id && v.DefinicionId == campo.Id && v.Valor == "ABC-123");
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
