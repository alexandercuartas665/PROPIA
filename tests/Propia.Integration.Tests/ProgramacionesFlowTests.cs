using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Common;
using Propia.Application.Programaciones;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Programaciones;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Tests del servicio de Programaciones de tareas (cronograma de 2.11).
/// Cubre el MERGE del costo estimado en el PUT (Punto 1, correccion de ENTREGA_04):
/// editar sin enviar costo conserva el actual; enviarlo lo cambia.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ProgramacionesFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;

    public ProgramacionesFlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddSingleton<ITenantContext, TenantContext>();
        sc.AddScoped<TenantConnectionInterceptor>();
        sc.AddDbContext<PropiaDbContext>((sp, opts) =>
            opts.UseNpgsql(_fx.AppConnectionString).AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>()));
        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Actualizar_sin_costo_conserva_el_actual_con_costo_lo_cambia()
    {
        var tenantId = await SeedTenantAsync("Prog Costo");
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;
        var uid = Guid.NewGuid();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        var creada = await svc.CrearAsync(new CrearProgramacionRequest(
            "Rev mensual", null, PrioridadTarea.Normal, null, PeriodicidadProgramacion.Mensual,
            hoy.AddDays(1), null, null, CostoEstimado: 100000m), uid, CancellationToken.None);
        Assert.Equal(100000m, creada.CostoEstimado);

        // Editar SIN enviar costo (como hace la ficha de equipo/zona): se conserva (PUT = MERGE).
        await svc.ActualizarAsync(creada.Id, new ActualizarProgramacionRequest(
            "Rev mensual (editada)", null, PrioridadTarea.Alta, null, PeriodicidadProgramacion.Mensual,
            hoy.AddDays(2), null, true, null), CancellationToken.None);
        var tras1 = await svc.GetAsync(creada.Id, CancellationToken.None);
        Assert.Equal(100000m, tras1!.CostoEstimado);

        // Editar CON costo: se cambia.
        await svc.ActualizarAsync(creada.Id, new ActualizarProgramacionRequest(
            "Rev mensual (editada)", null, PrioridadTarea.Alta, null, PeriodicidadProgramacion.Mensual,
            hoy.AddDays(2), null, true, null, CostoEstimado: 250000m), CancellationToken.None);
        var tras2 = await svc.GetAsync(creada.Id, CancellationToken.None);
        Assert.Equal(250000m, tras2!.CostoEstimado);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Actualizar_conserva_tipo_cron_zona_proveedor_cuando_el_request_no_los_trae()
    {
        var tenantId = await SeedTenantAsync("Prog M-10");
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;
        var uid = Guid.NewGuid();
        var provId = Guid.NewGuid();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        // Regla CRON con zona y proveedor.
        var creada = await svc.CrearAsync(new CrearProgramacionRequest(
            "Aseo lunes", null, PrioridadTarea.Normal, null, PeriodicidadProgramacion.Unica,
            hoy.AddDays(1), null, null,
            Tipo: TipoProgramacion.Cron, CronExpresion: "0 8 * * 1", ZonaHoraria: "America/Bogota",
            ProveedorId: provId, ProveedorNombre: "Aseo Total SAS"), uid, CancellationToken.None);
        Assert.Equal(TipoProgramacion.Cron, creada.Tipo);
        Assert.Equal("0 8 * * 1", creada.CronExpresion);
        Assert.Equal("Aseo Total SAS", creada.ProveedorNombre);

        // Ruta SERVICIO: PUT que NO reenvia cron/zona/proveedor (Tipo se mantiene Cron): se conservan.
        await svc.ActualizarAsync(creada.Id, new ActualizarProgramacionRequest(
            "Aseo lunes (editado)", null, PrioridadTarea.Alta, null, PeriodicidadProgramacion.Unica,
            hoy.AddDays(1), null, true, null,
            Tipo: TipoProgramacion.Cron), CancellationToken.None);
        var tras1 = await svc.GetAsync(creada.Id, CancellationToken.None);
        Assert.Equal(TipoProgramacion.Cron, tras1!.Tipo);
        Assert.Equal("0 8 * * 1", tras1.CronExpresion);
        Assert.Equal("America/Bogota", tras1.ZonaHoraria);
        Assert.Equal(provId, tras1.ProveedorId);
        Assert.Equal("Aseo Total SAS", tras1.ProveedorNombre);

        // Ruta MODAL: PUT que SI reenvia una cron nueva -> cambia.
        await svc.ActualizarAsync(creada.Id, new ActualizarProgramacionRequest(
            "Aseo lunes (editado)", null, PrioridadTarea.Alta, null, PeriodicidadProgramacion.Unica,
            hoy.AddDays(1), null, true, null,
            Tipo: TipoProgramacion.Cron, CronExpresion: "0 9 * * 2", ZonaHoraria: "America/Bogota",
            ProveedorNombre: "Aseo Total SAS"), CancellationToken.None);
        var tras2 = await svc.GetAsync(creada.Id, CancellationToken.None);
        Assert.Equal("0 9 * * 2", tras2!.CronExpresion);

        await CleanTenant(tenantId);
    }

    private (IProgramacionTareasService svc, PropiaDbContext db, IServiceScope scope) Build(Guid tenantId)
    {
        var scope = _services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        ctx.SetTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        return (new ProgramacionTareasService(db), db, scope);
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

    private async Task CleanTenant(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM programacion_tarea_responsables WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM programacion_tareas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }
}
