using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Auth;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// S-06 / S-01b (auditoria): RBAC en controllers de tenant. Un Residente NO puede ejecutar
/// escrituras criticas (Presupuesto, Cartera, Custodia); un Administrador si pasa la autorizacion.
/// Reusa la ApiFactory de AuthFlowTests (API completo + Postgres efimero).
/// </summary>
[Collection(nameof(PostgresCollection))]
public class RbacFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private AuthFlowTests.ApiFactory _factory = null!;

    public RbacFlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        _factory = new AuthFlowTests.ApiFactory(_fx.OwnerConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Residente_recibe_403_en_escrituras_criticas_y_Administrador_no()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var db = sp.GetRequiredService<PropiaDbContext>();

        var tenant = new Tenant { Nombre = "CP RBAC", Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.SinAdmin };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        // ----- Residente -----
        var (resiEmail, resiPersona) = await CrearUsuarioAsync(userManager, db, "Resi", "Dente");
        await InsertUsuarioTenantConRLSAsync(db, tenant.Id, resiPersona.Id, "Residente");
        var resiClient = await LoginYSwitchAsync(resiEmail, tenant.Id);

        // Presupuesto: crear -> 403
        var pRes = await resiClient.PostAsJsonAsync("/api/presupuesto", new { anio = 2027, nombre = "X" });
        Assert.Equal(HttpStatusCode.Forbidden, pRes.StatusCode);

        // Cartera: config -> 403
        var cRes = await resiClient.PutAsJsonAsync("/api/cartera/config", new { });
        Assert.Equal(HttpStatusCode.Forbidden, cRes.StatusCode);

        // Custodia (S-01b): escenario-c -> 403 (no es Administrador)
        var custRes = await resiClient.PostAsJsonAsync("/api/custodia/escenario-c/por-copropiedad",
            new { copropiedadId = tenant.Id, organizacionEntranteId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Forbidden, custRes.StatusCode);

        // S-06 fase 2 (modulo de negocio): MiCopropiedad crear torre -> 403 (Residente no tiene MiCopropiedad).
        var torreRes = await resiClient.PostAsJsonAsync("/api/mi-copropiedad/torres", new { nombre = "T1" });
        Assert.Equal(HttpStatusCode.Forbidden, torreRes.StatusCode);

        // S-06 fase 2 (lectura abierta al tenant): el mismo Residente SI puede LEER torres (GET sin permiso).
        var torreGet = await resiClient.GetAsync("/api/mi-copropiedad/torres");
        Assert.NotEqual(HttpStatusCode.Forbidden, torreGet.StatusCode);

        // ----- Administrador (control positivo) -----
        var (admEmail, admPersona) = await CrearUsuarioAsync(userManager, db, "Admin", "Istrador");
        await InsertUsuarioTenantConRLSAsync(db, tenant.Id, admPersona.Id, "Administrador");
        var admClient = await LoginYSwitchAsync(admEmail, tenant.Id);

        // El Administrador pasa la autorizacion (puede fallar por validacion de negocio, pero NO por 403).
        var pAdm = await admClient.PostAsJsonAsync("/api/presupuesto", new { anio = 2027, nombre = "X" });
        Assert.NotEqual(HttpStatusCode.Forbidden, pAdm.StatusCode);

        // S-06 fase 2: el Administrador (bypass) tampoco recibe 403 en un modulo de negocio.
        var torreAdm = await admClient.PostAsJsonAsync("/api/mi-copropiedad/torres", new { nombre = "T1" });
        Assert.NotEqual(HttpStatusCode.Forbidden, torreAdm.StatusCode);

        // Cleanup
        await db.Database.ExecuteSqlAsync($"DELETE FROM asp_net_users WHERE persona_id IN ({resiPersona.Id}, {admPersona.Id})");
        await db.Database.ExecuteSqlAsync($"DELETE FROM usuarios_tenant WHERE persona_id IN ({resiPersona.Id}, {admPersona.Id})");
        await db.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id IN ({resiPersona.Id}, {admPersona.Id})");
        await db.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenant.Id}");
    }

    private static async Task<(string email, Persona persona)> CrearUsuarioAsync(
        UserManager<ApplicationUser> userManager, PropiaDbContext db, string nombres, string apellidos)
    {
        var persona = new Persona
        {
            TipoDocumento = TipoDocumento.CC,
            Documento = $"D{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = nombres,
            Apellidos = apellidos
        };
        db.Personas.Add(persona);
        await db.SaveChangesAsync();

        var email = $"rbac-{Guid.NewGuid():N}@propia.com.co";
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, PersonaId = persona.Id, EmailConfirmed = true };
        var created = await userManager.CreateAsync(user, "Password1234!");
        Assert.True(created.Succeeded, string.Join(",", created.Errors.Select(e => e.Description)));
        return (email, persona);
    }

    private async Task<HttpClient> LoginYSwitchAsync(string email, Guid tenantId)
    {
        var client = _factory.CreateClient();
        var loginResp = await client.PostAsJsonAsync("/connect/token", new LoginRequest(email, "Password1234!"));
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var login = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login!.AccessToken);

        // Con 1 tenant el login puede auto-seleccionarlo; si no, se hace switch explicito.
        if (login.ActiveTenantId is null)
        {
            var sw = await client.PostAsJsonAsync("/connect/switch-tenant", new SwitchTenantRequest(tenantId));
            Assert.Equal(HttpStatusCode.OK, sw.StatusCode);
            var switched = await sw.Content.ReadFromJsonAsync<LoginResponse>();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", switched!.AccessToken);
        }
        return client;
    }

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
}
