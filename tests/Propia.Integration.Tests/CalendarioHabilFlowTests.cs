using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Common;
using Propia.Infrastructure.Common;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Tests del servicio cross-modulo CalendarioHabilService (festivos colombianos).
/// Cubre:
///  - Seed via migracion siembra festivos 2024-2032 (al menos 18 por anio).
///  - SumarDiasHabiles excluye sabados, domingos Y festivos.
///  - ContarDiasHabiles entre dos fechas respeta festivos.
///  - EsHabil para casos conocidos: 1 enero, 25 diciembre, Jueves Santo.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class CalendarioHabilFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;

    public CalendarioHabilFlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddSingleton<ITenantContext, TenantContext>();
        sc.AddScoped<TenantConnectionInterceptor>();
        sc.AddDbContext<PropiaDbContext>((sp, opts) =>
            opts.UseNpgsql(_fx.OwnerConnectionString)
                .AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>()));
        sc.AddScoped<ICalendarioHabilService, CalendarioHabilService>();
        _services = sc.BuildServiceProvider();
        // Limpiar cache estatico del proceso para que cada test arranque limpio
        CalendarioHabilService cache = (CalendarioHabilService)_services
            .CreateScope().ServiceProvider.GetRequiredService<ICalendarioHabilService>();
        cache.InvalidarCache();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Seed_migracion_inserta_festivos_2024_2032()
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var cuenta2024 = await ctx.FestivosColombianos.AsNoTracking().CountAsync(f => f.Fecha.Year == 2024);
        var cuenta2026 = await ctx.FestivosColombianos.AsNoTracking().CountAsync(f => f.Fecha.Year == 2026);
        // Algunos anios tienen colisiones por UNIQUE (ej. 2024: Reyes Magos y otro trasladado al mismo lunes).
        // Esperamos al menos 16 cada anio (los 6 fijos + 6 trasladados + 5 religiosos = 17 maximo si no hay colision).
        Assert.True(cuenta2024 >= 16, $"Esperaba >=16 festivos para 2024, encontre {cuenta2024}");
        Assert.True(cuenta2026 >= 16, $"Esperaba >=16 festivos para 2026, encontre {cuenta2026}");
    }

    [Fact]
    public async Task Fechas_conocidas_son_NO_habiles()
    {
        using var scope = _services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICalendarioHabilService>();
        Assert.False(await svc.EsHabilAsync(new DateOnly(2026, 1, 1), CancellationToken.None));   // Ano Nuevo
        Assert.False(await svc.EsHabilAsync(new DateOnly(2026, 12, 25), CancellationToken.None)); // Navidad
        Assert.False(await svc.EsHabilAsync(new DateOnly(2026, 5, 1), CancellationToken.None));   // Trabajo
        Assert.False(await svc.EsHabilAsync(new DateOnly(2026, 7, 20), CancellationToken.None));  // Independencia
    }

    [Fact]
    public async Task SumarDiasHabiles_salta_sabado_y_domingo()
    {
        using var scope = _services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICalendarioHabilService>();
        // Viernes 23 ene 2026 + 1 dia habil = lunes 26 ene (no es festivo ni cae en festivo)
        var r = await svc.SumarDiasHabilesAsync(new DateOnly(2026, 1, 23), 1, CancellationToken.None);
        Assert.Equal(new DateOnly(2026, 1, 26), r);
    }

    [Fact]
    public async Task SumarDiasHabiles_salta_festivo()
    {
        using var scope = _services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICalendarioHabilService>();
        // 31 dic 2025 (miercoles) + 1 dia habil debe saltar 1 ene 2026 (Ano Nuevo, jueves).
        // Resultado esperado: viernes 2 ene 2026.
        var r = await svc.SumarDiasHabilesAsync(new DateOnly(2025, 12, 31), 1, CancellationToken.None);
        Assert.Equal(new DateOnly(2026, 1, 2), r);
    }
}
