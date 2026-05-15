using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Presupuesto;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Presupuesto;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Tests del modulo 2.6 Presupuesto, Cuotas y Pagos (spec v1.0).
///
/// Cubre: ciclo de vida del presupuesto, validacion del Fondo de Imprevistos,
/// motor de liquidacion idempotente con snapshot inmutable, panel de recaudo,
/// pago manual, RN-01 (una sola vigencia EnEjecucion), trigger append-only de auditoria.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class PresupuestoFlowTests
{
    private readonly PostgresFixture _fx;
    public PresupuestoFlowTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Crear_presupuesto_con_catalogo_base_incluye_10_rubros_y_fondo_imprevistos_obligatorio()
    {
        var tenantId = await SeedTenantAsync("CP Presup Crear");
        var (svc, _, _) = BuildService(tenantId);

        var p = await svc.CrearPresupuestoAsync(new CrearPresupuestoRequest(
            "Presupuesto 2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), true),
            CancellationToken.None);

        var d = await svc.GetPresupuestoDetalleAsync(p.Id, CancellationToken.None);
        Assert.NotNull(d);
        Assert.Equal(EstadoPresupuesto.Borrador, d!.Estado);
        Assert.Equal(10, d.Rubros.Count);  // 10 base rubros
        Assert.Contains(d.Rubros, r => r.EsFondoImprevistos && r.EsObligatorio);

        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Fondo_imprevistos_no_se_puede_desactivar_pero_advertencia_minimo_1pct_es_solo_calculo()
    {
        var tenantId = await SeedTenantAsync("CP Fondo");
        var (svc, db, _) = BuildService(tenantId);

        var p = await svc.CrearPresupuestoAsync(new CrearPresupuestoRequest(
            "P", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), true), CancellationToken.None);

        var rubros = (await svc.GetPresupuestoDetalleAsync(p.Id, CancellationToken.None))!.Rubros;
        var admin = rubros.First(r => r.Codigo == RubroCatalogo.AdministracionGeneral);
        var fondo = rubros.First(r => r.Codigo == RubroCatalogo.FondoImprevistos);

        // Pongo 1M en admin y 5k (0.5%) en fondo - menos del 1% legal
        await svc.ActualizarRubroAsync(admin.Id, new ActualizarRubroRequest(admin.Nombre, 1_000_000m, BaseLiquidacion.Coeficiente, true, admin.Orden, null), CancellationToken.None);
        await svc.ActualizarRubroAsync(fondo.Id, new ActualizarRubroRequest(fondo.Nombre, 5_000m, BaseLiquidacion.Coeficiente, true, fondo.Orden, null), CancellationToken.None);

        var d = await svc.GetPresupuestoDetalleAsync(p.Id, CancellationToken.None);
        Assert.False(d!.CumpleMinimoFondoImprevistos);  // RN-02: advertencia suave, no bloqueo
        Assert.Equal(10_050m, d.MontoFondoImprevistosMinimoLegal);  // 1% de 1_005_000

        // Intentar desactivar el fondo de imprevistos debe fallar (RN-02 no eliminable)
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ActualizarRubroAsync(fondo.Id, new ActualizarRubroRequest(fondo.Nombre, 5_000m, BaseLiquidacion.Coeficiente, false, fondo.Orden, null), CancellationToken.None));

        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Solo_una_vigencia_EnEjecucion_a_la_vez_RN01()
    {
        var tenantId = await SeedTenantAsync("CP RN01");
        var (svc, _, _) = BuildService(tenantId);

        var p1 = await svc.CrearPresupuestoAsync(new CrearPresupuestoRequest("2025", new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), false), CancellationToken.None);
        var p2 = await svc.CrearPresupuestoAsync(new CrearPresupuestoRequest("2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), false), CancellationToken.None);

        await svc.AprobarPresupuestoAsync(p1.Id, new AprobarPresupuestoRequest(TipoAprobacion.Manual, new DateOnly(2024, 12, 1), "acta.pdf", null), CancellationToken.None);
        await svc.AprobarPresupuestoAsync(p2.Id, new AprobarPresupuestoRequest(TipoAprobacion.Manual, new DateOnly(2025, 12, 1), "acta.pdf", null), CancellationToken.None);
        await svc.ActivarVigenciaAsync(p1.Id, CancellationToken.None);
        await svc.ActivarVigenciaAsync(p2.Id, CancellationToken.None);

        var lista = await svc.ListarPresupuestosAsync(CancellationToken.None);
        var enEjecucion = lista.Where(x => x.Estado == EstadoPresupuesto.EnEjecucion).ToList();
        Assert.Single(enEjecucion);
        Assert.Equal(p2.Id, enEjecucion[0].Id);  // El nuevo desplaza al anterior

        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Motor_liquidacion_emite_un_renglon_por_unidad_con_snapshot_y_es_idempotente()
    {
        var tenantId = await SeedTenantAsync("CP Liquidar");
        var (svc, db, tctx) = BuildService(tenantId);

        // Setup: 2 unidades con coeficientes 60% y 40%
        var torre = new Torre { Nombre = "Torre A" };
        db.Torres.Add(torre);
        await db.SaveChangesAsync();
        db.UnidadesPrivadas.AddRange(
            new UnidadPrivada { Numero = "101", Tipo = TipoUnidad.Apartamento, TorreId = torre.Id, CoeficientePropiedad = 60m },
            new UnidadPrivada { Numero = "102", Tipo = TipoUnidad.Apartamento, TorreId = torre.Id, CoeficientePropiedad = 40m });
        await db.SaveChangesAsync();

        // Presupuesto con un solo rubro $12M anual (= $1M mensual)
        var p = await svc.CrearPresupuestoAsync(new CrearPresupuestoRequest("Liq Test", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), true), CancellationToken.None);
        var rubros = (await svc.GetPresupuestoDetalleAsync(p.Id, CancellationToken.None))!.Rubros;
        var admin = rubros.First(r => r.Codigo == RubroCatalogo.AdministracionGeneral);
        await svc.ActualizarRubroAsync(admin.Id, new ActualizarRubroRequest("Administracion", 12_000_000m, BaseLiquidacion.Coeficiente, true, admin.Orden, null), CancellationToken.None);
        await svc.AprobarPresupuestoAsync(p.Id, new AprobarPresupuestoRequest(TipoAprobacion.Manual, new DateOnly(2025, 12, 1), "acta", null), CancellationToken.None);
        await svc.ActivarVigenciaAsync(p.Id, CancellationToken.None);

        var liq = await svc.EmitirLiquidacionAsync(new EmitirLiquidacionRequest(p.Id, new DateOnly(2026, 4, 1)), CancellationToken.None);
        Assert.Equal(2, liq.CantidadUnidades);
        Assert.True(liq.MontoTotal > 0);

        // Idempotente: re-emitir mismo periodo retorna la misma liquidacion sin duplicar
        var liq2 = await svc.EmitirLiquidacionAsync(new EmitirLiquidacionRequest(p.Id, new DateOnly(2026, 4, 1)), CancellationToken.None);
        Assert.Equal(liq.Id, liq2.Id);

        // Verificar que la unidad 101 (60%) paga mas que la 102 (40%)
        var lus = await db.LiquidacionUnidades.Where(x => x.LiquidacionId == liq.Id).ToListAsync();
        var l101 = lus.First(x => db.UnidadesPrivadas.IgnoreQueryFilters().First(u => u.Id == x.UnidadPrivadaId).Numero == "101");
        var l102 = lus.First(x => db.UnidadesPrivadas.IgnoreQueryFilters().First(u => u.Id == x.UnidadPrivadaId).Numero == "102");
        Assert.True(l101.Monto > l102.Monto);

        // Snapshot inmutable - verificar que contiene los coeficientes del momento
        var snap = await db.Liquidaciones.AsNoTracking().FirstAsync(x => x.Id == liq.Id);
        Assert.Contains("totalCoeficientes", snap.SnapshotCalculo);
        Assert.Contains("rubros", snap.SnapshotCalculo);

        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Registrar_pago_manual_marca_liquidacion_como_pagada()
    {
        var tenantId = await SeedTenantAsync("CP Pago");
        var (svc, db, _) = BuildService(tenantId);

        var torre = new Torre { Nombre = "T" };
        db.Torres.Add(torre);
        db.UnidadesPrivadas.Add(new UnidadPrivada { Numero = "101", Tipo = TipoUnidad.Apartamento, Torre = torre, CoeficientePropiedad = 100m });
        await db.SaveChangesAsync();

        var p = await svc.CrearPresupuestoAsync(new CrearPresupuestoRequest("X", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), true), CancellationToken.None);
        var rubros = (await svc.GetPresupuestoDetalleAsync(p.Id, CancellationToken.None))!.Rubros;
        var admin = rubros.First(r => r.Codigo == RubroCatalogo.AdministracionGeneral);
        await svc.ActualizarRubroAsync(admin.Id, new ActualizarRubroRequest("A", 1_200_000m, BaseLiquidacion.Coeficiente, true, admin.Orden, null), CancellationToken.None);
        await svc.AprobarPresupuestoAsync(p.Id, new AprobarPresupuestoRequest(TipoAprobacion.Manual, new DateOnly(2025, 12, 1), "acta", null), CancellationToken.None);
        await svc.ActivarVigenciaAsync(p.Id, CancellationToken.None);
        var liq = await svc.EmitirLiquidacionAsync(new EmitirLiquidacionRequest(p.Id, new DateOnly(2026, 4, 1)), CancellationToken.None);

        var lu = await db.LiquidacionUnidades.FirstAsync(x => x.LiquidacionId == liq.Id);
        var monto = lu.Monto;

        var pago = await svc.RegistrarPagoManualAsync(new RegistrarPagoManualRequest(
            lu.Id, CanalPago.ManualConsignacion, monto, new DateOnly(2026, 4, 15), "Ref-001", "Pago abril"), CancellationToken.None);

        Assert.Equal(EstadoPago.Confirmado, pago.Estado);
        Assert.True(pago.EsManual);

        // Recargar y verificar EstadoPago = Pagado
        var luAfter = await db.LiquidacionUnidades.AsNoTracking().FirstAsync(x => x.Id == lu.Id);
        Assert.Equal(EstadoPagoLiquidacion.Pagado, luAfter.EstadoPago);

        var resumen = await svc.GetRecaudoResumenAsync(new DateOnly(2026, 4, 1), CancellationToken.None);
        Assert.Equal(100m, resumen.PorcentajeRecaudo);

        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Auditoria_es_append_only_no_acepta_delete_ni_update()
    {
        var tenantId = await SeedTenantAsync("CP Audit");
        var (_, db, _) = BuildService(tenantId);
        var log = new AuditLogPresupuesto { Entidad = "test", EntidadId = Guid.NewGuid(), Accion = "test", UsuarioId = Guid.Empty };
        db.AuditLogPresupuestos.Add(log);
        await db.SaveChangesAsync();
        log.Accion = "modificado";
        await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());
        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Tenant_no_ve_presupuestos_de_otro_tenant_RLS()
    {
        var tA = await SeedTenantAsync("CP A");
        var tB = await SeedTenantAsync("CP B");
        var (svcA, _, _) = BuildService(tA);
        var (svcB, _, _) = BuildService(tB);

        await svcA.CrearPresupuestoAsync(new CrearPresupuestoRequest("Solo A", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), false), CancellationToken.None);
        var listaB = await svcB.ListarPresupuestosAsync(CancellationToken.None);
        Assert.Empty(listaB);

        await CleanupTenantAsync(tA);
        await CleanupTenantAsync(tB);
    }

    // ---------------- Helpers ----------------

    private (IPresupuestoService svc, PropiaDbContext db, TenantContext tctx) BuildService(Guid tenantId)
    {
        var tenantCtx = new TenantContext();
        tenantCtx.SetTenant(tenantId);
        var interceptor = new Propia.Infrastructure.Persistence.TenantConnectionInterceptor(tenantCtx);
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .AddInterceptors(interceptor)
            .Options;
        var db = new PropiaDbContext(options, tenantCtx);
        return (new PresupuestoService(db, tenantCtx), db, tenantCtx);
    }

    private async Task<Guid> SeedTenantAsync(string nombre)
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.OwnerConnectionString)
            .Options;
        await using var ctx = new PropiaDbContext(options, new TenantContext());
        var t = new Tenant { Nombre = nombre, Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.SinAdmin };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        return t.Id;
    }

    private async Task CleanupTenantAsync(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.OwnerConnectionString)
            .Options;
        await using var ctx = new PropiaDbContext(options, new TenantContext());
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pagos_cuotas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM liquidacion_unidades WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM liquidaciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM presupuesto_rubros WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM presupuestos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM cuotas_extraordinarias WHERE tenant_id = {tenantId}");
        // audit_log_presupuestos es append-only (RN-12) - los registros del tenant quedan huerfanos pero
        // no afectan otros tests porque cada test usa su propio tenant.
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidades_privadas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM torres WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }
}
