using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Auth;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Tests del flujo de auth: login, /me, switch-tenant.
/// Levanta el API completo con WebApplicationFactory + Postgres efimero via PostgresFixture.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AuthFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private ApiFactory _factory = null!;

    public AuthFlowTests(PostgresFixture fx)
    {
        _fx = fx;
    }

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_fx.OwnerConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Login_funcional_y_switch_tenant_cambia_el_tenant_en_el_token()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var db = sp.GetRequiredService<PropiaDbContext>();

        // Setup: 1 persona, 1 user, 2 tenants, 2 vinculos UsuarioTenant
        var persona = new Persona
        {
            TipoDocumento = TipoDocumento.CC,
            Documento = $"D{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = "Auth",
            Apellidos = "Tester"
        };
        db.Personas.Add(persona);
        var tA = new Tenant { Nombre = "CP Alfa", Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.SinAdmin };
        var tB = new Tenant { Nombre = "CP Beta", Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.SinAdmin };
        db.Tenants.AddRange(tA, tB);
        await db.SaveChangesAsync();

        // RLS bloquea INSERT si app.tenant_id no coincide con el tenant_id a insertar.
        // Insertamos uno por uno seteando el contexto correspondiente, igual que en runtime.
        await InsertUsuarioTenantConRLSAsync(db, tA.Id, persona.Id, "Administrador");
        await InsertUsuarioTenantConRLSAsync(db, tB.Id, persona.Id, "Residente");

        var email = $"tester-{Guid.NewGuid():N}@propia.com.co";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            PersonaId = persona.Id,
            EmailConfirmed = true
        };
        var created = await userManager.CreateAsync(user, "Password1234!");
        Assert.True(created.Succeeded, string.Join(",", created.Errors.Select(e => e.Description)));

        // ---------- Act 1: login ----------
        var client = _factory.CreateClient();
        var loginResp = await client.PostAsJsonAsync("/connect/token", new LoginRequest(email, "Password1234!"));
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);

        var login = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login!.AccessToken));
        Assert.Null(login.ActiveTenantId);  // 2 tenants -> no se autoselecciona
        Assert.Equal(2, login.AvailableTenants.Count);

        // ---------- Act 2: /me con el token ----------
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login.AccessToken);
        var meResp = await client.GetAsync("/connect/me");
        Assert.Equal(HttpStatusCode.OK, meResp.StatusCode);
        var me = await meResp.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(me);
        Assert.Equal(email, me!.Email);
        Assert.Equal("Auth", me.PersonaNombres);

        // ---------- Act 3: switch-tenant ----------
        var switchResp = await client.PostAsJsonAsync("/connect/switch-tenant", new SwitchTenantRequest(tA.Id));
        Assert.Equal(HttpStatusCode.OK, switchResp.StatusCode);
        var switched = await switchResp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(switched);
        Assert.Equal(tA.Id, switched!.ActiveTenantId);

        // ---------- Act 4: switch a un tenant donde NO tengo acceso -> 403 ----------
        var forbiddenResp = await client.PostAsJsonAsync("/connect/switch-tenant", new SwitchTenantRequest(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResp.StatusCode);

        // Cleanup - ExecuteSqlAsync parametriza valores, Guid sin comillas, statements separados.
        await db.Database.ExecuteSqlAsync($"DELETE FROM asp_net_users WHERE id = {user.Id}");
        await db.Database.ExecuteSqlAsync($"DELETE FROM usuarios_tenant WHERE persona_id = {persona.Id}");
        await db.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id = {persona.Id}");
        await db.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id IN ({tA.Id}, {tB.Id})");
    }

    [Fact]
    public async Task Login_con_password_invalido_retorna_401()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"bad-{Guid.NewGuid():N}@propia.com.co";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(user, "Password1234!");

        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/connect/token", new LoginRequest(email, "wrong-password"));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);

        // Cleanup
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        await db.Database.ExecuteSqlAsync($"DELETE FROM asp_net_users WHERE id = {user.Id}");
    }

    /// <summary>
    /// Inserta un UsuarioTenant respetando RLS: abre conexion, setea app.tenant_id
    /// y luego inserta - simula el flujo de la app en runtime via TenantMiddleware.
    /// </summary>
    private static async Task InsertUsuarioTenantConRLSAsync(PropiaDbContext db, Guid tenantId, Guid personaId, string rol)
    {
        var conn = db.Database.GetDbConnection();
        var opened = conn.State != System.Data.ConnectionState.Open;
        if (opened) await conn.OpenAsync();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT set_config('app.tenant_id', '{tenantId}', false);
                INSERT INTO usuarios_tenant (id, tenant_id, persona_id, rol, estado, created_at)
                VALUES ('{Guid.NewGuid()}', '{tenantId}', '{personaId}', '{rol}', 1, now());";
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            if (opened) await conn.CloseAsync();
        }
    }

    // ----------------- API Factory -----------------
    public class ApiFactory : WebApplicationFactory<Program>
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
                    ["Jwt:AccessTokenMinutes"] = "60",
                    // El rate limiter no debe interferir en las pruebas de integracion (misma IP).
                    ["RateLimit:AuthPermitPerMinute"] = "100000"
                });
            });
        }
    }
}
