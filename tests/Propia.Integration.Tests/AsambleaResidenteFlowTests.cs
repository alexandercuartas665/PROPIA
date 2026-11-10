using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Asambleas;
using Propia.Application.Common;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Asambleas;
using Propia.Infrastructure.Persistence;
using System.Security.Claims;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Asambleas Ola 1.2: self-service del residente sobre el flujo admin existente. Fija el gate de
/// propiedad (Ley 675: vota el Propietario activo o su apoderado con poder Aprobado; el residente no),
/// que el voto/check-in registran a la persona que actua y que las reglas del admin siguen intactas.
/// Corre con el usuario de runtime (RLS activo).
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AsambleaResidenteFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;
    private Guid _adminUserId;
    private Guid _adminPersonaId;
    private readonly List<Guid> _personas = new();
    public AsambleaResidenteFlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        _adminUserId = Guid.NewGuid();
        _adminPersonaId = Guid.NewGuid();
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddDataProtection();
        sc.AddSingleton<ITenantContext, TenantContext>();
        sc.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = BuildFakeHttpContext(_adminUserId, _adminPersonaId) });
        sc.AddScoped<TenantConnectionInterceptor>();
        sc.AddDbContext<PropiaDbContext>((sp, opts) =>
            opts.UseNpgsql(_fx.AppConnectionString)
                .AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>()));
        sc.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<PropiaDbContext>()
            .AddDefaultTokenProviders();
        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Propietario_ve_solo_sus_unidades_marca_presente_y_vota_como_el_mismo()
    {
        var c = await EscenarioAsync("Asam Res Dueno");
        var (svc, db, _) = Build(c.TenantId);

        var vista = await svc.GetSesionParaResidenteAsync(c.SesionId, c.Dueno, CancellationToken.None);
        Assert.NotNull(vista);
        var mia = Assert.Single(vista!.MisUnidades);
        Assert.Equal(c.UnidadDueno, mia.UnidadPrivadaId);
        Assert.False(mia.PorPoder);
        Assert.Equal(3, vista.QuorumActual.ParticipantesTotales);

        Assert.True(await svc.MiCheckInAsync(c.SesionId, c.Dueno, c.UnidadDueno, true, CancellationToken.None));
        Assert.True(await svc.MiVotoAsync(c.SesionId, c.VotacionId, c.Dueno, c.UnidadDueno, OpcionesVotoBase.Si, CancellationToken.None));

        // Queda registrado el copropietario, no la persona placeholder del admin.
        var voto = await db.Votos.AsNoTracking().SingleAsync(v => v.VotacionId == c.VotacionId);
        Assert.Equal(c.Dueno, voto.PersonaId);
        Assert.Equal(0.5m, voto.CoeficienteAportado);
        var log = await db.SesionQuorumLog.AsNoTracking().SingleAsync(l => l.SesionId == c.SesionId);
        Assert.Equal(c.Dueno, log.PersonaId);

        var despues = await svc.GetSesionParaResidenteAsync(c.SesionId, c.Dueno, CancellationToken.None);
        var u = Assert.Single(despues!.MisUnidades);
        Assert.True(u.Presente);
        Assert.Equal(OpcionesVotoBase.Si, Assert.Single(u.Votos).Opcion);
        Assert.Equal(1, despues.QuorumActual.ParticipantesPresentes);

        await CleanTenant(c.TenantId);
    }

    [Fact]
    public async Task RN09_segundo_voto_del_residente_se_rechaza()
    {
        var c = await EscenarioAsync("Asam Res RN09");
        var (svc, _, _) = Build(c.TenantId);
        await svc.MiCheckInAsync(c.SesionId, c.Dueno, c.UnidadDueno, true, CancellationToken.None);
        await svc.MiVotoAsync(c.SesionId, c.VotacionId, c.Dueno, c.UnidadDueno, OpcionesVotoBase.Si, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.MiVotoAsync(c.SesionId, c.VotacionId, c.Dueno, c.UnidadDueno, OpcionesVotoBase.No, CancellationToken.None));

        await CleanTenant(c.TenantId);
    }

    [Fact]
    public async Task Propietario_no_puede_actuar_sobre_unidad_ajena()
    {
        var c = await EscenarioAsync("Asam Res Ajena");
        var (svc, _, _) = Build(c.TenantId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.MiCheckInAsync(c.SesionId, c.Dueno, c.UnidadOtroDueno, true, CancellationToken.None));
        // Aunque la unidad ajena este presente (la marco el admin), el dueno de otra no la vota.
        await svc.CheckInParticipanteAsync(c.SesionId, new CheckInParticipanteRequest(c.UnidadOtroDueno, true), CancellationToken.None);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.MiVotoAsync(c.SesionId, c.VotacionId, c.Dueno, c.UnidadOtroDueno, OpcionesVotoBase.Si, CancellationToken.None));

        await CleanTenant(c.TenantId);
    }

    [Fact]
    public async Task Residente_no_propietario_no_vota_ni_ve_unidades()
    {
        var c = await EscenarioAsync("Asam Res Residente");
        var (svc, _, _) = Build(c.TenantId);

        var vista = await svc.GetSesionParaResidenteAsync(c.SesionId, c.Residente, CancellationToken.None);
        Assert.Empty(vista!.MisUnidades);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.MiCheckInAsync(c.SesionId, c.Residente, c.UnidadDueno, true, CancellationToken.None));

        await CleanTenant(c.TenantId);
    }

    [Fact]
    public async Task Vinculo_de_propietario_inactivo_no_habilita()
    {
        var c = await EscenarioAsync("Asam Res Inactivo");
        var (svc, _, _) = Build(c.TenantId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.MiCheckInAsync(c.SesionId, c.ExDueno, c.UnidadOtroDueno, true, CancellationToken.None));

        await CleanTenant(c.TenantId);
    }

    [Fact]
    public async Task Apoderado_con_poder_aprobado_vota_la_unidad_representada()
    {
        var c = await EscenarioAsync("Asam Res Apoderado");
        var (svc, db, _) = Build(c.TenantId);

        // Sin poder: no habilitado.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.MiCheckInAsync(c.SesionId, c.Apoderado, c.UnidadSinDueno, true, CancellationToken.None));

        // Poder en PDF queda Pendiente: sigue sin habilitar.
        var pendiente = await svc.OtorgarPoderAsync(c.SesionId,
            new OtorgarPoderRequest(c.UnidadSinDueno, c.Apoderado, TipoPoder.Pdf, "https://x/poder.pdf"), CancellationToken.None);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.MiCheckInAsync(c.SesionId, c.Apoderado, c.UnidadSinDueno, true, CancellationToken.None));

        // Aprobado: habilita solo esa unidad.
        await svc.DecidirPoderAsync(pendiente.Id, new DecidirPoderRequest(true, null), CancellationToken.None);
        var vista = await svc.GetSesionParaResidenteAsync(c.SesionId, c.Apoderado, CancellationToken.None);
        var u = Assert.Single(vista!.MisUnidades);
        Assert.True(u.PorPoder);

        Assert.True(await svc.MiCheckInAsync(c.SesionId, c.Apoderado, c.UnidadSinDueno, true, CancellationToken.None));
        Assert.True(await svc.MiVotoAsync(c.SesionId, c.VotacionId, c.Apoderado, c.UnidadSinDueno, OpcionesVotoBase.No, CancellationToken.None));
        var voto = await db.Votos.AsNoTracking().SingleAsync(v => v.VotacionId == c.VotacionId);
        Assert.Equal(c.Apoderado, voto.PersonaId);
        Assert.Equal(c.UnidadSinDueno, voto.UnidadPrivadaId);

        await CleanTenant(c.TenantId);
    }

    [Fact]
    public async Task Checkin_repetido_no_duplica_el_quorum_log()
    {
        var c = await EscenarioAsync("Asam Res Idem");
        var (svc, db, _) = Build(c.TenantId);

        await svc.MiCheckInAsync(c.SesionId, c.Dueno, c.UnidadDueno, true, CancellationToken.None);
        await svc.MiCheckInAsync(c.SesionId, c.Dueno, c.UnidadDueno, true, CancellationToken.None);

        Assert.Equal(1, await db.SesionQuorumLog.AsNoTracking().CountAsync(l => l.SesionId == c.SesionId));

        await CleanTenant(c.TenantId);
    }

    [Fact]
    public async Task Votacion_de_otra_sesion_no_se_acepta()
    {
        var c = await EscenarioAsync("Asam Res OtraSesion");
        var (svc, _, _) = Build(c.TenantId);
        await svc.MiCheckInAsync(c.SesionId, c.Dueno, c.UnidadDueno, true, CancellationToken.None);

        Assert.False(await svc.MiVotoAsync(Guid.NewGuid(), c.VotacionId, c.Dueno, c.UnidadDueno, OpcionesVotoBase.Si, CancellationToken.None));

        await CleanTenant(c.TenantId);
    }

    [Fact]
    public async Task Flujo_admin_conserva_la_persona_del_participante()
    {
        var c = await EscenarioAsync("Asam Res Admin");
        var (svc, db, _) = Build(c.TenantId);

        await svc.CheckInParticipanteAsync(c.SesionId, new CheckInParticipanteRequest(c.UnidadDueno, true), CancellationToken.None);
        await svc.EmitirVotoAsync(c.VotacionId, new EmitirVotoRequest(c.UnidadDueno, OpcionesVotoBase.Si), CancellationToken.None);

        var voto = await db.Votos.AsNoTracking().SingleAsync(v => v.VotacionId == c.VotacionId);
        Assert.Equal(_adminPersonaId, voto.PersonaId);

        await CleanTenant(c.TenantId);
    }

    [Fact]
    public async Task Sesion_en_borrador_no_es_visible_para_el_residente()
    {
        var tenantId = await SeedTenantAsync("Asam Res Borrador");
        await SeedAdminAsync();
        var (svc, _, _) = Build(tenantId);
        var s = await svc.CrearSesionAsync(new CrearSesionRequest(
            TipoSesion.AsambleaOrdinaria, ModalidadSesion.Virtual, "Borrador",
            DateTimeOffset.UtcNow.AddDays(10), null, null,
            new[] { new CrearPuntoRequest(1, "P", null, true, TipoMayoria.Simple, 50m, ModalidadVoto.Publico, null) }),
            CancellationToken.None);

        Assert.Null(await svc.GetSesionParaResidenteAsync(s.Id, _adminPersonaId, CancellationToken.None));

        await CleanTenant(tenantId);
    }

    // ===================== Escenario =====================

    /// <summary>
    /// 3 unidades (0.5 / 0.3 / 0.2): Dueno es Propietario de la 1; OtroDueno de la 2 (y ExDueno con
    /// vinculo inactivo); la 3 sin dueno. Residente vive en la 1. Sesion EnCurso con votacion abierta.
    /// </summary>
    private async Task<Escenario> EscenarioAsync(string nombre)
    {
        var tenantId = await SeedTenantAsync(nombre);
        await SeedAdminAsync();
        var unidades = await SeedUnidadesAsync(tenantId, new[] { 0.5m, 0.3m, 0.2m });
        var dueno = await SeedPersonaAsync("Dueno");
        var otroDueno = await SeedPersonaAsync("OtroDueno");
        var exDueno = await SeedPersonaAsync("ExDueno");
        var residente = await SeedPersonaAsync("Residente");
        var apoderado = await SeedPersonaAsync("Apoderado");
        await SeedVinculoAsync(tenantId, unidades[0], dueno, RolUnidadPersona.Propietario, true);
        await SeedVinculoAsync(tenantId, unidades[0], residente, RolUnidadPersona.Residente, true);
        await SeedVinculoAsync(tenantId, unidades[1], otroDueno, RolUnidadPersona.Propietario, true);
        await SeedVinculoAsync(tenantId, unidades[1], exDueno, RolUnidadPersona.Propietario, false);

        var (svc, _, _) = Build(tenantId);
        var s = await svc.CrearSesionAsync(new CrearSesionRequest(
            TipoSesion.AsambleaOrdinaria, ModalidadSesion.Virtual, "Asamblea residente",
            DateTimeOffset.UtcNow.AddDays(10), null, null,
            new[] { new CrearPuntoRequest(1, "Aprobar presupuesto", null, true, TipoMayoria.Simple, 50m, ModalidadVoto.Publico, null) }),
            CancellationToken.None);
        await svc.EnviarCitacionAsync(s.Id, new EnviarCitacionRequest(null), CancellationToken.None);
        await svc.AbrirSalaAsync(s.Id, CancellationToken.None);
        var d = await svc.GetSesionAsync(s.Id, CancellationToken.None);
        var v = await svc.AbrirVotacionAsync(s.Id, new AbrirVotacionRequest(d!.Puntos[0].Id), CancellationToken.None);

        return new Escenario(tenantId, s.Id, v.Id, unidades[0], unidades[1], unidades[2],
            dueno, otroDueno, exDueno, residente, apoderado);
    }

    private record Escenario(Guid TenantId, Guid SesionId, Guid VotacionId,
        Guid UnidadDueno, Guid UnidadOtroDueno, Guid UnidadSinDueno,
        Guid Dueno, Guid OtroDueno, Guid ExDueno, Guid Residente, Guid Apoderado);

    // ===================== Helpers =====================

    private (IAsambleaService svc, PropiaDbContext db, IServiceScope scope) Build(Guid tenantId)
    {
        var scope = _services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        ctx.SetTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var http = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        return (new AsambleaService(db, ctx, http), db, scope);
    }

    private static HttpContext BuildFakeHttpContext(Guid userId, Guid personaId)
    {
        var ctx = new DefaultHttpContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("user_id", userId.ToString()),
            new Claim("persona_id", personaId.ToString())
        }, "test"));
        return ctx;
    }

    private PropiaDbContext OwnerDb()
        => new(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options, new TenantContext());

    private async Task<Guid> SeedTenantAsync(string nombre)
    {
        await using var ctx = OwnerDb();
        var t = new Tenant { Nombre = nombre, Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.ConAdmin };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        return t.Id;
    }

    private async Task SeedAdminAsync()
    {
        await using var ctx = OwnerDb();
        var email = $"admin.{Guid.NewGuid():N}@test.co";
        ctx.Personas.Add(new Persona
        {
            Id = _adminPersonaId,
            TipoDocumento = TipoDocumento.CC,
            Documento = $"A{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = "Admin",
            Apellidos = "Test",
            Email = email,
            PerfilIncompleto = false
        });
        ctx.Users.Add(new ApplicationUser
        {
            Id = _adminUserId,
            UserName = email,
            Email = email,
            NormalizedUserName = email.ToUpper(),
            NormalizedEmail = email.ToUpper(),
            EmailConfirmed = true,
            PersonaId = _adminPersonaId,
            SecurityStamp = Guid.NewGuid().ToString()
        });
        await ctx.SaveChangesAsync();
    }

    private async Task<Guid> SeedPersonaAsync(string nombre)
    {
        await using var ctx = OwnerDb();
        var p = new Persona
        {
            TipoDocumento = TipoDocumento.CC,
            Documento = $"R{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = nombre,
            Apellidos = "Test",
            PerfilIncompleto = false
        };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        _personas.Add(p.Id);
        return p.Id;
    }

    private async Task<Guid[]> SeedUnidadesAsync(Guid tenantId, decimal[] coeficientes)
    {
        await using var ctx = OwnerDb();
        var ids = new List<Guid>();
        for (int i = 0; i < coeficientes.Length; i++)
        {
            var u = new UnidadPrivada { TenantId = tenantId, Numero = $"{100 + i}", CoeficientePropiedad = coeficientes[i] };
            ctx.UnidadesPrivadas.Add(u);
            ids.Add(u.Id);
        }
        await ctx.SaveChangesAsync();
        return ids.ToArray();
    }

    private async Task SeedVinculoAsync(Guid tenantId, Guid unidadId, Guid personaId, RolUnidadPersona rol, bool activo)
    {
        await using var ctx = OwnerDb();
        ctx.UnidadPersonas.Add(new UnidadPersona
        {
            TenantId = tenantId,
            UnidadId = unidadId,
            PersonaId = personaId,
            Rol = rol,
            Activo = activo
        });
        await ctx.SaveChangesAsync();
    }

    private async Task CleanTenant(Guid tenantId)
    {
        await using var ctx = OwnerDb();
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE sesion_quorum_log DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM sesion_quorum_log WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE sesion_quorum_log ENABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM votos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM votaciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM actas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM sesion_poderes WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM sesion_participantes WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM sesion_puntos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM sesiones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM asamblea_config WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidad_personas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidades_privadas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM asp_net_users WHERE id = {_adminUserId}");
        foreach (var id in _personas.Append(_adminPersonaId))
            await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id = {id}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }
}
