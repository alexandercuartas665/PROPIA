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

/// <summary>Tests del modulo 2.8 Asambleas y Organos de Gobierno (spec v1.0 MVP).</summary>
[Collection(nameof(PostgresCollection))]
public class AsambleaFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;
    private Guid _userId;
    private Guid _personaId;
    public AsambleaFlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        _userId = Guid.NewGuid();
        _personaId = Guid.NewGuid();
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddDataProtection();
        sc.AddSingleton<ITenantContext, TenantContext>();
        sc.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = BuildFakeHttpContext(_userId, _personaId) });
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
    public async Task Crear_sesion_valida_titulo_y_al_menos_un_punto()
    {
        var tenantId = await SeedTenantAsync("Asam Crear");
        var (svc, _, _) = Build(tenantId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearSesionAsync(new CrearSesionRequest(
                TipoSesion.AsambleaOrdinaria, ModalidadSesion.Virtual, "", DateTimeOffset.UtcNow.AddDays(10),
                null, null, new List<CrearPuntoRequest>()), CancellationToken.None));

        var s = await svc.CrearSesionAsync(new CrearSesionRequest(
            TipoSesion.AsambleaOrdinaria, ModalidadSesion.Virtual,
            "Asamblea Ordinaria 2026", DateTimeOffset.UtcNow.AddDays(10),
            null, "https://meet.test/abc",
            new[] { new CrearPuntoRequest(1, "Aprobacion presupuesto", null, true, TipoMayoria.Simple, 50m, ModalidadVoto.Publico, null) }),
            CancellationToken.None);

        Assert.Equal(EstadoSesion.Borrador, s.Estado);
        Assert.Single(s.Puntos);
        Assert.Equal(50m, s.QuorumRequeridoPct);  // 1ra convocatoria asamblea

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Citar_registra_participantes_desde_unidades_del_tenant()
    {
        var tenantId = await SeedTenantAsync("Asam Citar");
        await SeedPersonaConApplicationUser(tenantId);
        var unidades = await SeedUnidadesAsync(tenantId, 3);
        var (svc, db, _) = Build(tenantId);

        var s = await svc.CrearSesionAsync(new CrearSesionRequest(
            TipoSesion.AsambleaOrdinaria, ModalidadSesion.Virtual,
            "Asamblea 2026", DateTimeOffset.UtcNow.AddDays(10), null, "https://meet/x",
            new[] { new CrearPuntoRequest(1, "Punto unico", null, true, TipoMayoria.Simple, 50m, ModalidadVoto.Publico, null) }),
            CancellationToken.None);

        await svc.EnviarCitacionAsync(s.Id, new EnviarCitacionRequest(null), CancellationToken.None);
        var d = await svc.GetSesionAsync(s.Id, CancellationToken.None);

        Assert.Equal(EstadoSesion.Citada, d!.Estado);
        Assert.NotNull(d.FechaCitacionEnviada);
        Assert.Equal(3, d.Participantes.Count);  // 3 unidades

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Ciclo_completo_borrador_citada_encurso_cerrada_con_acta_borrador()
    {
        var tenantId = await SeedTenantAsync("Asam Ciclo");
        await SeedPersonaConApplicationUser(tenantId);
        await SeedUnidadesAsync(tenantId, 2);
        var (svc, _, _) = Build(tenantId);

        var s = await svc.CrearSesionAsync(new CrearSesionRequest(
            TipoSesion.AsambleaOrdinaria, ModalidadSesion.Virtual,
            "Asamblea 2026", DateTimeOffset.UtcNow.AddDays(10), null, null,
            new[] { new CrearPuntoRequest(1, "P1", null, false, TipoMayoria.Simple, 50m, ModalidadVoto.Publico, null) }),
            CancellationToken.None);

        await svc.EnviarCitacionAsync(s.Id, new EnviarCitacionRequest(null), CancellationToken.None);
        await svc.AbrirSalaAsync(s.Id, CancellationToken.None);
        var enCurso = await svc.GetSesionAsync(s.Id, CancellationToken.None);
        Assert.Equal(EstadoSesion.EnCurso, enCurso!.Estado);
        Assert.NotNull(enCurso.HoraApertura);

        await svc.CerrarSesionAsync(s.Id, new CerrarSesionRequest(true), CancellationToken.None);
        var cerrada = await svc.GetSesionAsync(s.Id, CancellationToken.None);
        Assert.Equal(EstadoSesion.Cerrada, cerrada!.Estado);
        Assert.True(cerrada.QuorumAlcanzado);
        Assert.NotNull(cerrada.Acta);  // borrador auto-generado
        Assert.Equal(EstadoActa.Borrador, cerrada.Acta!.Estado);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Votacion_ponderada_por_coeficiente_aprueba_si_pasa_mayoria_simple()
    {
        var tenantId = await SeedTenantAsync("Asam Votacion");
        await SeedPersonaConApplicationUser(tenantId);
        var unidades = await SeedUnidadesAsync(tenantId, 3, coeficientes: new[] { 0.4m, 0.3m, 0.3m });
        var (svc, _, _) = Build(tenantId);

        var s = await svc.CrearSesionAsync(new CrearSesionRequest(
            TipoSesion.AsambleaOrdinaria, ModalidadSesion.Virtual,
            "Asamblea con votacion", DateTimeOffset.UtcNow.AddDays(10), null, null,
            new[] { new CrearPuntoRequest(1, "Aprobar reglamento", null, true, TipoMayoria.Simple, 50m, ModalidadVoto.Publico, null) }),
            CancellationToken.None);
        await svc.EnviarCitacionAsync(s.Id, new EnviarCitacionRequest(null), CancellationToken.None);
        await svc.AbrirSalaAsync(s.Id, CancellationToken.None);

        // Check-in de todas las unidades
        foreach (var u in unidades)
            await svc.CheckInParticipanteAsync(s.Id, new CheckInParticipanteRequest(u, true), CancellationToken.None);

        var d = await svc.GetSesionAsync(s.Id, CancellationToken.None);
        var puntoId = d!.Puntos[0].Id;

        var v = await svc.AbrirVotacionAsync(s.Id, new AbrirVotacionRequest(puntoId), CancellationToken.None);

        // Unidad 1 (coef 0.4) vota Si, Unidad 2 (coef 0.3) vota Si, Unidad 3 (coef 0.3) vota No
        await svc.EmitirVotoAsync(v.Id, new EmitirVotoRequest(unidades[0], OpcionesVotoBase.Si), CancellationToken.None);
        await svc.EmitirVotoAsync(v.Id, new EmitirVotoRequest(unidades[1], OpcionesVotoBase.Si), CancellationToken.None);
        await svc.EmitirVotoAsync(v.Id, new EmitirVotoRequest(unidades[2], OpcionesVotoBase.No), CancellationToken.None);

        var resultado = await svc.CerrarVotacionAsync(v.Id, new CerrarVotacionRequest(), CancellationToken.None);
        // 0.7 a favor / 1.0 total = 70% > 50% -> Aprobado
        Assert.Equal(ResultadoVotacion.Aprobado, resultado.ResultadoFinal);
        Assert.Equal(EstadoVotacion.Cerrada, resultado.Estado);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task RN09_solo_un_voto_por_unidad_por_votacion()
    {
        var tenantId = await SeedTenantAsync("Asam RN09");
        await SeedPersonaConApplicationUser(tenantId);
        var unidades = await SeedUnidadesAsync(tenantId, 1, coeficientes: new[] { 1m });
        var (svc, _, _) = Build(tenantId);

        var s = await svc.CrearSesionAsync(new CrearSesionRequest(
            TipoSesion.AsambleaOrdinaria, ModalidadSesion.Virtual, "X",
            DateTimeOffset.UtcNow.AddDays(10), null, null,
            new[] { new CrearPuntoRequest(1, "P", null, true, TipoMayoria.Simple, 50m, ModalidadVoto.Publico, null) }),
            CancellationToken.None);
        await svc.EnviarCitacionAsync(s.Id, new EnviarCitacionRequest(null), CancellationToken.None);
        await svc.AbrirSalaAsync(s.Id, CancellationToken.None);
        await svc.CheckInParticipanteAsync(s.Id, new CheckInParticipanteRequest(unidades[0], true), CancellationToken.None);

        var d = await svc.GetSesionAsync(s.Id, CancellationToken.None);
        var v = await svc.AbrirVotacionAsync(s.Id, new AbrirVotacionRequest(d!.Puntos[0].Id), CancellationToken.None);

        await svc.EmitirVotoAsync(v.Id, new EmitirVotoRequest(unidades[0], OpcionesVotoBase.Si), CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.EmitirVotoAsync(v.Id, new EmitirVotoRequest(unidades[0], OpcionesVotoBase.No), CancellationToken.None));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Firmar_acta_genera_hash_SHA256_y_la_marca_inmutable()
    {
        var tenantId = await SeedTenantAsync("Asam Acta");
        await SeedPersonaConApplicationUser(tenantId);
        await SeedUnidadesAsync(tenantId, 1);
        var (svc, _, _) = Build(tenantId);

        var s = await svc.CrearSesionAsync(new CrearSesionRequest(
            TipoSesion.AsambleaOrdinaria, ModalidadSesion.Virtual, "X",
            DateTimeOffset.UtcNow.AddDays(10), null, null,
            new[] { new CrearPuntoRequest(1, "P", null, false, TipoMayoria.Simple, 50m, ModalidadVoto.Publico, null) }),
            CancellationToken.None);
        await svc.EnviarCitacionAsync(s.Id, new EnviarCitacionRequest(null), CancellationToken.None);
        await svc.AbrirSalaAsync(s.Id, CancellationToken.None);
        await svc.CerrarSesionAsync(s.Id, new CerrarSesionRequest(true), CancellationToken.None);

        var d = await svc.GetSesionAsync(s.Id, CancellationToken.None);
        var actaId = d!.Acta!.Id;

        await svc.FirmarActaAsync(actaId, new FirmarActaRequest(
            "Narrativa del secretario: la sesion transcurrio con normalidad.",
            TipoFirmaActa.ElectronicaNativa), CancellationToken.None);

        var firmada = await svc.GetSesionAsync(s.Id, CancellationToken.None);
        Assert.Equal(EstadoActa.Firmada, firmada!.Acta!.Estado);
        Assert.NotNull(firmada.Acta.HashDocumento);
        Assert.Equal(64, firmada.Acta.HashDocumento!.Length);  // SHA-256 hex = 64 chars
        Assert.NotNull(firmada.Acta.TimestampFirma);

        // RN-10: ya no es editable
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.FirmarActaAsync(actaId, new FirmarActaRequest("otra", TipoFirmaActa.ElectronicaNativa), CancellationToken.None));

        // Publicar
        await svc.PublicarActaAsync(actaId, new PublicarActaRequest(), CancellationToken.None);
        var pub = await svc.GetSesionAsync(s.Id, CancellationToken.None);
        Assert.NotNull(pub!.Acta!.PublicadaEn);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Log_de_quorum_es_append_only_trigger_bloquea_update_delete()
    {
        var tenantId = await SeedTenantAsync("Asam Quorum");
        await SeedPersonaConApplicationUser(tenantId);
        var unidades = await SeedUnidadesAsync(tenantId, 1);
        var (svc, db, _) = Build(tenantId);

        var s = await svc.CrearSesionAsync(new CrearSesionRequest(
            TipoSesion.AsambleaOrdinaria, ModalidadSesion.Virtual, "X",
            DateTimeOffset.UtcNow.AddDays(10), null, null,
            new[] { new CrearPuntoRequest(1, "P", null, false, TipoMayoria.Simple, 50m, ModalidadVoto.Publico, null) }),
            CancellationToken.None);
        await svc.EnviarCitacionAsync(s.Id, new EnviarCitacionRequest(null), CancellationToken.None);
        await svc.AbrirSalaAsync(s.Id, CancellationToken.None);
        await svc.CheckInParticipanteAsync(s.Id, new CheckInParticipanteRequest(unidades[0], true), CancellationToken.None);

        var log = await db.SesionQuorumLog.AsNoTracking().FirstAsync(l => l.SesionId == s.Id);
        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlAsync($"UPDATE sesion_quorum_log SET coeficiente = 0 WHERE id = {log.Id}"));
        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlAsync($"DELETE FROM sesion_quorum_log WHERE id = {log.Id}"));

        await CleanTenant(tenantId);
    }

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

    private async Task<Guid> SeedTenantAsync(string nombre)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var t = new Tenant { Nombre = nombre, Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.ConAdmin };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        return t.Id;
    }

    private async Task SeedPersonaConApplicationUser(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var p = new Persona
        {
            Id = _personaId,
            TipoDocumento = TipoDocumento.CC,
            Documento = $"A{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = "Admin",
            Apellidos = "Test",
            Email = $"admin.{Guid.NewGuid():N}@test.co",
            PerfilIncompleto = false
        };
        ctx.Personas.Add(p);
        var u = new ApplicationUser
        {
            Id = _userId,
            UserName = p.Email,
            Email = p.Email,
            NormalizedUserName = p.Email!.ToUpper(),
            NormalizedEmail = p.Email.ToUpper(),
            EmailConfirmed = true,
            PersonaId = _personaId,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        ctx.Users.Add(u);
        await ctx.SaveChangesAsync();
    }

    private async Task<Guid[]> SeedUnidadesAsync(Guid tenantId, int cantidad, decimal[]? coeficientes = null)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var torre = new Torre { TenantId = tenantId, Nombre = "T1", Descripcion = "Test" };
        ctx.Torres.Add(torre);
        var ids = new List<Guid>();
        for (int i = 0; i < cantidad; i++)
        {
            var coef = coeficientes is { Length: > 0 } && i < coeficientes.Length ? coeficientes[i] : 1m / cantidad;
            var u = new UnidadPrivada
            {
                TenantId = tenantId,
                Numero = $"{100 + i}",
                TorreId = torre.Id,
                CoeficientePropiedad = coef
            };
            ctx.UnidadesPrivadas.Add(u);
            ids.Add(u.Id);
        }
        await ctx.SaveChangesAsync();
        return ids.ToArray();
    }

    private async Task CleanTenant(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE sesion_quorum_log DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM sesion_quorum_log WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE sesion_quorum_log ENABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM votos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM votaciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM eleccion_candidatos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM elecciones_consejo WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM actas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM sesion_poderes WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM sesion_participantes WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM sesion_documentos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM sesion_puntos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM sesiones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM asamblea_config WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidades_privadas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM torres WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM asp_net_users WHERE id = {_userId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id = {_personaId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }
}
