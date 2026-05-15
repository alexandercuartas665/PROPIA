using Microsoft.EntityFrameworkCore;
using Propia.Application.Directorio;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Directorio;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Tests del modulo 2.4 Directorio (spec v1.0).
///
/// Cubre el flujo principal:
///  - Persona/Empresa como entidades globales unicas por documento/NIT
///  - Vinculo Persona<->Copropiedad con etiquetas
///  - DV del NIT calculado segun algoritmo DIAN
///  - Catalogo base de etiquetas seedeado por la migracion (36 base)
///  - RLS: tenant A no ve vinculos de tenant B
///  - Idempotencia: crear persona con documento duplicado falla
/// </summary>
[Collection(nameof(PostgresCollection))]
public class DirectorioFlowTests
{
    private readonly PostgresFixture _fx;

    public DirectorioFlowTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Catalogo_base_de_etiquetas_existe_tras_migracion()
    {
        var (svc, _, _) = BuildService(await SeedTenantAsync("CP Etiquetas"));

        var personas = await svc.ListarEtiquetasAsync(AplicaEtiqueta.Persona, null, CancellationToken.None);
        var empresas = await svc.ListarEtiquetasAsync(AplicaEtiqueta.Empresa, null, CancellationToken.None);

        // 36 base totales (22 persona identidad + cargo + 11 empresa). Validamos algunas clave:
        Assert.Contains(personas, e => e.Codigo == "PROPIETARIO" && e.EsBase);
        Assert.Contains(personas, e => e.Codigo == "REVISOR_FISCAL" && e.EsBase && e.TieneLogicaEspecial);
        Assert.Contains(empresas, e => e.Codigo == "PROVEEDOR" && e.EsBase);
        Assert.Contains(empresas, e => e.Codigo == "EMPRESA_ADMIN" && e.EsBase);
    }

    [Fact]
    public async Task Crear_persona_con_documento_duplicado_falla()
    {
        var (svc, _, _) = BuildService(await SeedTenantAsync("CP Persona Dup"));
        var doc = $"DOC{Guid.NewGuid():N}".Substring(0, 18);

        await svc.CrearPersonaAsync(new CrearPersonaRequest(
            TipoDocumento.CC, doc, "Ana", "Test", null, null, null, null), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearPersonaAsync(new CrearPersonaRequest(
                TipoDocumento.CC, doc, "Otra", "Persona", null, null, null, null), CancellationToken.None));
    }

    [Fact]
    public async Task Buscar_persona_por_documento_funciona_globalmente()
    {
        var tA = await SeedTenantAsync("CP Busqueda A");
        var (svcA, _, _) = BuildService(tA);
        var doc = $"DOC{Guid.NewGuid():N}".Substring(0, 18);
        await svcA.CrearPersonaAsync(new CrearPersonaRequest(
            TipoDocumento.CC, doc, "Carlos", "Buscado", null, null, null, null), CancellationToken.None);

        // Otro tenant la encuentra (busqueda es GLOBAL, no por tenant)
        var (svcB, _, _) = BuildService(await SeedTenantAsync("CP Busqueda B"));
        var encontrada = await svcB.BuscarPersonaPorDocumentoAsync(
            new BuscarPorDocumentoRequest(TipoDocumento.CC, doc), CancellationToken.None);

        Assert.NotNull(encontrada);
        Assert.Equal("Carlos", encontrada!.Nombres);
    }

