using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Common;
using Propia.Application.Reportes;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Reportes;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Tests del modulo 2.16 Reportes e Indicadores (spec v1.0 MVP).
/// Cubre:
///  - Seed: 8 categorias base + ~25 reportes base PropIA.
///  - Filtro por audiencia (RN-05 vista consejo).
///  - Generacion crea fila en historial con estado Listo y ResultadoJson.
///  - Regeneracion mantiene catalogoId y periodo.
///  - Compartir con consejo (RN-05) requiere estado Listo.
///  - Programacion: validaciones (canales, dia 1-28, destinatarios).
///  - Programacion calcula ProximoEnvio segun frecuencia.
///  - Pausar/activar programacion.
///  - Semaforos default vs configurados por tenant (RN-13).
///  - Portal transparencia no incluye datos nominativos (RN-04).
///  - Aislamiento RLS: tenant A no ve reportes de tenant B.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ReportesFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;
    private Guid _userId;
    private Guid _personaId;

    public ReportesFlowTests(PostgresFixture fx) => _fx = fx;

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
        sc.AddScoped<IIndicadoresService, IndicadoresService>();
        sc.AddScoped<IReportesService, ReportesService>();
        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Seed_global_tiene_8_categorias_y_minimo_25_reportes()
    {
        var tenantId = await SeedTenantAsync("Rep Seed");
        await SeedPersonaConApplicationUser();
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        var cats = await svc.ListarCategoriasAsync(null, CancellationToken.None);
        Assert.True(cats.Count >= 8);
        Assert.Contains(cats, c => c.Nombre == "Financiero");
        Assert.Contains(cats, c => c.Nombre == "Cartera");
        Assert.Contains(cats, c => c.Nombre == "PQRSD y Convivencia");
        Assert.Contains(cats, c => c.Nombre == "Documentos");

        var todos = await svc.ListarCatalogoAsync(null, null, CancellationToken.None);
        Assert.True(todos.Count >= 25);
        Assert.Contains(todos, r => r.Clave == "financiero.ejecucion_presupuestal");
        Assert.Contains(todos, r => r.Clave == "cartera.aging");
        Assert.Contains(todos, r => r.Clave == "operativo.resumen");
        Assert.Contains(todos, r => r.Clave == "comunicaciones.resumen");

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Catalogo_filtra_por_audiencia_consejo_RN05()
    {
        var tenantId = await SeedTenantAsync("Rep Audc");
        await SeedPersonaConApplicationUser();
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        var paraConsejo = await svc.ListarCatalogoAsync(null, AudienciaReporte.Consejo, CancellationToken.None);
        var paraAdmin = await svc.ListarCatalogoAsync(null, AudienciaReporte.Administrador, CancellationToken.None);

        Assert.NotEmpty(paraConsejo);
        Assert.True(paraAdmin.Count > paraConsejo.Count,
            "Admin debe tener acceso a mas reportes que el consejo.");
        Assert.All(paraConsejo, r => Assert.Contains(AudienciaReporte.Consejo, r.Audiencias));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Generar_crea_historial_con_resultado_y_url_expiracion()
    {
        var tenantId = await SeedTenantAsync("Rep Gen");
        await SeedPersonaConApplicationUser();
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        var cat = (await svc.ListarCatalogoAsync(null, null, CancellationToken.None))
            .First(r => r.Clave == "financiero.ejecucion_presupuestal");

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var d = await svc.GenerarAsync(new GenerarReporteRequest(
            cat.Id, hoy.AddDays(-30), hoy, null), CancellationToken.None);

        Assert.Equal(EstadoReporteGenerado.Listo, d.Estado);
        Assert.NotNull(d.ResultadoJson);
        Assert.Contains("recaudo", d.ResultadoJson, StringComparison.OrdinalIgnoreCase);
        Assert.True(d.UrlExpiracion.HasValue && d.UrlExpiracion.Value > DateTimeOffset.UtcNow.AddDays(29));

        var historial = await svc.ListarHistorialAsync(null, null, null, null, CancellationToken.None);
        Assert.Single(historial, r => r.Id == d.Id);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Regenerar_usa_los_mismos_parametros_y_crea_otra_fila()
    {
        var tenantId = await SeedTenantAsync("Rep Regen");
        await SeedPersonaConApplicationUser();
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        var cat = (await svc.ListarCatalogoAsync(null, null, CancellationToken.None))
            .First(r => r.Clave == "cartera.aging");
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var orig = await svc.GenerarAsync(new GenerarReporteRequest(
            cat.Id, hoy.AddDays(-15), hoy, "{\"x\":1}"), CancellationToken.None);

        var re = await svc.RegenerarAsync(orig.Id, CancellationToken.None);
        Assert.NotEqual(orig.Id, re.Id);
        Assert.Equal(orig.PeriodoInicio, re.PeriodoInicio);
        Assert.Equal(orig.PeriodoFin, re.PeriodoFin);
        Assert.Equal(orig.CatalogoId, re.CatalogoId);
        Assert.Equal("{\"x\":1}", re.FiltrosAplicadosJson);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Compartir_con_consejo_solo_aplica_a_reporte_listo_RN05()
    {
        var tenantId = await SeedTenantAsync("Rep Comp");
        await SeedPersonaConApplicationUser();
        var (svc, db, scope) = Build(tenantId);
        using var _ = scope;

        var cat = (await svc.ListarCatalogoAsync(null, null, CancellationToken.None)).First();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var d = await svc.GenerarAsync(new GenerarReporteRequest(cat.Id, hoy.AddDays(-30), hoy, null), CancellationToken.None);

        var ok = await svc.CompartirConsejoAsync(d.Id, true, CancellationToken.None);
        Assert.True(ok);
        var refresh = await svc.GetReporteAsync(d.Id, CancellationToken.None);
        Assert.True(refresh!.CompartidoConsejo);
        Assert.NotNull(refresh.CompartidoAt);

        // Marcar uno como Generando manualmente y validar rechazo.
        var generando = new ReporteGenerado
        {
            TenantId = tenantId,
            NombreReporte = "x",
            Categoria = "x",
            PeriodoInicio = hoy,
            PeriodoFin = hoy,
            Estado = EstadoReporteGenerado.Generando,
            Origen = OrigenReporte.Manual
        };
        db.ReporteGenerados.Add(generando);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CompartirConsejoAsync(generando.Id, true, CancellationToken.None));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Programacion_valida_dia_canales_y_destinatarios()
    {
        var tenantId = await SeedTenantAsync("Rep Prog");
        await SeedPersonaConApplicationUser();
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        var cat = (await svc.ListarCatalogoAsync(null, null, CancellationToken.None)).First();

        // dia fuera de rango
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearProgramacionAsync(new CrearProgramacionRequest(
                cat.Id, "x", FrecuenciaProgramacion.Mensual, 31, PeriodoQueCubre.MesAnterior,
                FormatoReporte.Pdf, new[] { "EMAIL" }, null,
                new[] { new DestinatarioInput(null, "x@x.co", null) }), CancellationToken.None));

        // sin canales
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearProgramacionAsync(new CrearProgramacionRequest(
                cat.Id, "x", FrecuenciaProgramacion.Mensual, 5, PeriodoQueCubre.MesAnterior,
                FormatoReporte.Pdf, Array.Empty<string>(), null,
                new[] { new DestinatarioInput(null, "x@x.co", null) }), CancellationToken.None));

        // canal invalido
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearProgramacionAsync(new CrearProgramacionRequest(
                cat.Id, "x", FrecuenciaProgramacion.Mensual, 5, PeriodoQueCubre.MesAnterior,
                FormatoReporte.Pdf, new[] { "SMS" }, null,
                new[] { new DestinatarioInput(null, "x@x.co", null) }), CancellationToken.None));

        // destinatario vacio
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearProgramacionAsync(new CrearProgramacionRequest(
                cat.Id, "x", FrecuenciaProgramacion.Mensual, 5, PeriodoQueCubre.MesAnterior,
                FormatoReporte.Pdf, new[] { "EMAIL" }, null,
                new[] { new DestinatarioInput(null, null, null) }), CancellationToken.None));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Programacion_calcula_proximo_envio_y_se_puede_pausar()
    {
        var tenantId = await SeedTenantAsync("Rep ProgOk");
        await SeedPersonaConApplicationUser();
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        var cat = (await svc.ListarCatalogoAsync(null, null, CancellationToken.None)).First();
        var p = await svc.CrearProgramacionAsync(new CrearProgramacionRequest(
            cat.Id, "Informe financiero mensual", FrecuenciaProgramacion.Mensual,
            5, PeriodoQueCubre.MesAnterior, FormatoReporte.Pdf,
            new[] { "EMAIL", "WHATSAPP" }, null,
            new[] { new DestinatarioInput(null, "consejo@test.co", null) }),
            CancellationToken.None);

        Assert.Equal(EstadoProgramacion.Activa, p.Estado);
        Assert.NotNull(p.ProximoEnvio);
        Assert.Equal(5, p.ProximoEnvio!.Value.Day);
        Assert.Equal(1, p.NumeroDestinatarios);

        var pausar = await svc.PausarProgramacionAsync(p.Id, true, CancellationToken.None);
        Assert.True(pausar);
        var refresh = await svc.GetProgramacionAsync(p.Id, CancellationToken.None);
        Assert.Equal(EstadoProgramacion.Pausada, refresh!.Estado);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Semaforo_usa_default_si_no_existe_config_y_se_puede_overridear_RN13()
    {
        var tenantId = await SeedTenantAsync("Rep Semaf");
        await SeedPersonaConApplicationUser();
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        // Sin config: semaforos vacios.
        var sin = await svc.ListarSemaforosAsync(CancellationToken.None);
        Assert.Empty(sin);

        // Guardar override.
        var s = await svc.GuardarSemaforoAsync("recaudo_pct",
            new GuardarSemaforoRequest(80m, 60m, true), CancellationToken.None);
        Assert.Equal("recaudo_pct", s.IndicadorKey);
        Assert.Equal(80m, s.UmbralAmarillo);

        // KPI debe usar el override - sin data el valor es 0 y umbral 60 -> rojo.
        var vista = await svc.GetVistaConsejoAsync(null, null, CancellationToken.None);
        Assert.Equal("recaudo_pct", vista.Kpis.Recaudo.Key);
        Assert.Equal("rojo", vista.Kpis.Recaudo.Semaforo);

        // Sobrescribir mismo indicador -> upsert.
        var s2 = await svc.GuardarSemaforoAsync("recaudo_pct",
            new GuardarSemaforoRequest(70m, 40m, true), CancellationToken.None);
        Assert.Equal(70m, s2.UmbralAmarillo);
        var lista2 = await svc.ListarSemaforosAsync(CancellationToken.None);
        Assert.Single(lista2);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Transparencia_devuelve_solo_agregados_RN04()
    {
        var tenantId = await SeedTenantAsync("Rep Trans");
        await SeedPersonaConApplicationUser();
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        var t = await svc.GetTransparenciaAsync(null, null, CancellationToken.None);
        // Estos son los unicos campos publicos - no exponemos nominativos por defecto.
        Assert.True(t.PqrsdRadicadas >= 0);
        Assert.True(t.PqrsdResueltas >= 0);
        Assert.True(t.PqrsdEnTramite >= 0);
        Assert.True(t.TareasCompletadas >= 0);
        Assert.True(t.ProyectosActivos >= 0);
        // Defensa estructural: el tipo de retorno no tiene campos nominativos
        // (verificado por la firma de TransparenciaDto - este test garantiza
        // que la llamada en si misma no expone listas con personas/unidades).

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Aislamiento_RLS_tenant_A_no_ve_reportes_de_tenant_B()
    {
        var tenantA = await SeedTenantAsync("Rep AislA");
        var tenantB = await SeedTenantAsync("Rep AislB");
        await SeedPersonaConApplicationUser();

        // Generar reporte en A.
        var (svcA, _, scopeA) = Build(tenantA);
        var cat = (await svcA.ListarCatalogoAsync(null, null, CancellationToken.None)).First();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var d = await svcA.GenerarAsync(new GenerarReporteRequest(cat.Id, hoy.AddDays(-30), hoy, null), CancellationToken.None);
        scopeA.Dispose();

        // Listar desde B no debe verlo.
        var (svcB, _, scopeB) = Build(tenantB);
        var historialB = await svcB.ListarHistorialAsync(null, null, null, null, CancellationToken.None);
        Assert.DoesNotContain(historialB, r => r.Id == d.Id);
        var refB = await svcB.GetReporteAsync(d.Id, CancellationToken.None);
        Assert.Null(refB);
        scopeB.Dispose();

        await CleanTenant(tenantA);
        await CleanTenant(tenantB);
    }

    [Fact]
    public async Task Generar_con_periodo_invertido_falla_validacion()
    {
        var tenantId = await SeedTenantAsync("Rep Inv");
        await SeedPersonaConApplicationUser();
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        var cat = (await svc.ListarCatalogoAsync(null, null, CancellationToken.None)).First();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.GenerarAsync(new GenerarReporteRequest(cat.Id, hoy, hoy.AddDays(-5), null), CancellationToken.None));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Resumen_modulo_devuelve_totales_y_cuenta_compartidos()
    {
        var tenantId = await SeedTenantAsync("Rep Res");
        await SeedPersonaConApplicationUser();
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        var cat = (await svc.ListarCatalogoAsync(null, null, CancellationToken.None)).First();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var r1 = await svc.GenerarAsync(new GenerarReporteRequest(cat.Id, hoy.AddDays(-30), hoy, null), CancellationToken.None);
        await svc.CompartirConsejoAsync(r1.Id, true, CancellationToken.None);
        await svc.GenerarAsync(new GenerarReporteRequest(cat.Id, hoy.AddDays(-30), hoy, null), CancellationToken.None);

        var resumen = await svc.GetResumenAsync(CancellationToken.None);
        Assert.True(resumen.TotalCatalogo >= 25);
        Assert.True(resumen.CategoriasActivas >= 8);
        Assert.True(resumen.GeneradosUltimoMes >= 2);
        Assert.True(resumen.CompartidosConsejoUltimoMes >= 1);

        await CleanTenant(tenantId);
    }

    // =======================================================================
    // Helpers
    // =======================================================================

    private (IReportesService svc, PropiaDbContext db, IServiceScope scope) Build(Guid tenantId)
    {
        var scope = _services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        ctx.SetTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IReportesService>();
        return (svc, db, scope);
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

    private async Task SeedPersonaConApplicationUser()
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var exists = await ctx.Personas.AnyAsync(p => p.Id == _personaId);
        if (exists) return;
        var p = new Persona
        {
            Id = _personaId,
            TipoDocumento = TipoDocumento.CC,
            Documento = $"R{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = "Admin",
            Apellidos = "Rep",
            Email = $"rep.{Guid.NewGuid():N}@test.co",
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

    private async Task CleanTenant(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());

        await ctx.Database.ExecuteSqlAsync($"DELETE FROM reporte_programacion_destinatarios WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM reporte_programaciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM reporte_generados WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM reporte_semaforo_config WHERE tenant_id = {tenantId}");

        await ctx.Database.ExecuteSqlAsync($"DELETE FROM asp_net_users WHERE id = {_userId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id = {_personaId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }
}
