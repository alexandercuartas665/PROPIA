using System.Globalization;
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
/// Fase 2 del Selector de Campos (referencia en Unidades): campos FORMULA (calculado, solo lectura),
/// USUARIO (vincula usuario del tenant) y DIRECTORIO (vincula persona del directorio). Verifica el
/// SUSTRATO compartido (dominio + servicio) sin UI: computo de las 5 operaciones con NUMEROS sobre campos
/// propios Y de sistema, recompute al cambiar la fuente, solo-lectura de Formula, y el RECHAZO CROSS-TENANT
/// de Usuario/Directorio (personas es GLOBAL: un id de otro tenant no debe vincularse).
/// </summary>
[Collection(nameof(PostgresCollection))]
public class CamposFormulaUsuarioDirectorioTests
{
    private readonly PostgresFixture _fx;
    public CamposFormulaUsuarioDirectorioTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Formula_sobre_campos_propios_computa_las_5_operaciones()
    {
        var t = await SeedTenantAsync("F2 Formula propios");
        try
        {
            var svc = BuildService(t);
            var uId = (await svc.CrearUnidadAsync(NuevaUnidad("T1-201"), CancellationToken.None)).Id;
            var a = await CrearCampoAsync(svc, "Cuota extra A", TipoCampoTablero.Numero);
            var b = await CrearCampoAsync(svc, "Cuota extra B", TipoCampoTablero.Moneda);
            await svc.SetCampoValorUnidadAsync(uId, a, new SetCampoValorRequest("10"), CancellationToken.None);
            await svc.SetCampoValorUnidadAsync(uId, b, new SetCampoValorRequest("40"), CancellationToken.None);
            var fuentes = new[] { "cd:" + a, "cd:" + b };

            Assert.Equal("50", await ComputarAsync(t, uId, OperacionFormula.Suma, fuentes));
            Assert.Equal("25", await ComputarAsync(t, uId, OperacionFormula.Promedio, fuentes));
            Assert.Equal("2", await ComputarAsync(t, uId, OperacionFormula.Conteo, fuentes));
            Assert.Equal("10", await ComputarAsync(t, uId, OperacionFormula.Minimo, fuentes));
            Assert.Equal("40", await ComputarAsync(t, uId, OperacionFormula.Maximo, fuentes));
        }
        finally { await CleanupAsync(t); }
    }

    [Fact]
    public async Task Formula_sobre_campos_de_sistema_suma_los_modulos_contributivos()
    {
        var t = await SeedTenantAsync("F2 Formula sistema");
        try
        {
            var svc = BuildService(t);
            var req = NuevaUnidad("T1-202") with
            {
                CoeficientePropiedad = 1.5m,
                ModuloContributivo1 = 2m, ModuloContributivo2 = 3m, ModuloContributivo3 = 5m,
            };
            var uId = (await svc.CrearUnidadAsync(req, CancellationToken.None)).Id;
            // Fuentes de SISTEMA (sin crear campos propios): los 3 modulos con valor + coeficiente.
            Assert.Equal("10", await ComputarAsync(t, uId, OperacionFormula.Suma,
                new[] { "modcontrib1", "modcontrib2", "modcontrib3", "modcontrib4", "modcontrib5" }));   // 2+3+5, los vacios no cuentan
            Assert.Equal("3", await ComputarAsync(t, uId, OperacionFormula.Conteo,
                new[] { "modcontrib1", "modcontrib2", "modcontrib3", "modcontrib4", "modcontrib5" }));
            Assert.Equal("11.5", await ComputarAsync(t, uId, OperacionFormula.Suma,
                new[] { "coef", "modcontrib1", "modcontrib2", "modcontrib3" }));   // 1.5 + 2+3+5
        }
        finally { await CleanupAsync(t); }
    }

    [Fact]
    public async Task Formula_recomputa_al_cambiar_el_valor_de_una_fuente()
    {
        var t = await SeedTenantAsync("F2 Recompute");
        try
        {
            var svc = BuildService(t);
            var uId = (await svc.CrearUnidadAsync(NuevaUnidad("T1-203"), CancellationToken.None)).Id;
            var a = await CrearCampoAsync(svc, "N", TipoCampoTablero.Numero);
            await svc.SetCampoValorUnidadAsync(uId, a, new SetCampoValorRequest("7"), CancellationToken.None);
            var fuentes = new[] { "cd:" + a };
            Assert.Equal("7", await ComputarAsync(t, uId, OperacionFormula.Suma, fuentes));
            // Cambia la fuente -> la formula (solo lectura, computada) refleja el nuevo valor al releer.
            await BuildService(t).SetCampoValorUnidadAsync(uId, a, new SetCampoValorRequest("99"), CancellationToken.None);
            Assert.Equal("99", await ComputarAsync(t, uId, OperacionFormula.Suma, fuentes));
        }
        finally { await CleanupAsync(t); }
    }

