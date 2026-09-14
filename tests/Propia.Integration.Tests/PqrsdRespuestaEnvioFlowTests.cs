using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
/// G-07: enviar la respuesta oficial (POST .../respuestas/{rid}/enviar) debe pasar el expediente a
/// Respondida (o cerrarlo si ya hubo inconformidad), fijar RespuestaAdminAt e historial. Antes solo
/// marcaba la respuesta como Enviada y el expediente se quedaba En gestion: la ventana de inconformidad
/// nunca arrancaba y el cierre nocturno (que exige Estado==Respondida && RespuestaAdminAt != null) no lo
/// veia. El endpoint invoca <see cref="IPqrsdService.MarcarRespondidaAsync"/>; aqui se prueba esa logica.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class PqrsdRespuestaEnvioFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;
    private Guid _personaTestId;

    public PqrsdRespuestaEnvioFlowTests(PostgresFixture fx) => _fx = fx;

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
        sc.AddSingleton<INotificacionDispatcher, FakeNotificacionDispatcher>();
        sc.AddScoped<ICalendarioHabilService, CalendarioHabilService>();
        sc.AddScoped<Propia.Application.Tareas.ITareasService, Propia.Infrastructure.Tareas.TareasService>();
        sc.AddSingleton<Propia.Application.Documents.IMembreteDocumentBuilder, Propia.Infrastructure.Documents.MembreteDocumentBuilder>();
        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Enviar_respuesta_pasa_el_expediente_a_Respondida()
    {
        var tenantId = await SeedTenantAsync("G07 Respondida");
        await SeedPersonaAsync(tenantId);
        var (svc, _, scope) = Build(tenantId);
        using (scope)
        {
            var cats = await svc.ListarCategoriasAsync(CancellationToken.None);
            var x = await svc.RadicarAsync(new RadicarPqrsdRequest(
                TipoPqrsd.Peticion, cats[0].Id,
                "Solicito el reglamento interno de uso de zonas comunes y el conducto para reservarlas.",
                false, null), CancellationToken.None);
            await svc.TomarExpedienteAsync(x.Id, new TomarExpedienteRequest("Atendiendo"), CancellationToken.None);

            // Tomar deja el expediente En gestion; el envio es el paso que faltaba.
            var enGestion = await svc.GetExpedienteAsync(x.Id, CancellationToken.None);
            Assert.Equal(EstadoPqrsd.EnGestion, enGestion!.Estado);

            var ok = await svc.MarcarRespondidaAsync(x.Id,
                "<p>Se adjunta el reglamento vigente. Reservas con 48h por la porteria.</p>", CancellationToken.None);
            Assert.True(ok);
        }

        var exp = await LoadAsync(tenantId, e => true);
        Assert.Equal(EstadoPqrsd.Respondida, exp.Estado);
        Assert.NotNull(exp.RespuestaAdminAt);
        Assert.NotNull(exp.RespuestaAdmin);

        var historial = await ContarHistorialAsync(tenantId, EstadoPqrsd.Respondida);
        Assert.Equal(1, historial);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Enviar_respuesta_con_inconformidad_previa_cierra_el_expediente()
    {
        var tenantId = await SeedTenantAsync("G07 Definitiva");
        await SeedPersonaAsync(tenantId);
        var (svc, _, scope) = Build(tenantId);
        using (scope)
        {
            var cats = await svc.ListarCategoriasAsync(CancellationToken.None);
            var x = await svc.RadicarAsync(new RadicarPqrsdRequest(
                TipoPqrsd.Queja, cats[0].Id,
                "El gimnasio lleva dos semanas cerrado sin explicacion y sin fecha de reapertura.",
                false, null), CancellationToken.None);
            await svc.TomarExpedienteAsync(x.Id, new TomarExpedienteRequest(null), CancellationToken.None);
            // Primera respuesta (por el flujo de envio) -> Respondida
            await svc.MarcarRespondidaAsync(x.Id, "<p>El gimnasio cerro por mantenimiento del piso.</p>", CancellationToken.None);
            // El radicador queda inconforme -> vuelve a En gestion y habilita la respuesta definitiva
            await svc.ManifestarInconformidadAsync(x.Id,
                new ManifestarInconformidadRequest("Deberia haberse avisado con tiempo."), CancellationToken.None);

            var ok = await svc.MarcarRespondidaAsync(x.Id,
                "<p>Implementamos aviso automatico por WhatsApp para futuros cierres.</p>", CancellationToken.None);
            Assert.True(ok);
        }

        var exp = await LoadAsync(tenantId, e => true);
        Assert.Equal(EstadoPqrsd.Cerrada, exp.Estado);
        Assert.True(exp.Archivado);
        Assert.NotNull(exp.RespuestaDefinitiva);
        Assert.NotNull(exp.FechaCierre);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Reenviar_una_respuesta_ya_registrada_es_idempotente()
    {
        var tenantId = await SeedTenantAsync("G07 Idempotente");
        await SeedPersonaAsync(tenantId);
        var (svc, _, scope) = Build(tenantId);
        DateTimeOffset? primeraFecha;
        using (scope)
        {
            var cats = await svc.ListarCategoriasAsync(CancellationToken.None);
            var x = await svc.RadicarAsync(new RadicarPqrsdRequest(
                TipoPqrsd.Peticion, cats[0].Id,
                "Solicito el estado de cuenta del ultimo trimestre de la administracion.",
                false, null), CancellationToken.None);
            await svc.TomarExpedienteAsync(x.Id, new TomarExpedienteRequest(null), CancellationToken.None);

            await svc.MarcarRespondidaAsync(x.Id, "<p>Se adjunta el estado de cuenta.</p>", CancellationToken.None);
            primeraFecha = (await svc.GetExpedienteAsync(x.Id, CancellationToken.None))!.RespuestaAdminAt;

            // Reenvio: no debe volver a transicionar ni duplicar historial.
            await svc.MarcarRespondidaAsync(x.Id, "<p>Reenvio del mismo estado de cuenta.</p>", CancellationToken.None);
        }

        var exp = await LoadAsync(tenantId, e => true);
        Assert.Equal(EstadoPqrsd.Respondida, exp.Estado);
        Assert.Equal(primeraFecha, exp.RespuestaAdminAt);              // la fecha original no se piso
        Assert.Equal(1, await ContarHistorialAsync(tenantId, EstadoPqrsd.Respondida)); // un solo asiento

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Enviar_respuesta_HTML_larga_no_desborda_la_columna_varchar4000()
    {
        var tenantId = await SeedTenantAsync("G07 Larga");
        await SeedPersonaAsync(tenantId);
        var (svc, _, scope) = Build(tenantId);
        using (scope)
        {
            var cats = await svc.ListarCategoriasAsync(CancellationToken.None);
            var x = await svc.RadicarAsync(new RadicarPqrsdRequest(
                TipoPqrsd.Peticion, cats[0].Id,
                "Solicito el detalle completo del presupuesto anual y su ejecucion por cada rubro.",
                false, null), CancellationToken.None);
            await svc.TomarExpedienteAsync(x.Id, new TomarExpedienteRequest(null), CancellationToken.None);

            // H-1: cuerpo de TinyMCE muy por encima de 4000 caracteres, con etiquetas.
            var htmlLargo = "<p>" +
                string.Concat(Enumerable.Repeat("Detalle del rubro con su justificacion y soportes. ", 300)) + "</p>";
            Assert.True(htmlLargo.Length > 4000);

            var ok = await svc.MarcarRespondidaAsync(x.Id, htmlLargo, CancellationToken.None);
            Assert.True(ok);   // antes: PostgreSQL 22001 (varchar(4000) desbordado)
        }

        var exp = await LoadAsync(tenantId, e => true);
        Assert.Equal(EstadoPqrsd.Respondida, exp.Estado);
        Assert.NotNull(exp.RespuestaAdmin);
        Assert.True(exp.RespuestaAdmin!.Length <= 4000);          // recortado al ancho de la columna
        Assert.DoesNotContain("<", exp.RespuestaAdmin);           // texto plano, sin etiquetas

        await CleanTenant(tenantId);
    }

    // =======================================================================
    // Helpers (mismo patron que PqrsdFlowTests)
    // =======================================================================

    private (IPqrsdService svc, PropiaDbContext db, IServiceScope scope) Build(Guid tenantId)
    {
        var scope = _services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        ctx.SetTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var http = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var noti = scope.ServiceProvider.GetRequiredService<INotificacionDispatcher>();
        var tareas = scope.ServiceProvider.GetRequiredService<Propia.Application.Tareas.ITareasService>();
        var membrete = scope.ServiceProvider.GetRequiredService<Propia.Application.Documents.IMembreteDocumentBuilder>();
        return (new PqrsdService(db, ctx, http, noti, tareas, membrete), db, scope);
    }

    private HttpContext BuildFakeHttpContext(Guid personaId)
    {
        var ctx = new DefaultHttpContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("user_id", Guid.NewGuid().ToString()),
            new Claim("persona_id", personaId.ToString())
        }, "test"));
        return ctx;
    }

    private async Task<PqrsdExpediente> LoadAsync(Guid tenantId, Func<PqrsdExpediente, bool> _)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        return await ctx.PqrsdExpedientes.IgnoreQueryFilters().FirstAsync(x => x.TenantId == tenantId);
    }

    private async Task<int> ContarHistorialAsync(Guid tenantId, EstadoPqrsd estadoNuevo)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        return await ctx.PqrsdHistorialEstados.IgnoreQueryFilters()
            .CountAsync(h => h.TenantId == tenantId && h.EstadoNuevo == estadoNuevo);
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

    private async Task CleanTenant(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE pqrsd_historial_estados DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_historial_estados WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE pqrsd_historial_estados ENABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_adjuntos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_expedientes WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_configuracion_plazos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_categorias WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM notificaciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id = {_personaTestId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }
}
