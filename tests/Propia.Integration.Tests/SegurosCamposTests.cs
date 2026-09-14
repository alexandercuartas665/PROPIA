using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Propia.Application.MiCopropiedad;   // ActualizarCampoDefinicionRequest (forma estandar del componente)
using Propia.Application.Seguros;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Seguros;
using Propia.Infrastructure.Storage;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Selector de Campos (Fase 1, PREPARACION) - Seguros. Cubre: (1) el catalogo de campos de sistema
/// PolizaCamposSistema esta bien formado y reusa la misma forma que UnidadCampoSistema; (2) los metodos
/// de la forma estandar (ListTodosCamposValoresPolizaAsync / ListCamposDinPolizaAsync) devuelven lo mismo
/// que el flujo actual de campos EAV de poliza.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SegurosCamposTests
{
    private readonly PostgresFixture _fx;

    public SegurosCamposTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public void Catalogo_de_campos_de_sistema_esta_bien_formado()
    {
        var todos = PolizaCamposSistema.Todos;
        Assert.NotEmpty(todos);
        Assert.Equal(todos.Count, todos.Select(c => c.Clave).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        // Forma canonica: record propio con Tipo; seguros no tiene Excel (Encabezado/Ayuda null).
        Assert.All(todos, c => Assert.IsType<PolizaCampoSistema>(c));
        Assert.All(todos, c => Assert.Null(c.Encabezado));
        Assert.All(todos, c => Assert.False(c.SiempreEnPlantilla));
        // El numero de poliza es fijo (no se puede ocultar).
        Assert.True(PolizaCamposSistema.Por("numeroPoliza")!.Fija);
        Assert.Contains("aseguradora", PolizaCamposSistema.ClavesVisiblesPorDefecto);
        Assert.Equal(TipoCampoTablero.Moneda, PolizaCamposSistema.Por("valorPoliza")!.Tipo);
    }

    [Fact]
    public async Task Metodos_estandar_de_valores_devuelven_el_campo_capturado()
    {
        var tenantId = await SeedTenantAsync("[SELLO] Campos poliza");
        var svc = BuildService(tenantId);
        var p = await svc.CrearPolizaAsync(
            new CrearPolizaRequest("[SELLO] Aseguradora campos",
                FechaInicio: new DateOnly(2026, 1, 1), FechaFin: new DateOnly(2026, 12, 31), ValorPoliza: 100m),
            CancellationToken.None);
        var campo = await svc.CrearCampoAsync(
            new CrearPolizaCampoRequest("[SELLO] Deducible", TipoCampoTablero.Texto, null, null), CancellationToken.None);
        await svc.GuardarCampoValorAsync(p.Id, campo.Id, new GuardarPolizaCampoValorRequest("5%"), CancellationToken.None);

        var din = await svc.ListCamposDinPolizaAsync(p.Id, CancellationToken.None);
        var fila = Assert.Single(din);
        Assert.Equal(campo.Id, fila.DefinicionId);
        Assert.Equal("[SELLO] Deducible", fila.Label);
        Assert.Equal("5%", fila.Valor);

        var flat = await svc.ListTodosCamposValoresPolizaAsync(CancellationToken.None);
        Assert.Contains(flat, v => v.PolizaId == p.Id && v.DefinicionId == campo.Id && v.Valor == "5%");
        await CleanupTenantAsync(tenantId);
    }

    // ----- Selector de Campos (Fase 1): PUT de poliza-campo como MERGE frente al componente compartido -----

    [Fact]
    public async Task Editar_como_el_componente_conserva_Activo_y_Descripcion_MERGE()
    {
        var tenantId = await SeedTenantAsync("[SELLO] MERGE poliza campo");
        var svc = BuildService(tenantId);
        // Campo propio con descripcion; Activo=true por defecto al crear.
        var campo = await svc.CrearCampoAsync(
            new CrearPolizaCampoRequest("[SELLO] Deducible", TipoCampoTablero.Texto, null, "Nota interna del corredor"),
            CancellationToken.None);

        // Simula EXACTAMENTE el body que manda el componente (ConfigCamposEntidad) al renombrar:
        // ActualizarCampoDefinicionRequest(Label, Tipo, Opciones, Orden) via PutAsJsonAsync (Web defaults).
        // Al deserializarlo en mi DTO, Descripcion y Activo quedan sin enviar (null): eso PRUEBA que los
        // nombres calzan (el body bindea) y que el MERGE sabe distinguir "no enviado".
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var bodyComponente = new ActualizarCampoDefinicionRequest("[SELLO] Deducible (editado)", campo.Tipo, campo.Opciones, campo.Orden);
        var req = JsonSerializer.Deserialize<ActualizarPolizaCampoRequest>(
            JsonSerializer.Serialize(bodyComponente, web), web)!;
        Assert.Null(req.Activo);
        Assert.Null(req.Descripcion);
        Assert.Equal("[SELLO] Deducible (editado)", req.Label);

        Assert.True(await svc.ActualizarCampoAsync(campo.Id, req, CancellationToken.None));

        await using var db = AppDb(tenantId);
        var raw = await db.PolizaCampos.AsNoTracking().FirstAsync(x => x.Id == campo.Id);
        Assert.Equal("[SELLO] Deducible (editado)", raw.Label);      // el rename se aplico
        Assert.True(raw.Activo);                                     // NO se oculto (Activo conservado)
        Assert.Equal("Nota interna del corredor", raw.Descripcion);  // la descripcion se conservo
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task El_gestor_oculta_y_el_edit_del_componente_no_reactiva_MERGE()
    {
        var tenantId = await SeedTenantAsync("[SELLO] MERGE poliza oculto");
        var svc = BuildService(tenantId);
        var campo = await svc.CrearCampoAsync(
            new CrearPolizaCampoRequest("[SELLO] Placa", TipoCampoTablero.Texto, null, null), CancellationToken.None);

        // El gestor propio manda la forma COMPLETA con Activo=false (ocultar como columna) -> se aplica.
        await svc.ActualizarCampoAsync(campo.Id,
            new ActualizarPolizaCampoRequest("[SELLO] Placa", TipoCampoTablero.Texto, null, null, campo.Orden, false),
            CancellationToken.None);
        await using (var db1 = AppDb(tenantId))
            Assert.False((await db1.PolizaCampos.AsNoTracking().FirstAsync(x => x.Id == campo.Id)).Activo);

        // El componente edita (rename) SIN Activo -> el campo sigue oculto (el MERGE no lo reactiva).
        var req = new ActualizarPolizaCampoRequest("[SELLO] Placa vehiculo", TipoCampoTablero.Texto, null, null, campo.Orden, null);
        await svc.ActualizarCampoAsync(campo.Id, req, CancellationToken.None);

        await using var db2 = AppDb(tenantId);
        var raw = await db2.PolizaCampos.AsNoTracking().FirstAsync(x => x.Id == campo.Id);
        Assert.Equal("[SELLO] Placa vehiculo", raw.Label);   // el rename se aplico
        Assert.False(raw.Activo);                            // pero sigue oculto (Activo=false conservado)
        await CleanupTenantAsync(tenantId);
    }

    // ----------------------------- infraestructura -----------------------------

    // Contexto de aplicacion (respeta RLS/tenant) para leer el estado crudo del campo en las aserciones,
    // incluidos los inactivos (ListCamposAsync filtra Activo, aqui necesitamos ver el que se oculto).
    private PropiaDbContext AppDb(Guid tenantId)
    {
        var tenantCtx = new TenantContext();
        tenantCtx.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .AddInterceptors(new TenantConnectionInterceptor(tenantCtx))
            .Options;
        return new PropiaDbContext(options, tenantCtx);
    }

    private ISegurosService BuildService(Guid tenantId)
    {
        var tenantCtx = new TenantContext();
        tenantCtx.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .AddInterceptors(new TenantConnectionInterceptor(tenantCtx))
            .Options;
        return new SegurosService(new PropiaDbContext(options, tenantCtx), new NoopBlobStorage());
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
