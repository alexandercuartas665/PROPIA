using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Cartera;
using Propia.Application.Common;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Cartera;
using Propia.Infrastructure.Persistence;
using System.Security.Claims;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>Tests del modulo 2.7 Cartera y Estado de Cuenta (spec v1.0 MVP).</summary>
[Collection(nameof(PostgresCollection))]
public class CarteraFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;
    public CarteraFlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddDataProtection();
        sc.AddSingleton<ITenantContext, TenantContext>();
        sc.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = BuildFakeHttpContext() });
        sc.AddScoped<TenantConnectionInterceptor>();
        sc.AddDbContext<PropiaDbContext>((sp, opts) =>
            opts.UseNpgsql(_fx.AppConnectionString)
                .AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>()));
        sc.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<PropiaDbContext>()
            .AddDefaultTokenProviders();
        sc.AddSingleton<Propia.Application.Notificaciones.INotificacionDispatcher, FakeNotificacionDispatcher>();
        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Seed_lazy_crea_config_y_4_estados_base()
    {
        var tenantId = await SeedTenantAsync("Cartera Seed");
        var (svc, _, _) = Build(tenantId);

        var estados = await svc.ListarEstadosAsync(CancellationToken.None);
        var cfg = await svc.GetConfigAsync(CancellationToken.None);

        Assert.Equal(4, estados.Count);
        Assert.Single(estados, e => e.EsInicial && e.Nombre == EstadoCarteraBase.EnMora);
        Assert.Contains(estados, e => e.Nombre == EstadoCarteraBase.Juridico);
        Assert.Equal(ModoCalculoIntereses.PorCuotaIndividual, cfg.ModoCalculoIntereses);
        Assert.Equal(ReglaImputacion.InteresesCapitalAntiguo, cfg.ReglaImputacion);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Sincronizar_desde_presupuesto_genera_deuda_y_cartera_unidad()
    {
        var tenantId = await SeedTenantAsync("Cartera Sync");
        await SeedPresupuestoConLiquidacionVencidaAsync(tenantId);
        var (svc, db, _) = Build(tenantId);

        var n = await svc.SincronizarDesdePresupuestoAsync(CancellationToken.None);
        Assert.True(n >= 1);

        var tablero = await svc.GetTableroAsync(CancellationToken.None);
        Assert.True(tablero.Kpis.UnidadesEnMora >= 1);
        Assert.True(tablero.Kpis.TotalMoraCop > 0);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Crear_acuerdo_pasa_a_borrador_luego_pendiente_luego_vigente()
    {
        var tenantId = await SeedTenantAsync("Cartera Acuerdo");
        var unidadId = await SeedPresupuestoConLiquidacionVencidaAsync(tenantId);
        var (svc, _, _) = Build(tenantId);
        await svc.SincronizarDesdePresupuestoAsync(CancellationToken.None);

        var acuerdo = await svc.CrearAcuerdoAsync(
            new CrearAcuerdoRequest(unidadId, 3, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)), 0, "Acuerdo test"),
            CancellationToken.None);
        Assert.Equal(EstadoAcuerdoPago.Borrador, acuerdo.Estado);
        Assert.Equal(3, acuerdo.Cuotas.Count);

        await svc.EnviarParaAceptacionAsync(acuerdo.Id, CancellationToken.None);
        var pending = await svc.GetAcuerdoAsync(acuerdo.Id, CancellationToken.None);
        Assert.Equal(EstadoAcuerdoPago.PendienteAceptacion, pending!.Estado);

        await svc.AceptarAcuerdoAsync(acuerdo.Id, new AceptarAcuerdoRequest("TEST", "127.0.0.1"), CancellationToken.None);
        var vigente = await svc.GetAcuerdoAsync(acuerdo.Id, CancellationToken.None);
        Assert.Equal(EstadoAcuerdoPago.Vigente, vigente!.Estado);
        Assert.NotNull(vigente.AceptacionAt);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task AC01_Solo_un_acuerdo_activo_por_unidad()
    {
        var tenantId = await SeedTenantAsync("Cartera AC01");
        var unidadId = await SeedPresupuestoConLiquidacionVencidaAsync(tenantId);
        var (svc, _, _) = Build(tenantId);
        await svc.SincronizarDesdePresupuestoAsync(CancellationToken.None);

        var a = await svc.CrearAcuerdoAsync(new CrearAcuerdoRequest(unidadId, 3, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)), 0, null), CancellationToken.None);
        await svc.EnviarParaAceptacionAsync(a.Id, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearAcuerdoAsync(new CrearAcuerdoRequest(unidadId, 4, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)), 0, null), CancellationToken.None));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Condonacion_de_intereses_reduce_saldo_y_es_inmutable()
    {
        var tenantId = await SeedTenantAsync("Cartera Cond");
        var unidadId = await SeedPresupuestoConLiquidacionVencidaAsync(tenantId);
        var (svc, db, _) = Build(tenantId);
        await svc.SincronizarDesdePresupuestoAsync(CancellationToken.None);

        var antes = await svc.GetUnidadAsync(unidadId, CancellationToken.None);
        var interesesAntes = antes!.SaldoIntereses;
        if (interesesAntes <= 0)
        {
            // Forzar algun interes (en MVP los intereses dependen de dias mora)
            var dd = await db.DeudaDetalles.FirstAsync(d => d.UnidadPrivadaId == unidadId);
            dd.InteresAcumulado = 50_000m;
            await db.SaveChangesAsync();
            // Recompute snapshot manualmente con sincronizar (volvera a calcular pero al menos los intereses estan)
            interesesAntes = 50_000m;
        }

        var c = await svc.AplicarCondonacionAsync(
            new AplicarCondonacionRequest(unidadId, TipoCondonacion.Intereses, interesesAntes, "Acta de asamblea", null),
            CancellationToken.None);

        Assert.Equal(interesesAntes, c.MontoCondonado);

        // Append-only: el registro de condonacion no se puede modificar via SQL
        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlAsync($"UPDATE condonaciones SET motivo = 'alterado' WHERE id = {c.Id}"));
        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlAsync($"DELETE FROM condonaciones WHERE id = {c.Id}"));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task PazSalvo_Pleno_se_emite_si_saldo_es_cero()
    {
        var tenantId = await SeedTenantAsync("Cartera PS");
        var (svc, db, _) = Build(tenantId);
        await svc.SincronizarDesdePresupuestoAsync(CancellationToken.None);

        // Crear unidad sin deuda
        var torre = new Torre { TenantId = tenantId, Nombre = "T-PS", Descripcion = "Test" };
        db.Torres.Add(torre);
        var unidad = new UnidadPrivada { TenantId = tenantId, Numero = "PS-001", TorreId = torre.Id };
        db.UnidadesPrivadas.Add(unidad);
        await db.SaveChangesAsync();

        var ps = await svc.EmitirPazSalvoAsync(
            new EmitirPazSalvoRequest(unidad.Id, EmisionPazSalvo.Manual, null), CancellationToken.None);

        Assert.Equal(TipoPazSalvo.Pleno, ps.Tipo);
        Assert.Equal(EstadoPazSalvo.Vigente, ps.Estado);
        Assert.StartsWith("PS-", ps.CodigoVerificacion);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Cambiar_estado_unidad_requiere_motivo_y_registra_historial()
    {
        var tenantId = await SeedTenantAsync("Cartera CambioEst");
        var unidadId = await SeedPresupuestoConLiquidacionVencidaAsync(tenantId);
        var (svc, _, _) = Build(tenantId);
        await svc.SincronizarDesdePresupuestoAsync(CancellationToken.None);

        var estados = await svc.ListarEstadosAsync(CancellationToken.None);
        var juridico = estados.First(e => e.Nombre == EstadoCarteraBase.Juridico);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CambiarEstadoUnidadAsync(unidadId, new CambiarEstadoUnidadRequest(juridico.Id, ""), CancellationToken.None));

        await svc.CambiarEstadoUnidadAsync(unidadId, new CambiarEstadoUnidadRequest(juridico.Id, "Mora > 90 dias"), CancellationToken.None);
        var d = await svc.GetUnidadAsync(unidadId, CancellationToken.None);
        Assert.Equal(juridico.Id, d!.EstadoGestionId);
        Assert.Contains(d.Historial, h => h.TipoEvento == TipoEventoCartera.CambioEstadoGestion);

        await CleanTenant(tenantId);
    }

    // ===================== Helpers =====================

    private (ICarteraService svc, PropiaDbContext db, IServiceScope scope) Build(Guid tenantId)
    {
        var scope = _services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        ctx.SetTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var http = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var noti = scope.ServiceProvider.GetRequiredService<Propia.Application.Notificaciones.INotificacionDispatcher>();
        return (new CarteraService(db, ctx, http, noti), db, scope);
    }

    private static HttpContext BuildFakeHttpContext()
    {
        var ctx = new DefaultHttpContext();
        var uid = Guid.NewGuid().ToString();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("user_id", uid), new Claim("persona_id", uid)
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

    /// <summary>Crea un presupuesto + liquidacion con una unidad en mora (estado Vencido) para los tests.</summary>
    private async Task<Guid> SeedPresupuestoConLiquidacionVencidaAsync(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());

        var torre = new Torre { TenantId = tenantId, Nombre = "T1", Descripcion = "Test" };
        ctx.Torres.Add(torre);
        var unidad = new UnidadPrivada { TenantId = tenantId, Numero = "101", TorreId = torre.Id, CoeficientePropiedad = 1m };
        ctx.UnidadesPrivadas.Add(unidad);

        var presupuesto = new Domain.Entities.Presupuesto
        {
            TenantId = tenantId,
            Nombre = "Presup Test",
            VigenciaInicio = new DateOnly(2026, 1, 1),
            VigenciaFin = new DateOnly(2026, 12, 31),
            Estado = EstadoPresupuesto.EnEjecucion,
            MontoTotal = 1_200_000m
        };
        ctx.Presupuestos.Add(presupuesto);

        var liq = new Liquidacion
        {
            TenantId = tenantId,
            Presupuesto = presupuesto,
            Periodo = new DateOnly(DateTime.UtcNow.Year, Math.Max(1, DateTime.UtcNow.Month - 2), 1),
            Estado = EstadoLiquidacion.Emitida,
            MontoTotal = 100_000m,
            SnapshotCalculo = "{}"
        };
        ctx.Liquidaciones.Add(liq);

        var liqUnidad = new LiquidacionUnidad
        {
            TenantId = tenantId,
            Liquidacion = liq,
            UnidadPrivadaId = unidad.Id,
            Monto = 100_000m,
            Desglose = "{}",
            EstadoPago = EstadoPagoLiquidacion.Vencido
        };
        ctx.LiquidacionUnidades.Add(liqUnidad);

        await ctx.SaveChangesAsync();
        return unidad.Id;
    }

    private async Task CleanTenant(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        // Tablas append-only del modulo 2.7 - deshabilitar trigger temporalmente
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE cartera_historial DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM cartera_historial WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE cartera_historial ENABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE condonaciones DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM condonaciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE condonaciones ENABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM paz_salvos_emitidos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM acuerdo_cuotas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM acuerdos_pago WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM deuda_detalle WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM cartera_unidades WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM cartera_config WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM estados_cartera_config WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM liquidacion_unidades WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM liquidaciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM presupuestos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidades_privadas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM torres WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }
}
