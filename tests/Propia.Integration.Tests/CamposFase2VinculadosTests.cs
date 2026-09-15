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
/// Fase 2 replicada a las entidades VINCULADAS (Vehiculos y Mascotas): reusan los helpers compartidos
/// (CampoFormulaConfig.ComputarTexto + CamposAvanzados.ValidarValorAsync). Como NO tienen campos de sistema
/// Numero/Moneda, la Formula suma solo campos PROPIOS. Verifica el wiring por entidad: computo de Formula
/// en lectura y rechazo CROSS-TENANT de Usuario.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class CamposFase2VinculadosTests
{
    private readonly PostgresFixture _fx;
    public CamposFase2VinculadosTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Vehiculo_formula_sobre_campos_propios_computa_en_lectura()
    {
        var t = await SeedTenantAsync("F2 Veh formula");
        try
        {
            var svc = BuildService(t);
            var uId = (await svc.CrearUnidadAsync(NuevaUnidad("T1-301"), CancellationToken.None)).Id;
            var placaId = await SeedPlacaAsync(t, uId, "ABC123");
            var a = (await svc.CrearCampoDefVehiculoAsync(new CrearCampoDefinicionRequest("Peajes", TipoCampoTablero.Numero, null), CancellationToken.None)).Id;
            var b = (await svc.CrearCampoDefVehiculoAsync(new CrearCampoDefinicionRequest("Multas", TipoCampoTablero.Moneda, null), CancellationToken.None)).Id;
            var f = (await svc.CrearCampoDefVehiculoAsync(new CrearCampoDefinicionRequest("Total gastos", TipoCampoTablero.Formula,
                new CampoFormulaConfig(new[] { new PasoFormula(OperacionFormula.Suma, new[] { OperandoFormula.DeCampo("cd:" + a), OperandoFormula.DeCampo("cd:" + b) }) }).Serializar()), CancellationToken.None)).Id;

            await svc.SetCampoValorVehiculoDefAsync(placaId, a, new SetCampoValorRequest("15"), CancellationToken.None);
            await svc.SetCampoValorVehiculoDefAsync(placaId, b, new SetCampoValorRequest("35"), CancellationToken.None);

            var flat = await BuildService(t).ListTodosCamposValoresVehiculoAsync(CancellationToken.None);
            var comp = flat.First(x => x.UnidadPlacaId == placaId && x.DefinicionId == f).Valor;
            Assert.Equal("50", comp);

            // Formula es solo lectura: no admite escribir su valor.
            await Assert.ThrowsAnyAsync<Exception>(() => svc.SetCampoValorVehiculoDefAsync(placaId, f, new SetCampoValorRequest("999"), CancellationToken.None));
        }
        finally { await CleanupAsync(t); }
    }

    [Fact]
    public async Task Vehiculo_usuario_rechaza_un_usuario_de_otro_tenant()
    {
        var tA = await SeedTenantAsync("F2 Veh usuario A");
        var tB = await SeedTenantAsync("F2 Veh usuario B");
        try
        {
            var personaId = await SeedUsuarioTenantAsync(tA);   // usuario de A, NO de B
            var svcB = BuildService(tB);
            var uB = (await svcB.CrearUnidadAsync(NuevaUnidad("T1-302"), CancellationToken.None)).Id;
            var placaB = await SeedPlacaAsync(tB, uB, "BBB222");
            var campoB = (await svcB.CrearCampoDefVehiculoAsync(new CrearCampoDefinicionRequest("Responsable", TipoCampoTablero.Usuario, null), CancellationToken.None)).Id;
            await Assert.ThrowsAnyAsync<Exception>(() => svcB.SetCampoValorVehiculoDefAsync(
                placaB, campoB, new SetCampoValorRequest(personaId.ToString()), CancellationToken.None));
        }
        finally { await CleanupAsync(tA); await CleanupAsync(tB); }
    }

    [Fact]
    public async Task Mascota_formula_sobre_campos_propios_computa_en_lectura()
    {
        var t = await SeedTenantAsync("F2 Mas formula");
        try
        {
            var svc = BuildService(t);
            var uId = (await svc.CrearUnidadAsync(NuevaUnidad("T1-303"), CancellationToken.None)).Id;
            var masId = await SeedMascotaAsync(t, uId, "Rocky");
            var a = (await svc.CrearCampoDefMascotaAsync(new CrearCampoDefinicionRequest("Vacunas", TipoCampoTablero.Numero, null), CancellationToken.None)).Id;
            var b = (await svc.CrearCampoDefMascotaAsync(new CrearCampoDefinicionRequest("Consultas", TipoCampoTablero.Numero, null), CancellationToken.None)).Id;
            var f = (await svc.CrearCampoDefMascotaAsync(new CrearCampoDefinicionRequest("Total visitas", TipoCampoTablero.Formula,
                new CampoFormulaConfig(new[] { new PasoFormula(OperacionFormula.Conteo, new[] { OperandoFormula.DeCampo("cd:" + a), OperandoFormula.DeCampo("cd:" + b) }) }).Serializar()), CancellationToken.None)).Id;

            await svc.SetCampoValorMascotaDefAsync(masId, a, new SetCampoValorRequest("3"), CancellationToken.None);
            // b queda vacio -> Conteo cuenta solo los campos CON valor.
            var flat = await BuildService(t).ListTodosCamposValoresMascotaAsync(CancellationToken.None);
            Assert.Equal("1", flat.First(x => x.UnidadMascotaId == masId && x.DefinicionId == f).Valor);
        }
        finally { await CleanupAsync(t); }
    }

    [Fact]
    public async Task Residente_formula_sobre_campos_propios_computa_en_lectura()
    {
        var t = await SeedTenantAsync("F2 Res formula");
        try
        {
            var svc = BuildService(t);
            var uId = (await svc.CrearUnidadAsync(NuevaUnidad("T1-401"), CancellationToken.None)).Id;
            var upId = await SeedResidenteAsync(t, uId);
            var a = (await svc.CrearCampoDefPersonaAsync(new CrearCampoDefinicionRequest("Aportes", TipoCampoTablero.Moneda, null), CancellationToken.None)).Id;
            var b = (await svc.CrearCampoDefPersonaAsync(new CrearCampoDefinicionRequest("Deudas", TipoCampoTablero.Moneda, null), CancellationToken.None)).Id;
            var f = (await svc.CrearCampoDefPersonaAsync(new CrearCampoDefinicionRequest("Saldo", TipoCampoTablero.Formula,
                new CampoFormulaConfig(new[] { new PasoFormula(OperacionFormula.Suma, new[] { OperandoFormula.DeCampo("cd:" + a), OperandoFormula.DeCampo("cd:" + b) }) }).Serializar()), CancellationToken.None)).Id;
            await svc.SetCampoValorPersonaDefAsync(upId, a, new SetCampoValorRequest("120"), CancellationToken.None);
            await svc.SetCampoValorPersonaDefAsync(upId, b, new SetCampoValorRequest("80"), CancellationToken.None);

            var flat = await BuildService(t).ListTodosCamposValoresPersonaAsync(CancellationToken.None);
            Assert.Equal("200", flat.First(x => x.UnidadPersonaId == upId && x.DefinicionId == f).Valor);
            await Assert.ThrowsAnyAsync<Exception>(() => svc.SetCampoValorPersonaDefAsync(upId, f, new SetCampoValorRequest("1"), CancellationToken.None));
        }
        finally { await CleanupAsync(t); }
    }

    [Fact]
    public async Task Residente_usuario_rechaza_un_usuario_de_otro_tenant()
    {
        var tA = await SeedTenantAsync("F2 Res usuario A");
        var tB = await SeedTenantAsync("F2 Res usuario B");
        try
        {
            var personaId = await SeedUsuarioTenantAsync(tA);   // usuario de A, NO de B
            var svcB = BuildService(tB);
            var uB = (await svcB.CrearUnidadAsync(NuevaUnidad("T1-402"), CancellationToken.None)).Id;
            var upB = await SeedResidenteAsync(tB, uB);
            var campoB = (await svcB.CrearCampoDefPersonaAsync(new CrearCampoDefinicionRequest("Responsable", TipoCampoTablero.Usuario, null), CancellationToken.None)).Id;
            await Assert.ThrowsAnyAsync<Exception>(() => svcB.SetCampoValorPersonaDefAsync(
                upB, campoB, new SetCampoValorRequest(personaId.ToString()), CancellationToken.None));
        }
        finally { await CleanupAsync(tA); await CleanupAsync(tB); }
    }

    // ===================== helpers =====================
    private async Task<Guid> SeedResidenteAsync(Guid tenantId, Guid unidadId)
    {
        var personaId = await SeedPersonaGlobalAsync();
        await using var ctx = OwnerCtx();
        var up = new UnidadPersona { TenantId = tenantId, UnidadId = unidadId, PersonaId = personaId, Rol = RolUnidadPersona.Residente };
        ctx.UnidadPersonas.Add(up);
        await ctx.SaveChangesAsync();
        return up.Id;
    }

    private static CrearUnidadRequest NuevaUnidad(string numero)
        => new(numero, TipoUnidad.Apartamento, null, null, 1.0m, null, null, null, null, null, null);

    private async Task<Guid> SeedPlacaAsync(Guid tenantId, Guid unidadId, string placa)
    {
        await using var ctx = OwnerCtx();
        var p = new UnidadPlaca { TenantId = tenantId, UnidadId = unidadId, Placa = placa, TipoVehiculo = TipoVehiculo.Automovil };
        ctx.UnidadPlacas.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private async Task<Guid> SeedMascotaAsync(Guid tenantId, Guid unidadId, string nombre)
    {
        await using var ctx = OwnerCtx();
        var m = new UnidadMascota { TenantId = tenantId, UnidadId = unidadId, Nombre = nombre, Tipo = TipoMascota.Perro };
        ctx.UnidadMascotas.Add(m);
        await ctx.SaveChangesAsync();
        return m.Id;
    }

    private async Task<Guid> SeedUsuarioTenantAsync(Guid tenantId)
    {
        var personaId = await SeedPersonaGlobalAsync();
        await using var ctx = OwnerCtx();
        ctx.UsuariosTenant.Add(new UsuarioTenant { TenantId = tenantId, PersonaId = personaId, Rol = "Residente", Estado = EstadoUsuarioTenant.Activo });
        await ctx.SaveChangesAsync();
        return personaId;
    }

    private async Task<Guid> SeedPersonaGlobalAsync()
    {
        await using var ctx = OwnerCtx();
        var p = new Persona { TipoDocumento = TipoDocumento.CC, Documento = "D" + Guid.NewGuid().ToString("N")[..10], Nombres = "V", Apellidos = "Test" };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
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

    private PropiaDbContext OwnerCtx()
        => new(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options, new TenantContext());

    private async Task CleanupAsync(Guid tenantId)
    {
        await using var ctx = OwnerCtx();
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM vehiculo_campos_valores WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM vehiculo_campos_definiciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM mascota_campos_valores WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM mascota_campos_definiciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM persona_campos_valores WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM persona_campos_definiciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidad_personas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidad_placas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidad_mascotas WHERE tenant_id = {tenantId}");
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
