using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Auth;
using Propia.Application.Directorio;
using Propia.Application.SuperAdmin;
using Propia.Application.UsuariosAccesos;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// API de Super Admin para usuarios de cualquier copropiedad (admin/tenants/{tenantId}/...), usada con el
/// token pak_ real: 401/403, alta con persona nueva / reutilizada, rol / persona / usuario de otro tenant
/// 404, aislamiento, gestion (roles, cargos, revocar, reactivar, invitaciones) y auditoria "via API".
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AdminTenantUsuariosApiTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private Factory _factory = null!;
    private readonly List<Guid> _tenants = new();
    private readonly List<string> _emails = new();
    private Guid _tokenId;
    private HttpClient _api = null!;
    private Guid _founderId;

    public AdminTenantUsuariosApiTests(PostgresFixture fx) => _fx = fx;

    public async Task InitializeAsync()
    {
        _factory = new Factory(_fx.OwnerConnectionString);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
            // Guarda: la API debe estar en la base del contenedor, nunca en propia_dev (no tocar datos reales).
            Assert.Equal(new Npgsql.NpgsqlConnectionStringBuilder(_fx.OwnerConnectionString).Port,
                new Npgsql.NpgsqlConnectionStringBuilder(db.Database.GetConnectionString()).Port);
            var founder = await db.SuperAdminUsuarios.FirstAsync(u => u.Email == "founder@adgroup.com.co");
            if (founder.MfaConfigurado || !founder.Activo)
            {
                founder.MfaConfigurado = false; founder.MfaSecret = null; founder.Activo = true;
                await db.SaveChangesAsync();
            }
            _founderId = founder.Id;
        }

        var sesion = _factory.CreateClient();
        var login = await (await sesion.PostAsJsonAsync("/admin/login", new SuperAdminLoginRequest("founder@adgroup.com.co", "PropiaFounder2026!")))
            .Content.ReadFromJsonAsync<SuperAdminLoginResponse>();
        sesion.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        var creado = await (await sesion.PostAsJsonAsync("/api/admin/api-tokens", new CrearApiTokenRequest("[TEST] usuarios por API", 1)))
            .Content.ReadFromJsonAsync<ApiTokenCreadoDto>();
        _tokenId = creado!.Info.Id;
        _api = _factory.CreateClient();
        _api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", creado.Token);
    }

    public async Task DisposeAsync()
    {
        await using (var ctx = OwnerDb())
        {
            await ctx.Database.OpenConnectionAsync();
            await ctx.Database.ExecuteSqlRawAsync("SET session_replication_role = replica");
            var tablas = await ctx.Database.SqlQueryRaw<string>(
                "SELECT c.table_name AS \"Value\" FROM information_schema.columns c JOIN information_schema.tables t ON t.table_schema = c.table_schema AND t.table_name = c.table_name WHERE c.table_schema = 'public' AND c.column_name = 'tenant_id' AND t.table_type = 'BASE TABLE'").ToListAsync();
            foreach (var tid in _tenants)
            {
                foreach (var t in tablas)
                    await ctx.Database.ExecuteSqlRawAsync($"DELETE FROM \"{t}\" WHERE tenant_id = '{tid}'");
                await ctx.Database.ExecuteSqlRawAsync($"DELETE FROM tenants WHERE id = '{tid}'");
            }
            foreach (var e in _emails)
            {
                await ctx.Database.ExecuteSqlRawAsync("DELETE FROM asp_net_users WHERE email = {0}", e);
                await ctx.Database.ExecuteSqlRawAsync("DELETE FROM personas WHERE email = {0}", e);
            }
            await ctx.Database.ExecuteSqlRawAsync("DELETE FROM super_admin_api_tokens WHERE id = {0}", _tokenId);
            await ctx.Database.ExecuteSqlRawAsync("SET session_replication_role = DEFAULT");
        }
        await _factory.DisposeAsync();
    }

    // ===================== Pruebas =====================

    [Fact]
    public async Task Sin_token_401_y_usuario_que_no_es_super_admin_403()
    {
        var tA = await SeedTenantAsync("[TEST] API usuarios auth");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync($"/admin/tenants/{tA}/usuarios")).StatusCode);

        // Usuario normal de la plataforma (JWT de /connect/token): no es Super Admin.
        var email = Email("normal");
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var r = await um.CreateAsync(new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true }, "Password1234!");
            Assert.True(r.Succeeded);
        }
        var normal = _factory.CreateClient();
        var login = await (await normal.PostAsJsonAsync("/connect/token", new LoginRequest(email, "Password1234!"))).Content.ReadFromJsonAsync<LoginResponse>();
        normal.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await normal.GetAsync($"/admin/tenants/{tA}/usuarios")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await normal.PostAsJsonAsync($"/admin/tenants/{tA}/usuarios",
            new AdminCrearUsuarioRequest(null, Nueva("Nadie", email), null, null, false))).StatusCode);

        // Con token: 200; copropiedad inexistente o suspendida: 404.
        Assert.Equal(HttpStatusCode.OK, (await _api.GetAsync($"/admin/tenants/{tA}/usuarios")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.GetAsync($"/admin/tenants/{Guid.NewGuid()}/usuarios")).StatusCode);
        var tS = await SeedTenantAsync("[TEST] API usuarios suspendida", EstadoCopropiedad.Suspendida);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.GetAsync($"/admin/tenants/{tS}/roles")).StatusCode);
    }

    [Fact]
    public async Task Alta_con_persona_nueva_deja_lo_mismo_que_la_ui_y_audita_via_api()
    {
        var tA = await SeedTenantAsync("[TEST] API usuarios alta");
        var roles = await _api.GetFromJsonAsync<List<AdminRolTenantDto>>($"/admin/tenants/{tA}/roles");
        var admin = roles!.Single(r => r.Nombre == "Administrador");
        var email = Email("nueva");

        var resp = await _api.PostAsJsonAsync($"/admin/tenants/{tA}/usuarios", new AdminCrearUsuarioRequest(
            null, Nueva("Nueva", email), new[] { admin.Id }, new[] { "Contador externo" }, true, CanalEnvioInvitacion.Email));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var creado = (await resp.Content.ReadFromJsonAsync<AdminCrearUsuarioResponse>())!;
        Assert.False(creado.PersonaReutilizada);
        Assert.Equal(EstadoUsuarioTenant.Pendiente, creado.Estado);
        Assert.NotNull(creado.Invitacion);
        Assert.StartsWith("/invitacion/", creado.Invitacion!.LinkAceptacion);
        Assert.True(creado.Invitacion.ExpiraAt > DateTimeOffset.UtcNow.AddHours(70));

        // Lo mismo que deja la UI (alta + invitacion): usuario pendiente con su rol principal, roles y cargos,
        // persona en el Directorio de la copropiedad e invitacion pendiente con el rol principal.
        await using (var db = await TenantDbAsync(tA))
        {
            var ut = await db.UsuariosTenant.SingleAsync(u => u.Id == creado.UsuarioTenantId);
            Assert.Equal(creado.PersonaId, ut.PersonaId);
            Assert.Equal(admin.Id, ut.RolId);
            Assert.Equal(new[] { admin.Id }, await db.UsuarioTenantRoles.Where(x => x.UsuarioTenantId == ut.Id).Select(x => x.RolId).ToListAsync());
            Assert.Equal(new[] { "Contador externo" }, await db.UsuarioTenantCargos.Where(x => x.UsuarioTenantId == ut.Id).Select(x => x.Cargo).ToListAsync());
            Assert.True(await db.DirectorioVinculos.AnyAsync(v => v.EntidadTipo == EntidadDirectorio.Persona && v.EntidadId == creado.PersonaId && v.Estado == EstadoVinculo.Activo));
            var inv = await db.UsuarioInvitaciones.SingleAsync(i => i.Id == creado.Invitacion.Id);
            Assert.Equal(EstadoInvitacion.Pendiente, inv.Estado);
            Assert.Equal(admin.Id, inv.RolId);

            // Auditoria de usuarios del tenant: actor = Super Admin, canal Api, marca "via API".
            var aud = await db.AccesoAuditorias.Where(a => a.TenantId == tA && a.EntidadAfectadaId == inv.Id).SingleAsync();
            Assert.Equal(TipoEventoAuditoria.InvitacionEnviada, aud.TipoEvento);
            Assert.Equal(_founderId, aud.ActorUsuarioId);
            Assert.Equal(CanalAcceso.Api, aud.Canal);
            Assert.Contains("via API Super Admin founder@adgroup.com.co", aud.Detalle);
            // Log del Super Admin.
            Assert.True(await db.SuperAdminLogs.AnyAsync(l => l.Accion == "TENANT_USUARIO_CREAR"
                && l.EntidadAfectada == $"Tenant:{tA}/{creado.UsuarioTenantId}" && l.ActorId == _founderId));
        }

        // Lectura: lista, detalle y busqueda en el Directorio.
        var lista = await _api.GetFromJsonAsync<List<UsuarioListaDto>>($"/admin/tenants/{tA}/usuarios?estado=Pendiente&q=nueva");
        Assert.Contains(lista!, u => u.UsuarioTenantId == creado.UsuarioTenantId);
        var det = await _api.GetFromJsonAsync<UsuarioDetalleDto>($"/admin/tenants/{tA}/usuarios/{creado.UsuarioTenantId}");
        Assert.Equal(email, det!.Email);
        var personas = await _api.GetFromJsonAsync<List<PersonaResumenDto>>($"/admin/tenants/{tA}/personas?q={Uri.EscapeDataString(email)}");
        Assert.Single(personas!, p => p.Id == creado.PersonaId);

        // Validaciones de negocio -> 400 { error }.
        var sinPersona = await _api.PostAsJsonAsync($"/admin/tenants/{tA}/usuarios", new AdminCrearUsuarioRequest(null, null, null, null, false));
        Assert.Equal(HttpStatusCode.BadRequest, sinPersona.StatusCode);
        Assert.Contains("error", await sinPersona.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Alta_reutiliza_persona_por_documento_y_por_email_sin_duplicar()
    {
        var tA = await SeedTenantAsync("[TEST] API usuarios reuso A");
        var tB = await SeedTenantAsync("[TEST] API usuarios reuso B");
        var email = Email("reuso");
        var datos = Nueva("Reuso", email);

        var a = await CrearAsync(tA, new AdminCrearUsuarioRequest(null, datos, null, null, false));
        Assert.False(a.PersonaReutilizada);
        Assert.Null(a.Invitacion);

        // Mismo documento en OTRA copropiedad: se reutiliza y se vincula (no se crea otra persona).
        var b = await CrearAsync(tB, new AdminCrearUsuarioRequest(null, datos with { Email = null }, null, null, true));
        Assert.True(b.PersonaReutilizada);
        Assert.Equal(a.PersonaId, b.PersonaId);
        // S-02b intacta: ya es miembro de otra copropiedad -> no se expone el link (le llega al destinatario).
        Assert.Null(b.Invitacion!.LinkAceptacion);

        // Mismo email con OTRO documento en la misma copropiedad: reutiliza por email.
        var tC = await SeedTenantAsync("[TEST] API usuarios reuso C");
        var c = await CrearAsync(tC, new AdminCrearUsuarioRequest(null, datos with { Documento = Doc() }, null, null, false));
        Assert.True(c.PersonaReutilizada);
        Assert.Equal(a.PersonaId, c.PersonaId);

        // personaId existente de la copropiedad: tambien sirve.
        var tAOtro = await _api.PostAsJsonAsync($"/admin/tenants/{tA}/usuarios", new AdminCrearUsuarioRequest(a.PersonaId, null, null, null, false));
        Assert.Equal(HttpStatusCode.Created, tAOtro.StatusCode);

        await using var db = OwnerDb();
        Assert.Equal(1, await db.Personas.CountAsync(p => p.Email == email));
        // Sin roles -> el rol por defecto de la alta (Portal Residente), igual que la UI.
        await using var dbA = await TenantDbAsync(tA);
        var ut = await dbA.UsuariosTenant.Include(u => u.RolNavigation).SingleAsync(u => u.Id == a.UsuarioTenantId);
        Assert.Equal("Portal Residente", ut.RolNavigation!.Nombre);
    }

    [Fact]
    public async Task Rol_persona_usuario_o_invitacion_de_otro_tenant_da_404_y_hay_aislamiento()
    {
        var tA = await SeedTenantAsync("[TEST] API usuarios aisl A");
        var tB = await SeedTenantAsync("[TEST] API usuarios aisl B");
        var rolB = await SeedRolPropioAsync(tB, "[TEST] Rol solo de B");
        var enB = await CrearAsync(tB, new AdminCrearUsuarioRequest(null, Nueva("SoloB", Email("solob")), new[] { rolB }, null, true));

        // B ve su rol propio; A no.
        Assert.Contains(await _api.GetFromJsonAsync<List<AdminRolTenantDto>>($"/admin/tenants/{tB}/roles") ?? new(), r => r.Id == rolB);
        Assert.DoesNotContain(await _api.GetFromJsonAsync<List<AdminRolTenantDto>>($"/admin/tenants/{tA}/roles") ?? new(), r => r.Id == rolB);

        // Rol de B en A -> 404, y no se crea nada (ni la persona).
        var emailA = Email("conrolb");
        var conRolB = await _api.PostAsJsonAsync($"/admin/tenants/{tA}/usuarios", new AdminCrearUsuarioRequest(null, Nueva("ConRolB", emailA), new[] { rolB }, null, false));
        Assert.Equal(HttpStatusCode.NotFound, conRolB.StatusCode);
        await using (var db = OwnerDb())
            Assert.False(await db.Personas.AnyAsync(p => p.Email == emailA));

        // Persona de B (por id) en A -> 404.
        Assert.Equal(HttpStatusCode.NotFound, (await _api.PostAsJsonAsync($"/admin/tenants/{tA}/usuarios",
            new AdminCrearUsuarioRequest(enB.PersonaId, null, null, null, false))).StatusCode);

        // Usuario e invitacion de B vistos desde A -> 404 en todo.
        var u = enB.UsuarioTenantId;
        Assert.Equal(HttpStatusCode.NotFound, (await _api.GetAsync($"/admin/tenants/{tA}/usuarios/{u}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.PutAsJsonAsync($"/admin/tenants/{tA}/usuarios/{u}/roles", new ActualizarRolesUsuarioRequest(Array.Empty<Guid>()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.PutAsJsonAsync($"/admin/tenants/{tA}/usuarios/{u}/cargos", new ActualizarCargosUsuarioRequest(new[] { "x" }))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.PostAsJsonAsync($"/admin/tenants/{tA}/usuarios/{u}/revocar", new RevocarAccesoRequest("x"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.PostAsync($"/admin/tenants/{tA}/usuarios/{u}/reactivar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.PutAsync($"/admin/tenants/{tA}/invitaciones/{enB.Invitacion!.Id}/reenviar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.DeleteAsync($"/admin/tenants/{tA}/invitaciones/{enB.Invitacion.Id}")).StatusCode);

        // Aislamiento de listas: A no ve usuarios ni personas de B.
        var enA = await CrearAsync(tA, new AdminCrearUsuarioRequest(null, Nueva("SoloA", Email("soloa")), null, null, false));
        var listaA = await _api.GetFromJsonAsync<List<UsuarioListaDto>>($"/admin/tenants/{tA}/usuarios");
        Assert.Contains(listaA!, x => x.UsuarioTenantId == enA.UsuarioTenantId);
        Assert.DoesNotContain(listaA!, x => x.UsuarioTenantId == enB.UsuarioTenantId);
        var personasA = await _api.GetFromJsonAsync<List<PersonaResumenDto>>($"/admin/tenants/{tA}/personas");
        Assert.DoesNotContain(personasA!, p => p.Id == enB.PersonaId);

        // Y el usuario de B sigue intacto (rol propio, sin revocar).
        await using var dbB = await TenantDbAsync(tB);
        var utB = await dbB.UsuariosTenant.SingleAsync(x => x.Id == u);
        Assert.Equal(rolB, utB.RolId);
        Assert.Equal(EstadoUsuarioTenant.Pendiente, utB.Estado);
    }

    [Fact]
    public async Task Roles_cargos_revocar_reactivar_y_gestion_de_invitaciones()
    {
        var tA = await SeedTenantAsync("[TEST] API usuarios gestion");
        var roles = await _api.GetFromJsonAsync<List<AdminRolTenantDto>>($"/admin/tenants/{tA}/roles");
        var admin = roles!.Single(r => r.Nombre == "Administrador");
        var otro = roles!.First(r => r.Activo && r.Tipo == TipoRol.Base && r.Nombre != "Administrador" && !r.SoloDirectorio);
        var c = await CrearAsync(tA, new AdminCrearUsuarioRequest(null, Nueva("Gestion", Email("gestion")), new[] { admin.Id }, null, true));
        var u = c.UsuarioTenantId;

        Assert.Equal(HttpStatusCode.NoContent, (await _api.PutAsJsonAsync($"/admin/tenants/{tA}/usuarios/{u}/roles", new ActualizarRolesUsuarioRequest(new[] { otro.Id, admin.Id }))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _api.PutAsJsonAsync($"/admin/tenants/{tA}/usuarios/{u}/cargos", new ActualizarCargosUsuarioRequest(new[] { "Revisor", "Tesorero" }))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.PutAsJsonAsync($"/admin/tenants/{tA}/usuarios/{u}/roles", new ActualizarRolesUsuarioRequest(new[] { Guid.NewGuid() }))).StatusCode);
        await using (var db = await TenantDbAsync(tA))
        {
            var ut = await db.UsuariosTenant.SingleAsync(x => x.Id == u);
            Assert.Equal(otro.Id, ut.RolId);   // el primero es el principal
            Assert.Equal(2, await db.UsuarioTenantRoles.CountAsync(x => x.UsuarioTenantId == u));
            Assert.Equal(2, await db.UsuarioTenantCargos.CountAsync(x => x.UsuarioTenantId == u));
        }

        Assert.Equal(HttpStatusCode.NoContent, (await _api.PostAsJsonAsync($"/admin/tenants/{tA}/usuarios/{u}/revocar", new RevocarAccesoRequest("Prueba de revocacion"))).StatusCode);
        Assert.Equal(EstadoUsuarioTenant.Inactivo, (await _api.GetFromJsonAsync<UsuarioDetalleDto>($"/admin/tenants/{tA}/usuarios/{u}"))!.Estado);
        Assert.Equal(HttpStatusCode.NoContent, (await _api.PostAsync($"/admin/tenants/{tA}/usuarios/{u}/reactivar", null)).StatusCode);
        Assert.Equal(EstadoUsuarioTenant.Activo, (await _api.GetFromJsonAsync<UsuarioDetalleDto>($"/admin/tenants/{tA}/usuarios/{u}"))!.Estado);

        // Invitaciones: reenviar regenera el token; cancelar la deja Cancelada; reenviar una cancelada -> 400.
        string tokenAntes;
        await using (var db = await TenantDbAsync(tA))
            tokenAntes = (await db.UsuarioInvitaciones.SingleAsync(i => i.Id == c.Invitacion!.Id)).Token;
        Assert.Equal(HttpStatusCode.NoContent, (await _api.PutAsync($"/admin/tenants/{tA}/invitaciones/{c.Invitacion!.Id}/reenviar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _api.DeleteAsync($"/admin/tenants/{tA}/invitaciones/{c.Invitacion.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _api.PutAsync($"/admin/tenants/{tA}/invitaciones/{c.Invitacion.Id}/reenviar", null)).StatusCode);
        await using (var db = await TenantDbAsync(tA))
        {
            var inv = await db.UsuarioInvitaciones.SingleAsync(i => i.Id == c.Invitacion.Id);
            Assert.NotEqual(tokenAntes, inv.Token);
            Assert.Equal(EstadoInvitacion.Cancelada, inv.Estado);
            // Cada accion queda en el log del Super Admin y en la auditoria del tenant con canal Api.
            foreach (var accion in new[] { "TENANT_USUARIO_ROLES", "TENANT_USUARIO_CARGOS", "TENANT_USUARIO_REVOCAR", "TENANT_USUARIO_REACTIVAR", "TENANT_INVITACION_REENVIAR", "TENANT_INVITACION_CANCELAR" })
                Assert.True(await db.SuperAdminLogs.AnyAsync(l => l.Accion == accion && l.EntidadAfectada!.StartsWith($"Tenant:{tA}/")), accion);
            var eventos = await db.AccesoAuditorias.Where(a => a.TenantId == tA && a.EntidadAfectadaId == u).ToListAsync();
            Assert.Contains(eventos, e => e.TipoEvento == TipoEventoAuditoria.AccesoRevocado && e.Canal == CanalAcceso.Api && e.ActorUsuarioId == _founderId);
            Assert.Contains(eventos, e => e.TipoEvento == TipoEventoAuditoria.AccesoOtorgado && e.Detalle!.Contains("via API"));
        }
    }

    // ===================== Helpers =====================

    /// <summary>
    /// ApiFactory con la base del contenedor de VERDAD: con minimal hosting, AddInfrastructure lee
    /// ConnectionStrings:Propia al registrar servicios, ANTES de los ConfigureAppConfiguration de la fabrica
    /// (que entonces dejan la API apuntando a appsettings.Development = propia_dev). UseSetting entra antes.
    /// </summary>
    private sealed class Factory : AuthFlowTests.ApiFactory
    {
        private readonly string _conn;
        public Factory(string conn) : base(conn) => _conn = conn;

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("ConnectionStrings:Propia", _conn);
            builder.UseSetting("Jobs:Enabled", "false");
        }
    }

    private PropiaDbContext OwnerDb()
        => new(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options, new TenantContext());

    /// <summary>Contexto con el tenant fijado en EF y en la RLS (conexion abierta), para verificar lo que quedo.</summary>
    private async Task<PropiaDbContext> TenantDbAsync(Guid tenantId)
    {
        var tc = new TenantContext();
        tc.SetTenant(tenantId);
        var db = new PropiaDbContext(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options, tc);
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync("SELECT set_config('app.tenant_id', {0}, false)", tenantId.ToString());
        return db;
    }

    private async Task<Guid> SeedTenantAsync(string nombre, EstadoCopropiedad estado = EstadoCopropiedad.Activa)
    {
        await using var ctx = OwnerDb();
        var t = new Tenant { Nombre = nombre, Estado = estado, EstadoCustodia = EstadoCustodia.ConAdmin };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        _tenants.Add(t.Id);
        return t.Id;
    }

    private async Task<Guid> SeedRolPropioAsync(Guid tenantId, string nombre)
    {
        await using var db = await TenantDbAsync(tenantId);
        var rol = new Rol { TenantId = tenantId, Nombre = nombre, Tipo = TipoRol.Personalizado, Activo = true };
        db.RolesCopropiedad.Add(rol);
        await db.SaveChangesAsync();
        return rol.Id;
    }

    private async Task<AdminCrearUsuarioResponse> CrearAsync(Guid tenantId, AdminCrearUsuarioRequest req)
    {
        var r = await _api.PostAsJsonAsync($"/admin/tenants/{tenantId}/usuarios", req);
        Assert.True(r.StatusCode == HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await r.Content.ReadFromJsonAsync<AdminCrearUsuarioResponse>())!;
    }

    private string Email(string prefijo)
    {
        var e = $"api-usr-{prefijo}-{Guid.NewGuid():N}"[..30] + "@example.com";
        _emails.Add(e);
        return e;
    }

    private static string Doc() => $"9{Random.Shared.NextInt64(100_000_000, 999_999_999)}";

    private static AdminPersonaNuevaRequest Nueva(string nombre, string email)
        => new(nombre, "Prueba API", TipoDocumento.CC, Doc(), email, "3001234567");
}