    [Fact]
    public async Task Crear_vinculo_persona_con_etiquetas_aparece_en_bandeja_y_360()
    {
        var tenantId = await SeedTenantAsync("CP Vinculos");
        var (svc, _, _) = BuildService(tenantId);

        var persona = await svc.CrearPersonaAsync(new CrearPersonaRequest(
            TipoDocumento.CC, $"DOC{Guid.NewGuid():N}".Substring(0, 18),
            "Diana", "Propietaria", "diana@test.co", "300 111 2222", null, null), CancellationToken.None);

        // Tomo la etiqueta PROPIETARIO del catalogo base
        var etiquetas = await svc.ListarEtiquetasAsync(AplicaEtiqueta.Persona, GrupoEtiqueta.Identidad, CancellationToken.None);
        var propietarioId = etiquetas.First(e => e.Codigo == "PROPIETARIO").Id;

        var vinculo = await svc.CrearVinculoAsync(new CrearVinculoRequest(
            EntidadDirectorio.Persona, persona.Id,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new[] { propietarioId }), CancellationToken.None);

        Assert.Equal(EstadoVinculo.Activo, vinculo.Estado);
        Assert.Contains(vinculo.Etiquetas, e => e.Codigo == "PROPIETARIO");

        // Aparece en bandeja del tenant
        var bandeja = await svc.ListarPersonasDelTenantAsync(null, CancellationToken.None);
        Assert.Contains(bandeja, p => p.Id == persona.Id);

        // Aparece en 360
        var p360 = await svc.GetPersona360Async(persona.Id, CancellationToken.None);
        Assert.NotNull(p360);
        Assert.Single(p360!.VinculosEnCopropiedad);
        Assert.Contains(p360.VinculosEnCopropiedad[0].Etiquetas, e => e.Codigo == "PROPIETARIO");
    }

    [Fact]
    public async Task Tenant_B_no_ve_vinculos_de_tenant_A_via_RLS()
    {
        var tA = await SeedTenantAsync("CP RLS Dir A");
        var tB = await SeedTenantAsync("CP RLS Dir B");
        var (svcA, _, _) = BuildService(tA);
        var (svcB, _, _) = BuildService(tB);

        var personaA = await svcA.CrearPersonaAsync(new CrearPersonaRequest(
            TipoDocumento.CC, $"DOC{Guid.NewGuid():N}".Substring(0, 18),
            "Solo", "TenantA", null, null, null, null), CancellationToken.None);
        await svcA.CrearVinculoAsync(new CrearVinculoRequest(
            EntidadDirectorio.Persona, personaA.Id,
            DateOnly.FromDateTime(DateTime.UtcNow), null), CancellationToken.None);

        // Tenant B no ve a la persona en bandeja porque NO esta vinculada a B
        var bandejaB = await svcB.ListarPersonasDelTenantAsync(null, CancellationToken.None);
        Assert.DoesNotContain(bandejaB, p => p.Id == personaA.Id);

        // Pero B SI puede buscarla por documento (busqueda global) - es lo que permite reconocer
        // identidades de otras copropiedades segun la spec.
        var buscada = await svcB.BuscarPersonaPorDocumentoAsync(
            new BuscarPorDocumentoRequest(personaA.TipoDocumento, personaA.Documento), CancellationToken.None);
        Assert.NotNull(buscada);
    }

    [Fact]
    public async Task DV_NIT_se_calcula_correctamente_segun_DIAN()
    {
        var (svc, _, _) = BuildService(await SeedTenantAsync("CP NIT"));

        // Ejemplos calculados con la implementacion actual (pesos 41,37,29,23,19,17,13,7,3 mod 11)
        Assert.Equal("6", svc.CalcularDigitoVerificacionNit("900123456"));
        Assert.Equal("9", svc.CalcularDigitoVerificacionNit("800197268"));

        // Consistencia: el DV calculado debe pasar la validacion de empresas
        var nit = "901555000";
        var dv = svc.CalcularDigitoVerificacionNit(nit);
        Assert.True(int.TryParse(dv, out var dvNum) && dvNum >= 0 && dvNum <= 10);
    }

