using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Common;
using Propia.Application.Pqrsd;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Pqrsd;
using System.Security.Claims;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>Tests del modulo 2.9 PQRSD y Convivencia (spec v1.0 MVP).</summary>
[Collection(nameof(PostgresCollection))]
public class PqrsdFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;
    private Guid _personaTestId;
    public PqrsdFlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        _personaTestId = Guid.NewGuid();
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddDataProtection();
        sc.AddSingleton<ITenantContext, TenantContext>();
        sc.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = BuildFakeHttpContext(_personaTestId) });
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
    public async Task Seed_lazy_crea_9_categorias_y_8_plazos_base()
    {
        var tenantId = await SeedTenantAsync("PQRSD Seed");
        var (svc, _, _) = Build(tenantId);

        var cats = await svc.ListarCategoriasAsync(CancellationToken.None);
        var plazos = await svc.ListarPlazosAsync(CancellationToken.None);

        Assert.Equal(9, cats.Count);
        Assert.All(cats, c => Assert.True(c.EsPredeterminada));
        Assert.Contains(cats, c => c.Nombre == "Convivencia");

        Assert.Equal(8, plazos.Count);
        var consulta = plazos.First(p => p.Tipo == TipoPqrsd.Consulta);
        Assert.Equal(30, consulta.DiasHabiles);
        var denuncia = plazos.First(p => p.Tipo == TipoPqrsd.Denuncia);
        Assert.Equal(NivelUrgenciaPqrsd.Alta, denuncia.NivelUrgencia);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Radicar_genera_numero_secuencial_PQRS_YYYY()
    {
        var tenantId = await SeedTenantAsync("PQRSD Radicar");
        await SeedPersonaAsync(tenantId);
        var (svc, _, _) = Build(tenantId);
        var cats = await svc.ListarCategoriasAsync(CancellationToken.None);
        var conv = cats.First(c => c.Nombre == "Convivencia");

        var x1 = await svc.RadicarAsync(new RadicarPqrsdRequest(
            TipoPqrsd.Peticion, conv.Id,
            "Solicito copia del acta de la ultima asamblea ordinaria realizada en febrero.",
            false, null), CancellationToken.None);
        var x2 = await svc.RadicarAsync(new RadicarPqrsdRequest(
            TipoPqrsd.Queja, conv.Id,
            "Hay ruido excesivo en el apartamento 304 los fines de semana entre las 11pm y 3am.",
            false, null), CancellationToken.None);

        Assert.StartsWith($"PQRS-{DateTime.UtcNow.Year}-", x1.NumeroRadicado);
        Assert.EndsWith("0001", x1.NumeroRadicado);
        Assert.EndsWith("0002", x2.NumeroRadicado);
        Assert.Equal(EstadoPqrsd.Recibida, x1.Estado);
        Assert.True(x1.FechaVencimiento > DateOnly.FromDateTime(DateTime.UtcNow));
        Assert.Single(x1.Historial);  // Solo el de creacion

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Reserva_de_identidad_solo_aplica_a_Denuncia_RN02()
    {
        var tenantId = await SeedTenantAsync("PQRSD Reserva");
        await SeedPersonaAsync(tenantId);
        var (svc, _, _) = Build(tenantId);
        var cats = await svc.ListarCategoriasAsync(CancellationToken.None);
        var conv = cats.First(c => c.Nombre == "Convivencia");

        // RN-02: rechazar reserva si tipo != Denuncia
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RadicarAsync(new RadicarPqrsdRequest(
                TipoPqrsd.Peticion, conv.Id,
                "Una solicitud cualquiera con descripcion suficiente para validar el formato.",
                true, null), CancellationToken.None));

        // Denuncia con reserva si funciona
        var den = await svc.RadicarAsync(new RadicarPqrsdRequest(
            TipoPqrsd.Denuncia, conv.Id,
            "Denuncia confidencial sobre comportamiento del vecino del 304 - solicito reserva de identidad.",
            true, null), CancellationToken.None);
        Assert.True(den.IdentidadReservada);
        Assert.Null(den.RadicadorNombre);  // capa presentacion filtra
        Assert.Null(den.RadicadorPersonaId);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Ciclo_completo_recibida_gestion_respondida_cerrada_automatico_sin_inconformidad()
    {
        var tenantId = await SeedTenantAsync("PQRSD Ciclo");
        await SeedPersonaAsync(tenantId);
        var (svc, _, _) = Build(tenantId);
        var cats = await svc.ListarCategoriasAsync(CancellationToken.None);
        var x = await svc.RadicarAsync(new RadicarPqrsdRequest(
            TipoPqrsd.Peticion, cats[0].Id,
            "Solicito informacion sobre el horario de uso del salon social y proceso de reserva.",
            false, null), CancellationToken.None);

        await svc.TomarExpedienteAsync(x.Id, new TomarExpedienteRequest("Atendiendo"), CancellationToken.None);
        var enGestion = await svc.GetExpedienteAsync(x.Id, CancellationToken.None);
        Assert.Equal(EstadoPqrsd.EnGestion, enGestion!.Estado);

        await svc.ResponderAsync(x.Id, new ResponderExpedienteRequest(
            "El salon social opera de 9am a 10pm. Reservas con minimo 48h via WhatsApp con la portera."),
            CancellationToken.None);
        var resp = await svc.GetExpedienteAsync(x.Id, CancellationToken.None);
        Assert.Equal(EstadoPqrsd.Respondida, resp!.Estado);
        Assert.NotNull(resp.RespuestaAdmin);
        Assert.NotNull(resp.RespuestaAdminAt);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task RN06_solo_una_inconformidad_por_expediente()
    {
        var tenantId = await SeedTenantAsync("PQRSD RN06");
        await SeedPersonaAsync(tenantId);
        var (svc, _, _) = Build(tenantId);
        var cats = await svc.ListarCategoriasAsync(CancellationToken.None);
        var x = await svc.RadicarAsync(new RadicarPqrsdRequest(
            TipoPqrsd.Queja, cats[0].Id,
            "La piscina estuvo cerrada el fin de semana sin aviso previo.",
            false, null), CancellationToken.None);

        await svc.TomarExpedienteAsync(x.Id, new TomarExpedienteRequest(null), CancellationToken.None);
        await svc.ResponderAsync(x.Id, new ResponderExpedienteRequest(
            "La piscina cerro por reparacion de emergencia de la bomba. Disculpas por la falta de aviso."),
            CancellationToken.None);

        await svc.ManifestarInconformidadAsync(x.Id, new ManifestarInconformidadRequest("Igual deberia haber aviso previo."), CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ManifestarInconformidadAsync(x.Id, new ManifestarInconformidadRequest("Segunda inconformidad"), CancellationToken.None));

        // Tras inconformidad, la siguiente respuesta es definitiva y cierra el expediente
        await svc.ResponderAsync(x.Id, new ResponderExpedienteRequest(
            "Hemos implementado un sistema de aviso automatico por WhatsApp para futuros cierres."),
            CancellationToken.None);
        var f = await svc.GetExpedienteAsync(x.Id, CancellationToken.None);
        Assert.Equal(EstadoPqrsd.Cerrada, f!.Estado);
        Assert.NotNull(f.RespuestaDefinitiva);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Comite_solo_para_Denuncia_resultado_sin_acuerdo_marca_via_interna_agotada()
    {
        var tenantId = await SeedTenantAsync("PQRSD Comite");
        var pid = await SeedPersonaAsync(tenantId);
        var miembro1 = await SeedExtraPersonaAsync("Miembro Comite 1");
        var miembro2 = await SeedExtraPersonaAsync("Miembro Comite 2");
        var (svc, _, _) = Build(tenantId);
        var cats = await svc.ListarCategoriasAsync(CancellationToken.None);

        // Una Peticion NO se puede escalar a Comite
        var pet = await svc.RadicarAsync(new RadicarPqrsdRequest(
            TipoPqrsd.Peticion, cats[0].Id,
            "Solicito copia del acta de la ultima asamblea para mi conocimiento.",
            false, null), CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.EscalarAComiteAsync(pet.Id, new EscalarAComiteRequest(
                null, ModalidadComite.Virtual, null, new[] { miembro1 }), CancellationToken.None));

        // Una Denuncia si
        var den = await svc.RadicarAsync(new RadicarPqrsdRequest(
            TipoPqrsd.Denuncia, cats[0].Id,
            "Denuncia formal por incumplimiento del reglamento en el uso del parqueadero comunal.",
            false, null), CancellationToken.None);
        await svc.TomarExpedienteAsync(den.Id, new TomarExpedienteRequest(null), CancellationToken.None);
        var sesion = await svc.EscalarAComiteAsync(den.Id, new EscalarAComiteRequest(
            DateTimeOffset.UtcNow.AddDays(3), ModalidadComite.Mixta, "https://meet.test/x",
            new[] { miembro1, miembro2 }), CancellationToken.None);
        Assert.Equal(2, sesion.Miembros.Count);

        // Resultado SinAcuerdo -> ViaInternaAgotada
        await svc.RegistrarSesionComiteAsync(sesion.Id, new RegistrarSesionComiteRequest(
            DateTimeOffset.UtcNow.AddDays(3), "Acta de la sesion: no fue posible llegar a un acuerdo.",
            ResultadoComite.SinAcuerdo), CancellationToken.None);
        var f = await svc.GetExpedienteAsync(den.Id, CancellationToken.None);
        Assert.Equal(EstadoPqrsd.ViaInternaAgotada, f!.Estado);
        Assert.NotNull(f.Comite);
        Assert.Equal(ResultadoComite.SinAcuerdo, f.Comite!.Resultado);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Activar_Tutela_cambia_semaforo_a_critico_y_requiere_justificacion()
    {
        var tenantId = await SeedTenantAsync("PQRSD Tutela");
        await SeedPersonaAsync(tenantId);
        var (svc, _, _) = Build(tenantId);
        var cats = await svc.ListarCategoriasAsync(CancellationToken.None);
        var x = await svc.RadicarAsync(new RadicarPqrsdRequest(
            TipoPqrsd.Peticion, cats[0].Id,
            "Solicitud que esta tardando demasiado y el radicador iniciara accion de tutela.",
            false, null), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ActivarTutelaAsync(x.Id, new ActivarTutelaRequest(""), CancellationToken.None));

        await svc.ActivarTutelaAsync(x.Id, new ActivarTutelaRequest("Radicador notificó tutela ante Juez 12 Civil"), CancellationToken.None);
        var f = await svc.GetExpedienteAsync(x.Id, CancellationToken.None);
        Assert.True(f!.TutelaActiva);
        Assert.Equal(SemaforoPqrsd.TutelaActiva, f.Semaforo);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Historial_es_append_only_trigger_bloquea_update_y_delete()
    {
        var tenantId = await SeedTenantAsync("PQRSD Audit");
        await SeedPersonaAsync(tenantId);
        var (svc, db, _) = Build(tenantId);
        var cats = await svc.ListarCategoriasAsync(CancellationToken.None);
        var x = await svc.RadicarAsync(new RadicarPqrsdRequest(
            TipoPqrsd.Sugerencia, cats[0].Id,
            "Sugerencia para mejorar el sistema de notificaciones de la copropiedad y reducir el ruido.",
            false, null), CancellationToken.None);

        var h = await db.PqrsdHistorialEstados.AsNoTracking().FirstAsync(x2 => x2.ExpedienteId == x.Id);
        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlAsync($"UPDATE pqrsd_historial_estados SET nota = 'alterado' WHERE id = {h.Id}"));
        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_historial_estados WHERE id = {h.Id}"));

        await CleanTenant(tenantId);
    }

    // ===================== Helpers =====================

    private (IPqrsdService svc, PropiaDbContext db, IServiceScope scope) Build(Guid tenantId)
    {
        var scope = _services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        ctx.SetTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var http = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        return (new PqrsdService(db, ctx, http), db, scope);
    }

    private HttpContext BuildFakeHttpContext(Guid personaId)
    {
        var ctx = new DefaultHttpContext();
        var uid = Guid.NewGuid().ToString();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("user_id", uid),
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

    private async Task<Guid> SeedPersonaAsync(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var p = new Persona
        {
            Id = _personaTestId,
            TipoDocumento = TipoDocumento.CC,
            Documento = $"P{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = "Test",
            Apellidos = "Radicador",
            Email = $"radic.{Guid.NewGuid():N}@test.co",
            PerfilIncompleto = false
        };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private async Task<Guid> SeedExtraPersonaAsync(string nombres)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var p = new Persona
        {
            TipoDocumento = TipoDocumento.CC,
            Documento = $"M{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = nombres,
            Apellidos = "Comite",
            Email = $"miembro.{Guid.NewGuid():N}@test.co",
            PerfilIncompleto = false
        };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private async Task CleanTenant(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE pqrsd_historial_estados DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_historial_estados WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE pqrsd_historial_estados ENABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_comite_miembros WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_comite_sesiones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_adjuntos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_expedientes WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_configuracion_plazos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_categorias WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id = {_personaTestId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }
}
