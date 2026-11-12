using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.SuperAdmin;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Tokens de API de la Consola A&amp;D (Bearer pak_...) por HTTP: se crean con sesion del Super Admin y el valor
/// sale una sola vez; con el token se llega a la API de Super Admin (indice de semillas, plantillas de agentes,
/// escrituras de semillas con log); NO sirve para gestionar tokens ni el equipo (solo sesion), NI para endpoints
/// de copropiedad; revocado, vencido o inventado da 401.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SuperAdminApiTokensFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private AuthFlowTests.ApiFactory _factory = null!;

    public SuperAdminApiTokensFlowTests(PostgresFixture fx) => _fx = fx;

    public async Task InitializeAsync()
    {
        _factory = new AuthFlowTests.ApiFactory(_fx.OwnerConnectionString);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var founder = await db.SuperAdminUsuarios.FirstOrDefaultAsync(u => u.Email == "founder@adgroup.com.co");
        if (founder is not null && (founder.MfaConfigurado || !founder.Activo))
        {
            founder.MfaConfigurado = false; founder.MfaSecret = null; founder.Activo = true;
            await db.SaveChangesAsync();
        }
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task<HttpClient> SesionAsync()
    {
        var c = _factory.CreateClient();
        var r = await c.PostAsJsonAsync("/admin/login", new SuperAdminLoginRequest("founder@adgroup.com.co", "PropiaFounder2026!"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var login = await r.Content.ReadFromJsonAsync<SuperAdminLoginResponse>();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return c;
    }

    private HttpClient ConToken(string token)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    [Fact]
    public async Task Token_llega_a_la_api_de_super_admin_y_no_a_lo_que_exige_sesion()
    {
        var sesion = await SesionAsync();
        var crear = await sesion.PostAsJsonAsync("/api/admin/api-tokens", new CrearApiTokenRequest("[TEST] token api", 1));
        Assert.Equal(HttpStatusCode.OK, crear.StatusCode);
        var creado = await crear.Content.ReadFromJsonAsync<ApiTokenCreadoDto>();
        Assert.StartsWith("pak_", creado!.Token);
        Assert.Equal("Activo", creado.Info.Estado);
        Assert.Equal(creado.Token[..8], creado.Info.Prefijo);

        // El listado nunca trae el token completo.
        var lista = await sesion.GetStringAsync("/api/admin/api-tokens");
        Assert.DoesNotContain(creado.Token, lista);

        var api = ConToken(creado.Token);
        // Indice de semillas y una API existente de Super Admin.
        var indice = await api.GetFromJsonAsync<List<SemillaTipoDto>>("/api/admin/semillas");
        Assert.Contains(indice!, s => s.Clave == "roles");
        Assert.Contains(indice!, s => s.Clave == "agentes");
        Assert.Equal(HttpStatusCode.OK, (await api.GetAsync("/admin/ai-agent-templates")).StatusCode);

        // Escritura de semilla con token: queda en el log del Super Admin.
        var etq = await api.PostAsJsonAsync("/api/admin/semillas/etiquetas-documentos", new GuardarEtiquetaDocumentoSemillaRequest($"[TEST] Etq {Guid.NewGuid():N}"[..30], "#123ABC"));
        Assert.Equal(HttpStatusCode.OK, etq.StatusCode);
        var etqDto = await etq.Content.ReadFromJsonAsync<EtiquetaDocumentoSemillaDto>();

        // Solo sesion: gestionar tokens y el equipo A&D.
        Assert.Equal(HttpStatusCode.Forbidden, (await api.GetAsync("/api/admin/api-tokens")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.PostAsJsonAsync("/api/admin/api-tokens", new CrearApiTokenRequest("x", null))).StatusCode);
        // No abre endpoints de copropiedad.
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync("/api/folders/arbol")).StatusCode);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
            Assert.True(await db.SuperAdminLogs.AnyAsync(l => l.Accion == "API_TOKEN_USO" && l.EntidadAfectada == $"ApiToken:{creado.Info.Id}"));
            var t = await db.SuperAdminApiTokens.SingleAsync(x => x.Id == creado.Info.Id);
            Assert.NotNull(t.UltimoUsoAt);
            Assert.NotEqual(creado.Token, t.TokenHash);     // solo se guarda el hash
            db.DocumentoEtiquetasCatalogo.Remove(await db.DocumentoEtiquetasCatalogo.IgnoreQueryFilters().SingleAsync(x => x.Id == etqDto!.Id));
            await db.SaveChangesAsync();
        }

        // Revocado -> 401.
        Assert.Equal(HttpStatusCode.NoContent, (await sesion.DeleteAsync($"/api/admin/api-tokens/{creado.Info.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync("/api/admin/semillas")).StatusCode);
        await BorrarTokenAsync(creado.Info.Id);
    }

    [Fact]
    public async Task Token_vencido_inventado_o_ausente_da_401()
    {
        var sesion = await SesionAsync();
        var creado = await (await sesion.PostAsJsonAsync("/api/admin/api-tokens", new CrearApiTokenRequest("[TEST] vencido", 30)))
            .Content.ReadFromJsonAsync<ApiTokenCreadoDto>();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
            var t = await db.SuperAdminApiTokens.SingleAsync(x => x.Id == creado!.Info.Id);
            t.ExpiraAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await ConToken(creado!.Token).GetAsync("/api/admin/semillas")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ConToken("pak_inventado123").GetAsync("/api/admin/semillas")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/admin/semillas")).StatusCode);
        // La sesion normal sigue funcionando igual.
        Assert.Equal(HttpStatusCode.OK, (await sesion.GetAsync("/api/admin/semillas")).StatusCode);
        // Validaciones al crear.
        Assert.Equal(HttpStatusCode.BadRequest, (await sesion.PostAsJsonAsync("/api/admin/api-tokens", new CrearApiTokenRequest("  ", 30))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await sesion.PostAsJsonAsync("/api/admin/api-tokens", new CrearApiTokenRequest("x", 0))).StatusCode);
        await BorrarTokenAsync(creado.Info.Id);
    }

    private async Task BorrarTokenAsync(Guid id)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        await db.SuperAdminApiTokens.Where(x => x.Id == id).ExecuteDeleteAsync();
    }
}
