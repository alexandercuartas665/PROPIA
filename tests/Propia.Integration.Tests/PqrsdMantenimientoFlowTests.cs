using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Propia.Application.Common;
using Propia.Application.Notificaciones;
using Propia.Application.Pqrsd;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Common;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Pqrsd;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Tests del servicio PqrsdMantenimientoService (cierre automatico tras ventana de
/// inconformidad). Spec 2.9 RN-06.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class PqrsdMantenimientoFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;
    private Guid _personaRadicador;

    public PqrsdMantenimientoFlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        _personaRadicador = Guid.NewGuid();
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        sc.AddSingleton<ITenantContext, TenantContext>();
        sc.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
        sc.AddScoped<TenantConnectionInterceptor>();
        sc.AddDbContext<PropiaDbContext>((sp, opts) =>
            opts.UseNpgsql(_fx.OwnerConnectionString)
                .AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>()));
        sc.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<PropiaDbContext>()
            .AddDefaultTokenProviders();
        sc.AddScoped<ICalendarioHabilService, CalendarioHabilService>();
        sc.AddSingleton<INotificacionDispatcher, FakeNotificacionDispatcher>();
        sc.AddScoped<PqrsdMantenimientoService>();
        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Cierra_automaticamente_expediente_Respondida_tras_ventana_inconformidad()
    {
        var tenantId = await SeedTenantAsync("PQRSD Cierre Auto 1");
        var (catId, plazoId) = await SeedCategoriaYPlazoAsync(tenantId, TipoPqrsd.Peticion, diasInconformidad: 10);

        // Expediente respondido hace 30 dias - ya vencio la ventana de 10 dias habiles.
        var expedienteId = await SeedExpedienteRespondidaAsync(tenantId, catId, TipoPqrsd.Peticion,
            respuestaHaceDias: 30);

        using var scope = _services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<PqrsdMantenimientoService>();
        var cerrados = await svc.CerrarVencidosTrasInconformidadAsync(CancellationToken.None);
        Assert.True(cerrados >= 1);

        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var verif = new PropiaDbContext(opts, new TenantContext());
        var exp = await verif.PqrsdExpedientes.IgnoreQueryFilters().FirstAsync(x => x.Id == expedienteId);
        Assert.Equal(EstadoPqrsd.Cerrada, exp.Estado);
        Assert.NotNull(exp.FechaCierre);
        Assert.Null(exp.CerradoPorUsuarioId); // cierre del sistema
        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task No_cierra_si_ventana_inconformidad_aun_vigente()
    {
        var tenantId = await SeedTenantAsync("PQRSD Cierre Auto 2");
        var (catId, _) = await SeedCategoriaYPlazoAsync(tenantId, TipoPqrsd.Consulta, diasInconformidad: 15);

        // Respondido hace 2 dias - aun en ventana
        var expedienteId = await SeedExpedienteRespondidaAsync(tenantId, catId, TipoPqrsd.Consulta,
            respuestaHaceDias: 2);

        using var scope = _services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<PqrsdMantenimientoService>();
        await svc.CerrarVencidosTrasInconformidadAsync(CancellationToken.None);

        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var verif = new PropiaDbContext(opts, new TenantContext());
        var exp = await verif.PqrsdExpedientes.IgnoreQueryFilters().FirstAsync(x => x.Id == expedienteId);
        Assert.Equal(EstadoPqrsd.Respondida, exp.Estado); // NO se cerro
        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task No_cierra_si_inconformidad_fue_activada()
    {
        var tenantId = await SeedTenantAsync("PQRSD Cierre Auto 3");
        var (catId, _) = await SeedCategoriaYPlazoAsync(tenantId, TipoPqrsd.Reclamo, diasInconformidad: 10);

        var expedienteId = await SeedExpedienteRespondidaAsync(tenantId, catId, TipoPqrsd.Reclamo,
            respuestaHaceDias: 30, inconformidadActivada: true);

        using var scope = _services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<PqrsdMantenimientoService>();
        var cerrados = await svc.CerrarVencidosTrasInconformidadAsync(CancellationToken.None);

        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var verif = new PropiaDbContext(opts, new TenantContext());
        var exp = await verif.PqrsdExpedientes.IgnoreQueryFilters().FirstAsync(x => x.Id == expedienteId);
        Assert.Equal(EstadoPqrsd.Respondida, exp.Estado);
        await CleanTenant(tenantId);
    }

    // =======================================================================
    // Helpers
    // =======================================================================

    private async Task<Guid> SeedTenantAsync(string nombre)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var t = new Tenant { Nombre = nombre, Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.ConAdmin };
        ctx.Tenants.Add(t);
        var p = new Persona
        {
            Id = _personaRadicador,
            TipoDocumento = TipoDocumento.CC,
            Documento = $"R{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = "Rad",
            Apellidos = "Mant",
            Email = $"rad.{Guid.NewGuid():N}@test.co",
            PerfilIncompleto = false
        };
        if (!await ctx.Personas.AnyAsync(x => x.Id == _personaRadicador))
            ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        return t.Id;
    }

    private async Task<(Guid catId, Guid plazoId)> SeedCategoriaYPlazoAsync(
        Guid tenantId, TipoPqrsd tipo, int diasInconformidad)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        var tc = new TenantContext(); tc.SetTenant(tenantId);
        await using var ctx = new PropiaDbContext(opts, tc);
        var cat = new PqrsdCategoria { TenantId = tenantId, Nombre = "Test", Orden = 1, Activa = true, EsPredeterminada = false };
        ctx.PqrsdCategorias.Add(cat);
        var plazo = new PqrsdConfiguracionPlazo
        {
            TenantId = tenantId,
            Tipo = tipo,
            DiasHabiles = 15,
            DiasInconformidad = diasInconformidad,
            NivelUrgencia = NivelUrgenciaPqrsd.Media
        };
        ctx.PqrsdConfiguracionPlazos.Add(plazo);
        await ctx.SaveChangesAsync();
        return (cat.Id, plazo.Id);
    }

    private async Task<Guid> SeedExpedienteRespondidaAsync(
        Guid tenantId, Guid catId, TipoPqrsd tipo, int respuestaHaceDias,
        bool inconformidadActivada = false)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        var tc = new TenantContext(); tc.SetTenant(tenantId);
        await using var ctx = new PropiaDbContext(opts, tc);
        var hoy = DateTimeOffset.UtcNow;
        var respuestaAt = hoy.AddDays(-respuestaHaceDias);
        var exp = new PqrsdExpediente
        {
            TenantId = tenantId,
            CategoriaId = catId,
            Tipo = tipo,
            Estado = EstadoPqrsd.Respondida,
            Descripcion = "Smoke test cierre auto trabajo nocturno PR3.",
            RadicadorPersonaId = _personaRadicador,
            NumeroRadicado = $"PQRS-2026-{Guid.NewGuid().GetHashCode() & 0x7FFF:0000}",
            FechaVencimiento = DateOnly.FromDateTime(respuestaAt.AddDays(15).UtcDateTime),
            RespuestaAdmin = "Respuesta de prueba",
            RespuestaAdminAt = respuestaAt,
            RespuestaAdminPorUsuarioId = Guid.NewGuid(),
            InconformidadTexto = inconformidadActivada ? "Inconforme con la respuesta" : null,
            InconformidadAt = inconformidadActivada ? hoy.AddDays(-1) : null,
            IdentidadReservada = false,
            TutelaActiva = false
        };
        ctx.PqrsdExpedientes.Add(exp);
        ctx.PqrsdHistorialEstados.Add(new PqrsdHistorialEstado
        {
            TenantId = tenantId,
            ExpedienteId = exp.Id,
            EstadoAnterior = EstadoPqrsd.EnGestion,
            EstadoNuevo = EstadoPqrsd.Respondida,
            ActorUsuarioId = Guid.NewGuid(),
            Origen = OrigenCambioEstado.Manual,
            Nota = "Respondida (seed test)"
        });
        await ctx.SaveChangesAsync();
        return exp.Id;
    }

    private async Task CleanTenant(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE pqrsd_historial_estados DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_historial_estados WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE pqrsd_historial_estados ENABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_expedientes WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_configuracion_plazos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_categorias WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM notificaciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }
}
