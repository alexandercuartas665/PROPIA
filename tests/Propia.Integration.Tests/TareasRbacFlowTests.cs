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
/// H-5 (seguimiento re-revision VIGIA, modulo 2.10 Tareas): RBAC de escritura por rol a nivel HTTP.
/// Un Consejero SIN permisos recibe 403 en POST /api/tareas (gate Crear) y POST /api/tareas/tableros
/// (gate Aprobar); un Administrador pasa la autorizacion por bypass (control positivo). Reusa la
/// ApiFactory de AuthFlowTests (API completo + Postgres efimero via HTTP), como RbacFlowTests.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class TareasRbacFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private AuthFlowTests.ApiFactory _factory = null!;

    public TareasRbacFlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        _factory = new AuthFlowTests.ApiFactory(_fx.OwnerConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Consejero_sin_permiso_recibe_403_al_crear_tarea_y_tablero_y_Administrador_no()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var db = sp.GetRequiredService<PropiaDbContext>();

        var tenant = new Tenant { Nombre = "CP RBAC Tareas", Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.SinAdmin };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        // ----- Consejero: rol sin rol_id -> sin permisos de Tareas -----
        var (consEmail, consPersona) = await CrearUsuarioAsync(userManager, db, "Con", "Sejero");
        await InsertUsuarioTenantConRLSAsync(db, tenant.Id, consPersona.Id, "Consejero");
        var consClient = await LoginYSwitchAsync(consEmail, tenant.Id);

        // POST /api/tareas -> 403 (gate Crear). El cuerpo no importa: el 403 salta en el filtro de permiso
        // antes del binding/handler.
        var tareaCons = await consClient.PostAsJsonAsync("/api/tareas",
            new { titulo = "Tarea RBAC", prioridad = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, tareaCons.StatusCode);

        // POST /api/tareas/tableros -> 403 (gate Aprobar).
        var tableroCons = await consClient.PostAsJsonAsync("/api/tareas/tableros",
            new { nombre = "Tablero RBAC", descripcion = (string?)null, color = "#6D4FE3", usuarioPersonaIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.Forbidden, tableroCons.StatusCode);

        // ----- Administrador: control positivo por BYPASS (no se le siembran permisos) -----
        var (admEmail, admPersona) = await CrearUsuarioAsync(userManager, db, "Admin", "Tareas");
        await InsertUsuarioTenantConRLSAsync(db, tenant.Id, admPersona.Id, "Administrador");
        var admClient = await LoginYSwitchAsync(admEmail, tenant.Id);

        // Crear tablero -> 201 (bypass; cuerpo valido). Prueba que el Admin llega al handler y crea.
        var tableroAdm = await admClient.PostAsJsonAsync("/api/tareas/tableros",
            new { nombre = "Tablero Admin", descripcion = (string?)null, color = "#6D4FE3", usuarioPersonaIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.Created, tableroAdm.StatusCode);

        // Crear tarea -> NO 403 (bypass). Puede ser 201 o fallar por validacion de negocio, pero nunca 403.
        var tareaAdm = await admClient.PostAsJsonAsync("/api/tareas",
            new { titulo = "Tarea Admin", prioridad = 1 });
        Assert.NotEqual(HttpStatusCode.Forbidden, tareaAdm.StatusCode);

        await CleanAsync(tenant.Id, new[] { consPersona.Id, admPersona.Id });
    }

    // ---- Helpers de setup (duplicados de RbacFlowTests a proposito: no tocar el test transversal) ----

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

        var email = $"rbac-tareas-{Guid.NewGuid():N}@propia.com.co";
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

    // Limpieza con conexion OWNER (bypasea RLS), igual patron que TareasFlowTests.CleanTenant.
    // El trigger append-only de tarea_historial se desactiva para poder borrar (solo dev/test).
    // Borrar el tenant al final elimina en cascada tableros/tablero_usuarios/tablero_campos.
    private async Task CleanAsync(Guid tenantId, Guid[] personaIds)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE tarea_historial DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tarea_historial WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE tarea_historial ENABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tarea_comentarios WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tarea_etiqueta_asignaciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tarea_campo_valores WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tarea_colaboradores WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tareas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tarea_etiquetas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tarea_estados WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM asp_net_users WHERE persona_id = ANY({personaIds})");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM usuarios_tenant WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id = ANY({personaIds})");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }
}
