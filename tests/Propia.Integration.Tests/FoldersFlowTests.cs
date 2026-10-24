using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Common;
using Propia.Application.Documentos;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Documentos;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Modulo 2.15 Documentos - vista Folders (v2). Cubre:
///  - Siembra base + tarjetas con totales recursivos.
///  - Arbol de profundidad arbitraria: crear, renombrar, mover (sin ciclos), eliminar solo vacio.
///  - Documentos: cargar, versionar (historial intacto), archivar/restaurar (nunca borrar), duplicar.
///  - Metadatos: herencia por cadena de folders, obligatorios que bloquean, validacion por tipo.
///  - ZIP recursivo que respeta la jerarquia.
///  - Aislamiento por tenant (RLS + HasQueryFilter) con el rol propia_app.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class FoldersFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;
    private readonly Guid _userId = Guid.NewGuid();

    public FoldersFlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        // Scoped (no singleton): el test de aislamiento abre dos scopes con tenants distintos a la vez.
        sc.AddScoped<ITenantContext, TenantContext>();
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("user_id", _userId.ToString()) }, "test"));
        sc.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = http });
        sc.AddSingleton<IBlobStorage, InMemoryBlobStorage>();
        sc.AddScoped<TenantConnectionInterceptor>();
        sc.AddDbContext<PropiaDbContext>((sp, opts) =>
            opts.UseNpgsql(_fx.AppConnectionString)
                .AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>()));
        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =======================================================================

    [Fact]
    public async Task Siembra_base_y_tarjetas_cuentan_recursivo()
    {
        var t = await SeedTenantAsync("[FOLDER] seed");
        var (svc, _, scope) = Build(t);
        using var _ = scope;

        var arbol = await svc.ArbolAsync(CancellationToken.None);
        Assert.Equal(4, arbol.Count);
        Assert.Equal(8, arbol.Sum(r => r.Hijos.Count));
        Assert.All(arbol, r => Assert.Equal(3, r.Campos.Count)); // campos base en la raiz

        var tarjetas = await svc.TarjetasAsync(CancellationToken.None);
        Assert.Equal(4, tarjetas.Count);
        var actas = tarjetas.Single(x => x.Nombre == "Actas y gobierno");
        Assert.Equal(2, actas.Subfolders);
        Assert.Equal(7, actas.Total);                 // 4 + 3 casillas
        Assert.Equal(0, actas.Cargados);
        Assert.Equal(4, actas.PendientesObligatorios); // 2 obligatorias por subfolder

        await CleanTenantAsync(t);
    }

    [Fact]
    public async Task Arbol_profundidad_arbitraria_crear_renombrar_mover_eliminar()
    {
        var t = await SeedTenantAsync("[FOLDER] arbol");
        var (svc, db, scope) = Build(t);
        using var _ = scope;

        var raiz = await svc.CrearFolderAsync(new CrearFolderRequest("[F] Raíz"), CancellationToken.None);
        var n1 = await svc.CrearFolderAsync(new CrearFolderRequest("[F] Nivel 1", raiz.Id), CancellationToken.None);
        var n2 = await svc.CrearFolderAsync(new CrearFolderRequest("[F] Nivel 2", n1.Id), CancellationToken.None);
        var n3 = await svc.CrearFolderAsync(new CrearFolderRequest("[F] Nivel 3", n2.Id), CancellationToken.None);
        Assert.Null(raiz.Codigo);
        Assert.NotNull(n1.Codigo); // FRA-001 (prefijo del folder raiz)

        var det3 = await svc.GetFolderAsync(n3.Id, CancellationToken.None);
        Assert.NotNull(det3);
        Assert.Equal(4, det3!.Ruta.Count);
        Assert.Equal(new[] { raiz.Id, n1.Id, n2.Id, n3.Id }, det3.Ruta.Select(r => r.Id).ToArray());

        // Renombrar (merge: solo nombre)
        Assert.True(await svc.ActualizarFolderAsync(n3.Id, new ActualizarFolderRequest(Nombre: "[F] Nivel 3 renombrado"), CancellationToken.None));
        Assert.Equal("[F] Nivel 3 renombrado", (await svc.GetFolderAsync(n3.Id, CancellationToken.None))!.Nombre);

        // Mover n3 directo bajo la raiz
        Assert.True(await svc.ActualizarFolderAsync(n3.Id, new ActualizarFolderRequest(ParentId: raiz.Id), CancellationToken.None));
        Assert.Equal(2, (await svc.GetFolderAsync(n3.Id, CancellationToken.None))!.Ruta.Count);

        // Ciclo prohibido: mover la raiz dentro de su nieto
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ActualizarFolderAsync(raiz.Id, new ActualizarFolderRequest(ParentId: n2.Id), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ActualizarFolderAsync(n1.Id, new ActualizarFolderRequest(ParentId: n1.Id), CancellationToken.None));

        // Eliminar: con archivo dentro -> rechaza; vacio -> borra en cascada (hojas primero)
        await svc.CargarDocumentoAsync(n2.Id, Archivo("acta.pdf", "contenido"), CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.EliminarFolderAsync(n1.Id, CancellationToken.None));
        await svc.CrearDocumentoAsync(n3.Id, new CrearDocumentoRequest("[F] casilla vacía"), CancellationToken.None);
        Assert.True(await svc.EliminarFolderAsync(n3.Id, CancellationToken.None));
        Assert.Null(await svc.GetFolderAsync(n3.Id, CancellationToken.None));
        Assert.False(await db.FolderDocumentos.AnyAsync(d => d.FolderId == n3.Id));

        await CleanTenantAsync(t);
    }

    [Fact]
    public async Task Documento_cargar_versionar_archivar_restaurar_duplicar()
    {
        var t = await SeedTenantAsync("[FOLDER] docs");
        var (svc, db, scope) = Build(t);
        using var _ = scope;

        var f = await svc.CrearFolderAsync(new CrearFolderRequest("[F] Docs"), CancellationToken.None);
        var doc = await svc.CargarDocumentoAsync(f.Id, Archivo("balance.pdf", "v1"), CancellationToken.None);
        Assert.True(doc.Cargado);
        Assert.Equal("balance", doc.Nombre);
        Assert.Equal(1, doc.NumeroVersiones);

        // Reemplazar = version nueva; la v1 sigue descargable con su contenido
        var doc2 = await svc.CargarVersionAsync(doc.Id, new CargarVersionRequest("balance-v2.pdf", "application/pdf", 2, B64("v2"), NotasCambio: "Corrección"), CancellationToken.None);
        Assert.Equal(2, doc2!.NumeroVersiones);
        var versiones = await svc.ListarVersionesAsync(doc.Id, CancellationToken.None);
        Assert.Equal(new[] { 2, 1 }, versiones.Select(v => v.Numero).ToArray());
        Assert.Equal(_userId, versiones[0].SubidoPorUsuarioId);
        var v1 = await svc.DescargarVersionAsync(doc.Id, versiones[1].Id, CancellationToken.None);
        Assert.Equal("v1", Encoding.UTF8.GetString(Convert.FromBase64String(v1!.ContenidoBase64)));
        var actual = await svc.DescargarDocumentoAsync(doc.Id, CancellationToken.None);
        Assert.Equal("v2", Encoding.UTF8.GetString(Convert.FromBase64String(actual!.ContenidoBase64)));
        Assert.Equal("balance-v2.pdf", actual.NombreArchivo);

        // Con archivo NO se elimina: se archiva y se restaura
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.EliminarDocumentoAsync(doc.Id, CancellationToken.None));
        Assert.True(await svc.ArchivarDocumentoAsync(doc.Id, CancellationToken.None));
        var det = await svc.GetFolderAsync(f.Id, CancellationToken.None);
        Assert.Empty(det!.Documentos);
        Assert.Single(det.Archivados);
        Assert.Equal(2, (await svc.ListarVersionesAsync(doc.Id, CancellationToken.None)).Count); // historial intacto
        Assert.True(await svc.RestaurarDocumentoAsync(doc.Id, CancellationToken.None));
        det = await svc.GetFolderAsync(f.Id, CancellationToken.None);
        Assert.Single(det!.Documentos);
        Assert.Empty(det.Archivados);

        // Duplicar: copia con metadatos propios + valores, archivo actual como v1
        var campo = await svc.CrearCampoAsync(new CrearFolderCampoRequest(null, doc.Id, "[F] Folios", TipoCampoTablero.Numero), CancellationToken.None);
        await svc.GuardarValoresAsync(doc.Id, new GuardarValoresRequest(new[] { new CampoValorRequest(campo.Id, "12") }), CancellationToken.None);
        var copia = await svc.DuplicarDocumentoAsync(doc.Id, CancellationToken.None);
        Assert.NotNull(copia);
        Assert.Equal("balance (copia)", copia!.Nombre);
        Assert.Equal(1, copia.NumeroVersiones);
        Assert.Single(copia.CamposPropios);
        Assert.NotEqual(campo.Id, copia.CamposPropios[0].Id);
        Assert.Equal("12", copia.Valores.Single().Valor);
        var copiaBytes = await svc.DescargarDocumentoAsync(copia.Id, CancellationToken.None);
        Assert.Equal("v2", Encoding.UTF8.GetString(Convert.FromBase64String(copiaBytes!.ContenidoBase64)));

        // Casilla vacia si se puede eliminar; archivo no permitido se rechaza; >20MB se rechaza
        var casilla = await svc.CrearDocumentoAsync(f.Id, new CrearDocumentoRequest("[F] Casilla"), CancellationToken.None);
        Assert.True(await svc.EliminarDocumentoAsync(casilla.Id, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CargarDocumentoAsync(f.Id, Archivo("virus.exe", "x"), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CargarDocumentoAsync(f.Id, new CargarDocumentoRequest("grande.pdf", "application/pdf", 21L * 1024 * 1024, B64("x")), CancellationToken.None));

        Assert.Equal(3, await db.FolderDocumentoVersiones.CountAsync());
        await CleanTenantAsync(t);
    }

    [Fact]
    public async Task Metadatos_heredados_y_obligatorios_bloquean()
    {
        var t = await SeedTenantAsync("[FOLDER] meta");
        var (svc, _, scope) = Build(t);
        using var _ = scope;

        var raiz = await svc.CrearFolderAsync(new CrearFolderRequest("[F] Contratos"), CancellationToken.None);
        var sub = await svc.CrearFolderAsync(new CrearFolderRequest("[F] Aseo", raiz.Id), CancellationToken.None);
        var hoja = await svc.CrearFolderAsync(new CrearFolderRequest("[F] 2026", sub.Id), CancellationToken.None);

        var fecha = await svc.CrearCampoAsync(new CrearFolderCampoRequest(raiz.Id, null, "[F] Fecha de firma", TipoCampoTablero.Fecha, Obligatorio: true,
            Descripcion: "Fecha en que se firmó el contrato; la IA la busca junto a las firmas."), CancellationToken.None);
        var valor = await svc.CrearCampoAsync(new CrearFolderCampoRequest(sub.Id, null, "[F] Valor mensual", TipoCampoTablero.Moneda), CancellationToken.None);
        var estado = await svc.CrearCampoAsync(new CrearFolderCampoRequest(hoja.Id, null, "[F] Estado", TipoCampoTablero.Seleccion, Opciones: "Vigente\nVencido"), CancellationToken.None);

        // Sin el obligatorio heredado de la raiz -> bloquea y NO deja casilla huerfana en el folder.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CargarDocumentoAsync(hoja.Id, Archivo("contrato.pdf", "c"), CancellationToken.None));
        Assert.Contains("[F] Fecha de firma", ex.Message);
        Assert.Empty((await svc.GetFolderAsync(hoja.Id, CancellationToken.None))!.Documentos);

        // Con el obligatorio -> ok; el esquema efectivo trae los 3 con su origen
        var doc = await svc.CargarDocumentoAsync(hoja.Id, Archivo("contrato.pdf", "c") with
        {
            Valores = new[] { new CampoValorRequest(fecha.Id, "2026-03-15"), new CampoValorRequest(valor.Id, "1.500.000") }
        }, CancellationToken.None);
        Assert.True(doc.Cargado);
        var efectivos = await svc.CamposEfectivosAsync(doc.Id, CancellationToken.None);
        Assert.Equal(3, efectivos.Count);
        Assert.Equal("[F] Contratos", efectivos[0].HeredadoDe);
        Assert.Equal("[F] Aseo", efectivos[1].HeredadoDe);
        Assert.Equal("[F] 2026", efectivos[2].HeredadoDe);

        // Validacion por tipo: numero invalido y opcion fuera de la lista
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GuardarValoresAsync(doc.Id, new GuardarValoresRequest(new[] { new CampoValorRequest(valor.Id, "mucho") }), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GuardarValoresAsync(doc.Id, new GuardarValoresRequest(new[] { new CampoValorRequest(estado.Id, "Perdido") }), CancellationToken.None));
        Assert.True(await svc.GuardarValoresAsync(doc.Id, new GuardarValoresRequest(new[] { new CampoValorRequest(estado.Id, "Vigente") }), CancellationToken.None));

        // Merge: el valor previo de fecha se conserva; vaciar el obligatorio bloquea
        var d = await svc.GetDocumentoAsync(doc.Id, CancellationToken.None);
        Assert.Equal("2026-03-15", d!.Valores.Single(v => v.DefinicionId == fecha.Id).Valor);
        Assert.Equal("Vigente", d.Valores.Single(v => v.DefinicionId == estado.Id).Valor);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GuardarValoresAsync(doc.Id, new GuardarValoresRequest(new[] { new CampoValorRequest(fecha.Id, "") }), CancellationToken.None));

        // Editar definicion (merge) y eliminar: el valor cae en cascada
        Assert.True(await svc.ActualizarCampoAsync(estado.Id, new ActualizarFolderCampoRequest(Descripcion: "Estado del contrato"), CancellationToken.None));
        Assert.True(await svc.EliminarCampoAsync(estado.Id, CancellationToken.None));
        d = await svc.GetDocumentoAsync(doc.Id, CancellationToken.None);
        Assert.DoesNotContain(d!.Valores, v => v.DefinicionId == estado.Id);

        // Un metadato debe tener dueno (folder XOR documento)
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CrearCampoAsync(new CrearFolderCampoRequest(raiz.Id, doc.Id, "[F] Ambos"), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CrearCampoAsync(new CrearFolderCampoRequest(null, null, "[F] Ninguno"), CancellationToken.None));

        await CleanTenantAsync(t);
    }

    [Fact]
    public async Task Zip_recursivo_respeta_jerarquia()
    {
        var t = await SeedTenantAsync("[FOLDER] zip");
        var (svc, _, scope) = Build(t);
        using var _ = scope;

        var raiz = await svc.CrearFolderAsync(new CrearFolderRequest("[F] Asamblea 2026"), CancellationToken.None);
        var sub = await svc.CrearFolderAsync(new CrearFolderRequest("[F] Anexos", raiz.Id), CancellationToken.None);
        var subsub = await svc.CrearFolderAsync(new CrearFolderRequest("[F] Poderes", sub.Id), CancellationToken.None);
        await svc.CargarDocumentoAsync(raiz.Id, Archivo("acta.pdf", "acta"), CancellationToken.None);
        await svc.CargarDocumentoAsync(sub.Id, Archivo("lista.xlsx", "lista"), CancellationToken.None);
        await svc.CargarDocumentoAsync(subsub.Id, Archivo("poder.pdf", "p1"), CancellationToken.None);
        await svc.CargarDocumentoAsync(subsub.Id, Archivo("poder.pdf", "p2"), CancellationToken.None); // nombre repetido
        var arch = await svc.CargarDocumentoAsync(raiz.Id, Archivo("viejo.pdf", "viejo"), CancellationToken.None);
        await svc.ArchivarDocumentoAsync(arch.Id, CancellationToken.None);
        await svc.CrearDocumentoAsync(raiz.Id, new CrearDocumentoRequest("[F] sin archivo"), CancellationToken.None);

        var zip = await svc.DescargarZipAsync(raiz.Id, CancellationToken.None);
        Assert.NotNull(zip);
        Assert.Equal(5, zip!.Archivos);
        using var za = new ZipArchive(new MemoryStream(zip.Contenido), ZipArchiveMode.Read);
        var nombres = za.Entries.Select(e => e.FullName).OrderBy(x => x).ToList();
        Assert.Contains("[F] Asamblea 2026/acta.pdf", nombres);
        Assert.Contains("[F] Asamblea 2026/[F] Anexos/lista.xlsx", nombres);
        Assert.Contains("[F] Asamblea 2026/[F] Anexos/[F] Poderes/poder.pdf", nombres);
        Assert.Contains("[F] Asamblea 2026/[F] Anexos/[F] Poderes/poder (2).pdf", nombres);
        Assert.Contains("[F] Asamblea 2026/Archivados/viejo.pdf", nombres);
        using var sr = new StreamReader(za.GetEntry("[F] Asamblea 2026/acta.pdf")!.Open());
        Assert.Equal("acta", await sr.ReadToEndAsync());

        // ZIP de un subfolder: solo su rama
        var zipSub = await svc.DescargarZipAsync(sub.Id, CancellationToken.None);
        Assert.Equal(3, zipSub!.Archivos);

        await CleanTenantAsync(t);
    }

    [Fact]
    public async Task Aislamiento_por_tenant()
    {
        var tA = await SeedTenantAsync("[FOLDER] iso A");
        var tB = await SeedTenantAsync("[FOLDER] iso B");
        var (svcA, _, scopeA) = Build(tA);
        var (svcB, dbB, scopeB) = Build(tB);
        using var _a = scopeA;
        using var _b = scopeB;

        var fA = await svcA.CrearFolderAsync(new CrearFolderRequest("[F] Solo A"), CancellationToken.None);
        var dA = await svcA.CargarDocumentoAsync(fA.Id, Archivo("a.pdf", "A"), CancellationToken.None);
        var cA = await svcA.CrearCampoAsync(new CrearFolderCampoRequest(fA.Id, null, "[F] Campo A"), CancellationToken.None);
        await svcB.CrearFolderAsync(new CrearFolderRequest("[F] Solo B"), CancellationToken.None);

        var arbolB = await svcB.ArbolAsync(CancellationToken.None);
        Assert.DoesNotContain(arbolB, f => f.Nombre == "[F] Solo A");
        Assert.Null(await svcB.GetFolderAsync(fA.Id, CancellationToken.None));
        Assert.Null(await svcB.GetDocumentoAsync(dA.Id, CancellationToken.None));
        Assert.Null(await svcB.DescargarDocumentoAsync(dA.Id, CancellationToken.None));
        Assert.Null(await svcB.DescargarZipAsync(fA.Id, CancellationToken.None));
        Assert.Empty(await svcB.ListarVersionesAsync(dA.Id, CancellationToken.None));
        Assert.False(await svcB.ArchivarDocumentoAsync(dA.Id, CancellationToken.None));
        Assert.False(await svcB.EliminarCampoAsync(cA.Id, CancellationToken.None));
        // B no puede colgar un folder del de A ni crear campos sobre el documento de A
        await Assert.ThrowsAsync<InvalidOperationException>(() => svcB.CrearFolderAsync(new CrearFolderRequest("[F] intruso", fA.Id), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svcB.CrearCampoAsync(new CrearFolderCampoRequest(null, dA.Id, "[F] intruso"), CancellationToken.None));
        // RLS con propia_app: ni con IgnoreQueryFilters se ven las filas de A
        Assert.False(await dbB.Folders.IgnoreQueryFilters().AnyAsync(f => f.Id == fA.Id));
        Assert.False(await dbB.FolderDocumentoVersiones.IgnoreQueryFilters().AnyAsync(v => v.DocumentoId == dA.Id));

        await CleanTenantAsync(tA);
        await CleanTenantAsync(tB);
    }

    // =======================================================================
    // Helpers
    // =======================================================================

    private static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));

    private static CargarDocumentoRequest Archivo(string nombre, string contenido)
        => new(nombre, nombre.EndsWith(".pdf") ? "application/pdf" : "application/octet-stream", contenido.Length, B64(contenido));

    private (IFoldersService svc, PropiaDbContext db, IServiceScope scope) Build(Guid tenantId)
    {
        var scope = _services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        ctx.SetTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var http = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var storage = scope.ServiceProvider.GetRequiredService<IBlobStorage>();
        return (new FoldersService(db, ctx, storage, http), db, scope);
    }

    private async Task<Guid> SeedTenantAsync(string nombre)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var t = new Tenant { Nombre = nombre, Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.ConAdmin };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        return t.Id;
    }

    private async Task CleanTenantAsync(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM folder_documento_campo_valores WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM folder_campo_definiciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM folder_documento_versiones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM folder_documentos WHERE tenant_id = {tenantId}");
        // Hojas primero (FK padre Restrict).
        for (var i = 0; i < 10; i++)
            await ctx.Database.ExecuteSqlAsync($"DELETE FROM folders f WHERE f.tenant_id = {tenantId} AND NOT EXISTS (SELECT 1 FROM folders h WHERE h.parent_id = f.id)");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }

    private sealed class InMemoryBlobStorage : IBlobStorage
    {
        private readonly Dictionary<string, byte[]> _store = new();

        public Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct)
        {
            using var ms = new MemoryStream();
            content.CopyTo(ms);
            _store[key] = ms.ToArray();
            return Task.FromResult(GetPublicUrl(key));
        }

        public Task DeleteAsync(string key, CancellationToken ct) { _store.Remove(key); return Task.CompletedTask; }
        public string GetPublicUrl(string key) => $"/mem/{key}";
        public string? ResolveUrl(string? storedValueOrKey) => storedValueOrKey;
        public Task<byte[]?> DownloadAsync(string key, CancellationToken ct)
            => Task.FromResult(_store.TryGetValue(key, out var b) ? b : null);
    }
}
