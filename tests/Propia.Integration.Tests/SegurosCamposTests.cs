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
        Assert.True(PolizaCamposSistema.Por("numeropoliza")!.Fija);
        Assert.Contains("aseguradora", PolizaCamposSistema.ClavesVisiblesPorDefecto);
        Assert.Equal(TipoCampoTablero.Moneda, PolizaCamposSistema.Por("valorpoliza")!.Tipo);
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

    // ----- Fase 2: tipos avanzados (Formula / Usuario / Directorio) en polizas -----

    [Fact]
    public async Task Formula_en_poliza_computa_sobre_campo_propio_y_de_sistema()
    {
        var tenantId = await SeedTenantAsync("[SELLO] Formula poliza");
        var svc = BuildService(tenantId);
        var p = await svc.CrearPolizaAsync(new CrearPolizaRequest("[SELLO] Aseg formula",
            FechaInicio: new DateOnly(2026, 1, 1), FechaFin: new DateOnly(2026, 12, 31), ValorPoliza: 100m), CancellationToken.None);
        // Campo propio Numero con valor 40; formula = Suma(valorpoliza sistema + cd:propio) = 100 + 40 = 140.
        var num = await svc.CrearCampoAsync(new CrearPolizaCampoRequest("[SELLO] Extra", TipoCampoTablero.Numero, null, null), CancellationToken.None);
        await svc.GuardarCampoValorAsync(p.Id, num.Id, new GuardarPolizaCampoValorRequest("40"), CancellationToken.None);
        var f = await svc.CrearCampoAsync(new CrearPolizaCampoRequest("[SELLO] Total", TipoCampoTablero.Formula,
            new CampoFormulaConfig(OperacionFormula.Suma, new[] { "valorpoliza", "cd:" + num.Id }).Serializar(), null), CancellationToken.None);

        var din = await svc.ListCamposDinPolizaAsync(p.Id, CancellationToken.None);
        Assert.Equal("140", din.First(d => d.DefinicionId == f.Id).Valor);
        // Y la lectura flat (la que pinta la tabla) tambien emite el valor calculado.
        var flat = await svc.ListTodosCamposValoresPolizaAsync(CancellationToken.None);
        Assert.Contains(flat, v => v.PolizaId == p.Id && v.DefinicionId == f.Id && v.Valor == "140");
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Formula_en_poliza_es_solo_lectura_y_rechaza_fuente_no_numerica()
    {
        var tenantId = await SeedTenantAsync("[SELLO] Formula RO poliza");
        var svc = BuildService(tenantId);
        // Fuente no numerica -> rechazo al CREAR la formula (anti-nesting / tipo).
        var texto = await svc.CrearCampoAsync(new CrearPolizaCampoRequest("[SELLO] Texto", TipoCampoTablero.Texto, null, null), CancellationToken.None);
        await Assert.ThrowsAnyAsync<Exception>(() => svc.CrearCampoAsync(new CrearPolizaCampoRequest("[SELLO] Mala", TipoCampoTablero.Formula,
            new CampoFormulaConfig(OperacionFormula.Suma, new[] { "cd:" + texto.Id }).Serializar(), null), CancellationToken.None));
        // Formula valida, pero de SOLO LECTURA: no admite escribir su valor.
        var num = await svc.CrearCampoAsync(new CrearPolizaCampoRequest("[SELLO] N", TipoCampoTablero.Numero, null, null), CancellationToken.None);
        var f = await svc.CrearCampoAsync(new CrearPolizaCampoRequest("[SELLO] Suma", TipoCampoTablero.Formula,
            new CampoFormulaConfig(OperacionFormula.Suma, new[] { "cd:" + num.Id }).Serializar(), null), CancellationToken.None);
        var p = await svc.CrearPolizaAsync(new CrearPolizaRequest("[SELLO] Aseg RO", FechaInicio: new DateOnly(2026, 1, 1)), CancellationToken.None);
        await Assert.ThrowsAnyAsync<Exception>(() => svc.GuardarCampoValorAsync(p.Id, f.Id, new GuardarPolizaCampoValorRequest("5"), CancellationToken.None));
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Usuario_y_Directorio_en_poliza_rechazan_ids_de_otro_tenant()
    {
        var tA = await SeedTenantAsync("[SELLO] Adv poliza A");
        var tB = await SeedTenantAsync("[SELLO] Adv poliza B");
        var usuarioA = await SeedUsuarioTenantAsync(tA);
        var personaDirA = await SeedPersonaGlobalAsync();
        await SeedDirectorioVinculoAsync(tA, personaDirA);

        var svcA = BuildService(tA);
        var pA = await svcA.CrearPolizaAsync(new CrearPolizaRequest("[SELLO] pA", FechaInicio: new DateOnly(2026, 1, 1)), CancellationToken.None);
        var campoU = await svcA.CrearCampoAsync(new CrearPolizaCampoRequest("[SELLO] Resp", TipoCampoTablero.Usuario, null, null), CancellationToken.None);
        var campoD = await svcA.CrearCampoAsync(new CrearPolizaCampoRequest("[SELLO] Contacto", TipoCampoTablero.Directorio, null, null), CancellationToken.None);
        // En A: ids validos -> se guardan.
        await svcA.GuardarCampoValorAsync(pA.Id, campoU.Id, new GuardarPolizaCampoValorRequest(usuarioA.ToString()), CancellationToken.None);
        await svcA.GuardarCampoValorAsync(pA.Id, campoD.Id, new GuardarPolizaCampoValorRequest(personaDirA.ToString()), CancellationToken.None);

        var svcB = BuildService(tB);
        var pB = await svcB.CrearPolizaAsync(new CrearPolizaRequest("[SELLO] pB", FechaInicio: new DateOnly(2026, 1, 1)), CancellationToken.None);
        var campoUB = await svcB.CrearCampoAsync(new CrearPolizaCampoRequest("[SELLO] Resp", TipoCampoTablero.Usuario, null, null), CancellationToken.None);
        var campoDB = await svcB.CrearCampoAsync(new CrearPolizaCampoRequest("[SELLO] Contacto", TipoCampoTablero.Directorio, null, null), CancellationToken.None);
        // En B: el mismo id NO pertenece a B (personas es global) -> rechazo CROSS-TENANT.
        await Assert.ThrowsAnyAsync<Exception>(() => svcB.GuardarCampoValorAsync(pB.Id, campoUB.Id, new GuardarPolizaCampoValorRequest(usuarioA.ToString()), CancellationToken.None));
        await Assert.ThrowsAnyAsync<Exception>(() => svcB.GuardarCampoValorAsync(pB.Id, campoDB.Id, new GuardarPolizaCampoValorRequest(personaDirA.ToString()), CancellationToken.None));
        await CleanupTenantAsync(tA);
        await CleanupTenantAsync(tB);
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

    // Fase 2: seeders de usuario/directorio del tenant para el rechazo cross-tenant (personas es GLOBAL).
    private async Task<Guid> SeedUsuarioTenantAsync(Guid tenantId)
    {
        var personaId = await SeedPersonaGlobalAsync();
        await using var ctx = OwnerDb();
        ctx.UsuariosTenant.Add(new UsuarioTenant
        { TenantId = tenantId, PersonaId = personaId, Rol = "Residente", Estado = EstadoUsuarioTenant.Activo });
        await ctx.SaveChangesAsync();
        return personaId;
    }

    private async Task<Guid> SeedPersonaGlobalAsync()
    {
        await using var ctx = OwnerDb();
        var p = new Persona { TipoDocumento = TipoDocumento.CC, Documento = "D" + Guid.NewGuid().ToString("N")[..10], Nombres = "Sello", Apellidos = "Test" };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private async Task SeedDirectorioVinculoAsync(Guid tenantId, Guid personaId)
    {
        await using var ctx = OwnerDb();
        ctx.DirectorioVinculos.Add(new DirectorioVinculo
        {
            TenantId = tenantId, EntidadTipo = EntidadDirectorio.Persona, EntidadId = personaId,
            FechaDesde = DateOnly.FromDateTime(DateTime.UtcNow), Estado = EstadoVinculo.Activo
        });
        await ctx.SaveChangesAsync();
    }

    private async Task CleanupTenantAsync(Guid tenantId)
    {
        await using var ctx = OwnerDb();
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM poliza_campo_valores WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM poliza_campos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM polizas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM directorio_vinculos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM usuarios_tenant WHERE tenant_id = {tenantId}");
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
