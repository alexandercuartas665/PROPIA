using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Common;
using Propia.Application.Mantenimiento;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Mantenimiento;
using Propia.Infrastructure.Persistence;
using System.Security.Claims;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Tests del modulo 2.11 Mantenimiento y Activos (spec v1.0 MVP).
/// Cubre: planes preventivos, intervenciones, vinculo con Tareas (RN-03),
/// bitacora append-only (RN-06), cambio de estado del activo (RN-07),
/// recalculo de proxima_ejecucion, RN-15 codigo unico, semaforo.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class MantenimientoFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;
    private Guid _userId;
    private Guid _personaId;
    public MantenimientoFlowTests(PostgresFixture fx) => _fx = fx;

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
        sc.AddSingleton<Propia.Application.Notificaciones.INotificacionDispatcher, FakeNotificacionDispatcher>();
        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }
    public Task DisposeAsync() => Task.CompletedTask;

    // =======================================================================
    // Tests
    // =======================================================================

    [Theory]
    // Instante UTC -> dia esperado en Colombia (UTC-5). Es la prueba de M-09.
    [InlineData("2026-01-15T03:00:00Z", "2026-01-14")]  // 22:00 del 14 en Colombia: sigue siendo el 14, no el 15
    [InlineData("2026-01-15T04:59:00Z", "2026-01-14")]  // 23:59 del 14: aun el 14 (con UtcNow crudo seria el 15: el bug)
    [InlineData("2026-01-15T05:00:00Z", "2026-01-15")]  // 00:00 del 15 en Colombia: ya es el 15
    [InlineData("2026-01-15T12:00:00Z", "2026-01-15")]  // 07:00 del 15: mismo dia en ambas zonas
    public void HoyEnColombia_usa_hora_local_no_UTC(string instanteUtc, string diaEsperado)
    {
        var utc = DateTime.Parse(instanteUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
        var hoy = Propia.Infrastructure.Jobs.MantenimientoPreventivoJob.HoyEnColombia(utc);
        Assert.Equal(DateOnly.Parse(diaEsperado), hoy);
    }

    [Fact]
    public async Task Crear_plan_preventivo_inicializa_proxima_ejecucion_en_fecha_inicio()
    {
        var tenantId = await SeedTenantAsync("Mant Plan");
        await SeedPersonaConApplicationUser(tenantId);
        var equipoId = await SeedEquipoAsync(tenantId, "Ascensor Torre A");
        var (svc, _, _) = Build(tenantId);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = await svc.CrearPlanAsync(new CrearPlanRequest(
            TipoActivoMantenimiento.Equipo, equipoId,
            "Revision anual", null, FrecuenciaMantenimiento.Anual, null,
            hoy.AddDays(30), null, DisparoPlanMantenimiento.ConConfirmacion, 7, false), CancellationToken.None);

        Assert.Equal(hoy.AddDays(30), plan.FechaInicio);
        Assert.Equal(hoy.AddDays(30), plan.ProximaEjecucion);
        Assert.True(plan.Activo);
        Assert.Equal(SemaforoMantenimiento.Verde, plan.Semaforo);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Plan_con_fecha_inicio_en_el_pasado_rechaza_RN02()
    {
        var tenantId = await SeedTenantAsync("Mant RN02");
        await SeedPersonaConApplicationUser(tenantId);
        var equipoId = await SeedEquipoAsync(tenantId, "Bomba");
        var (svc, _, _) = Build(tenantId);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearPlanAsync(new CrearPlanRequest(
                TipoActivoMantenimiento.Equipo, equipoId,
                "Pasada", null, FrecuenciaMantenimiento.Mensual, null,
                hoy.AddDays(-1), null, DisparoPlanMantenimiento.ConConfirmacion, 7, false), CancellationToken.None));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Crear_intervencion_genera_tarea_vinculada_RN03()
    {
        var tenantId = await SeedTenantAsync("Mant Tarea");
        await SeedPersonaConApplicationUser(tenantId);
        var equipoId = await SeedEquipoAsync(tenantId, "Bomba principal");
        var (svc, db, _) = Build(tenantId);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var det = await svc.CrearIntervencionAsync(new CrearIntervencionRequest(
            TipoIntervencionMantenimiento.Correctivo, TipoActivoMantenimiento.Equipo, equipoId, null,
            OrigenIntervencion.Manual, null,
            "Fuga en sello mecanico", "Reportada por portero",
            PrioridadIntervencion.Alta, null, null, hoy.AddDays(2), false), CancellationToken.None);

        Assert.NotNull(det.TareaId);
        Assert.StartsWith("MNT-", det.Codigo);
        Assert.StartsWith("T-", det.TareaNumero);

        var tarea = await db.Tareas.AsNoTracking().FirstOrDefaultAsync(t => t.Id == det.TareaId);
        Assert.NotNull(tarea);
        Assert.Equal(OrigenTarea.ModuloExterno, tarea!.Origen);
        Assert.Equal("2.11", tarea.ModuloOrigenCodigo);
        Assert.Equal(det.Id, tarea.ModuloOrigenEntidadId);
        Assert.Contains("[CORRECTIVO]", tarea.Titulo);
        Assert.Equal(PrioridadTarea.Alta, tarea.Prioridad);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Codigo_intervencion_es_secuencial_dentro_del_tenant_RN15()
    {
        var tenantId = await SeedTenantAsync("Mant RN15");
        await SeedPersonaConApplicationUser(tenantId);
        var eqId = await SeedEquipoAsync(tenantId, "Equipo");
        var (svc, _, _) = Build(tenantId);

        var year = DateTime.UtcNow.Year;
        var a = await svc.CrearIntervencionAsync(new CrearIntervencionRequest(
            TipoIntervencionMantenimiento.Correctivo, TipoActivoMantenimiento.Equipo, eqId, null,
            OrigenIntervencion.Manual, null, "Primera", null, PrioridadIntervencion.Normal,
            null, null, null, false), CancellationToken.None);
        var b = await svc.CrearIntervencionAsync(new CrearIntervencionRequest(
            TipoIntervencionMantenimiento.Correctivo, TipoActivoMantenimiento.Equipo, eqId, null,
            OrigenIntervencion.Manual, null, "Segunda", null, PrioridadIntervencion.Normal,
            null, null, null, false), CancellationToken.None);

        Assert.Equal($"MNT-{year}-0001", a.Codigo);
        Assert.Equal($"MNT-{year}-0002", b.Codigo);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Cerrar_preventivo_recalcula_proxima_ejecucion()
    {
        var tenantId = await SeedTenantAsync("Mant Cerrar");
        await SeedPersonaConApplicationUser(tenantId);
        var eqId = await SeedEquipoAsync(tenantId, "Ascensor");
        var (svc, db, _) = Build(tenantId);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = await svc.CrearPlanAsync(new CrearPlanRequest(
            TipoActivoMantenimiento.Equipo, eqId, "Revision mensual",
            null, FrecuenciaMantenimiento.Mensual, null,
            hoy.AddDays(1), null, DisparoPlanMantenimiento.Automatico, 7, false), CancellationToken.None);

        var i = await svc.CrearIntervencionAsync(new CrearIntervencionRequest(
            TipoIntervencionMantenimiento.Preventivo, TipoActivoMantenimiento.Equipo, eqId,
            plan.Id, OrigenIntervencion.Manual, null,
            "Revision mensual ascensor", null, PrioridadIntervencion.Normal,
            null, null, hoy.AddDays(1), false), CancellationToken.None);

        var fechaCierre = hoy;
        var ok = await svc.CerrarIntervencionAsync(i.Id, new CerrarIntervencionRequest(
            fechaCierre, "Revision realizada. Sin novedades.",
            false, null, false, null), CancellationToken.None);

        Assert.True(ok);
        var planActualizado = await db.MantenimientoPlanes.AsNoTracking().FirstAsync(p => p.Id == plan.Id);
        Assert.Equal(fechaCierre.AddDays(30), planActualizado.ProximaEjecucion);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Cerrar_requiere_bitacora_validacion_seccion16()
    {
        var tenantId = await SeedTenantAsync("Mant Bitacora Obligatoria");
        await SeedPersonaConApplicationUser(tenantId);
        var eqId = await SeedEquipoAsync(tenantId, "Tanque");
        var (svc, _, _) = Build(tenantId);

        var i = await svc.CrearIntervencionAsync(new CrearIntervencionRequest(
            TipoIntervencionMantenimiento.Correctivo, TipoActivoMantenimiento.Equipo, eqId, null,
            OrigenIntervencion.Manual, null, "Falla", null, PrioridadIntervencion.Alta,
            null, null, null, false), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CerrarIntervencionAsync(i.Id, new CerrarIntervencionRequest(
                DateOnly.FromDateTime(DateTime.UtcNow), "", false, null, false, null), CancellationToken.None));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Cancelar_requiere_motivo()
    {
        var tenantId = await SeedTenantAsync("Mant Cancel");
        await SeedPersonaConApplicationUser(tenantId);
        var eqId = await SeedEquipoAsync(tenantId, "Eq Cancel");
        var (svc, _, _) = Build(tenantId);

        var i = await svc.CrearIntervencionAsync(new CrearIntervencionRequest(
            TipoIntervencionMantenimiento.Correctivo, TipoActivoMantenimiento.Equipo, eqId, null,
            OrigenIntervencion.Manual, null, "Falla a cancelar", null, PrioridadIntervencion.Normal,
            null, null, null, false), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CancelarIntervencionAsync(i.Id, new CancelarIntervencionRequest(""), CancellationToken.None));

        var ok = await svc.CancelarIntervencionAsync(i.Id, new CancelarIntervencionRequest("Duplicada"), CancellationToken.None);
        Assert.True(ok);
        var det = await svc.GetIntervencionAsync(i.Id, CancellationToken.None);
        Assert.Equal(EstadoIntervencion.Cancelada, det!.Estado);
        Assert.Equal("Duplicada", det.MotivoCancelacion);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Bitacora_es_append_only_trigger_bloquea_update_y_delete_RN06()
    {
        var tenantId = await SeedTenantAsync("Mant Bit Append");
        await SeedPersonaConApplicationUser(tenantId);
        var eqId = await SeedEquipoAsync(tenantId, "Eq Append");
        var (svc, db, _) = Build(tenantId);

        var i = await svc.CrearIntervencionAsync(new CrearIntervencionRequest(
            TipoIntervencionMantenimiento.Correctivo, TipoActivoMantenimiento.Equipo, eqId, null,
            OrigenIntervencion.Manual, null, "Para append", null, PrioridadIntervencion.Normal,
            null, null, null, false), CancellationToken.None);

        var entrada = await svc.AgregarEntradaBitacoraAsync(i.Id, new AgregarBitacoraRequest(
            "Entrada de prueba", TipoAutorBitacoraMantenimiento.Administrador), CancellationToken.None);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlAsync($"UPDATE mantenimiento_bitacora SET contenido = 'hack' WHERE id = {entrada.Id}"));
        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlAsync($"DELETE FROM mantenimiento_bitacora WHERE id = {entrada.Id}"));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Cambio_estado_activo_escribe_en_equipo_y_genera_historial_RN07_RN14()
    {
        var tenantId = await SeedTenantAsync("Mant Estado");
        await SeedPersonaConApplicationUser(tenantId);
        var eqId = await SeedEquipoAsync(tenantId, "Tanque B");
        var (svc, db, _) = Build(tenantId);

        var ok = await svc.CambiarEstadoActivoAsync(new CambioEstadoActivoRequest(
            TipoActivoMantenimiento.Equipo, eqId, "EnMantenimiento",
            "Vacio para limpieza", false, null), CancellationToken.None);

        Assert.True(ok);
        var equipo = await db.EquiposActivos.AsNoTracking().FirstAsync(e => e.Id == eqId);
        Assert.Equal(EstadoEquipoActivo.EnMantenimiento, equipo.Estado);

        var historial = await svc.ListarHistorialEstadoAsync(TipoActivoMantenimiento.Equipo, eqId, CancellationToken.None);
        Assert.Single(historial);
        Assert.Equal("Operativo", historial[0].EstadoAnterior);
        Assert.Equal("EnMantenimiento", historial[0].EstadoNuevo);

        // Mismo estado debe rechazar
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CambiarEstadoActivoAsync(new CambioEstadoActivoRequest(
                TipoActivoMantenimiento.Equipo, eqId, "EnMantenimiento",
                "Duplicado", false, null), CancellationToken.None));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Semaforo_panel_marca_rojo_para_plan_vencido_y_amarillo_para_proximo()
    {
        var tenantId = await SeedTenantAsync("Mant Semaforo");
        await SeedPersonaConApplicationUser(tenantId);
        var eqVencido = await SeedEquipoAsync(tenantId, "Vencido");
        var eqProximo = await SeedEquipoAsync(tenantId, "Proximo");
        var eqSano = await SeedEquipoAsync(tenantId, "Sano");
        var eqSinPlan = await SeedEquipoAsync(tenantId, "SinPlan");
        var (svc, db, _) = Build(tenantId);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        // Tres planes con fechas distintas. Usamos fecha_inicio en pasado solo escribiendo
        // directamente al DbContext porque CrearPlan rechaza pasado (RN-02).
        db.MantenimientoPlanes.Add(new MantenimientoPlan
        {
            ActivoTipo = TipoActivoMantenimiento.Equipo,
            ActivoId = eqVencido,
            Nombre = "Plan vencido",
            Frecuencia = FrecuenciaMantenimiento.Mensual,
            FechaInicio = hoy.AddDays(-30),
            ProximaEjecucion = hoy.AddDays(-5),
            Disparo = DisparoPlanMantenimiento.Automatico,
            DiasAlertaPrevio = 7,
            Activo = true,
            CreadoPorUsuarioId = _userId
        });
        db.MantenimientoPlanes.Add(new MantenimientoPlan
        {
            ActivoTipo = TipoActivoMantenimiento.Equipo,
            ActivoId = eqProximo,
            Nombre = "Plan proximo",
            Frecuencia = FrecuenciaMantenimiento.Mensual,
            FechaInicio = hoy.AddDays(-15),
            ProximaEjecucion = hoy.AddDays(3),
            Disparo = DisparoPlanMantenimiento.Automatico,
            DiasAlertaPrevio = 7,
            Activo = true,
            CreadoPorUsuarioId = _userId
        });
        db.MantenimientoPlanes.Add(new MantenimientoPlan
        {
            ActivoTipo = TipoActivoMantenimiento.Equipo,
            ActivoId = eqSano,
            Nombre = "Plan sano",
            Frecuencia = FrecuenciaMantenimiento.Anual,
            FechaInicio = hoy,
            ProximaEjecucion = hoy.AddDays(180),
            Disparo = DisparoPlanMantenimiento.Automatico,
            DiasAlertaPrevio = 7,
            Activo = true,
            CreadoPorUsuarioId = _userId
        });
        await db.SaveChangesAsync();

        var panel = await svc.ListarActivosPanelAsync(TipoActivoMantenimiento.Equipo, null, null, CancellationToken.None);
        Assert.Equal(SemaforoMantenimiento.Rojo, panel.First(a => a.ActivoId == eqVencido).Semaforo);
        Assert.Equal(SemaforoMantenimiento.Amarillo, panel.First(a => a.ActivoId == eqProximo).Semaforo);
        Assert.Equal(SemaforoMantenimiento.Verde, panel.First(a => a.ActivoId == eqSano).Semaforo);
        Assert.Equal(SemaforoMantenimiento.Negro, panel.First(a => a.ActivoId == eqSinPlan).Semaforo);

        await CleanTenant(tenantId);
    }


    // =======================================================================
    // M-01: job diario del preventivo (MantenimientoPreventivoJob)
    // =======================================================================

    [Fact]
    public async Task Job_plan_automatico_vencido_crea_intervencion_y_avanza_proxima_ejecucion()
    {
        var tenantId = await SeedTenantAsync("Mant Job Auto");
        await SeedPersonaConApplicationUser(tenantId);
        var equipoId = await SeedEquipoAsync(tenantId, "Planta electrica");
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var planId = await SeedPlanAsync(tenantId, equipoId, "Revision mensual planta",
            FrecuenciaMantenimiento.Mensual, hoy.AddDays(-3), DisparoPlanMantenimiento.Automatico, 7);

        var (job, db, scope) = BuildJob();
        using (scope)
        {
            await job.EjecutarAsync(CancellationToken.None);

            var intervenciones = await LeerIntervencionesAsync(tenantId);
            var i = Assert.Single(intervenciones);
            Assert.Equal(planId, i.PlanId);
            Assert.Equal(OrigenIntervencion.Automatico, i.Origen);
            Assert.Equal(TipoIntervencionMantenimiento.Preventivo, i.Tipo);
            // RN-03: la intervencion arrastra su tarea en 2.10
            Assert.NotNull(i.TareaId);

            // Avanza una frecuencia desde la fecha que vencia (-3 + 30 = +27), no desde hoy
            var plan = await LeerPlanAsync(planId);
            Assert.Equal(hoy.AddDays(27), plan.ProximaEjecucion);
            Assert.True(plan.ProximaEjecucion > hoy);
        }

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Job_plan_con_confirmacion_vencido_no_crea_intervencion_y_deja_alerta()
    {
        var tenantId = await SeedTenantAsync("Mant Job Confirm");
        await SeedPersonaConApplicationUser(tenantId);
        var equipoId = await SeedEquipoAsync(tenantId, "Ascensor");
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var planId = await SeedPlanAsync(tenantId, equipoId, "Revision ascensor",
            FrecuenciaMantenimiento.Mensual, hoy.AddDays(-1), DisparoPlanMantenimiento.ConConfirmacion, 7);

        var (job, db, scope) = BuildJob();
        using (scope)
        {
            await job.EjecutarAsync(CancellationToken.None);

            Assert.Empty(await LeerIntervencionesAsync(tenantId));

            var alertas = await LeerAlertasAsync(tenantId);
            var a = Assert.Single(alertas);
            Assert.Equal(planId, a.EntidadId);
            Assert.Equal(SeveridadAlerta.Critica, a.Severidad);
            Assert.True(a.Activa);

            // La fecha NO se toca: sigue vencido hasta que alguien confirme
            var plan = await LeerPlanAsync(planId);
            Assert.Equal(hoy.AddDays(-1), plan.ProximaEjecucion);
        }

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Job_plan_proximo_a_vencer_deja_alerta_de_advertencia_sin_intervencion()
    {
        var tenantId = await SeedTenantAsync("Mant Job Amarillo");
        await SeedPersonaConApplicationUser(tenantId);
        var equipoId = await SeedEquipoAsync(tenantId, "Motobomba");
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        // Vence en 3 dias y avisa con 7: cae dentro de la ventana de alerta previa
        var planId = await SeedPlanAsync(tenantId, equipoId, "Revision motobomba",
            FrecuenciaMantenimiento.Mensual, hoy.AddDays(3), DisparoPlanMantenimiento.Automatico, 7);

        var (job, db, scope) = BuildJob();
        using (scope)
        {
            await job.EjecutarAsync(CancellationToken.None);

            Assert.Empty(await LeerIntervencionesAsync(tenantId));
            var a = Assert.Single(await LeerAlertasAsync(tenantId));
            Assert.Equal(SeveridadAlerta.Advertencia, a.Severidad);
            Assert.Equal(planId, a.EntidadId);
        }

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Job_dos_corridas_no_duplican_intervencion_ni_alerta()
    {
        var tenantId = await SeedTenantAsync("Mant Job Idem");
        await SeedPersonaConApplicationUser(tenantId);
        var eqAuto = await SeedEquipoAsync(tenantId, "Tanque");
        var eqConfirm = await SeedEquipoAsync(tenantId, "Puerta garaje");
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedPlanAsync(tenantId, eqAuto, "Lavado tanque",
            FrecuenciaMantenimiento.Mensual, hoy.AddDays(-2), DisparoPlanMantenimiento.Automatico, 7);
        await SeedPlanAsync(tenantId, eqConfirm, "Engrase puerta",
            FrecuenciaMantenimiento.Mensual, hoy.AddDays(-2), DisparoPlanMantenimiento.ConConfirmacion, 7);

        var (job, db, scope) = BuildJob();
        using (scope)
        {
            await job.EjecutarAsync(CancellationToken.None);
            await job.EjecutarAsync(CancellationToken.None);

            // El automatico genero UNA sola intervencion (la 2a corrida ya no lo ve vencido)
            Assert.Single(await LeerIntervencionesAsync(tenantId));
            // El de confirmacion sigue vencido pero no acumula una alerta por corrida
            Assert.Single(await LeerAlertasAsync(tenantId));
        }

        await CleanTenant(tenantId);
    }

    // =======================================================================
    // Helpers
    // =======================================================================

    private (IMantenimientoService svc, PropiaDbContext db, IServiceScope scope) Build(Guid tenantId)
    {
        var scope = _services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        ctx.SetTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var http = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var noti = scope.ServiceProvider.GetRequiredService<Propia.Application.Notificaciones.INotificacionDispatcher>();
        return (new MantenimientoService(db, ctx, http, noti), db, scope);
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
            Documento = $"M{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = "Admin",
            Apellidos = "Mant",
            Email = $"mant.{Guid.NewGuid():N}@test.co",
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

    private async Task<Guid> SeedEquipoAsync(Guid tenantId, string nombre)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var e = new EquipoActivo
        {
            TenantId = tenantId,
            Nombre = nombre,
            Categoria = CategoriaEquipo.Otros,
            Estado = EstadoEquipoActivo.Operativo
        };
        ctx.EquiposActivos.Add(e);
        await ctx.SaveChangesAsync();
        return e.Id;
    }

    private async Task CleanTenant(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        // Deshabilitar triggers append-only para limpieza
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE mantenimiento_bitacora DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE mantenimiento_historial_estado DISABLE TRIGGER ALL");

        await ctx.Database.ExecuteSqlAsync($"DELETE FROM mantenimiento_adjuntos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM mantenimiento_bitacora WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM mantenimiento_historial_estado WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM mantenimiento_intervenciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM mantenimiento_planes WHERE tenant_id = {tenantId}");

        // Tareas creadas como vinculo de las intervenciones
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE tarea_historial DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tarea_historial WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE tarea_historial ENABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tareas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tarea_estados WHERE tenant_id = {tenantId}");

        await ctx.Database.ExecuteSqlAsync($"DELETE FROM equipos_activos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM zonas_comunes WHERE tenant_id = {tenantId}");

        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE mantenimiento_bitacora ENABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE mantenimiento_historial_estado ENABLE TRIGGER ALL");

        await ctx.Database.ExecuteSqlAsync($"DELETE FROM asp_net_users WHERE id = {_userId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id = {_personaId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM alertas_copropiedad WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }

    // ---- Helpers de M-01 (job del preventivo) ----

    private (Propia.Infrastructure.Jobs.MantenimientoPreventivoJob job, PropiaDbContext db, IServiceScope scope) BuildJob()
    {
        var scope = _services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var http = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var noti = scope.ServiceProvider.GetRequiredService<Propia.Application.Notificaciones.INotificacionDispatcher>();
        var svc = new MantenimientoService(db, ctx, http, noti);
        return (new Propia.Infrastructure.Jobs.MantenimientoPreventivoJob(db, ctx, svc), db, scope);
    }

    private async Task<Guid> SeedPlanAsync(
        Guid tenantId, Guid activoId, string nombre, FrecuenciaMantenimiento frecuencia,
        DateOnly proximaEjecucion, DisparoPlanMantenimiento disparo, int diasAlertaPrevio)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var p = new MantenimientoPlan
        {
            TenantId = tenantId,
            ActivoTipo = TipoActivoMantenimiento.Equipo,
            ActivoId = activoId,
            Nombre = nombre,
            Frecuencia = frecuencia,
            FechaInicio = proximaEjecucion,
            ProximaEjecucion = proximaEjecucion,
            Disparo = disparo,
            DiasAlertaPrevio = diasAlertaPrevio,
            Activo = true,
            CreadoPorUsuarioId = _userId
        };
        ctx.MantenimientoPlanes.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private async Task<List<MantenimientoIntervencion>> LeerIntervencionesAsync(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        return await ctx.MantenimientoIntervenciones.IgnoreQueryFilters()
            .Where(i => i.TenantId == tenantId).ToListAsync();
    }

    private async Task<List<AlertaCopropiedad>> LeerAlertasAsync(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        return await ctx.AlertasCopropiedad.IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId).ToListAsync();
    }

    private async Task<MantenimientoPlan> LeerPlanAsync(Guid planId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        return await ctx.MantenimientoPlanes.IgnoreQueryFilters().FirstAsync(p => p.Id == planId);
    }

}
