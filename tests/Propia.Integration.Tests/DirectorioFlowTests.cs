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
    public async Task Catalogo_base_de_etiquetas_existe_en_lista_global()
    {
        // Las etiquetas del Directorio son ahora una lista del catalogo global (directorio.etiqueta),
        // sembrada por CatalogoListasSeeder (8 semillas). Editable por A&D; sin color/icono/grupo.
        var (_, db, _) = BuildService(await SeedTenantAsync("CP Etiquetas"));
        await Propia.Infrastructure.Catalogos.CatalogoListasSeeder.SembrarAsync(db);

        var labels = await db.CatalogoOpciones
            .Where(o => o.Lista == "directorio.etiqueta")
            .Select(o => o.Label).ToListAsync();

        Assert.Contains("Propietario", labels);
        Assert.Contains("Residente", labels);
        Assert.Contains("Contratista", labels);
        Assert.Contains("Proveedor", labels);
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

        // Etiqueta de texto (valor de la lista directorio.etiqueta).
        var vinculo = await svc.CrearVinculoAsync(new CrearVinculoRequest(
            EntidadDirectorio.Persona, persona.Id,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new[] { "Propietario" }), CancellationToken.None);

        Assert.Equal(EstadoVinculo.Activo, vinculo.Estado);
        Assert.Contains(vinculo.Etiquetas, e => e.Valor == "Propietario");

        // Aparece en bandeja del tenant
        var bandeja = await svc.ListarPersonasDelTenantAsync(null, CancellationToken.None);
        Assert.Contains(bandeja, p => p.Id == persona.Id);

        // Aparece en 360
        var p360 = await svc.GetPersona360Async(persona.Id, CancellationToken.None);
        Assert.NotNull(p360);
        Assert.Single(p360!.VinculosEnCopropiedad);
        Assert.Contains(p360.VinculosEnCopropiedad[0].Etiquetas, e => e.Valor == "Propietario");
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
        // NIT reales (DIAN 800197268-4, Bancolombia 890903938-8, Ecopetrol 899999068-1, Davivienda 860034313-7).
        Assert.Equal("4", svc.CalcularDigitoVerificacionNit("800197268"));
        Assert.Equal("8", svc.CalcularDigitoVerificacionNit("890903938"));
        Assert.Equal("1", svc.CalcularDigitoVerificacionNit("899999068"));
        Assert.Equal("7", svc.CalcularDigitoVerificacionNit("860.034.313"));

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
    public async Task Asignar_etiqueta_texto_a_vinculo_y_dedupe_acento_insensible()
    {
        var tenantId = await SeedTenantAsync("CP Etiqueta Texto");
        var (svc, _, _) = BuildService(tenantId);

        // Creo persona y vinculo
        var p = await svc.CrearPersonaAsync(new CrearPersonaRequest(
            TipoDocumento.CC, $"DOC{Guid.NewGuid():N}".Substring(0, 18),
            "Con", "Etiqueta", null, null, null, null), CancellationToken.None);
        var v = await svc.CrearVinculoAsync(new CrearVinculoRequest(
            EntidadDirectorio.Persona, p.Id,
            DateOnly.FromDateTime(DateTime.UtcNow), null), CancellationToken.None);

        // Etiqueta de texto (valor custom del tenant).
        var asign = await svc.AsignarEtiquetaAsync(new AsignarEtiquetaRequest(v.Id, "VIP"), CancellationToken.None);
        Assert.Contains(asign.Etiquetas, e => e.Valor == "VIP");

        // Re-asignar la misma etiqueta (variante de acento/mayusculas) falla: dedupe normalizado.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AsignarEtiquetaAsync(new AsignarEtiquetaRequest(v.Id, "vip"), CancellationToken.None));
    }

    // ---------------- Crear terceros con IA (RUT / Camara de Comercio), 2026-11-14 ----------------

    [Fact]
    public async Task Crear_tercero_desde_documento_crea_completa_sin_pisar_y_asocia_el_documento()
    {
        var tenantId = await SeedTenantAsync("Terceros IA");
        var (dir, db, tctx) = BuildService(tenantId);
        var ia = new Propia.Infrastructure.Directorio.TercerosIaService(dir, db, tctx);
        var nit = Random.Shared.Next(800_000_000, 999_999_999).ToString();
        var cedulaRep = Random.Shared.Next(10_000_000, 99_999_999).ToString();
        Stream Pdf() => new MemoryStream(new byte[] { 1, 2, 3 });

        // 1) RUT de empresa nueva: se crea, con su representante legal (en mayusculas -> nombre propio) y el RUT en Documentos.
        var dvOk = dir.CalcularDigitoVerificacionNit(nit);
        var rut = new TerceroLeidoRequest("rut", "juridica", nit + "-" + dvOk, "ASEO TOTAL S.A.S.", "Aseo Total", Direccion: "Calle 10 # 5-20", Ciudad: "CALI",
            Email: "contacto@aseototal.test", RegimenTributario: "Responsable de IVA",
            RepresentanteNombres: "JUAN CARLOS", RepresentanteApellidos: "PEREZ GOMEZ", RepresentanteTipoIdentificacion: "CC", RepresentanteIdentificacion: cedulaRep);
        var r1 = await ia.CrearDesdeDocumentoAsync(rut, "rut.pdf", "application/pdf", 3, Pdf(), default);
        Assert.True(r1.Creado);
        Assert.Equal(EntidadDirectorio.Empresa, r1.Tipo);
        Assert.Equal("RUT - rut.pdf", r1.DocumentoAsociado);
        Assert.Equal("Juan Carlos Perez Gomez", r1.RepresentanteLegal);
        var e1 = (await dir.ObtenerEmpresaAsync(r1.Id, default))!;
        Assert.Equal((nit, dvOk, "Calle 10 # 5-20, Cali"), (e1.Nit, e1.DigitoVerificacion, e1.Direccion));
        Assert.NotNull(e1.RepresentanteLegalPersonaId);

        // 2) Camara de Comercio de la MISMA empresa con otro correo: no se duplica ni se pisa lo que ya tenia; completa lo vacio.
        var camara = new TerceroLeidoRequest("camara_comercio", "juridica", nit, "ASEO TOTAL SAS", Email: "otro@correo.test", Telefono: "6025550000");
        var r2 = await ia.CrearDesdeDocumentoAsync(camara, "camara.pdf", "application/pdf", 3, Pdf(), default);
        Assert.False(r2.Creado);
        Assert.Equal(r1.Id, r2.Id);
        Assert.Equal("Cámara de Comercio - camara.pdf", r2.DocumentoAsociado);
        var e2 = (await dir.ObtenerEmpresaAsync(r1.Id, default))!;
        Assert.Equal(("contacto@aseototal.test", "6025550000"), (e2.Email, e2.Telefono));
        Assert.Equal(2, (await dir.ListarAdjuntosAsync(EntidadDirectorio.Empresa, r1.Id, default)).Count);

        // 3) RUT de persona natural (el NIT es la cedula).
        var cc = Random.Shared.Next(10_000_000, 99_999_999).ToString();
        var r3 = await ia.CrearDesdeDocumentoAsync(new TerceroLeidoRequest("rut", "natural", TipoIdentificacion: "CC", NumeroIdentificacion: cc,
            Nombres: "MARIA", Apellidos: "LOPEZ"), "rut-maria.pdf", "application/pdf", 3, Pdf(), default);
        Assert.Equal((EntidadDirectorio.Persona, true, "Maria Lopez"), (r3.Tipo, r3.Creado, r3.Nombre));

        // 3b) Certificacion bancaria del titular (la misma empresa, sin DV): no duplica, guarda la cuenta en el nombre del documento.
        var cert = new TerceroLeidoRequest("certificacion_bancaria", "juridica", nit, "Aseo Total SAS", Banco: "BANCOLOMBIA", TipoCuenta: "AHORROS",
            NumeroCuenta: "123-456789-01");
        var r4 = await ia.CrearDesdeDocumentoAsync(cert, "cert.pdf", "application/pdf", 3, Pdf(), default);
        Assert.Equal((r1.Id, false), (r4.Id, r4.Creado));
        Assert.Equal("Certificación bancaria · Bancolombia · Ahorros No. 123-456789-01 - cert.pdf", r4.DocumentoAsociado);
        Assert.Equal(3, (await dir.ListarAdjuntosAsync(EntidadDirectorio.Empresa, r1.Id, default)).Count);

        // 4) Sin NIT ni documento: error claro, no crea nada.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ia.CrearDesdeDocumentoAsync(new TerceroLeidoRequest("rut", "juridica", RazonSocial: "Sin Nit SAS"), "x.pdf", "application/pdf", 3, Pdf(), default));
        Assert.Contains("NIT", ex.Message);

        // 5) NIT con digito de verificacion que no cuadra (IA leyo mal un digito): no crea, avisa para revisar.
        var otroNit = Random.Shared.Next(800_000_000, 999_999_999).ToString();
        var dvMalo = ((int.Parse(dir.CalcularDigitoVerificacionNit(otroNit)) + 1) % 10).ToString();
        var exDv = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ia.CrearDesdeDocumentoAsync(new TerceroLeidoRequest("rut", "juridica", otroNit + "-" + dvMalo, "Mal Leida SAS"), "y.pdf", "application/pdf", 3, Pdf(), default));
        Assert.Contains("dígito de verificación", exDv.Message);
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
        return (new DirectorioService(db, tenantCtx, new NoopBlobStorage()), db, tenantCtx);
    }

    private sealed class NoopBlobStorage : Propia.Infrastructure.Storage.IBlobStorage
    {
        public Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct) => Task.FromResult($"/mem/{key}");
        public Task DeleteAsync(string key, CancellationToken ct) => Task.CompletedTask;
        public string GetPublicUrl(string key) => $"/mem/{key}";
        public string? ResolveUrl(string? storedValueOrKey) => storedValueOrKey;
        public Task<byte[]?> DownloadAsync(string key, CancellationToken ct) => Task.FromResult<byte[]?>(null);
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
