using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Propia.Api.Controllers;
using Propia.Application.Directorio;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Terceros ola C: registro PUBLICO de terceros por link (PublicTercerosController) sobre el API completo con
/// propia_app (RLS). Fija: copropiedad inactiva = 404, honeypot y formato rechazados, limite por IP = 429, y que el
/// registro crea el tercero en la copropiedad del link con su documento (sin sesion).
/// </summary>
[Collection(nameof(PostgresCollection))]
public class PublicTercerosHttpTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private AuthFlowTests.ApiFactory _factory = null!;
    private string? _connPrevia;
    public PublicTercerosHttpTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        _connPrevia = Environment.GetEnvironmentVariable("ConnectionStrings__Propia");
        Environment.SetEnvironmentVariable("ConnectionStrings__Propia", _fx.AppConnectionString);
        _factory = new AuthFlowTests.ApiFactory(_fx.AppConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__Propia", _connPrevia);
    }

    [Fact]
    public async Task Copropiedad_inexistente_o_inactiva_da_404()
    {
        var suspendida = await SeedTenantAsync("Ter Pub Suspendida", EstadoCopropiedad.Suspendida);
        var http = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"/api/publico/terceros/{Guid.NewGuid()}/config")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"/api/publico/terceros/{suspendida}/config")).StatusCode);
        var reg = await http.PostAsync($"/api/publico/terceros/{suspendida}/registrar", Form(Datos("900000001"), "rut.pdf", "application/pdf"));
        Assert.Equal(HttpStatusCode.NotFound, reg.StatusCode);
    }

    [Fact]
    public async Task Honeypot_y_formato_no_soportado_se_rechazan()
    {
        var tenant = await SeedTenantAsync("Ter Pub Honeypot", EstadoCopropiedad.Activa);
        var http = _factory.CreateClient();

        var bot = await http.PostAsync($"/api/publico/terceros/{tenant}/registrar", Form(Datos("900000002"), "rut.pdf", "application/pdf", website: "http://spam"));
        Assert.Equal(HttpStatusCode.BadRequest, bot.StatusCode);
        var exe = await http.PostAsync($"/api/publico/terceros/{tenant}/registrar", Form(Datos("900000002"), "virus.exe", "application/octet-stream"));
        Assert.Equal(HttpStatusCode.BadRequest, exe.StatusCode);

        await using var db = Owner();
        Assert.False(await db.DirectorioAdjuntos.IgnoreQueryFilters().AnyAsync(a => a.TenantId == tenant));
    }

    [Fact]
    public async Task Limite_por_ip_da_429()
    {
        var tenant = await SeedTenantAsync("Ter Pub Limite", EstadoCopropiedad.Activa);
        const string ip = "10.77.66.55";
        var cache = _factory.Services.GetRequiredService<IMemoryCache>();
        cache.Set($"terpub:{tenant:N}:{ip}", PublicTercerosController.MaxDocumentosPorHora, TimeSpan.FromMinutes(5));

        var http = _factory.CreateClient();
        http.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        var leer = await http.PostAsJsonAsync($"/api/publico/terceros/{tenant}/leer", new { perfil = "tercero", texto = "RUT" });
        Assert.Equal((HttpStatusCode)429, leer.StatusCode);
        var reg = await http.PostAsync($"/api/publico/terceros/{tenant}/registrar", Form(Datos("900000003"), "rut.pdf", "application/pdf"));
        Assert.Equal((HttpStatusCode)429, reg.StatusCode);
    }

    [Fact]
    public async Task Registrar_crea_el_tercero_en_la_copropiedad_del_link_con_su_documento()
    {
        var tenant = await SeedTenantAsync("Ter Pub Feliz", EstadoCopropiedad.Activa);
        var otro = await SeedTenantAsync("Ter Pub Otro", EstadoCopropiedad.Activa);
        var http = _factory.CreateClient();

        var cfg = await http.GetFromJsonAsync<JsonElement>($"/api/publico/terceros/{tenant}/config");
        Assert.Equal("Ter Pub Feliz", cfg.GetProperty("nombre").GetString());

        var nit = Random.Shared.Next(800_000_000, 899_999_999).ToString();
        var resp = await http.PostAsync($"/api/publico/terceros/{tenant}/registrar", Form(Datos(nit + "-" + Dv(nit)), "rut-proveedor.pdf", "application/pdf"));
        Assert.True(resp.IsSuccessStatusCode, $"{(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");
        var r = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(r.GetProperty("creado").GetBoolean());
        Assert.Equal("RUT - rut-proveedor.pdf", r.GetProperty("documento").GetString());

        await using var db = Owner();
        var emp = await db.Empresas.SingleAsync(e => e.Nit == nit);
        Assert.Equal("PROVEEDOR PUBLICO S.A.S.", emp.RazonSocial);   // la razon social se guarda tal cual la leyo la IA
        var adj = await db.DirectorioAdjuntos.IgnoreQueryFilters().Where(a => a.EntidadId == emp.Id).ToListAsync();
        Assert.Single(adj);
        Assert.Equal(tenant, adj[0].TenantId);
        Assert.False(await db.DirectorioAdjuntos.IgnoreQueryFilters().AnyAsync(a => a.TenantId == otro));
    }

    [Fact]
    public async Task Registrar_un_RUT_con_actividad_economica_larga_no_falla()
    {
        // Caso real de prod (2026-11-14): el RUT trae la actividad completa (>100) y el INSERT fallaba con 22001.
        var tenant = await SeedTenantAsync("Ter Pub Largo", EstadoCopropiedad.Activa);
        var http = _factory.CreateClient();
        var nit = Random.Shared.Next(800_000_000, 899_999_999).ToString();
        var actividad = "6201 - Actividades de desarrollo de sistemas informaticos (planificacion, analisis, diseno, programacion, pruebas) y otras conexas";
        var datos = Datos(nit + "-" + Dv(nit)) with { ActividadEconomica = actividad, RegimenTributario = new string('R', 140) };

        var resp = await http.PostAsync($"/api/publico/terceros/{tenant}/registrar", Form(datos, "rut-largo.pdf", "application/pdf"));
        Assert.True(resp.IsSuccessStatusCode, $"{(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");

        await using var db = Owner();
        var emp = await db.Empresas.SingleAsync(e => e.Nit == nit);
        Assert.NotNull(emp.SectorEconomico);
        Assert.True(emp.SectorEconomico!.Length <= 100);
        Assert.StartsWith("6201 - Actividades de desarrollo", emp.SectorEconomico);
        Assert.True(emp.RegimenTributario!.Length <= 100);
    }

    // ---- helpers ----

    private static TerceroLeidoRequest Datos(string nit)
        => new("rut", "juridica", nit, "PROVEEDOR PUBLICO S.A.S.", Ciudad: "Bogota", Email: "proveedor@publico.test");

    private static MultipartFormDataContent Form(TerceroLeidoRequest datos, string archivo, string mime, string? website = null)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(new byte[] { 0x25, 0x50, 0x44, 0x46, 1, 2, 3 });
        file.Headers.ContentType = new MediaTypeHeaderValue(mime);
        content.Add(file, "file", archivo);
        content.Add(new StringContent(JsonSerializer.Serialize(datos, new JsonSerializerOptions(JsonSerializerDefaults.Web))), "datos");
        if (website is not null) content.Add(new StringContent(website), "website");
        return content;
    }

    // DV del NIT (DIAN): pesos desde el digito de la derecha.
    private static string Dv(string nit)
    {
        int[] pesos = { 3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71 };
        var suma = 0;
        for (var i = 0; i < nit.Length; i++) suma += (nit[nit.Length - 1 - i] - '0') * pesos[i];
        var r = suma % 11;
        return (r <= 1 ? r : 11 - r).ToString();
    }

    private PropiaDbContext Owner()
        => new(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options, new TenantContext());

    private async Task<Guid> SeedTenantAsync(string nombre, EstadoCopropiedad estado)
    {
        await using var ctx = Owner();
        var t = new Tenant { Nombre = nombre, Estado = estado, EstadoCustodia = EstadoCustodia.SinAdmin };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        return t.Id;
    }
}
