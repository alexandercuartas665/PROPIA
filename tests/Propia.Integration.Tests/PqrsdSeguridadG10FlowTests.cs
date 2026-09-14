using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Auth;
using Propia.Application.Common;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// G-10 (auditoria 2.9): (a) el endpoint /contexto NO debe filtrar la identidad del radicador cuando el
/// expediente tiene identidad reservada; (b) los GET que exponen datos de expediente exigen permiso Ver, y
/// mis-pqrsd (lo propio del residente) sigue abierto. Reusa la ApiFactory de AuthFlowTests (API completo).
/// </summary>
[Collection(nameof(PostgresCollection))]
public class PqrsdSeguridadG10FlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private AuthFlowTests.ApiFactory _factory = null!;

    public PqrsdSeguridadG10FlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        _factory = new AuthFlowTests.ApiFactory(_fx.OwnerConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task GET_de_expediente_exigen_Ver_pero_mis_pqrsd_queda_abierto()
    {
        Guid tenantId, resiPersonaId, admPersonaId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sp = scope.ServiceProvider;
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var db = sp.GetRequiredService<PropiaDbContext>();
            var tenant = new Tenant { Nombre = "CP G10 perm", Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.SinAdmin };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            tenantId = tenant.Id;

            var (resiEmail, resiPersona) = await CrearUsuarioAsync(userManager, db, "Resi", "Dente");
            await InsertUsuarioTenantConRLSAsync(db, tenantId, resiPersona.Id, "Residente");
            resiPersonaId = resiPersona.Id;

            var (admEmail, admPersona) = await CrearUsuarioAsync(userManager, db, "Admin", "Istrador");
            await InsertUsuarioTenantConRLSAsync(db, tenantId, admPersona.Id, "Administrador");
            admPersonaId = admPersona.Id;

            var resiClient = await LoginYSwitchAsync(resiEmail, tenantId);
            var admClient = await LoginYSwitchAsync(admEmail, tenantId);

            // Residente (sin Pqrs.Ver): la bandeja y el detalle dan 403.
            Assert.Equal(HttpStatusCode.Forbidden, (await resiClient.GetAsync("/api/pqrsd/bandeja")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await resiClient.GetAsync($"/api/pqrsd/{Guid.NewGuid()}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await resiClient.GetAsync($"/api/pqrsd/{Guid.NewGuid()}/contexto")).StatusCode);
            // Pero lo SUYO (mis-pqrsd) sigue abierto: no es 403.
            Assert.NotEqual(HttpStatusCode.Forbidden, (await resiClient.GetAsync("/api/pqrsd/mis-pqrsd")).StatusCode);
            // Un catalogo que alimenta formularios sigue abierto.
            Assert.NotEqual(HttpStatusCode.Forbidden, (await resiClient.GetAsync("/api/pqrsd/categorias")).StatusCode);

            // Administrador (bypass): no recibe 403 en la bandeja.
            Assert.NotEqual(HttpStatusCode.Forbidden, (await admClient.GetAsync("/api/pqrsd/bandeja")).StatusCode);
        }

        await CleanupUsuariosAsync(tenantId, resiPersonaId, admPersonaId);
    }

    [Fact]
    public async Task Contexto_con_identidad_reservada_no_filtra_al_radicador()
    {
        Guid tenantId, admPersonaId, radicadorId;
        Guid expReservado, expAbierto;
        string admEmail;
        var radEmail = $"ana-{Guid.NewGuid():N}@test.co";
        const string radTel = "3009998877";

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sp = scope.ServiceProvider;
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var db = sp.GetRequiredService<PropiaDbContext>();
            var tenant = new Tenant { Nombre = "CP G10 reserva", Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.SinAdmin };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            tenantId = tenant.Id;

            var (email, admPersona) = await CrearUsuarioAsync(userManager, db, "Admin", "G10");
            admEmail = email;
            await InsertUsuarioTenantConRLSAsync(db, tenantId, admPersona.Id, "Administrador");
            admPersonaId = admPersona.Id;

            // Radicador (persona global).
            radicadorId = Guid.NewGuid();
            db.Personas.Add(new Persona
            {
                Id = radicadorId,
                TipoDocumento = TipoDocumento.CC, Documento = $"R{Guid.NewGuid():N}".Substring(0, 18),
                Nombres = "Ana", Apellidos = "Anonima", Email = radEmail, Telefono = radTel
            });
            await db.SaveChangesAsync();

            // Expedientes (uno reservado, otro abierto) en el MISMO DbContext del host, con el tenant fijado.
            var tctx = sp.GetRequiredService<ITenantContext>();
            tctx.SetTenant(tenantId);
            var cat = new PqrsdCategoria { TenantId = tenantId, Nombre = "Test", Orden = 1, Activa = true };
            db.PqrsdCategorias.Add(cat);
            await db.SaveChangesAsync();
            var expR = NuevoExpediente(tenantId, cat.Id, radicadorId, reservada: true);
            var expA = NuevoExpediente(tenantId, cat.Id, radicadorId, reservada: false);
            db.PqrsdExpedientes.AddRange(expR, expA);
            await db.SaveChangesAsync();
            expReservado = expR.Id; expAbierto = expA.Id;
            tctx.Clear();
        }

        var admClient2 = await LoginYSwitchAsync(admEmail, tenantId);

        // Reservado: /contexto sin personas y sin datos de unidad -> no filtra documento/email/telefono ni radicador.
        var rResv = await admClient2.GetAsync($"/api/pqrsd/{expReservado}/contexto");
        Assert.Equal(HttpStatusCode.OK, rResv.StatusCode);
        using (var doc = JsonDocument.Parse(await rResv.Content.ReadAsStringAsync()))
        {
            var root = doc.RootElement;
            Assert.Equal(0, root.GetProperty("personas").GetArrayLength());
            Assert.Equal(JsonValueKind.Null, root.GetProperty("unidad").GetProperty("unidadId").ValueKind);
            var raw = doc.RootElement.GetRawText();
            Assert.DoesNotContain(radTel, raw);     // telefono del radicador NO aparece
            Assert.DoesNotContain(radEmail, raw);   // email del radicador NO aparece
        }

        // Control: un expediente ABIERTO si trae al radicador (para no romper el caso normal).
        var rAbi = await admClient2.GetAsync($"/api/pqrsd/{expAbierto}/contexto");
        Assert.Equal(HttpStatusCode.OK, rAbi.StatusCode);
        using (var doc = JsonDocument.Parse(await rAbi.Content.ReadAsStringAsync()))
        {
            var personas = doc.RootElement.GetProperty("personas");
            Assert.True(personas.GetArrayLength() >= 1);
            Assert.Contains(personas.EnumerateArray(), p => p.GetProperty("esRadicador").GetBoolean());
        }

        await CleanExpedientesAsync(tenantId, radicadorId);
        await CleanupUsuariosAsync(tenantId, admPersonaId);
    }

    // ===================== Helpers =====================

    private static PqrsdExpediente NuevoExpediente(Guid tenantId, Guid catId, Guid radId, bool reservada) => new()
    {
        TenantId = tenantId, CategoriaId = catId, Tipo = TipoPqrsd.Denuncia, Estado = EstadoPqrsd.EnGestion,
        Descripcion = "Denuncia de prueba para el contexto y la reserva de identidad (G-10).",
        RadicadorPersonaId = radId,
        NumeroRadicado = $"PQRSD-2026-{Guid.NewGuid().GetHashCode() & 0x7FFF:0000}",
        FechaVencimiento = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)),
        IdentidadReservada = reservada, TutelaActiva = false
    };

    private static async Task<(string email, Persona persona)> CrearUsuarioAsync(
        UserManager<ApplicationUser> userManager, PropiaDbContext db, string nombres, string apellidos)
    {
        var persona = new Persona
        {
            TipoDocumento = TipoDocumento.CC, Documento = $"D{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = nombres, Apellidos = apellidos
        };
        db.Personas.Add(persona);
        await db.SaveChangesAsync();
        var email = $"g10-{Guid.NewGuid():N}@propia.com.co";
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
        finally { if (opened) await conn.CloseAsync(); }
    }

    // La limpieza es best-effort: la BD del fixture es efimera y cada test usa ids nuevos. Se fija el tenant
    // para que la RLS permita borrar las filas del expediente, y se borra por el FK del radicador para no
    // dejar referencias colgando.
    private async Task CleanExpedientesAsync(Guid tenantId, Guid radicadorId)
    {
        try
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var ctx = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
            await ctx.Database.ExecuteSqlRawAsync($"DELETE FROM pqrsd_expedientes WHERE radicador_persona_id = '{radicadorId}'");
            await ctx.Database.ExecuteSqlRawAsync($"DELETE FROM pqrsd_categorias WHERE tenant_id = '{tenantId}'");
            await ctx.Database.ExecuteSqlRawAsync($"DELETE FROM personas WHERE id = '{radicadorId}'");
        }
        catch { /* best-effort */ }
    }

    private async Task CleanupUsuariosAsync(Guid tenantId, params Guid[] personaIds)
    {
        try
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var ctx = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
            var ids = string.Join(",", personaIds.Select(p => $"'{p}'"));
            await ctx.Database.ExecuteSqlRawAsync($"DELETE FROM asp_net_users WHERE persona_id IN ({ids})");
            await ctx.Database.ExecuteSqlRawAsync($"DELETE FROM usuarios_tenant WHERE persona_id IN ({ids})");
            await ctx.Database.ExecuteSqlRawAsync($"DELETE FROM personas WHERE id IN ({ids})");
            await ctx.Database.ExecuteSqlRawAsync($"DELETE FROM tenants WHERE id = '{tenantId}'");
        }
        catch { /* best-effort */ }
    }
}
