using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Auth;
using Propia.Application.UsuariosAccesos;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Matriz de roles, Ola 2 (2026-11-21): el permiso Exportar.
/// - La migracion ExportarIgualAVer le da Exportar al rol base Consejero donde tiene Ver (DIRECTORIO, REPORTES)
///   y no donde no lo tiene (PRESUPUESTO no es exportable).
/// - GET /api/mis-permisos refleja la matriz efectiva (default + override por copropiedad) y el bypass del Admin.
/// - El Excel de Reportes exige Exportar: con el override Exportar=false de la copropiedad -> 403.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ExportarPermisoFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private AuthFlowTests.ApiFactory _factory = null!;

    public ExportarPermisoFlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        _factory = new AuthFlowTests.ApiFactory(_fx.OwnerConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Exportar_sale_de_la_matriz_respeta_el_override_y_gatea_el_excel_de_reportes()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var db = sp.GetRequiredService<PropiaDbContext>();

        var tenant = new Tenant { Nombre = "CP Exportar", Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.SinAdmin };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var consejeroRolId = await db.RolesCopropiedad.IgnoreQueryFilters()
            .Where(r => r.TenantId == null && r.Nombre == "Consejero").Select(r => r.Id).SingleAsync();

        var (consEmail, consPersona) = await CrearUsuarioAsync(userManager, db, "Con", "Sejero");
        await EjecutarConTenantAsync(db, tenant.Id, $@"
            INSERT INTO usuarios_tenant (id, tenant_id, persona_id, rol, rol_id, estado, created_at)
            VALUES ('{Guid.NewGuid()}', '{tenant.Id}', '{consPersona.Id}', 'Consejero', '{consejeroRolId}', 1, now());");
        var cons = await LoginYSwitchAsync(consEmail, tenant.Id);

        // 1) Default del catalogo (sembrado por la migracion): Exportar donde hay Ver.
        var p1 = await cons.GetFromJsonAsync<MisPermisosDto>("/api/mis-permisos");
        Assert.NotNull(p1);
        Assert.False(p1!.EsAdministrador);
        Assert.True(p1.Puede(ModuloCodigo.Directorio, AccionPermiso.Exportar));
        Assert.True(p1.Puede(ModuloCodigo.Reportes, AccionPermiso.Exportar));
        Assert.False(p1.Puede(ModuloCodigo.Presupuesto, AccionPermiso.Exportar));

        var excel1 = await cons.PostAsJsonAsync("/api/reportes/contratos-por-vencer/excel", new { tenantIds = (Guid[]?)null });
        Assert.NotEqual(HttpStatusCode.Forbidden, excel1.StatusCode);

        // 2) La copropiedad le quita Exportar de Reportes al Consejero (override por tenant).
        await EjecutarConTenantAsync(db, tenant.Id, $@"
            INSERT INTO rol_permisos_tenant (id, tenant_id, rol_id, modulo_codigo, accion, habilitado, nivel_dato, created_at)
            VALUES ('{Guid.NewGuid()}', '{tenant.Id}', '{consejeroRolId}', '{ModuloCodigo.Reportes}', 6, false, 2, now());");

        var p2 = await cons.GetFromJsonAsync<MisPermisosDto>("/api/mis-permisos");
        Assert.False(p2!.Puede(ModuloCodigo.Reportes, AccionPermiso.Exportar));
        Assert.True(p2.Puede(ModuloCodigo.Directorio, AccionPermiso.Exportar)); // el resto intacto

        var excel2 = await cons.PostAsJsonAsync("/api/reportes/contratos-por-vencer/excel", new { tenantIds = (Guid[]?)null });
        Assert.Equal(HttpStatusCode.Forbidden, excel2.StatusCode);

        // 3) Administrador: bypass, puede todo.
        var (admEmail, admPersona) = await CrearUsuarioAsync(userManager, db, "Admin", "Exportar");
        await EjecutarConTenantAsync(db, tenant.Id, $@"
            INSERT INTO usuarios_tenant (id, tenant_id, persona_id, rol, estado, created_at)
            VALUES ('{Guid.NewGuid()}', '{tenant.Id}', '{admPersona.Id}', 'Administrador', 1, now());");
        var adm = await LoginYSwitchAsync(admEmail, tenant.Id);
        var pa = await adm.GetFromJsonAsync<MisPermisosDto>("/api/mis-permisos");
        Assert.True(pa!.EsAdministrador);
        Assert.True(pa.Puede(ModuloCodigo.Presupuesto, AccionPermiso.Exportar));

        await CleanAsync(tenant.Id, new[] { consPersona.Id, admPersona.Id });
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

        var email = $"exportar-{Guid.NewGuid():N}@propia.com.co";
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

    private static async Task EjecutarConTenantAsync(PropiaDbContext db, Guid tenantId, string sql)
    {
        var conn = db.Database.GetDbConnection();
        var opened = conn.State != System.Data.ConnectionState.Open;
        if (opened) await conn.OpenAsync();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT set_config('app.tenant_id', '{tenantId}', false); {sql}";
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            if (opened) await conn.CloseAsync();
        }
    }

    // Limpieza con conexion OWNER (bypasea RLS).
    private async Task CleanAsync(Guid tenantId, Guid[] personaIds)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM rol_permisos_tenant WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM asp_net_users WHERE persona_id = ANY({personaIds})");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM usuarios_tenant WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id = ANY({personaIds})");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }
}
