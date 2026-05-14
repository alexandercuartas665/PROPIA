using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.SuperAdmin;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Tests del modulo 0.1 Super Admin Console - flujos end-to-end via HTTP.
/// Valida login del SuperAdmin + CRUD de tenants/equipo + log inmutable +
/// regla del ultimo SuperAdmin.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SuperAdminFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private ApiFactory _factory = null!;

    public SuperAdminFlowTests(PostgresFixture fx) => _fx = fx;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory(_fx.OwnerConnectionString);
        // Founder es seedeado por SuperAdminSeeder al levantar el host (Development env).
        // Aqui solo aseguramos que NO tiene MFA configurado (otros tests pudieron tocarlo).
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var founder = await db.SuperAdminUsuarios.FirstOrDefaultAsync(u => u.Email == "founder@adgroup.com.co");
        if (founder is not null && (founder.MfaConfigurado || !founder.Activo))
        {
            founder.MfaConfigurado = false;
            founder.MfaSecret = null;
            founder.Activo = true;
            await db.SaveChangesAsync();
        }
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Flujo_completo_SuperAdmin_login_crear_tenant_cambiar_estado_y_ver_log()
    {
        var client = _factory.CreateClient();

        // 1) Login (founder no tiene MFA configurado aun -> JWT directo)
        var loginResp = await client.PostAsJsonAsync("/admin/login",
            new SuperAdminLoginRequest("founder@adgroup.com.co", "PropiaFounder2026!"));
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var login = await loginResp.Content.ReadFromJsonAsync<SuperAdminLoginResponse>();
        Assert.NotNull(login);
        Assert.False(login!.RequiresMfa);
        Assert.NotNull(login.AccessToken);
        Assert.Equal(RolSuperAdmin.SuperAdmin, login.Rol);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login.AccessToken);

        // 2) Crear organizacion
        var orgResp = await client.PostAsJsonAsync("/admin/organizaciones",
            new CrearOrganizacionRequest($"Admin SAS {Guid.NewGuid():N}", TipoOrganizacion.Administradora, "900123456", "admin@empresa.com", null));
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);
        var org = await orgResp.Content.ReadFromJsonAsync<OrganizacionDto>();

        // 3) Crear tenant ligado a la org
        var tenantResp = await client.PostAsJsonAsync("/admin/tenants",
            new CrearTenantRequest($"Conjunto Test {Guid.NewGuid():N}", "800999777", "Calle 1 #2-3", null, org!.Id));
        Assert.Equal(HttpStatusCode.Created, tenantResp.StatusCode);
        var tenant = await tenantResp.Content.ReadFromJsonAsync<TenantDto>();
        Assert.Equal(EstadoCopropiedad.Activa, tenant!.Estado);
        Assert.Equal(EstadoCustodia.ConAdmin, tenant.EstadoCustodia);

        // 4) Cambiar estado del tenant a Suspendida (requiere justificacion)
        var suspendResp = await client.PutAsJsonAsync($"/admin/tenants/{tenant.Id}/estado",
            new CambiarEstadoTenantRequest(EstadoCopropiedad.Suspendida, "Mora superior a 60 dias - test"));
        Assert.Equal(HttpStatusCode.OK, suspendResp.StatusCode);
        var suspended = await suspendResp.Content.ReadFromJsonAsync<TenantDto>();
        Assert.Equal(EstadoCopropiedad.Suspendida, suspended!.Estado);

        // 5) Verificar que los logs registraron las acciones (3 acciones nuevas mas el login)
        var logsResp = await client.GetAsync("/admin/logs?take=10");
        Assert.Equal(HttpStatusCode.OK, logsResp.StatusCode);
        var logs = await logsResp.Content.ReadFromJsonAsync<List<SuperAdminLogDto>>();
        Assert.NotNull(logs);
        Assert.Contains(logs!, l => l.Accion == "SUPER_ADMIN_LOGIN");
        Assert.Contains(logs!, l => l.Accion == "CREATE_ORGANIZACION");
        Assert.Contains(logs!, l => l.Accion == "CREATE_TENANT");
        Assert.Contains(logs!, l => l.Accion == "CHANGE_TENANT_STATE" && (l.Justificacion ?? "").Contains("Mora"));

        // Cleanup
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        await db.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenant.Id}");
        await db.Database.ExecuteSqlAsync($"DELETE FROM organizaciones WHERE id = {org.Id}");
    }

    [Fact]
    public async Task No_se_puede_desactivar_el_ultimo_SuperAdmin_activo()
    {
        var client = await LoggedInClientAsync();

        // Confirmamos que solo hay 1 SuperAdmin activo (el founder) y intentamos desactivarlo
        var equipoResp = await client.GetAsync("/admin/equipo");
        var equipo = await equipoResp.Content.ReadFromJsonAsync<List<SuperAdminUsuarioDto>>();
        var founder = equipo!.First(u => u.Email == "founder@adgroup.com.co" && u.Rol == RolSuperAdmin.SuperAdmin && u.Activo);

        var resp = await client.PutAsync($"/admin/equipo/{founder.Id}/desactivar", null);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("ultimo SuperAdmin", body);
    }

    [Fact]
    public async Task Acceso_sin_token_a_endpoints_admin_retorna_401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/admin/tenants");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Token_de_usuario_normal_sin_claim_is_super_admin_retorna_403()
    {
        // Login como user normal (sin claim is_super_admin)
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"user-{Guid.NewGuid():N}@propia.com.co";
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true };
        await userManager.CreateAsync(user, "Password1234!");

        var client = _factory.CreateClient();
        var loginResp = await client.PostAsJsonAsync("/connect/token",
            new Propia.Application.Auth.LoginRequest(email, "Password1234!"));
        var login = await loginResp.Content.ReadFromJsonAsync<Propia.Application.Auth.LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login!.AccessToken);

        var resp = await client.GetAsync("/admin/tenants");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);

        // Cleanup
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        await db.Database.ExecuteSqlAsync($"DELETE FROM asp_net_users WHERE id = {user.Id}");
    }

    private async Task<HttpClient> LoggedInClientAsync()
    {
        var client = _factory.CreateClient();
        var loginResp = await client.PostAsJsonAsync("/admin/login",
            new SuperAdminLoginRequest("founder@adgroup.com.co", "PropiaFounder2026!"));
        var login = await loginResp.Content.ReadFromJsonAsync<SuperAdminLoginResponse>();
        Assert.False(login!.RequiresMfa);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login.AccessToken);
        return client;
    }

    // Reuse de la ApiFactory definida en AuthFlowTests.
    private class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _connString;
        public ApiFactory(string connString) => _connString = connString;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((ctx, conf) =>
            {
                conf.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Propia"] = _connString,
                    ["Jwt:SigningKey"] = "DEV-ONLY-PropIA-Jwt-SigningKey-Min32CharsForHS256-AuthTest",
                    ["Jwt:Issuer"] = "propia-api",
                    ["Jwt:Audience"] = "propia-clients",
                    ["Jwt:AccessTokenMinutes"] = "60"
                });
            });
        }
    }
}