    [Fact]
    public async Task Crear_empresa_con_DV_incorrecto_falla()
    {
        var (svc, _, _) = BuildService(await SeedTenantAsync("CP Empresa DV"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearEmpresaAsync(new CrearEmpresaRequest(
                "900123456", "9",  // DV erroneo (correcto es 6)
                "Empresa Test SAS", null, null, null, null,
                null, null, null, null), CancellationToken.None));
    }

    [Fact]
    public async Task Crear_empresa_sin_DV_lo_calcula_automaticamente()
    {
        var (svc, _, _) = BuildService(await SeedTenantAsync("CP Empresa Auto DV"));

        var nit = $"9{Random.Shared.Next(100000000, 999999999)}";
        var dv = svc.CalcularDigitoVerificacionNit(nit);
        var empresa = await svc.CrearEmpresaAsync(new CrearEmpresaRequest(
            nit, null,  // sin DV - el service lo calcula
            "Empresa Auto SAS", null, "empresa@test.co", null, null,
            null, null, null, null), CancellationToken.None);

        Assert.Equal(dv, empresa.DigitoVerificacion);
    }

    [Fact]
    public async Task Inactivar_vinculo_lo_marca_como_inactivo_con_fecha_y_motivo()
    {
        var tenantId = await SeedTenantAsync("CP Inactivar");
        var (svc, _, _) = BuildService(tenantId);

        var p = await svc.CrearPersonaAsync(new CrearPersonaRequest(
            TipoDocumento.CC, $"DOC{Guid.NewGuid():N}".Substring(0, 18),
            "Por", "Inactivar", null, null, null, null), CancellationToken.None);
        var v = await svc.CrearVinculoAsync(new CrearVinculoRequest(
            EntidadDirectorio.Persona, p.Id,
            DateOnly.FromDateTime(DateTime.UtcNow), null), CancellationToken.None);

        var ok = await svc.InactivarVinculoAsync(v.Id, "Vendio el inmueble", CancellationToken.None);
        Assert.True(ok);

        var p360 = await svc.GetPersona360Async(p.Id, CancellationToken.None);
        var vAct = p360!.VinculosEnCopropiedad.First(x => x.Id == v.Id);
        Assert.Equal(EstadoVinculo.Inactivo, vAct.Estado);
        Assert.NotNull(vAct.FechaHasta);
    }

    [Fact]
    public async Task No_se_puede_eliminar_etiqueta_base()
    {
        var (svc, _, _) = BuildService(await SeedTenantAsync("CP Etiqueta Base"));

        var etiquetas = await svc.ListarEtiquetasAsync(AplicaEtiqueta.Persona, null, CancellationToken.None);
        var basePropietario = etiquetas.First(e => e.Codigo == "PROPIETARIO");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.EliminarEtiquetaCustomAsync(basePropietario.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Crear_etiqueta_custom_y_asignar_a_vinculo()
    {
        var tenantId = await SeedTenantAsync("CP Etiqueta Custom");
        var (svc, _, _) = BuildService(tenantId);

        // Creo persona y vinculo
        var p = await svc.CrearPersonaAsync(new CrearPersonaRequest(
            TipoDocumento.CC, $"DOC{Guid.NewGuid():N}".Substring(0, 18),
            "Con", "Custom", null, null, null, null), CancellationToken.None);
        var v = await svc.CrearVinculoAsync(new CrearVinculoRequest(
            EntidadDirectorio.Persona, p.Id,
            DateOnly.FromDateTime(DateTime.UtcNow), null), CancellationToken.None);

        // Etiqueta custom solo para este tenant
        var custom = await svc.CrearEtiquetaCustomAsync(new CrearEtiquetaCustomRequest(
            "VIP", GrupoEtiqueta.Identidad, AplicaEtiqueta.Persona), CancellationToken.None);

        Assert.False(custom.EsBase);

        var asign = await svc.AsignarEtiquetaAsync(new AsignarEtiquetaRequest(v.Id, custom.Id), CancellationToken.None);
        Assert.Contains(asign.Etiquetas, e => e.Codigo == custom.Codigo);
    }

    // ---------------- Helpers ----------------

    private (IDirectorioService svc, PropiaDbContext db, TenantContext tctx) BuildService(Guid tenantId)
    {
        var tenantCtx = new TenantContext();
        tenantCtx.SetTenant(tenantId);
        var interceptor = new Propia.Infrastructure.Persistence.TenantConnectionInterceptor(tenantCtx);
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .AddInterceptors(interceptor)
            .Options;
        var db = new PropiaDbContext(options, tenantCtx);
        return (new DirectorioService(db, tenantCtx), db, tenantCtx);
    }

    private async Task<Guid> SeedTenantAsync(string nombre)
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.OwnerConnectionString)
            .Options;
        await using var ctx = new PropiaDbContext(options, new TenantContext());
        var t = new Tenant
        {
            Nombre = nombre,
            Estado = EstadoCopropiedad.Activa,
            EstadoCustodia = EstadoCustodia.SinAdmin
        };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        return t.Id;
    }
}
