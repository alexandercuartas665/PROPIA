using System.Text.Json;
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
        // El contratista es fijo (no se puede ocultar); las listas son Tipo Seleccion. Las claves = las de la
        // tabla de /contratos (adopcion Fase 1): el contratista es "tercero", el valor visible "valorTotal".
        Assert.True(ContratoCamposSistema.Por("tercero")!.Fija);
        Assert.Equal(TipoCampoTablero.Seleccion, ContratoCamposSistema.Por("tipocontrato")!.Tipo);
        // Las Seleccion de contrato salen de enums del dominio: lista fija, no editable.
        Assert.All(todos.Where(c => c.Tipo == TipoCampoTablero.Seleccion), c => Assert.False(c.OpcionesEditables));
        // Hay columnas visibles por defecto (las de la tabla de /contratos).
        Assert.Contains("tercero", ContratoCamposSistema.ClavesVisiblesPorDefecto);
        Assert.Equal(TipoCampoTablero.Moneda, ContratoCamposSistema.Por("valortotal")!.Tipo);
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

    // ----- Selector de Campos (Fase 1): PUT de contrato-campo como MERGE frente al componente compartido -----

    [Fact]
    public async Task Editar_como_el_componente_conserva_Activo_y_Descripcion_MERGE()
    {
        var tenantId = await SeedTenantAsync("[SELLO] MERGE contrato campo");
        var svc = BuildService(tenantId);
        var campo = await svc.CrearContratoCampoAsync(
            new CrearContratoCampoRequest("[SELLO] Placa", TipoCampoTablero.Texto, null, "Nota interna del contrato"),
            CancellationToken.None);

        // Simula EXACTAMENTE el body que manda el componente (ConfigCamposEntidad) al renombrar:
        // ActualizarCampoDefinicionRequest(Label, Tipo, Opciones, Orden) via PutAsJsonAsync (Web defaults).
        // Al deserializarlo en mi DTO, Descripcion y Activo quedan sin enviar (null): eso PRUEBA que los
        // nombres calzan (el body bindea) y que el MERGE sabe distinguir "no enviado".
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var bodyComponente = new ActualizarCampoDefinicionRequest("[SELLO] Placa (editado)", campo.Tipo, campo.Opciones, campo.Orden);
        var req = JsonSerializer.Deserialize<ActualizarContratoCampoRequest>(
            JsonSerializer.Serialize(bodyComponente, web), web)!;
        Assert.Null(req.Activo);
        Assert.Null(req.Descripcion);
        Assert.Equal("[SELLO] Placa (editado)", req.Label);

        Assert.True(await svc.ActualizarContratoCampoAsync(campo.Id, req, CancellationToken.None));

        await using var db = AppDb(tenantId);
        var raw = await db.ContratoCampos.AsNoTracking().FirstAsync(x => x.Id == campo.Id);
        Assert.Equal("[SELLO] Placa (editado)", raw.Label);         // el rename se aplico
        Assert.True(raw.Activo);                                    // NO se oculto (Activo conservado)
        Assert.Equal("Nota interna del contrato", raw.Descripcion); // la descripcion se conservo
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task El_gestor_oculta_y_el_edit_del_componente_no_reactiva_MERGE()
    {
        var tenantId = await SeedTenantAsync("[SELLO] MERGE contrato oculto");
        var svc = BuildService(tenantId);
        var campo = await svc.CrearContratoCampoAsync(
            new CrearContratoCampoRequest("[SELLO] Placa", TipoCampoTablero.Texto, null, null), CancellationToken.None);

        // El gestor propio manda la forma COMPLETA con Activo=false (ocultar como columna) -> se aplica.
        await svc.ActualizarContratoCampoAsync(campo.Id,
            new ActualizarContratoCampoRequest("[SELLO] Placa", TipoCampoTablero.Texto, null, null, campo.Orden, false),
            CancellationToken.None);
        await using (var db1 = AppDb(tenantId))
            Assert.False((await db1.ContratoCampos.AsNoTracking().FirstAsync(x => x.Id == campo.Id)).Activo);

        // El componente edita (rename) SIN Activo -> el campo sigue oculto (el MERGE no lo reactiva).
        var req = new ActualizarContratoCampoRequest("[SELLO] Placa contratista", TipoCampoTablero.Texto, null, null, campo.Orden, null);
        await svc.ActualizarContratoCampoAsync(campo.Id, req, CancellationToken.None);

        await using var db2 = AppDb(tenantId);
        var raw = await db2.ContratoCampos.AsNoTracking().FirstAsync(x => x.Id == campo.Id);
        Assert.Equal("[SELLO] Placa contratista", raw.Label);   // el rename se aplico
        Assert.False(raw.Activo);                               // pero sigue oculto (Activo=false conservado)
        await CleanupTenantAsync(tenantId);
    }

    // ----- Fase 2: tipos avanzados (Formula / Usuario / Directorio) en contratos -----

    [Fact]
    public async Task Formula_en_contrato_computa_sobre_campo_propio_y_de_sistema()
    {
        var tenantId = await SeedTenantAsync("[SELLO] Formula contrato");
        var svc = BuildService(tenantId);
        var c = await svc.CrearContratoAsync(new CrearContratoServicioRequest(
            TipoServicio.Aseo, "[SELLO] Prov formula", null, null, new DateOnly(2026, 1, 1), null, 1_000_000m, null),
            CancellationToken.None);
        // Campo propio Numero con valor 40; formula = Suma(valor sistema + cd:propio) = 1000000 + 40 = 1000040.
        var num = await svc.CrearContratoCampoAsync(new CrearContratoCampoRequest("[SELLO] Extra", TipoCampoTablero.Numero, null, null), CancellationToken.None);
        await svc.GuardarContratoCampoValorAsync(c.Id, num.Id, new GuardarContratoCampoValorRequest("40"), CancellationToken.None);
        var f = await svc.CrearContratoCampoAsync(new CrearContratoCampoRequest("[SELLO] Total", TipoCampoTablero.Formula,
            new CampoFormulaConfig(new[] { new PasoFormula(OperacionFormula.Suma, new[] { OperandoFormula.DeCampo("valor"), OperandoFormula.DeCampo("cd:" + num.Id) }) }).Serializar(), null), CancellationToken.None);

        var din = await svc.ListCamposDinContratoAsync(c.Id, CancellationToken.None);
        Assert.Equal("1000040", din.First(d => d.DefinicionId == f.Id).Valor);
        var flat = await svc.ListTodosCamposValoresContratoAsync(CancellationToken.None);
        Assert.Contains(flat, v => v.ContratoId == c.Id && v.DefinicionId == f.Id && v.Valor == "1000040");
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Formula_en_contrato_es_solo_lectura_y_rechaza_fuente_no_numerica()
    {
        var tenantId = await SeedTenantAsync("[SELLO] Formula RO contrato");
        var svc = BuildService(tenantId);
        var texto = await svc.CrearContratoCampoAsync(new CrearContratoCampoRequest("[SELLO] Texto", TipoCampoTablero.Texto, null, null), CancellationToken.None);
        await Assert.ThrowsAnyAsync<Exception>(() => svc.CrearContratoCampoAsync(new CrearContratoCampoRequest("[SELLO] Mala", TipoCampoTablero.Formula,
            new CampoFormulaConfig(new[] { new PasoFormula(OperacionFormula.Suma, new[] { OperandoFormula.DeCampo("cd:" + texto.Id) }) }).Serializar(), null), CancellationToken.None));
        var num = await svc.CrearContratoCampoAsync(new CrearContratoCampoRequest("[SELLO] N", TipoCampoTablero.Numero, null, null), CancellationToken.None);
        var f = await svc.CrearContratoCampoAsync(new CrearContratoCampoRequest("[SELLO] Suma", TipoCampoTablero.Formula,
            new CampoFormulaConfig(new[] { new PasoFormula(OperacionFormula.Suma, new[] { OperandoFormula.DeCampo("cd:" + num.Id) }) }).Serializar(), null), CancellationToken.None);
        var c = await svc.CrearContratoAsync(new CrearContratoServicioRequest(
            TipoServicio.Aseo, "[SELLO] Prov RO", null, null, new DateOnly(2026, 1, 1), null, 100m, null), CancellationToken.None);
        await Assert.ThrowsAnyAsync<Exception>(() => svc.GuardarContratoCampoValorAsync(c.Id, f.Id, new GuardarContratoCampoValorRequest("5"), CancellationToken.None));
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Usuario_y_Directorio_en_contrato_rechazan_ids_de_otro_tenant()
    {
        var tA = await SeedTenantAsync("[SELLO] Adv contrato A");
        var tB = await SeedTenantAsync("[SELLO] Adv contrato B");
        var usuarioA = await SeedUsuarioTenantAsync(tA);
        var personaDirA = await SeedPersonaGlobalAsync();
        await SeedDirectorioVinculoAsync(tA, personaDirA);

        var svcA = BuildService(tA);
        var cA = await svcA.CrearContratoAsync(new CrearContratoServicioRequest(
            TipoServicio.Aseo, "[SELLO] Prov A", null, null, new DateOnly(2026, 1, 1), null, 100m, null), CancellationToken.None);
        var campoU = await svcA.CrearContratoCampoAsync(new CrearContratoCampoRequest("[SELLO] Resp", TipoCampoTablero.Usuario, null, null), CancellationToken.None);
        var campoD = await svcA.CrearContratoCampoAsync(new CrearContratoCampoRequest("[SELLO] Contacto", TipoCampoTablero.Directorio, null, null), CancellationToken.None);
        await svcA.GuardarContratoCampoValorAsync(cA.Id, campoU.Id, new GuardarContratoCampoValorRequest(usuarioA.ToString()), CancellationToken.None);
        await svcA.GuardarContratoCampoValorAsync(cA.Id, campoD.Id, new GuardarContratoCampoValorRequest(personaDirA.ToString()), CancellationToken.None);

        var svcB = BuildService(tB);
        var cB = await svcB.CrearContratoAsync(new CrearContratoServicioRequest(
            TipoServicio.Aseo, "[SELLO] Prov B", null, null, new DateOnly(2026, 1, 1), null, 100m, null), CancellationToken.None);
        var campoUB = await svcB.CrearContratoCampoAsync(new CrearContratoCampoRequest("[SELLO] Resp", TipoCampoTablero.Usuario, null, null), CancellationToken.None);
        var campoDB = await svcB.CrearContratoCampoAsync(new CrearContratoCampoRequest("[SELLO] Contacto", TipoCampoTablero.Directorio, null, null), CancellationToken.None);
        // El mismo id NO pertenece a B (personas es global) -> rechazo CROSS-TENANT.
        await Assert.ThrowsAnyAsync<Exception>(() => svcB.GuardarContratoCampoValorAsync(cB.Id, campoUB.Id, new GuardarContratoCampoValorRequest(usuarioA.ToString()), CancellationToken.None));
        await Assert.ThrowsAnyAsync<Exception>(() => svcB.GuardarContratoCampoValorAsync(cB.Id, campoDB.Id, new GuardarContratoCampoValorRequest(personaDirA.ToString()), CancellationToken.None));
        await CleanupTenantAsync(tA);
        await CleanupTenantAsync(tB);
    }

    // ----------------------------- infraestructura -----------------------------

    // Contexto de aplicacion (respeta RLS/tenant) para leer el estado crudo del campo en las aserciones.
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
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM bitacora_mi_copropiedad WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM contrato_campo_valores WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM contrato_campos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM contratos_servicio WHERE tenant_id = {tenantId}");
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
