using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Propia.Application.Auth;
using Propia.Application.Common;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Restablecer contrasena (2.5): clave temporal del admin de la copropiedad + cambio obligatorio (el API
/// bloquea /api con pwd_chg), enlace al correo cuando el usuario esta en otra copropiedad, reglas (propio,
/// otro tenant, no-admin sobre admin) y "olvide mi contrasena" con el token del enlace.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class RestablecerClaveTests : IAsyncLifetime
{
    private const string ClaveInicial = "Password1234!";
    private readonly PostgresFixture _fx;
    private Factory _factory = null!;
    private readonly List<Guid> _tenants = new();
    private readonly List<string> _emails = new();

    public RestablecerClaveTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        _factory = new Factory(_fx.OwnerConnectionString);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
            // Guarda: la API debe estar en la base del contenedor, nunca en propia_dev.
            Assert.Equal(new Npgsql.NpgsqlConnectionStringBuilder(_fx.OwnerConnectionString).Port,
                new Npgsql.NpgsqlConnectionStringBuilder(db.Database.GetConnectionString()).Port);
        }
        return Task.CompletedTask;
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
                await ctx.Database.ExecuteSqlRawAsync("DELETE FROM asp_net_user_claims WHERE user_id IN (SELECT id FROM asp_net_users WHERE email = {0})", e);
                await ctx.Database.ExecuteSqlRawAsync("DELETE FROM personas WHERE id IN (SELECT persona_id FROM asp_net_users WHERE email = {0})", e);
                await ctx.Database.ExecuteSqlRawAsync("DELETE FROM asp_net_users WHERE email = {0}", e);
                await ctx.Database.ExecuteSqlRawAsync("DELETE FROM personas WHERE email = {0}", e);
            }
            await ctx.Database.ExecuteSqlRawAsync("SET session_replication_role = DEFAULT");
        }
        await _factory.DisposeAsync();
    }

    // ===================== Pruebas =====================

    [Fact]
    public async Task Admin_pone_clave_temporal_y_el_usuario_debe_cambiarla_antes_de_usar_el_api()
    {
        var tA = await SeedTenantAsync("[TEST] clave temporal");
        var admin = await SeedCuentaAsync(tA, "Administrador", "admin");
        var objetivo = await SeedCuentaAsync(tA, "Portal Residente", "residente");
        var http = await LoginAsync(admin.Email);

        var r = await http.PostAsJsonAsync($"/api/usuarios/{objetivo.UsuarioTenantId}/restablecer-clave",
            new RestablecerClaveUsuarioRequest("Temporal2026X"));
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        var res = (await r.Content.ReadFromJsonAsync<RestablecerClaveUsuarioResponse>())!;
        Assert.Equal("temporal", res.Modo);
        Assert.Equal("Temporal2026X", res.ClaveTemporal);

        // La clave vieja ya no sirve; la temporal entra marcada.
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/connect/token", new LoginRequest(objetivo.Email, ClaveInicial))).StatusCode);
        var loginTmp = (await (await anon.PostAsJsonAsync("/connect/login", new LoginRequest(objetivo.Email, "Temporal2026X")))
            .Content.ReadFromJsonAsync<UnifiedLoginResponse>())!;
        Assert.True(loginTmp.DebeCambiarClave);

        // Con ese JWT: /api bloqueado (403 debe_cambiar_clave); /connect/me abierto.
        var tmp = Cliente(loginTmp.AccessToken!);
        var bloqueado = await tmp.GetAsync("/api/usuarios");
        Assert.Equal(HttpStatusCode.Forbidden, bloqueado.StatusCode);
        Assert.Contains(ClaveTemporal.ErrorDebeCambiar, await bloqueado.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await tmp.GetAsync("/connect/me")).StatusCode);
        // El refresh conserva la marca.
        var refrescado = (await (await tmp.PostAsync("/connect/refresh", null)).Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.True(refrescado.DebeCambiarClave);

        // Cambio obligatorio: igual a la temporal -> 400; valida -> 204.
        Assert.Equal(HttpStatusCode.BadRequest, (await tmp.PostAsJsonAsync("/connect/cambiar-clave-obligatoria",
            new CambiarClaveObligatoriaRequest("Temporal2026X", "Temporal2026X"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await tmp.PostAsJsonAsync("/connect/cambiar-clave-obligatoria",
            new CambiarClaveObligatoriaRequest("corta", "corta"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await tmp.PostAsJsonAsync("/connect/cambiar-clave-obligatoria",
            new CambiarClaveObligatoriaRequest("MiClavePropia2026", "MiClavePropia2026"))).StatusCode);
        // Una sola vez: sin marca ya no se puede cambiar sin la clave actual.
        Assert.Equal(HttpStatusCode.BadRequest, (await tmp.PostAsJsonAsync("/connect/cambiar-clave-obligatoria",
            new CambiarClaveObligatoriaRequest("OtraClave20266", "OtraClave20266"))).StatusCode);

        // Entra con la propia, sin marca, y el /api ya no le responde debe_cambiar_clave.
        var loginNuevo = (await (await anon.PostAsJsonAsync("/connect/login", new LoginRequest(objetivo.Email, "MiClavePropia2026")))
            .Content.ReadFromJsonAsync<UnifiedLoginResponse>())!;
        Assert.False(loginNuevo.DebeCambiarClave);
        var libre = await Cliente(loginNuevo.AccessToken!).GetAsync("/api/usuarios");
        Assert.DoesNotContain(ClaveTemporal.ErrorDebeCambiar, await libre.Content.ReadAsStringAsync());

        // Auditoria del tenant: evento con el admin como actor.
        await using var db = await TenantDbAsync(tA);
        var aud = await db.AccesoAuditorias.SingleAsync(a => a.TenantId == tA && a.EntidadAfectadaId == objetivo.UsuarioTenantId
            && a.TipoEvento == TipoEventoAuditoria.ClaveRestablecida);
        Assert.Equal(admin.UserId, aud.ActorUsuarioId);
        Assert.Contains("temporal", aud.Detalle);
    }

    [Fact]
    public async Task Clave_vacia_la_genera_el_sistema_y_desbloquea_la_cuenta()
    {
        var tA = await SeedTenantAsync("[TEST] clave generada");
        var admin = await SeedCuentaAsync(tA, "Administrador", "admin");
        var objetivo = await SeedCuentaAsync(tA, "Portal Residente", "bloqueado");
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var u = (await um.FindByEmailAsync(objetivo.Email))!;
            await um.SetLockoutEndDateAsync(u, DateTimeOffset.UtcNow.AddMinutes(15));
        }

        var r = await (await LoginAsync(admin.Email)).PostAsJsonAsync($"/api/usuarios/{objetivo.UsuarioTenantId}/restablecer-clave",
            new RestablecerClaveUsuarioRequest(null));
        var res = (await r.Content.ReadFromJsonAsync<RestablecerClaveUsuarioResponse>())!;
        Assert.Equal("temporal", res.Modo);
        Assert.Null(ClaveTemporal.ValidarPolitica(res.ClaveTemporal));
        Assert.Equal(HttpStatusCode.OK, (await _factory.CreateClient().PostAsJsonAsync("/connect/token",
            new LoginRequest(objetivo.Email, res.ClaveTemporal!))).StatusCode);
    }

    [Fact]
    public async Task Usuario_que_tambien_esta_en_otra_copropiedad_recibe_enlace_y_su_clave_no_cambia()
    {
        var tA = await SeedTenantAsync("[TEST] clave multi A");
        var tB = await SeedTenantAsync("[TEST] clave multi B");
        var admin = await SeedCuentaAsync(tA, "Administrador", "admin");
        var objetivo = await SeedCuentaAsync(tA, "Portal Residente", "multi");
        await VincularAsync(tB, objetivo.PersonaId, "Administrador");
        var antes = _factory.Correos.Count;

        var r = await (await LoginAsync(admin.Email)).PostAsJsonAsync($"/api/usuarios/{objetivo.UsuarioTenantId}/restablecer-clave",
            new RestablecerClaveUsuarioRequest("Temporal2026X"));
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        var res = (await r.Content.ReadFromJsonAsync<RestablecerClaveUsuarioResponse>())!;
        Assert.Equal("enlace", res.Modo);
        Assert.Null(res.ClaveTemporal);
        Assert.StartsWith(objetivo.Email[..2] + "***@", res.CorreoEnmascarado);

        // Le llego el correo con el enlace; la temporal NO se fijo y su clave sigue igual.
        Assert.Contains(_factory.Correos.Skip(antes), c => c.Para == objetivo.Email && c.Html.Contains("/restablecer-contrasena?"));
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/connect/token", new LoginRequest(objetivo.Email, "Temporal2026X"))).StatusCode);
        var login = (await (await anon.PostAsJsonAsync("/connect/login", new LoginRequest(objetivo.Email, ClaveInicial)))
            .Content.ReadFromJsonAsync<UnifiedLoginResponse>())!;
        Assert.False(login.DebeCambiarClave);
    }

    [Fact]
    public async Task Reglas_propio_otro_tenant_inactivo_y_no_admin_sobre_admin()
    {
        var tA = await SeedTenantAsync("[TEST] clave reglas A");
        var tB = await SeedTenantAsync("[TEST] clave reglas B");
        var admin = await SeedCuentaAsync(tA, "Administrador", "admin");
        var otroAdmin = await SeedCuentaAsync(tA, "Administrador", "admin2");
        var residente = await SeedCuentaAsync(tA, "Portal Residente", "res");
        var deB = await SeedCuentaAsync(tB, "Portal Residente", "deb");
        var http = await LoginAsync(admin.Email);

        // Su propia clave -> 400 (va por Mi perfil).
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync($"/api/usuarios/{admin.UsuarioTenantId}/restablecer-clave",
            new RestablecerClaveUsuarioRequest("Temporal2026X"))).StatusCode);
        // Usuario de otra copropiedad -> 404 y su clave intacta.
        Assert.Equal(HttpStatusCode.NotFound, (await http.PostAsJsonAsync($"/api/usuarios/{deB.UsuarioTenantId}/restablecer-clave",
            new RestablecerClaveUsuarioRequest("Temporal2026X"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _factory.CreateClient().PostAsJsonAsync("/connect/token", new LoginRequest(deB.Email, ClaveInicial))).StatusCode);
        // Clave que no cumple la politica -> 400.
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync($"/api/usuarios/{residente.UsuarioTenantId}/restablecer-clave",
            new RestablecerClaveUsuarioRequest("corta"))).StatusCode);

        // Un rol propio con permiso de editar Usuarios (no Administrador) no puede con un Administrador.
        var rolGestor = await SeedRolConPermisoUsuariosAsync(tA);
        var gestor = await SeedCuentaAsync(tA, null, "gestor", rolGestor);
        var hg = await LoginAsync(gestor.Email);
        var contraAdmin = await hg.PostAsJsonAsync($"/api/usuarios/{otroAdmin.UsuarioTenantId}/restablecer-clave",
            new RestablecerClaveUsuarioRequest("Temporal2026X"));
        Assert.Equal(HttpStatusCode.BadRequest, contraAdmin.StatusCode);
        Assert.Contains("Administrador", await contraAdmin.Content.ReadAsStringAsync());
        // ...pero si con un residente.
        var contraRes = await hg.PostAsJsonAsync($"/api/usuarios/{residente.UsuarioTenantId}/restablecer-clave",
            new RestablecerClaveUsuarioRequest("Temporal2026X"));
        Assert.True(contraRes.IsSuccessStatusCode, await contraRes.Content.ReadAsStringAsync());

        // Sin permiso de editar Usuarios -> 403.
        var hr = await LoginAsync(otroAdmin.Email);
        Assert.True((await hr.PostAsJsonAsync($"/api/usuarios/{gestor.UsuarioTenantId}/restablecer-clave",
            new RestablecerClaveUsuarioRequest("Temporal2026X"))).IsSuccessStatusCode);
        var resLogin = (await (await _factory.CreateClient().PostAsJsonAsync("/connect/token", new LoginRequest(residente.Email, "Temporal2026X")))
            .Content.ReadFromJsonAsync<LoginResponse>())!;
        // El residente con clave temporal ni siquiera llega al filtro de permisos: 403 debe_cambiar_clave.
        var sinPermiso = await Cliente(resLogin.AccessToken).PostAsJsonAsync($"/api/usuarios/{admin.UsuarioTenantId}/restablecer-clave",
            new RestablecerClaveUsuarioRequest("Temporal2026X"));
        Assert.Equal(HttpStatusCode.Forbidden, sinPermiso.StatusCode);

        // Usuario inactivo -> 400.
        await using (var db = await TenantDbAsync(tA))
        {
            var ut = await db.UsuariosTenant.SingleAsync(u => u.Id == residente.UsuarioTenantId);
            ut.Estado = EstadoUsuarioTenant.Inactivo;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync($"/api/usuarios/{residente.UsuarioTenantId}/restablecer-clave",
            new RestablecerClaveUsuarioRequest("Temporal2026X"))).StatusCode);
    }

    [Fact]
    public async Task Lista_marca_cuenta_propia_por_persona_aunque_el_correo_del_directorio_no_coincida()
    {
        // Regresion prod 2026-11-20: TieneCuentaPropia se calculaba SOLO por correo exacto; una persona sin
        // correo en el Directorio (o con otra capitalizacion) salia "sin cuenta" y la ficha ocultaba
        // "Restablecer contrasena" aunque el usuario estuviera activo y con cuenta.
        var tA = await SeedTenantAsync("[TEST] cuenta propia");
        var admin = await SeedCuentaAsync(tA, "Administrador", "admin");
        var sinCorreo = await SeedCuentaAsync(tA, "Portal Residente", "sincorreo");
        var mayus = await SeedCuentaAsync(tA, "Portal Residente", "mayus");
        await using (var ctx = OwnerDb())
        {
            await ctx.Database.ExecuteSqlRawAsync("UPDATE personas SET email = NULL WHERE id = {0}", sinCorreo.PersonaId);
            await ctx.Database.ExecuteSqlRawAsync("UPDATE personas SET email = upper(email) WHERE id = {0}", mayus.PersonaId);
        }

        var http = await LoginAsync(admin.Email);
        var lista = (await http.GetFromJsonAsync<List<Propia.Application.UsuariosAccesos.UsuarioListaDto>>("/api/usuarios"))!;
        Assert.True(lista.Single(u => u.UsuarioTenantId == sinCorreo.UsuarioTenantId).TieneCuentaPropia);
        Assert.True(lista.Single(u => u.UsuarioTenantId == mayus.UsuarioTenantId).TieneCuentaPropia);

        // Y el restablecimiento funciona para el que no tiene correo en el Directorio.
        var r = await http.PostAsJsonAsync($"/api/usuarios/{sinCorreo.UsuarioTenantId}/restablecer-clave",
            new RestablecerClaveUsuarioRequest("Temporal2026X"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task Olvide_mi_contrasena_envia_enlace_de_un_solo_uso_y_no_revela_cuentas()
    {
        var tA = await SeedTenantAsync("[TEST] olvide clave");
        var u = await SeedCuentaAsync(tA, "Portal Residente", "olvido");
        var anon = _factory.CreateClient();
        var antes = _factory.Correos.Count;

        // Correo inexistente: misma respuesta (202) y no sale nada.
        Assert.Equal(HttpStatusCode.Accepted, (await anon.PostAsJsonAsync("/connect/recuperar-clave",
            new SolicitarRecuperacionRequest("nadie-" + Guid.NewGuid().ToString("N")[..8] + "@example.com", null))).StatusCode);
        Assert.Equal(antes, _factory.Correos.Count);

        Assert.Equal(HttpStatusCode.Accepted, (await anon.PostAsJsonAsync("/connect/recuperar-clave",
            new SolicitarRecuperacionRequest(u.Email, "http://localhost:5105/"))).StatusCode);
        var correo = _factory.Correos.Skip(antes).Single(c => c.Para == u.Email);
        var link = System.Text.RegularExpressions.Regex.Match(correo.Html, "href=\"([^\"]+)\"").Groups[1].Value;
        Assert.StartsWith("http://localhost:5105/restablecer-contrasena?", link);
        var qs = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(link).Query);

        // Token alterado -> 400; valido -> 204; reusado -> 400.
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsJsonAsync("/connect/restablecer-clave",
            new RestablecerConTokenRequest(u.Email, qs["token"] + "x", "NuevaClave2026A", "NuevaClave2026A"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await anon.PostAsJsonAsync("/connect/restablecer-clave",
            new RestablecerConTokenRequest(qs["email"]!, qs["token"]!, "NuevaClave2026A", "NuevaClave2026A"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsJsonAsync("/connect/restablecer-clave",
            new RestablecerConTokenRequest(qs["email"]!, qs["token"]!, "OtraClave2026B", "OtraClave2026B"))).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/connect/token", new LoginRequest(u.Email, ClaveInicial))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anon.PostAsJsonAsync("/connect/token", new LoginRequest(u.Email, "NuevaClave2026A"))).StatusCode);
    }

    // ===================== Helpers =====================

    private sealed record Cuenta(Guid UserId, Guid PersonaId, Guid UsuarioTenantId, string Email);

    public sealed record CorreoEnviado(string Para, string Asunto, string Html);

    private sealed class FakeEmailSender : IEmailSender
    {
        private readonly ConcurrentQueue<CorreoEnviado> _cola;
        public FakeEmailSender(ConcurrentQueue<CorreoEnviado> cola) => _cola = cola;
        public Task<EmailSendResult> SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            _cola.Enqueue(new CorreoEnviado(toEmail, subject, htmlBody));
            return Task.FromResult(new EmailSendResult(true, null));
        }
    }

    /// <summary>ApiFactory sobre la base del contenedor (UseSetting) y con un IEmailSender que captura los correos.</summary>
    private sealed class Factory : AuthFlowTests.ApiFactory
    {
        private readonly string _conn;
        public ConcurrentQueue<CorreoEnviado> Correos { get; } = new();
        public Factory(string conn) : base(conn) => _conn = conn;

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("ConnectionStrings:Propia", _conn);
            builder.UseSetting("Jobs:Enabled", "false");
            builder.UseSetting("App:PublicBaseUrl", "");
            builder.ConfigureTestServices(s =>
            {
                s.RemoveAll<IEmailSender>();
                s.AddSingleton<IEmailSender>(new FakeEmailSender(Correos));
            });
        }
    }

    private HttpClient Cliente(string jwt)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        return c;
    }

    private async Task<HttpClient> LoginAsync(string email)
    {
        var resp = await _factory.CreateClient().PostAsJsonAsync("/connect/token", new LoginRequest(email, ClaveInicial));
        Assert.True(resp.IsSuccessStatusCode, $"login {email}: {(int)resp.StatusCode}");
        var login = (await resp.Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.NotNull(login.ActiveTenantId);
        return Cliente(login.AccessToken);
    }

    private PropiaDbContext OwnerDb()
        => new(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options, new TenantContext());

    private async Task<PropiaDbContext> TenantDbAsync(Guid tenantId)
    {
        var tc = new TenantContext();
        tc.SetTenant(tenantId);
        var db = new PropiaDbContext(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options, tc);
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync("SELECT set_config('app.tenant_id', {0}, false)", tenantId.ToString());
        return db;
    }

    private async Task<Guid> SeedTenantAsync(string nombre)
    {
        await using var ctx = OwnerDb();
        var t = new Tenant { Nombre = nombre, Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.ConAdmin };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        _tenants.Add(t.Id);
        return t.Id;
    }

    /// <summary>Persona + cuenta (clave inicial) + usuario ACTIVO en la copropiedad con el rol dado.</summary>
    private async Task<Cuenta> SeedCuentaAsync(Guid tenantId, string? rolBase, string prefijo, Guid? rolId = null)
    {
        var email = $"clave-{prefijo}-{Guid.NewGuid():N}"[..26] + "@example.com";
        _emails.Add(email);
        var persona = new Persona
        {
            Nombres = "Prueba " + prefijo,
            Apellidos = "Clave",
            TipoDocumento = TipoDocumento.CC,
            Documento = $"8{Random.Shared.NextInt64(100_000_000, 999_999_999)}",
            Email = email
        };
        await using (var ctx = OwnerDb())
        {
            ctx.Personas.Add(persona);
            await ctx.SaveChangesAsync();
        }
        Guid userId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true, PersonaId = persona.Id };
            var r = await um.CreateAsync(user, ClaveInicial);
            Assert.True(r.Succeeded, string.Join("; ", r.Errors.Select(e => e.Description)));
            userId = user.Id;
        }
        var utId = await VincularAsync(tenantId, persona.Id, rolBase, rolId);
        return new Cuenta(userId, persona.Id, utId, email);
    }

    private async Task<Guid> VincularAsync(Guid tenantId, Guid personaId, string? rolBase, Guid? rolId = null)
    {
        await using var db = await TenantDbAsync(tenantId);
        var rol = rolId is Guid id
            ? await db.RolesCopropiedad.SingleAsync(r => r.Id == id)
            : await db.RolesCopropiedad.SingleAsync(r => r.TenantId == null && r.Nombre == rolBase);
        var ut = new UsuarioTenant
        {
            TenantId = tenantId,
            PersonaId = personaId,
            RolId = rol.Id,
            Rol = rol.Nombre,
            Estado = EstadoUsuarioTenant.Activo,
            FechaActivacion = DateTimeOffset.UtcNow
        };
        db.UsuariosTenant.Add(ut);
        db.UsuarioTenantRoles.Add(new UsuarioTenantRol { TenantId = tenantId, UsuarioTenantId = ut.Id, RolId = rol.Id });
        await db.SaveChangesAsync();
        return ut.Id;
    }

    private async Task<Guid> SeedRolConPermisoUsuariosAsync(Guid tenantId)
    {
        await using var db = await TenantDbAsync(tenantId);
        var rol = new Rol { TenantId = tenantId, Nombre = "[TEST] Gestor de usuarios", Tipo = TipoRol.Personalizado, Activo = true };
        db.RolesCopropiedad.Add(rol);
        foreach (var accion in new[] { AccionPermiso.Ver, AccionPermiso.Editar })
            db.RolPermisos.Add(new RolPermiso { RolId = rol.Id, ModuloCodigo = ModuloCodigo.UsuariosAccesos, Accion = accion, Habilitado = true, NivelDato = NivelDato.Copropiedad });
        await db.SaveChangesAsync();
        return rol.Id;
    }
}
