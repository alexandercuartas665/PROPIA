using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
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
/// Tests del modulo 2.15 Documentos y Archivo Digital (spec v1.0 MVP).
/// Cubre:
///  - Seed: 9 categorias base + 7 etiquetas base con es_base=true.
///  - Subida crea documento + v1 con hash SHA-256.
///  - Nueva version incrementa numero, conserva historial (RN-13).
///  - Categoria base no se puede editar/desactivar (RN-12).
///  - Eliminar es soft delete (RN-01) - no remueve fisicamente.
///  - Auditoria append-only - trigger bloquea UPDATE/DELETE (RN-15).
///  - Filtros, busqueda por texto y estadisticas.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class DocumentosFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;
    private Guid _userId;
    private Guid _personaId;

    public DocumentosFlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        _userId = Guid.NewGuid();
        _personaId = Guid.NewGuid();
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddDataProtection();
        sc.AddSingleton<ITenantContext, TenantContext>();
        sc.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = BuildFakeHttpContext(_userId, _personaId) });
        sc.AddSingleton<IBlobStorage, InMemoryBlobStorage>();
        sc.AddScoped<TenantConnectionInterceptor>();
        sc.AddDbContext<PropiaDbContext>((sp, opts) =>
            opts.UseNpgsql(_fx.AppConnectionString)
                .AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>()));
        sc.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<PropiaDbContext>()
            .AddDefaultTokenProviders();
        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =======================================================================
    // Tests
    // =======================================================================

    [Fact]
    public async Task Seed_global_tiene_9_categorias_y_7_etiquetas_base()
    {
        var tenantId = await SeedTenantAsync("Doc Seed");
        await SeedPersonaConApplicationUser();
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        var cats = await svc.ListarCategoriasAsync(CancellationToken.None);
        var basesCats = cats.Where(c => c.EsBase).ToList();
        Assert.True(basesCats.Count >= 9);
        Assert.Contains(basesCats, c => c.Nombre == "Reglamentos y normativa");
        Assert.Contains(basesCats, c => c.Nombre == "Actas y asambleas");
        Assert.Contains(basesCats, c => c.Nombre == "Financieros");

        var etiquetas = await svc.ListarEtiquetasAsync(CancellationToken.None);
        var basesEt = etiquetas.Where(e => e.EsBase).ToList();
        Assert.True(basesEt.Count >= 7);
        Assert.Contains(basesEt, e => e.Nombre == "Vigente");
        Assert.Contains(basesEt, e => e.Nombre == "Confidencial");

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Categoria_base_no_es_editable_ni_desactivable_RN12()
    {
        var tenantId = await SeedTenantAsync("Doc RN12");
        await SeedPersonaConApplicationUser();
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        var cats = await svc.ListarCategoriasAsync(CancellationToken.None);
        var baseCat = cats.First(c => c.EsBase);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ActualizarCategoriaAsync(baseCat.Id, new ActualizarCategoriaRequest(
                "Hack", null, null, null, 0), CancellationToken.None));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.DesactivarCategoriaAsync(baseCat.Id, CancellationToken.None));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Subir_documento_crea_v1_con_hash_y_emite_auditoria()
    {
        var tenantId = await SeedTenantAsync("Doc Subir");
        await SeedPersonaConApplicationUser();
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        var cat = (await svc.ListarCategoriasAsync(CancellationToken.None)).First(c => c.EsBase);
        var bytes = System.Text.Encoding.UTF8.GetBytes("contenido del PDF de prueba");
        var b64 = Convert.ToBase64String(bytes);

        var d = await svc.SubirDocumentoAsync(new SubirDocumentoRequest(
            cat.Id, null, "Reglamento 2026", "RPH actualizado",
            "reglamento.pdf", "application/pdf", bytes.Length, b64,
            "EQUIPO", null, OrigenDocumento.Manual, null), CancellationToken.None);

        Assert.Equal(EstadoDocumento.Vigente, d.Estado);
        Assert.Equal(1, d.NumeroVersiones);
        Assert.NotNull(d.VersionActual);
        Assert.Equal(1, d.VersionActual.Numero);
        Assert.Equal(64, d.VersionActual.HashSha256.Length);  // SHA-256 hex
        Assert.Equal("Manual", d.Origen.ToString());

        var auditoria = await svc.ListarAuditoriaAsync(d.Id, CancellationToken.None);
        Assert.Contains(auditoria, a => a.TipoEvento == TipoEventoDocumento.Subida);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Nueva_version_incrementa_numero_y_conserva_historial_RN13()
    {
        var tenantId = await SeedTenantAsync("Doc Ver");
        await SeedPersonaConApplicationUser();
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        var cat = (await svc.ListarCategoriasAsync(CancellationToken.None)).First(c => c.EsBase);
        var d = await SubirAsync(svc, cat.Id, "Acta", "v1 contenido");

        var v2 = await svc.SubirNuevaVersionAsync(d.Id, new NuevaVersionRequest(
            "acta_v2.pdf", "application/pdf", 100,
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("contenido nuevo")),
            "Correccion de fechas"), CancellationToken.None);
        Assert.Equal(2, v2.Numero);

        var detalle = await svc.GetDocumentoAsync(d.Id, CancellationToken.None);
        Assert.NotNull(detalle);
        Assert.Equal(2, detalle!.NumeroVersiones);
        Assert.Equal(2, detalle.VersionActual.Numero);
        Assert.Single(detalle.Historial);
        Assert.Equal(1, detalle.Historial[0].Numero);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Eliminar_documento_es_soft_delete_RN01()
    {
        var tenantId = await SeedTenantAsync("Doc Soft");
        await SeedPersonaConApplicationUser();
        var (svc, db, scope) = Build(tenantId);
        using var _ = scope;

        var cat = (await svc.ListarCategoriasAsync(CancellationToken.None)).First(c => c.EsBase);
        var d = await SubirAsync(svc, cat.Id, "Plan emergencia", "x");

        var ok = await svc.EliminarDocumentoAsync(d.Id, CancellationToken.None);
        Assert.True(ok);

        // Soft delete: el registro sigue en DB con activo=false, estado=Archivado.
        var registro = await db.Documentos.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == d.Id);
        Assert.NotNull(registro);
        Assert.False(registro!.Activo);
        Assert.Equal(EstadoDocumento.Archivado, registro.Estado);

        // Y no aparece al listar normal.
        var page = await svc.ListarDocumentosAsync(new DocumentosFiltro(null, null, null, null, null, null, null, null), CancellationToken.None);
        Assert.DoesNotContain(page.Items, x => x.Id == d.Id);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Auditoria_es_append_only_trigger_bloquea_update_RN15()
    {
        var tenantId = await SeedTenantAsync("Doc Audit");
        await SeedPersonaConApplicationUser();
        var (svc, db, scope) = Build(tenantId);
        using var _ = scope;

        var cat = (await svc.ListarCategoriasAsync(CancellationToken.None)).First(c => c.EsBase);
        var d = await SubirAsync(svc, cat.Id, "Polilegal", "y");
        var evento = (await svc.ListarAuditoriaAsync(d.Id, CancellationToken.None)).First();

        // Intento de UPDATE directo -> trigger debe rechazar con PostgresException.
        var ex = await Assert.ThrowsAsync<Npgsql.PostgresException>(async () =>
        {
            await db.Database.ExecuteSqlAsync($"UPDATE documento_auditoria SET detalle_json = 'tampered' WHERE id = {evento.Id}");
        });
        Assert.Contains("append-only", ex.Message);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Filtro_por_categoria_y_texto_funciona()
    {
        var tenantId = await SeedTenantAsync("Doc Filtro");
        await SeedPersonaConApplicationUser();
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        var cats = await svc.ListarCategoriasAsync(CancellationToken.None);
        var cat1 = cats.First(c => c.Nombre == "Financieros");
        var cat2 = cats.First(c => c.Nombre == "Otros");

        await SubirAsync(svc, cat1.Id, "Estado Financiero Marzo", "fin1");
        await SubirAsync(svc, cat1.Id, "Estado Financiero Abril", "fin2");
        await SubirAsync(svc, cat2.Id, "Documento varios", "v");

        var soloFin = await svc.ListarDocumentosAsync(new DocumentosFiltro(cat1.Id, null, null, null, null, null, null, null), CancellationToken.None);
        Assert.Equal(2, soloFin.Total);
        Assert.All(soloFin.Items, d => Assert.Equal(cat1.Id, d.CategoriaId));

        var porTexto = await svc.ListarDocumentosAsync(new DocumentosFiltro(null, null, null, null, null, "Marzo", null, null), CancellationToken.None);
        Assert.Single(porTexto.Items);
        Assert.Contains("Marzo", porTexto.Items[0].Titulo);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Resumen_modulo_devuelve_totales_correctos()
    {
        var tenantId = await SeedTenantAsync("Doc Resumen");
        await SeedPersonaConApplicationUser();
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        var cat = (await svc.ListarCategoriasAsync(CancellationToken.None)).First();
        await SubirAsync(svc, cat.Id, "Doc 1", "1");
        await SubirAsync(svc, cat.Id, "Doc 2", "22");

        var resumen = await svc.GetResumenAsync(CancellationToken.None);
        Assert.Equal(2, resumen.TotalDocumentos);
        Assert.True(resumen.TotalCategorias >= 9);
        Assert.True(resumen.TamanoTotalBytes > 0);
        Assert.Equal(2, resumen.DocumentosUltimos30Dias);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Descargar_emite_consumo_y_devuelve_bytes()
    {
        var tenantId = await SeedTenantAsync("Doc Desc");
        await SeedPersonaConApplicationUser();
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        var cat = (await svc.ListarCategoriasAsync(CancellationToken.None)).First();
        var d = await SubirAsync(svc, cat.Id, "PDF descargable", "contenido inmutable");

        var descarga = await svc.DescargarAsync(d.Id, null, CancellationToken.None);
        Assert.NotNull(descarga);
        var bytes = Convert.FromBase64String(descarga!.ContenidoBase64);
        Assert.Equal("contenido inmutable", System.Text.Encoding.UTF8.GetString(bytes));

        var auditoria = await svc.ListarAuditoriaAsync(d.Id, CancellationToken.None);
        Assert.Contains(auditoria, a => a.TipoEvento == TipoEventoDocumento.Descarga);

        var stats = await svc.GetEstadisticasAsync(d.Id, CancellationToken.None);
        Assert.Equal(1, stats.TotalDescargas);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Crear_categoria_custom_del_tenant_funciona_y_es_editable()
    {
        var tenantId = await SeedTenantAsync("Doc Custom");
        await SeedPersonaConApplicationUser();
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        var nueva = await svc.CrearCategoriaAsync(new CrearCategoriaRequest(
            "Manuales internos", "Manuales operativos de la copropiedad",
            "fi-rr-book", "#0ea5e9"), CancellationToken.None);
        Assert.False(nueva.EsBase);
        Assert.Equal("Manuales internos", nueva.Nombre);

        var ok = await svc.ActualizarCategoriaAsync(nueva.Id, new ActualizarCategoriaRequest(
            "Manuales operativos", "desc", "fi-rr-book", "#0ea5e9", 50), CancellationToken.None);
        Assert.True(ok);

        var refresh = await svc.GetCategoriaAsync(nueva.Id, CancellationToken.None);
        Assert.Equal("Manuales operativos", refresh!.Nombre);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Destacado_personal_persiste_por_usuario()
    {
        var tenantId = await SeedTenantAsync("Doc Dest");
        await SeedPersonaConApplicationUser();
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        var cat = (await svc.ListarCategoriasAsync(CancellationToken.None)).First();
        var d = await SubirAsync(svc, cat.Id, "Doc destacado", "z");

        var ok = await svc.MarcarDestacadoPersonalAsync(d.Id, CancellationToken.None);
        Assert.True(ok);

        var refresh = await svc.GetDocumentoAsync(d.Id, CancellationToken.None);
        Assert.True(refresh!.DestacadoPersonal);

        var quitar = await svc.QuitarDestacadoPersonalAsync(d.Id, CancellationToken.None);
        Assert.True(quitar);

        await CleanTenant(tenantId);
    }

    // =======================================================================
    // Helpers
    // =======================================================================

    private (IDocumentosService svc, PropiaDbContext db, IServiceScope scope) Build(Guid tenantId)
    {
        var scope = _services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        ctx.SetTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var http = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var storage = scope.ServiceProvider.GetRequiredService<IBlobStorage>();
        return (new DocumentosService(db, ctx, http, storage), db, scope);
    }

    private static async Task<DocumentoDetalleDto> SubirAsync(IDocumentosService svc, Guid categoriaId, string titulo, string contenidoTexto)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(contenidoTexto);
        return await svc.SubirDocumentoAsync(new SubirDocumentoRequest(
            categoriaId, null, titulo, null,
            $"{titulo}.pdf", "application/pdf", bytes.Length,
            Convert.ToBase64String(bytes), "EQUIPO", null,
            OrigenDocumento.Manual, null), CancellationToken.None);
    }

    private static HttpContext BuildFakeHttpContext(Guid userId, Guid personaId)
    {
        var ctx = new DefaultHttpContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("user_id", userId.ToString()),
            new Claim("persona_id", personaId.ToString())
        }, "test"));
        return ctx;
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

    private async Task SeedPersonaConApplicationUser()
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var p = new Persona
        {
            Id = _personaId,
            TipoDocumento = TipoDocumento.CC,
            Documento = $"D{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = "Admin",
            Apellidos = "Doc",
            Email = $"doc.{Guid.NewGuid():N}@test.co",
            PerfilIncompleto = false
        };
        ctx.Personas.Add(p);
        var u = new ApplicationUser
        {
            Id = _userId,
            UserName = p.Email,
            Email = p.Email,
            NormalizedUserName = p.Email!.ToUpper(),
            NormalizedEmail = p.Email.ToUpper(),
            EmailConfirmed = true,
            PersonaId = _personaId,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        ctx.Users.Add(u);
        await ctx.SaveChangesAsync();
    }

    private async Task CleanTenant(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());

        // Append-only triggers bloquearian DELETE en auditoria, consumo, versiones; los desactivamos en cleanup.
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE documento_auditoria DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE documento_consumo DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE documento_versiones DISABLE TRIGGER ALL");

        await ctx.Database.ExecuteSqlAsync($"DELETE FROM documento_auditoria WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM documento_consumo WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM documento_destacados_personal WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM documento_etiqueta_asignaciones WHERE tenant_id = {tenantId}");
        // Rompe FK documentos.version_actual_id antes de borrar versiones.
        await ctx.Database.ExecuteSqlAsync($"UPDATE documentos SET version_actual_id = NULL WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM documento_versiones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM documentos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM documento_carpetas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM documento_etiquetas_catalogo WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM documento_categorias WHERE tenant_id = {tenantId}");

        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE documento_auditoria ENABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE documento_consumo ENABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE documento_versiones ENABLE TRIGGER ALL");

        await ctx.Database.ExecuteSqlAsync($"DELETE FROM asp_net_users WHERE id = {_userId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id = {_personaId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }

    // =======================================================================
    // InMemoryBlobStorage - implementa IBlobStorage usando un dictionary en memoria.
    // No persiste a disco para no ensuciar el filesystem en tests.
    // =======================================================================

    private sealed class InMemoryBlobStorage : IBlobStorage
    {
        private readonly Dictionary<string, byte[]> _store = new();
        private readonly Dictionary<string, string> _mime = new();

        public Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct)
        {
            using var ms = new MemoryStream();
            content.CopyTo(ms);
            _store[key] = ms.ToArray();
            _mime[key] = contentType;
            return Task.FromResult(GetPublicUrl(key));
        }

        public Task DeleteAsync(string key, CancellationToken ct)
        {
            _store.Remove(key);
            _mime.Remove(key);
            return Task.CompletedTask;
        }

        public string GetPublicUrl(string key) => $"/mem/{key}";

        public Task<byte[]?> DownloadAsync(string key, CancellationToken ct)
        {
            return Task.FromResult(_store.TryGetValue(key, out var bytes) ? bytes : null);
        }
    }
}
