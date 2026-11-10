using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Asambleas;
using Propia.Application.Auth;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Asambleas Ola 1.3: endpoint PUBLICO por token (PublicAsambleaController) sobre el API completo.
/// Fija que el token se valida contra el tenant de la URL, que el tenant se fija antes de tocar datos
/// y que los errores del servicio salen con el codigo correcto (401 / 403 / 400). El token se emite con
/// el mismo key ring de Data Protection de la app (como lo hara el admin).
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AsambleaPublicoHttpTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private AuthFlowTests.ApiFactory _factory = null!;
    private string? _connPrevia;
    private readonly List<Guid> _usuarios = new();
    public AsambleaPublicoHttpTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        // Program.cs lee la cadena de conexion de builder.Configuration ANTES de que se apliquen los
        // ConfigureAppConfiguration de la factory: sin esto la API apuntaria a la BD de appsettings
        // (propia_dev), no al contenedor. La variable de entorno si se lee al crear el builder. Usuario
        // de runtime (propia_app) para que el endpoint publico corra con RLS como en produccion.
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
    public async Task Token_invalido_o_de_otro_tenant_da_401()
    {
        var c = await SeedAsync("Asam Pub 401");
        var http = _factory.CreateClient();

        var basura = await http.GetAsync($"/api/publico/asambleas/{c.TenantId}/sesion/no-es-un-token");
        Assert.Equal(HttpStatusCode.Unauthorized, basura.StatusCode);

        var otroTenant = await http.GetAsync($"/api/publico/asambleas/{Guid.NewGuid()}/sesion/{c.TokenDueno}");
        Assert.Equal(HttpStatusCode.Unauthorized, otroTenant.StatusCode);

        await CleanAsync(c);
    }

    [Fact]
    public async Task Dueno_marca_presente_y_vota_por_el_enlace()
    {
        var c = await SeedAsync("Asam Pub Feliz");
        var http = _factory.CreateClient();
        var baseUrl = $"/api/publico/asambleas/{c.TenantId}/sesion/{c.TokenDueno}";

        var get = await http.GetAsync(baseUrl);
        Assert.True(get.IsSuccessStatusCode, $"{(int)get.StatusCode}: {await get.Content.ReadAsStringAsync()}");
        var vista = await get.Content.ReadFromJsonAsync<JsonElement>();
        var unidades = vista.GetProperty("misUnidades");
        Assert.Equal(1, unidades.GetArrayLength());
        Assert.Equal(c.UnidadDueno, unidades[0].GetProperty("unidadPrivadaId").GetGuid());

        var asis = await http.PostAsJsonAsync($"{baseUrl}/asistencia", new MiAsistenciaRequest(c.UnidadDueno, true));
        Assert.Equal(HttpStatusCode.OK, asis.StatusCode);

        var voto = await http.PostAsJsonAsync($"{baseUrl}/voto", new MiVotoRequest(c.VotacionId, c.UnidadDueno, OpcionesVotoBase.Si));
        Assert.Equal(HttpStatusCode.OK, voto.StatusCode);

        // RN-09 -> 400; unidad ajena -> 403.
        var repetido = await http.PostAsJsonAsync($"{baseUrl}/voto", new MiVotoRequest(c.VotacionId, c.UnidadDueno, OpcionesVotoBase.No));
        Assert.Equal(HttpStatusCode.BadRequest, repetido.StatusCode);
        var ajena = await http.PostAsJsonAsync($"{baseUrl}/asistencia", new MiAsistenciaRequest(c.UnidadAjena, true));
        Assert.Equal(HttpStatusCode.Forbidden, ajena.StatusCode);

        await using (var db = OwnerDb())
        {
            var v = await db.Votos.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.VotacionId == c.VotacionId);
            Assert.Equal(c.Dueno, v.PersonaId);
            Assert.Equal(0.6m, v.CoeficienteAportado);
        }

        await CleanAsync(c);
    }

    [Fact]
    public async Task Mi_enlace_logueado_solo_para_propietario_o_apoderado()
    {
        var c = await SeedAsync("Asam Pub MiEnlace");
        var ajena = NuevaPersona("Ajena");
        await using (var db = OwnerDb()) { db.Personas.Add(ajena); await db.SaveChangesAsync(); }
        var http = _factory.CreateClient();
        var url = $"/api/asambleas/{c.SesionId}/mi-enlace";

        var anonimo = await http.GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, anonimo.StatusCode);

        // Dueno con cuenta: recibe SU enlace (persona del JWT), valido para la pagina publica.
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await JwtAsync(c.Dueno, c.TenantId));
        var ok = await http.GetAsync(url);
        Assert.True(ok.IsSuccessStatusCode, $"{(int)ok.StatusCode}: {await ok.Content.ReadAsStringAsync()}");
        var enlace = await ok.Content.ReadFromJsonAsync<EnlaceResidenteDto>();
        Assert.StartsWith($"/publico/asambleas/{c.TenantId}/", enlace!.Ruta);
        var tokens = _factory.Services.GetRequiredService<IAsambleaTokenService>();
        Assert.Equal(new AsambleaEnlaceToken(c.TenantId, c.SesionId, c.Dueno), tokens.Validar(enlace.Token));

        // Usuario del tenant sin unidades habilitadas: 403 (sin boton en la ficha).
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await JwtAsync(ajena.Id, c.TenantId));
        var ajenaResp = await http.GetAsync(url);
        Assert.Equal(HttpStatusCode.Forbidden, ajenaResp.StatusCode);

        await CleanAsync(c, ajena.Id);
    }

    // ===================== Escenario =====================

    private record Caso(Guid TenantId, Guid SesionId, Guid VotacionId, Guid UnidadDueno, Guid UnidadAjena,
        Guid Dueno, Guid OtroDueno, string TokenDueno);

    /// <summary>Sesion EnCurso con 2 unidades (0.6 / 0.4), cada una con su propietario, y votacion abierta.</summary>
    private async Task<Caso> SeedAsync(string nombre)
    {
        await using var db = OwnerDb();
        var t = new Tenant { Nombre = nombre, Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.ConAdmin };
        db.Tenants.Add(t);
        var dueno = NuevaPersona("Dueno");
        var otro = NuevaPersona("Otro");
        db.Personas.AddRange(dueno, otro);
        var u1 = new UnidadPrivada { TenantId = t.Id, Numero = "101", CoeficientePropiedad = 0.6m };
        var u2 = new UnidadPrivada { TenantId = t.Id, Numero = "102", CoeficientePropiedad = 0.4m };
        db.UnidadesPrivadas.AddRange(u1, u2);
        db.UnidadPersonas.AddRange(
            new UnidadPersona { TenantId = t.Id, UnidadId = u1.Id, PersonaId = dueno.Id, Rol = RolUnidadPersona.Propietario },
            new UnidadPersona { TenantId = t.Id, UnidadId = u2.Id, PersonaId = otro.Id, Rol = RolUnidadPersona.Propietario });
        var s = new Sesion
        {
            TenantId = t.Id,
            Tipo = TipoSesion.AsambleaOrdinaria,
            Modalidad = ModalidadSesion.Virtual,
            Estado = EstadoSesion.EnCurso,
            Titulo = "Sesion publica",
            FechaSesion = DateTimeOffset.UtcNow,
            QuorumRequeridoPct = 50m,
            HoraApertura = DateTimeOffset.UtcNow
        };
        db.Sesiones.Add(s);
        var p = new SesionPunto { TenantId = t.Id, SesionId = s.Id, Numero = 1, Titulo = "Punto", Estado = EstadoPunto.EnVotacion };
        db.SesionPuntos.Add(p);
        db.SesionParticipantes.AddRange(
            new SesionParticipante { TenantId = t.Id, SesionId = s.Id, PersonaId = dueno.Id, UnidadPrivadaId = u1.Id, Coeficiente = 0.6m },
            new SesionParticipante { TenantId = t.Id, SesionId = s.Id, PersonaId = otro.Id, UnidadPrivadaId = u2.Id, Coeficiente = 0.4m });
        var v = new Votacion
        {
            TenantId = t.Id,
            SesionId = s.Id,
            PuntoId = p.Id,
            Estado = EstadoVotacion.Abierta,
            HoraApertura = DateTimeOffset.UtcNow,
            CoeficienteTotalSala = 1m
        };
        db.Votaciones.Add(v);
        await db.SaveChangesAsync();

        var tokens = _factory.Services.GetRequiredService<IAsambleaTokenService>();
        var token = tokens.Emitir(new AsambleaEnlaceToken(t.Id, s.Id, dueno.Id), DateTimeOffset.UtcNow.AddHours(1));
        return new Caso(t.Id, s.Id, v.Id, u1.Id, u2.Id, dueno.Id, otro.Id, token);
    }

    private static Persona NuevaPersona(string nombre) => new()
    {
        TipoDocumento = TipoDocumento.CC,
        Documento = $"P{Guid.NewGuid():N}".Substring(0, 18),
        Nombres = nombre,
        Apellidos = "Test",
        PerfilIncompleto = false
    };

    /// <summary>Usuario con cuenta para la persona y JWT real del API (mismo TokenService del login).</summary>
    private async Task<string> JwtAsync(Guid personaId, Guid tenantId)
    {
        var email = $"u.{Guid.NewGuid():N}@test.co";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            NormalizedUserName = email.ToUpper(),
            NormalizedEmail = email.ToUpper(),
            EmailConfirmed = true,
            PersonaId = personaId,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        await using (var db = OwnerDb()) { db.Users.Add(user); await db.SaveChangesAsync(); }
        _usuarios.Add(user.Id);
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ITokenService>().IssueAccessToken(user, tenantId).Token;
    }

    private PropiaDbContext OwnerDb()
        => new(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options, new TenantContext());

    private async Task CleanAsync(Caso c, params Guid[] otrasPersonas)
    {
        await using var ctx = OwnerDb();
        foreach (var u in _usuarios)
            await ctx.Database.ExecuteSqlAsync($"DELETE FROM asp_net_users WHERE id = {u}");
        foreach (var p in otrasPersonas)
            await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id = {p}");
        var tenantId = c.TenantId;
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE sesion_quorum_log DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM sesion_quorum_log WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE sesion_quorum_log ENABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM votos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM votaciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM sesion_participantes WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM sesion_puntos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM sesiones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidad_personas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidades_privadas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id = {c.Dueno} OR id = {c.OtroDueno}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }
}