    [Fact]
    public async Task Formula_es_solo_lectura_y_rechaza_fuente_no_numerica()
    {
        var t = await SeedTenantAsync("F2 Formula reglas");
        try
        {
            var svc = BuildService(t);
            var texto = await CrearCampoAsync(svc, "Un texto", TipoCampoTablero.Texto);
            // Anti-nesting / tipo: una fuente que no es Numero/Moneda se rechaza al crear la formula.
            await Assert.ThrowsAnyAsync<Exception>(() => svc.CrearCampoDefinicionAsync(
                new CrearCampoDefinicionRequest("Mala formula", TipoCampoTablero.Formula,
                    new CampoFormulaConfig(new[] { new PasoFormula(OperacionFormula.Suma, new[] { OperandoFormula.DeCampo("cd:" + texto) }) }).Serializar()), CancellationToken.None));

            // Formula valida, pero es de SOLO LECTURA: no admite escribir su valor.
            var n = await CrearCampoAsync(svc, "Num", TipoCampoTablero.Numero);
            var f = (await svc.CrearCampoDefinicionAsync(new CrearCampoDefinicionRequest("Suma", TipoCampoTablero.Formula,
                new CampoFormulaConfig(new[] { new PasoFormula(OperacionFormula.Suma, new[] { OperandoFormula.DeCampo("cd:" + n) }) }).Serializar()), CancellationToken.None)).Id;
            var uId = (await svc.CrearUnidadAsync(NuevaUnidad("T1-204"), CancellationToken.None)).Id;
            await Assert.ThrowsAnyAsync<Exception>(() => svc.SetCampoValorUnidadAsync(uId, f, new SetCampoValorRequest("123"), CancellationToken.None));
        }
        finally { await CleanupAsync(t); }
    }

    [Fact]
    public async Task Usuario_acepta_un_usuario_del_tenant_y_rechaza_uno_de_otro_tenant()
    {
        var tA = await SeedTenantAsync("F2 Usuario A");
        var tB = await SeedTenantAsync("F2 Usuario B");
        try
        {
            var personaId = await SeedUsuarioTenantAsync(tA);   // la persona es usuario de A, NO de B

            var svcA = BuildService(tA);
            var uA = (await svcA.CrearUnidadAsync(NuevaUnidad("T1-205"), CancellationToken.None)).Id;
            var campoA = await CrearCampoAsync(svcA, "Responsable", TipoCampoTablero.Usuario);
            // En A: valido -> se guarda.
            await svcA.SetCampoValorUnidadAsync(uA, campoA, new SetCampoValorRequest(personaId.ToString()), CancellationToken.None);

            var svcB = BuildService(tB);
            var uB = (await svcB.CrearUnidadAsync(NuevaUnidad("T1-205"), CancellationToken.None)).Id;
            var campoB = await CrearCampoAsync(svcB, "Responsable", TipoCampoTablero.Usuario);
            // En B: el mismo id NO es usuario de B -> CROSS-TENANT rechazado.
            await Assert.ThrowsAnyAsync<Exception>(() => svcB.SetCampoValorUnidadAsync(
                uB, campoB, new SetCampoValorRequest(personaId.ToString()), CancellationToken.None));
        }
        finally { await CleanupAsync(tA); await CleanupAsync(tB); }
    }

    [Fact]
    public async Task Directorio_acepta_persona_del_tenant_y_rechaza_una_de_otro_tenant()
    {
        var tA = await SeedTenantAsync("F2 Dir A");
        var tB = await SeedTenantAsync("F2 Dir B");
        var personaId = await SeedPersonaGlobalAsync();
        try
        {
            await SeedDirectorioVinculoAsync(tA, personaId);   // la persona esta en el directorio de A, NO de B

            var svcA = BuildService(tA);
            var uA = (await svcA.CrearUnidadAsync(NuevaUnidad("T1-206"), CancellationToken.None)).Id;
            var campoA = await CrearCampoAsync(svcA, "Contacto", TipoCampoTablero.Directorio);
            await svcA.SetCampoValorUnidadAsync(uA, campoA, new SetCampoValorRequest(personaId.ToString()), CancellationToken.None);

            var svcB = BuildService(tB);
            var uB = (await svcB.CrearUnidadAsync(NuevaUnidad("T1-206"), CancellationToken.None)).Id;
            var campoB = await CrearCampoAsync(svcB, "Contacto", TipoCampoTablero.Directorio);
            await Assert.ThrowsAnyAsync<Exception>(() => svcB.SetCampoValorUnidadAsync(
                uB, campoB, new SetCampoValorRequest(personaId.ToString()), CancellationToken.None));
        }
        finally { await CleanupAsync(tA); await CleanupAsync(tB); }
    }

