using Microsoft.EntityFrameworkCore;
using Propia.Application.Documents;
using Propia.Application.Plantillas;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Documents;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Plantillas;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Encabezados y pies de pagina (membretes_documento, RLS): la copropiedad nace con "Institucional" (encabezado y pie
/// predeterminados, con su config de membrete absorbida); un solo predeterminado por tipo; el predeterminado no se
/// elimina; al eliminar uno fijado sus plantillas pasan a Predeterminado; la plantilla elige pred / ninguno / uno
/// concreto; el documento se compone con el membrete elegido y {documento.datos}; nada cruza entre copropiedades.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class MembretesFlowTests
{
    private readonly PostgresFixture _fx;

    public MembretesFlowTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Nace_con_Institucional_y_un_predeterminado_por_tipo()
    {
        var t = await SeedTenantAsync("[MB] semilla", t => { t.MembreteLineaLegal = "Linea legal propia"; t.MembreteColorAcento = "#123456"; t.TelefonoContacto = "604 1234"; });
        var (mb, _) = Build(t);
        var ct = CancellationToken.None;

        var lista = await mb.ListarAsync(ct);
        Assert.Equal(2, lista.Items.Count);
        Assert.All(lista.Items, m => { Assert.Equal(MembretesService.NombreSemilla, m.Nombre); Assert.True(m.Predeterminado); });
        var enc = lista.Items.Single(m => m.Tipo == TipoMembrete.Encabezado);
        var pie = lista.Items.Single(m => m.Tipo == TipoMembrete.Pie);
        Assert.Contains(MembreteTokens.DocumentoDatos, enc.Html);     // tipo, radicado y fecha del documento
        Assert.Contains("#123456", enc.Html);                          // color de acento absorbido
        Assert.Contains("Linea legal propia", pie.Html);               // linea legal absorbida
        Assert.Contains("{copropiedad.telefono}", pie.Html);           // contacto con datos
        Assert.Equal(2, (await mb.ListarAsync(ct)).Items.Count);       // idempotente

        // Marcar otro como predeterminado le quita la marca al anterior.
        var nav = await mb.CrearAsync(new GuardarMembreteRequest(TipoMembrete.Encabezado, "Navidad", "<p>Feliz Navidad {copropiedad.nombre}</p>", true), ct);
        Assert.True(nav.Predeterminado);
        lista = await mb.ListarAsync(ct);
        Assert.Single(lista.Items, m => m.Tipo == TipoMembrete.Encabezado && m.Predeterminado);
        Assert.False(lista.Items.Single(m => m.Id == enc.Id).Predeterminado);

        // El predeterminado no se elimina; el que no lo es, si.
        await Assert.ThrowsAsync<InvalidOperationException>(() => mb.EliminarAsync(nav.Id, ct));
        Assert.True(await mb.EliminarAsync(enc.Id, ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => mb.CrearAsync(new GuardarMembreteRequest(TipoMembrete.Pie, "  ", "<p>x</p>"), ct));

        await CleanupTenantAsync(t);
    }

    [Fact]
    public async Task La_plantilla_elige_y_al_eliminar_el_fijo_vuelve_al_predeterminado()
    {
        var t = await SeedTenantAsync("[MB] eleccion");
        var (mb, plt) = Build(t);
        var ct = CancellationToken.None;

        var nav = await mb.CrearAsync(new GuardarMembreteRequest(TipoMembrete.Encabezado, "Navidad", "<p>Navidad</p>"), ct);
        var p = await plt.CrearAsync(new GuardarPlantillaDocumentoRequest("Saludo", "comunicaciones", "<p>Hola</p>", Encabezado: nav.Id.ToString(), Pie: MembreteEleccion.Ninguno), ct);
        Assert.Equal(nav.Id.ToString(), p.Encabezado);
        Assert.Equal(MembreteEleccion.Ninguno, p.Pie);
        Assert.Equal(1, (await mb.ListarAsync(ct)).Items.Single(m => m.Id == nav.Id).Usos);

        // Un Id que no existe (o del otro tipo) se rechaza; el PUT sin Encabezado/Pie no los toca.
        await Assert.ThrowsAsync<InvalidOperationException>(() => plt.ActualizarAsync(p.Id, new GuardarPlantillaDocumentoRequest(null, null, null, Pie: nav.Id.ToString()), ct));
        var r = await plt.ActualizarAsync(p.Id, new GuardarPlantillaDocumentoRequest("Saludo 2", null, null), ct);
        Assert.Equal(nav.Id.ToString(), r!.Encabezado);

        // Documento: encabezado fijo, sin pie.
        var (encHtml, pieHtml) = await mb.ParaDocumentoAsync(p.Id, null, ct);
        Assert.Equal("<p>Navidad</p>", encHtml);
        Assert.Null(pieHtml);

        Assert.True(await mb.EliminarAsync(nav.Id, ct));
        var tras = await plt.ObtenerAsync(p.Id, ct);
        Assert.Equal(MembreteEleccion.Predeterminado, tras!.Encabezado);
        Assert.Equal(MembreteEleccion.Ninguno, tras.Pie);

        await CleanupTenantAsync(t);
    }

    [Fact]
    public async Task El_documento_se_compone_con_los_predeterminados_y_los_datos_de_la_copropiedad()
    {
        var t = await SeedTenantAsync("[MB] documento", t => { t.Nit = "900123456"; t.DigitoVerificacion = "7"; t.Ciudad = "Medellín"; t.EmailContacto = "admin@copro.co"; });
        var (mb, _) = Build(t);
        var ct = CancellationToken.None;

        var (enc, pie) = await mb.ParaDocumentoAsync(null, new Dictionary<string, string> { ["administrador.nombre"] = "Ana Gómez" }, ct);
        Assert.NotNull(enc);
        Assert.Contains("[MB] documento", enc);                        // {copropiedad.nombre}
        Assert.Contains("900123456-7", enc);                           // {copropiedad.nit} con DV
        Assert.Contains(MembreteTokens.DocumentoDatos, enc);           // lo pone el armador
        Assert.Contains("admin@copro.co", pie);                        // {copropiedad.correo}

        Tenant tenant;
        await using (var ctx = OwnerDb()) tenant = await ctx.Tenants.AsNoTracking().SingleAsync(x => x.Id == t);
        var html = new MembreteDocumentBuilder().Construir(tenant,
            new MembreteDocContenido("Respuesta PQRSD", "Radicado", "PQRSD-2026-0001", DateTimeOffset.Now, "<p>Cuerpo</p>"), enc, pie);
        Assert.DoesNotContain(MembreteTokens.DocumentoDatos, html);
        Assert.Contains("PQRSD-2026-0001", html);
        Assert.Contains("<p>Cuerpo</p>", html);
        Assert.Contains("Operado por A&amp;D GROUP", html);

        // Sin encabezado: los datos del documento igual aparecen (arriba, a la derecha).
        var sinEnc = new MembreteDocumentBuilder().Construir(tenant,
            new MembreteDocContenido("Certificado", "Consecutivo", "C-9", DateTimeOffset.Now, "<p>x</p>"), null, null);
        Assert.Contains("C-9", sinEnc);

        await CleanupTenantAsync(t);
    }

    [Fact]
    public async Task Los_membretes_no_se_ven_ni_se_tocan_entre_copropiedades()
    {
        var tA = await SeedTenantAsync("[MB] ISO A");
        var tB = await SeedTenantAsync("[MB] ISO B");
        var (mbA, _) = Build(tA);
        var (mbB, pltB) = Build(tB);
        var ct = CancellationToken.None;

        var deA = await mbA.CrearAsync(new GuardarMembreteRequest(TipoMembrete.Pie, "Solo de A", "<p>A</p>"), ct);
        var listaB = await mbB.ListarAsync(ct);
        Assert.DoesNotContain(listaB.Items, m => m.Id == deA.Id);
        Assert.Equal(2, listaB.Items.Count);                            // B nace con su propio Institucional
        Assert.Null(await mbB.ActualizarAsync(deA.Id, new GuardarMembreteRequest(null, "Hack", null), ct));
        Assert.False(await mbB.EliminarAsync(deA.Id, ct));
        // B no puede fijar en su plantilla un membrete de A.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            pltB.CrearAsync(new GuardarPlantillaDocumentoRequest("x", "pqrsd", "<p>x</p>", Pie: deA.Id.ToString()), ct));
        Assert.Equal("Solo de A", (await mbA.ListarAsync(ct)).Items.Single(m => m.Id == deA.Id).Nombre);

        await CleanupTenantAsync(tA);
        await CleanupTenantAsync(tB);
    }

    // ----------------------------- infraestructura -----------------------------

    private (IMembretesService Membretes, IPlantillasDocumentoService Plantillas) Build(Guid tenantId)
    {
        var tenantCtx = new TenantContext();
        tenantCtx.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .AddInterceptors(new TenantConnectionInterceptor(tenantCtx))
            .Options;
        var db = new PropiaDbContext(options, tenantCtx);
        return (new MembretesService(db, tenantCtx), new PlantillasDocumentoService(db, tenantCtx));
    }

    private PropiaDbContext OwnerDb()
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        return new PropiaDbContext(options, new TenantContext());
    }

    private async Task<Guid> SeedTenantAsync(string nombre, Action<Tenant>? config = null)
    {
        await using var ctx = OwnerDb();
        var t = new Tenant { Nombre = nombre, Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.SinAdmin };
        config?.Invoke(t);
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        return t.Id;
    }

    private async Task CleanupTenantAsync(Guid tenantId)
    {
        await using var ctx = OwnerDb();
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM membretes_documento WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM plantillas_documento WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }
}
