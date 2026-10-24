using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Propia.Application.Common;
using Propia.Application.Documentos;
using Propia.Application.InfraestructuraIa;
using Propia.Application.Ocr;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Documentos;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Documentos v2 - Ola 3: chat con los documentos de un folder. Con proveedor de IA STUB (captura el system
/// prompt) y extractor STUB (PDF -> texto fijo). Cubre:
///  - El contexto que recibe la IA trae la estructura, cada documento, sus metadatos (con descripcion) y el
///    texto extraido (txt local, "pdf" via extractor); las casillas vacias se marcan como SIN ARCHIVO.
///  - La conversacion persiste por folder raiz + usuario y se envia como historial; limpiar la borra.
///  - La indexacion se reutiliza mientras no cambie la version y se recalcula al cambiar.
///  - Sin proveedor configurado responde con un aviso (Error=true) y no rompe.
///  - Aislamiento por tenant: otra copropiedad no ve la conversacion.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class FolderChatFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly StubAi _ai = new();
    private readonly StubExtractor _extractor = new();

    public FolderChatFlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddScoped<ITenantContext, TenantContext>();
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("user_id", _userId.ToString()) }, "test"));
        sc.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = http });
        sc.AddSingleton<IBlobStorage, InMemoryBlobStorage>();
        sc.AddSingleton<IAiProviderClient>(_ai);
        sc.AddSingleton<IAiDocumentExtractor>(_extractor);
        sc.AddSingleton<ISecretProtector, PlainSecret>();
        sc.AddScoped<TenantConnectionInterceptor>();
        sc.AddDbContext<PropiaDbContext>((sp, opts) =>
            opts.UseNpgsql(_fx.AppConnectionString)
                .AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>()));
        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Contexto_historial_persistencia_e_indexacion()
    {
        var t = await SeedTenantAsync("[FOLDER] chat");
        await SeedProveedorAsync(true);
        var (folders, chat, db, scope) = Build(t);
        using var _ = scope;

        var raiz = await folders.CrearFolderAsync(new CrearFolderRequest("[F] Asamblea 2026"), CancellationToken.None);
        var sub = await folders.CrearFolderAsync(new CrearFolderRequest("[F] Anexos", raiz.Id), CancellationToken.None);
        var campo = await folders.CrearCampoAsync(new CrearFolderCampoRequest(raiz.Id, null, "[F] Fecha de firma", TipoCampoTablero.Fecha,
            Descripcion: "Fecha junto a las firmas"), CancellationToken.None);
        var acta = await folders.CargarDocumentoAsync(raiz.Id, Archivo("acta.txt", "El quorum fue del 71 por ciento y se aprobo el presupuesto."), CancellationToken.None);
        await folders.GuardarValoresAsync(acta.Id, new GuardarValoresRequest(new[] { new CampoValorRequest(campo.Id, "2026-03-15") }), CancellationToken.None);
        var pdf = await folders.CargarDocumentoAsync(sub.Id, Archivo("poder.pdf", "%PDF-1.4 fake"), CancellationToken.None);
        await folders.CrearDocumentoAsync(sub.Id, new CrearDocumentoRequest("[F] Listado de asistencia", true), CancellationToken.None);

        // 1) Primer turno: contexto completo.
        _ai.Respuesta = "El quórum fue del 71 %.";
        var r1 = await chat.EnviarAsync(raiz.Id, new EnviarFolderChatRequest("¿Cuál fue el quórum?", sub.Id), CancellationToken.None);
        Assert.NotNull(r1);
        Assert.False(r1!.Asistente.Error);
        Assert.Equal("El quórum fue del 71 %.", r1.Asistente.Texto);
        var sys = _ai.UltimoSystem!;
        Assert.Contains("FOLDER RAÍZ: [F] Asamblea 2026", sys);
        Assert.Contains("FOLDER SELECCIONADO POR EL USUARIO: [F] Anexos", sys);
        Assert.Contains("- [F] Anexos (2 documento(s))", sys);
        Assert.Contains("### acta", sys);
        Assert.Contains("[F] Fecha de firma (Fecha junto a las firmas): 2026-03-15", sys);
        Assert.Contains("quorum fue del 71 por ciento", sys);           // texto del .txt (local)
        Assert.Contains("TEXTO EXTRAIDO POR IA DEL PDF", sys);           // texto del "pdf" (extractor stub)
        Assert.Contains("SIN ARCHIVO (casilla vacía, OBLIGATORIA)", sys);
        Assert.Equal(1, _extractor.Llamadas);                            // solo el pdf fue a la IA
        Assert.Single(_ai.UltimosTurnos!);
        Assert.Equal(1, await db.FolderChatMensajes.CountAsync(m => m.Rol == "user"));
        Assert.Equal(1, await db.FolderChatMensajes.CountAsync(m => m.Rol == "assistant"));

        // Indexacion persistida por version; no se reextrae si no cambia la version.
        var tx = await chat.IndexarDocumentoAsync(pdf.Id, false, CancellationToken.None);
        Assert.Equal("ia", tx!.Fuente); Assert.Equal(1, tx.NumeroVersion);
        Assert.Equal(1, _extractor.Llamadas);

        // 2) Segundo turno: historial (user, model) + nueva pregunta.
        _ai.Respuesta = "Sí, el 15 de marzo de 2026.";
        var r2 = await chat.EnviarAsync(raiz.Id, new EnviarFolderChatRequest("¿Y cuándo se firmó?"), CancellationToken.None);
        Assert.Equal(3, _ai.UltimosTurnos!.Count);
        Assert.Equal("user", _ai.UltimosTurnos[0].Role); Assert.Equal("model", _ai.UltimosTurnos[1].Role); Assert.Equal("user", _ai.UltimosTurnos[2].Role);
        var lista = await chat.ListarAsync(raiz.Id, CancellationToken.None);
        Assert.True(lista!.IaDisponible);
        Assert.Equal(4, lista.Mensajes.Count);
        Assert.Equal("¿Cuál fue el quórum?", lista.Mensajes[0].Texto);

        // 3) Nueva version del pdf -> se reindexa en el siguiente turno.
        await folders.CargarVersionAsync(pdf.Id, new CargarVersionRequest("poder-v2.pdf", "application/pdf", 5, B64("%PDF-1.4 v2")), CancellationToken.None);
        await chat.EnviarAsync(raiz.Id, new EnviarFolderChatRequest("¿Qué cambió?"), CancellationToken.None);
        Assert.Equal(2, _extractor.Llamadas);
        var tx2 = await db.FolderDocumentoTextos.AsNoTracking().SingleAsync(x => x.DocumentoId == pdf.Id);
        Assert.Equal(2, tx2.NumeroVersion);

        // 4) Limpiar borra la conversacion del usuario en ese folder.
        Assert.True(await chat.LimpiarAsync(raiz.Id, CancellationToken.None));
        Assert.Empty((await chat.ListarAsync(raiz.Id, CancellationToken.None))!.Mensajes);

        await CleanTenantAsync(t);
    }

    [Fact]
    public async Task Sin_proveedor_responde_aviso_y_aislamiento_por_tenant()
    {
        var tA = await SeedTenantAsync("[FOLDER] chat A");
        var tB = await SeedTenantAsync("[FOLDER] chat B");
        await SeedProveedorAsync(false);
        var (fA, cA, _, sA) = Build(tA);
        var (_, cB, dbB, sB) = Build(tB);
        using var _a = sA; using var _b = sB;

        var raiz = await fA.CrearFolderAsync(new CrearFolderRequest("[F] Solo A"), CancellationToken.None);
        var r = await cA.EnviarAsync(raiz.Id, new EnviarFolderChatRequest("hola"), CancellationToken.None);
        Assert.True(r!.Asistente.Error);
        Assert.Contains("no está configurada", r.Asistente.Texto);
        Assert.False((await cA.ListarAsync(raiz.Id, CancellationToken.None))!.IaDisponible);

        Assert.Null(await cB.ListarAsync(raiz.Id, CancellationToken.None));
        Assert.Null(await cB.EnviarAsync(raiz.Id, new EnviarFolderChatRequest("intruso"), CancellationToken.None));
        Assert.False(await dbB.FolderChatMensajes.IgnoreQueryFilters().AnyAsync(m => m.FolderId == raiz.Id));

        await CleanTenantAsync(tA);
        await CleanTenantAsync(tB);
    }

    // =======================================================================
    private static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
    private static CargarDocumentoRequest Archivo(string nombre, string contenido)
        => new(nombre, nombre.EndsWith(".pdf") ? "application/pdf" : "text/plain", contenido.Length, B64(contenido));

    private (IFoldersService folders, IFolderChatService chat, PropiaDbContext db, IServiceScope scope) Build(Guid tenantId)
    {
        var scope = _services.CreateScope();
        var sp = scope.ServiceProvider;
        var ctx = sp.GetRequiredService<ITenantContext>(); ctx.SetTenant(tenantId);
        var db = sp.GetRequiredService<PropiaDbContext>();
        var http = sp.GetRequiredService<IHttpContextAccessor>();
        var storage = sp.GetRequiredService<IBlobStorage>();
        var usage = new Propia.Infrastructure.InfraestructuraIa.AiUsageService(db, ctx);
        var folders = new FoldersService(db, ctx, storage, http);
        var chat = new FolderChatService(db, ctx, http, storage, _ai, usage, sp.GetRequiredService<ISecretProtector>(), _extractor, NullLogger<FolderChatService>.Instance);
        return (folders, chat, db, scope);
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

    /// <summary>Config GLOBAL del proveedor (Super Admin): habilitada o no. Deja una sola fila Gemini.</summary>
    private async Task SeedProveedorAsync(bool habilitado)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM ai_provider_configs");
        if (habilitado)
        {
            ctx.AiProviderConfigs.Add(new AiProviderConfig { Provider = AiProvider.Gemini, IsEnabled = true, ApiKeyEncrypted = "clave-prueba", Model = "modelo-prueba" });
            await ctx.SaveChangesAsync();
        }
    }

    private async Task CleanTenantAsync(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM ai_usage_logs WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM folder_chat_mensajes WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM folder_documento_textos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM folder_documento_campo_valores WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM folder_campo_definiciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM folder_documento_versiones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM folder_documentos WHERE tenant_id = {tenantId}");
        for (var i = 0; i < 10; i++)
            await ctx.Database.ExecuteSqlAsync($"DELETE FROM folders f WHERE f.tenant_id = {tenantId} AND NOT EXISTS (SELECT 1 FROM folders h WHERE h.parent_id = f.id)");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }

    // ----- Stubs -----
    private sealed class StubAi : IAiProviderClient
    {
        public string Respuesta = "ok";
        public string? UltimoSystem;
        public IReadOnlyList<AiChatTurn>? UltimosTurnos;
        public Task<AiChatResult> CompleteAsync(AiProvider provider, string apiKey, string? baseUrl, string model, string systemPrompt, IReadOnlyList<AiChatTurn> turns, CancellationToken ct = default)
        { UltimoSystem = systemPrompt; UltimosTurnos = turns; return Task.FromResult(new AiChatResult(true, Respuesta, null, 10, 5)); }
        public Task<AiCompletion> CompleteWithToolsAsync(AiProvider provider, string apiKey, string? baseUrl, string model, string systemPrompt, IReadOnlyList<AiToolMessage> messages, IReadOnlyList<AiToolSpec> tools, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<AiChatResult> ExtractFromDocumentAsync(AiProvider provider, string apiKey, string? baseUrl, string model, string instruction, byte[] documentBytes, string mimeType, string jsonSchema, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class StubExtractor : IAiDocumentExtractor
    {
        public int Llamadas;
        public Task<bool> DisponibleAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<DocumentExtractionResult> ExtraerAsync(byte[] documento, string mimeType, string? nombreArchivo, IReadOnlyList<CampoObjetivo> campos, string modulo, CancellationToken ct = default)
        {
            Llamadas++;
            return Task.FromResult(new DocumentExtractionResult(true, null, "stub", "m", new[] { new ExtractedField("texto_completo", "TEXTO EXTRAIDO POR IA DEL PDF " + nombreArchivo, 1) }, null));
        }
    }

    private sealed class PlainSecret : ISecretProtector
    {
        public string Protect(string plain) => plain;
        public string Unprotect(string cipher) => cipher;
    }

    private sealed class InMemoryBlobStorage : IBlobStorage
    {
        private readonly Dictionary<string, byte[]> _store = new();
        public Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct)
        { using var ms = new MemoryStream(); content.CopyTo(ms); _store[key] = ms.ToArray(); return Task.FromResult(GetPublicUrl(key)); }
        public Task DeleteAsync(string key, CancellationToken ct) { _store.Remove(key); return Task.CompletedTask; }
        public string GetPublicUrl(string key) => $"/mem/{key}";
        public string? ResolveUrl(string? storedValueOrKey) => storedValueOrKey;
        public Task<byte[]?> DownloadAsync(string key, CancellationToken ct) => Task.FromResult(_store.TryGetValue(key, out var b) ? b : null);
    }
}
