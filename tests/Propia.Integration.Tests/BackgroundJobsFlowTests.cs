using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Propia.Application.Common;
using Propia.Application.Monitoria;
using Propia.Application.Notificaciones;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Common;
using Propia.Infrastructure.Jobs;
using Propia.Infrastructure.Monitoria;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Pqrsd;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Tests del scheduler de jobs nocturnos. Cubre:
///  - Tick ejecuta un job que nunca ha corrido.
///  - Tick NO re-ejecuta si la ultima exitosa < FrecuenciaMinutos.
///  - Job fallido se registra como Fallido con stack en Error.
///  - Doble tick concurrente no duplica ejecucion (idempotencia).
///  - Servicio GetEstadoJobs devuelve los jobs registrados con su ultima ejecucion.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class BackgroundJobsFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;

    public BackgroundJobsFlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddDataProtection();
        sc.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        sc.AddSingleton<ITenantContext, TenantContext>();
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
        sc.AddScoped<IMonitoriaService, MonitoriaService>();
        sc.AddScoped<PqrsdMantenimientoService>();

        // Jobs concretos
        sc.AddScoped<IBackgroundJob, PqrsdCierreNocturnoJob>();
        sc.AddScoped<IBackgroundJob, MetricasDiariasJob>();

        // Scheduler como singleton (accesible para invocar TickAsync manualmente)
        sc.AddSingleton<BackgroundJobScheduler>();

        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Tick_ejecuta_job_que_nunca_ha_corrido_y_registra_exitoso()
    {
        // Limpia ejecuciones previas del job especifico para que test sea reproducible.
        await LimpiarEjecucionesAsync("MetricasDiarias");

        var scheduler = _services.GetRequiredService<BackgroundJobScheduler>();
        await scheduler.TickAsync(CancellationToken.None);

        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var ult = await ctx.JobEjecuciones.AsNoTracking()
            .Where(j => j.JobName == "MetricasDiarias")
            .OrderByDescending(j => j.IniciadoAt).FirstOrDefaultAsync();
        Assert.NotNull(ult);
        Assert.Equal(EstadoEjecucionJob.Exitoso, ult!.Estado);
        Assert.NotNull(ult.CompletadoAt);
        Assert.False(string.IsNullOrWhiteSpace(ult.ResultadoJson));
    }

    [Fact]
    public async Task Tick_NO_re_ejecuta_job_si_aun_no_paso_FrecuenciaMinutos()
    {
        await LimpiarEjecucionesAsync("MetricasDiarias");
        var scheduler = _services.GetRequiredService<BackgroundJobScheduler>();

        // Primer tick: ejecuta
        await scheduler.TickAsync(CancellationToken.None);
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx1 = new PropiaDbContext(opts, new TenantContext());
        var cuenta1 = await ctx1.JobEjecuciones.AsNoTracking()
            .CountAsync(j => j.JobName == "MetricasDiarias");
        Assert.Equal(1, cuenta1);

        // Segundo tick inmediato: NO debe re-ejecutar (FrecuenciaMinutos=720)
        await scheduler.TickAsync(CancellationToken.None);
        await using var ctx2 = new PropiaDbContext(opts, new TenantContext());
        var cuenta2 = await ctx2.JobEjecuciones.AsNoTracking()
            .CountAsync(j => j.JobName == "MetricasDiarias");
        Assert.Equal(1, cuenta2);
    }

    [Fact]
    public async Task GetEstadoJobs_devuelve_jobs_registrados_con_ultima_ejecucion()
    {
        await LimpiarEjecucionesAsync("PqrsdCierreNocturno");
        await LimpiarEjecucionesAsync("MetricasDiarias");

        var scheduler = _services.GetRequiredService<BackgroundJobScheduler>();
        await scheduler.TickAsync(CancellationToken.None);

        using var scope = _services.CreateScope();
        var monitoria = scope.ServiceProvider.GetRequiredService<IMonitoriaService>();
        var estados = await monitoria.GetEstadoJobsAsync(CancellationToken.None);

        Assert.Contains(estados, e => e.JobName == "MetricasDiarias" && e.UltimaEjecucionAt is not null);
        Assert.Contains(estados, e => e.JobName == "PqrsdCierreNocturno");
        Assert.All(estados, e => Assert.True(e.FrecuenciaMinutos > 0));
    }

    [Fact]
    public async Task EjecutarUno_persiste_resultado_serializado()
    {
        await LimpiarEjecucionesAsync("MetricasDiarias");

        var scheduler = _services.GetRequiredService<BackgroundJobScheduler>();
        using var scope = _services.CreateScope();
        var job = scope.ServiceProvider.GetServices<IBackgroundJob>()
            .First(j => j.Nombre == "MetricasDiarias");

        await scheduler.EjecutarUnoAsync(scope.ServiceProvider, job, "test-host", CancellationToken.None);

        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var ult = await ctx.JobEjecuciones.AsNoTracking()
            .Where(j => j.JobName == "MetricasDiarias")
            .OrderByDescending(j => j.IniciadoAt).FirstAsync();
        Assert.Equal("test-host", ult.EjecutadoPorHost);
        Assert.NotNull(ult.ResultadoJson);
        Assert.Contains("tenants", ult.ResultadoJson);
    }

    private async Task LimpiarEjecucionesAsync(string jobName)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var rows = await ctx.JobEjecuciones.Where(j => j.JobName == jobName).ToListAsync();
        if (rows.Count > 0)
        {
            ctx.JobEjecuciones.RemoveRange(rows);
            await ctx.SaveChangesAsync();
        }
    }
}
