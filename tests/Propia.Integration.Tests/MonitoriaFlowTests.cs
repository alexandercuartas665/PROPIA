using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Common;
using Propia.Application.Monitoria;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Monitoria;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Tests del modulo 0.3 Monitoria y Auditoria Global (MVP).
/// Cubre logs, incidentes y calculo de metricas globales.
/// Servicio opera SIN tenant activo (es global SuperAdmin).
/// </summary>
[Collection(nameof(PostgresCollection))]
public class MonitoriaFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;

    public MonitoriaFlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddDataProtection();
        sc.AddSingleton<ITenantContext, TenantContext>();
        sc.AddScoped<TenantConnectionInterceptor>();
        sc.AddDbContext<PropiaDbContext>((sp, opts) =>
            opts.UseNpgsql(_fx.OwnerConnectionString)
                .AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>()));
        sc.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<PropiaDbContext>()
            .AddDefaultTokenProviders();
        sc.AddScoped<IMonitoriaService, MonitoriaService>();
        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RegistrarLog_persiste_y_recupera()
    {
        using var scope = _services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IMonitoriaService>();

        var id = await svc.RegistrarLogAsync(new RegistrarLogRequest(
            TipoEventoSistema.AccesoExitoso, "MCP test login", SeveridadIncidente.Info,
            ModuloOrigenCodigo: "0.1", Ip: "127.0.0.1"), CancellationToken.None);
        Assert.NotEqual(Guid.Empty, id);

        var logs = await svc.ListarLogsAsync(new FiltroLogsRequest(
            TipoEvento: TipoEventoSistema.AccesoExitoso, Limite: 10), CancellationToken.None);
        Assert.Contains(logs, l => l.Id == id);
    }

    [Fact]
    public async Task AbrirIncidente_crea_incidente_y_log_correlacionado()
    {
        using var scope = _services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IMonitoriaService>();

        var inc = await svc.AbrirIncidenteAsync(new AbrirIncidenteRequest(
            SeveridadIncidente.Critico, "T.2 dispatcher caido", "Errores 100% en envio email",
            "T.2"), CancellationToken.None);
        Assert.Equal(EstadoIncidente.Abierto, inc.Estado);
        Assert.Equal(SeveridadIncidente.Critico, inc.Severidad);

        // Verifica que se creo log automatico
        var logs = await svc.ListarLogsAsync(new FiltroLogsRequest(
            ModuloOrigenCodigo: "0.3", Limite: 10), CancellationToken.None);
        Assert.Contains(logs, l => l.Mensaje.Contains("T.2 dispatcher caido"));
    }

    [Fact]
    public async Task ResolverIncidente_fija_causa_solucion_y_resueltoAt()
    {
        using var scope = _services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IMonitoriaService>();
        var inc = await svc.AbrirIncidenteAsync(new AbrirIncidenteRequest(
            SeveridadIncidente.Error, "Latencia API alta"), CancellationToken.None);

        var ok = await svc.ResolverIncidenteAsync(inc.Id, new ResolverIncidenteRequest(
            "Connection pool exhausted",
            "Aumentado MaxPoolSize a 100"), CancellationToken.None);
        Assert.True(ok);

        var get = await svc.GetIncidenteAsync(inc.Id, CancellationToken.None);
        Assert.NotNull(get);
        Assert.Equal(EstadoIncidente.Resuelto, get!.Estado);
        Assert.NotNull(get.ResueltoAt);
        Assert.Equal("Connection pool exhausted", get.CausaRaiz);
    }

    [Fact]
    public async Task CalcularMetricasHoy_devuelve_snapshot_y_es_idempotente()
    {
        using var scope = _services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IMonitoriaService>();

        var m1 = await svc.CalcularYGuardarMetricasHoyAsync(CancellationToken.None);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow.Date), m1.Fecha);
        Assert.True(m1.TotalTenants >= 0);

        // Segunda llamada el mismo dia = upsert, no duplica
        var m2 = await svc.CalcularYGuardarMetricasHoyAsync(CancellationToken.None);
        Assert.Equal(m1.Fecha, m2.Fecha);

        // Verifica que hay UNA sola fila para hoy
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var cuenta = await ctx.MetricasUsoDiarias.AsNoTracking()
            .CountAsync(x => x.Fecha == m1.Fecha);
        Assert.Equal(1, cuenta);
    }

    [Fact]
    public async Task GetResumen_devuelve_counters_consistentes()
    {
        using var scope = _services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IMonitoriaService>();
        await svc.RegistrarLogAsync(new RegistrarLogRequest(
            TipoEventoSistema.AccesoFallido, "MCP resumen test", SeveridadIncidente.Error),
            CancellationToken.None);
        var resumen = await svc.GetResumenAsync(CancellationToken.None);
        Assert.True(resumen.LogsUlt24h >= 1);
        Assert.True(resumen.LogsErrorUlt24h >= 1);
    }

    [Fact]
    public async Task CambiarEstado_a_resuelto_setea_resueltoAt_si_estaba_null()
    {
        using var scope = _services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IMonitoriaService>();
        var inc = await svc.AbrirIncidenteAsync(new AbrirIncidenteRequest(
            SeveridadIncidente.Advertencia, "Test cambio estado"), CancellationToken.None);
        var ok = await svc.CambiarEstadoIncidenteAsync(inc.Id,
            new CambiarEstadoIncidenteRequest(EstadoIncidente.FalsoPositivo, "Alerta de prueba"),
            CancellationToken.None);
        Assert.True(ok);
        var get = await svc.GetIncidenteAsync(inc.Id, CancellationToken.None);
        Assert.NotNull(get!.ResueltoAt);
    }
}