    [Fact]
    public async Task Usuario_persiste_el_id_y_se_relee_en_un_contexto_nuevo()
    {
        var t = await SeedTenantAsync("F2 Persistencia");
        try
        {
            var personaId = await SeedUsuarioTenantAsync(t);
            var svc = BuildService(t);
            var uId = (await svc.CrearUnidadAsync(NuevaUnidad("T1-207"), CancellationToken.None)).Id;
            var campo = await CrearCampoAsync(svc, "Responsable", TipoCampoTablero.Usuario);
            await svc.SetCampoValorUnidadAsync(uId, campo, new SetCampoValorRequest(personaId.ToString()), CancellationToken.None);

            // Simula F5: contexto/servicio NUEVO. El valor (el Guid) persiste.
            var campos = await BuildService(t).ListCamposUnidadAsync(uId, CancellationToken.None);
            var val = campos.First(c => c.DefinicionId == campo).Valor;
            Assert.Equal(personaId.ToString(), val);
        }
        finally { await CleanupAsync(t); }
    }

    // ===================== helpers =====================
    private async Task<string?> ComputarAsync(Guid tenantId, Guid unidadId, OperacionFormula op, string[] fuentes)
    {
        var svc = BuildService(tenantId);
        var f = (await svc.CrearCampoDefinicionAsync(new CrearCampoDefinicionRequest(
            $"F {op} {Guid.NewGuid():N}".Substring(0, 20), TipoCampoTablero.Formula,
            new CampoFormulaConfig(new[] { new PasoFormula(op, fuentes.Select(OperandoFormula.DeCampo).ToArray()) }).Serializar()), CancellationToken.None)).Id;
        var campos = await svc.ListCamposUnidadAsync(unidadId, CancellationToken.None);
        return campos.First(c => c.DefinicionId == f).Valor;
    }

    private static async Task<Guid> CrearCampoAsync(IMiCopropiedadService svc, string label, TipoCampoTablero tipo)
        => (await svc.CrearCampoDefinicionAsync(new CrearCampoDefinicionRequest(label, tipo, null), CancellationToken.None)).Id;

    private static CrearUnidadRequest NuevaUnidad(string numero)
        => new(numero, TipoUnidad.Apartamento, null, null, 1.0m, null, null, null, null, null, null);

    private IMiCopropiedadService BuildService(Guid tenantId)
    {
        var tenantCtx = new TenantContext();
        tenantCtx.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .AddInterceptors(new TenantConnectionInterceptor(tenantCtx))
            .Options;
        var db = new PropiaDbContext(options, tenantCtx);
        var blob = new NoopBlobStorage();
        var dir = new Propia.Infrastructure.Directorio.DirectorioService(db, tenantCtx, blob);
        return new MiCopropiedadService(db, tenantCtx, blob, new StubSeedUsuarioRolService(), dir);
    }

    private async Task<Guid> SeedTenantAsync(string nombre)
    {
        await using var ctx = OwnerCtx();
        var t = new Tenant { Nombre = nombre, Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.SinAdmin };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        return t.Id;
    }

    // Crea una Persona GLOBAL (usuarios_tenant tiene FK a personas) y la registra como usuario del tenant.
    private async Task<Guid> SeedUsuarioTenantAsync(Guid tenantId)
    {
        var personaId = await SeedPersonaGlobalAsync();
        await using var ctx = OwnerCtx();
        ctx.UsuariosTenant.Add(new UsuarioTenant
        { TenantId = tenantId, PersonaId = personaId, Rol = "Residente", Estado = EstadoUsuarioTenant.Activo });
        await ctx.SaveChangesAsync();
        return personaId;
    }

    private async Task<Guid> SeedPersonaGlobalAsync()
    {
        await using var ctx = OwnerCtx();
        var p = new Persona { TipoDocumento = TipoDocumento.CC, Documento = "D" + Guid.NewGuid().ToString("N")[..10], Nombres = "Dir", Apellidos = "Test" };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private async Task SeedDirectorioVinculoAsync(Guid tenantId, Guid personaId)
    {
        await using var ctx = OwnerCtx();
        ctx.DirectorioVinculos.Add(new DirectorioVinculo
        {
            TenantId = tenantId, EntidadTipo = EntidadDirectorio.Persona, EntidadId = personaId,
            FechaDesde = DateOnly.FromDateTime(DateTime.UtcNow), Estado = EstadoVinculo.Activo
        });
        await ctx.SaveChangesAsync();
    }

    private PropiaDbContext OwnerCtx()
        => new(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options, new TenantContext());

    private async Task CleanupAsync(Guid tenantId)
    {
        await using var ctx = OwnerCtx();
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidad_campos_valores WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidad_campos_definiciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM directorio_vinculos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM usuarios_tenant WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidad_coeficientes WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tipos_coeficiente WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidades_privadas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM torres WHERE tenant_id = {tenantId}");
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
