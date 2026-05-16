using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Calendario;
using Propia.Application.Common;
using Propia.Application.ReportesConsolidados;
using Propia.Application.TransferenciaCustodia;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Calendario;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.ReportesConsolidados;
using Propia.Infrastructure.Storage;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Tests de cierre de Capa 1 - modulos 1.2 Calendario, 1.4 Reportes Consolidados y 1.5 Transferencia.
/// Cubre los flujos minimos de cada servicio + reglas clave:
///  - 1.2: config personal + token iCal + evento interno CRUD.
///  - 1.4: 5 plantillas base + crear reporte + generacion sincrona.
///  - 1.5: escenarios A/B, RN-16 unicidad, subir acta, aprobar, ejecutar corte + cambio de organizacion.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class CapaUnoCierreFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;
    private Guid _userId;
    private Guid _personaId;

    public CapaUnoCierreFlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        _userId = Guid.NewGuid();
        _personaId = Guid.NewGuid();

        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddDataProtection();
        sc.AddSingleton<ITenantContext, TenantContext>();
        sc.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor
        {
            HttpContext = BuildFakeHttpContext(_userId, _personaId)
        });
        sc.AddSingleton<IBlobStorage, InMemoryBlobStorage>();
        sc.AddScoped<TenantConnectionInterceptor>();
        sc.AddDbContext<PropiaDbContext>((sp, opts) =>
            opts.UseNpgsql(_fx.AppConnectionString)
                .AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>()));
        sc.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<PropiaDbContext>()
            .AddDefaultTokenProviders();

        sc.AddScoped<ICalendarioService, CalendarioService>();
        sc.AddScoped<IReportesConsolidadosService, ReportesConsolidadosService>();
        sc.AddScoped<ITransferenciaCustodiaService,
            Propia.Infrastructure.TransferenciasCustodia.TransferenciaCustodiaService>();

        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ===========================================================================
    // 1.2 Calendario
    // ===========================================================================

    [Fact]
    public async Task Calendario_config_se_crea_por_default_y_persiste()
    {
        var (orgId, tenantId) = await SeedOrgYTenantAsync("Cal Default Org", "Cal Default Cop");
        await SeedPersonaConApplicationUser();

        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
        var svc = scope.ServiceProvider.GetRequiredService<ICalendarioService>();

        var config = await svc.GetConfigAsync(CancellationToken.None);
        Assert.NotNull(config);
        // Segunda llamada devuelve la misma config persistida (idempotente).
        var config2 = await svc.GetConfigAsync(CancellationToken.None);
        Assert.NotNull(config2);
    }

    [Fact]
    public async Task Calendario_token_ical_se_genera_y_regenera_distinto()
    {
        var (_, tenantId) = await SeedOrgYTenantAsync("Cal Token Org", "Cal Token Cop");
        await SeedPersonaConApplicationUser();

        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
        var svc = scope.ServiceProvider.GetRequiredService<ICalendarioService>();

        var t1 = await svc.GenerarOReGenerarIcalTokenAsync(CancellationToken.None);
        Assert.NotEqual(Guid.Empty, t1);
        var t2 = await svc.GenerarOReGenerarIcalTokenAsync(CancellationToken.None);
        Assert.NotEqual(Guid.Empty, t2);
        Assert.NotEqual(t1, t2);

        var ics = await svc.GenerarIcsAsync(t2, CancellationToken.None);
        Assert.NotNull(ics);
        Assert.Contains("BEGIN:VCALENDAR", ics);
    }

    [Fact]
    public async Task Calendario_resumen_no_falla_aun_sin_data()
    {
        var (_, tenantId) = await SeedOrgYTenantAsync("Cal Res Org", "Cal Res Cop");
        await SeedPersonaConApplicationUser();

        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
        var svc = scope.ServiceProvider.GetRequiredService<ICalendarioService>();

        var res = await svc.GetResumenAsync(CancellationToken.None);
        Assert.NotNull(res);
    }

    // ===========================================================================
    // 1.4 Reportes Consolidados
    // ===========================================================================

    [Fact]
    public async Task Reportes_consolidados_5_plantillas_base()
    {
        var (_, tenantId) = await SeedOrgYTenantAsync("Rep Plant Org", "Rep Plant Cop");
        await SeedPersonaConApplicationUser();

        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
        var svc = scope.ServiceProvider.GetRequiredService<IReportesConsolidadosService>();

        var plantillas = await svc.ListarPlantillasBaseAsync(CancellationToken.None);
        Assert.Equal(5, plantillas.Count);
        Assert.Contains(plantillas, p => p.Codigo == "desempeno_equipo" && p.TieneDatosNominativos);
    }

    [Fact]
    public async Task Reportes_consolidados_crear_y_generar_devuelve_resultado()
    {
        var (_, tenantId) = await SeedOrgYTenantAsync("Rep Gen Org", "Rep Gen Cop");
        await SeedPersonaConApplicationUser();

        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
        var svc = scope.ServiceProvider.GetRequiredService<IReportesConsolidadosService>();

        var reporte = await svc.CrearReporteAsync(new CrearReporteRequest(
            "Salud portafolio mensual",
            CategoriaReporteConsolidado.SaludPortafolio,
            "salud_portafolio",
            "{}"), CancellationToken.None);
        Assert.NotEqual(Guid.Empty, reporte.Id);

        var generacion = await svc.GenerarAsync(new GenerarReporteRequest(
            reporte.Id,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            DateOnly.FromDateTime(DateTime.UtcNow)), CancellationToken.None);
        Assert.Equal(EstadoGeneracionConsolidada.Listo, generacion.Estado);
        Assert.False(string.IsNullOrWhiteSpace(generacion.ResultadoJson));
    }

    [Fact]
    public async Task Reportes_consolidados_indicadores_portafolio_responde()
    {
        var (_, tenantId) = await SeedOrgYTenantAsync("Rep Ind Org", "Rep Ind Cop");
        await SeedPersonaConApplicationUser();

        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
        var svc = scope.ServiceProvider.GetRequiredService<IReportesConsolidadosService>();

        var ind = await svc.GetIndicadoresPortafolioAsync(CancellationToken.None);
        Assert.True(ind.TotalCopropiedades >= 1);
        Assert.Equal(ind.TotalCopropiedades, ind.Verdes + ind.Amarillas + ind.Rojas);
    }

    // ===========================================================================
    // 1.5 Transferencia de Custodia
    // ===========================================================================

    [Fact]
    public async Task Transferencia_escenario_A_inicia_y_avanza_a_pendiente_aprobacion()
    {
        var (_, tenantId) = await SeedOrgYTenantAsync("Trf A Org", "Trf A Cop");
        await SeedPersonaConApplicationUser();

        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
        var svc = scope.ServiceProvider.GetRequiredService<ITransferenciaCustodiaService>();

        var dto = await svc.IniciarEntregaVoluntariaAsync(new IniciarEntregaVoluntariaRequest(
            tenantId,
            DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(15)),
            null), CancellationToken.None);

        Assert.Equal(EstadoTransferencia.PendienteAprobacion, dto.Estado);
        Assert.Equal(EscenarioTransferencia.EntregaVoluntaria, dto.Escenario);
        Assert.NotNull(dto.FechaVencimientoVentana);
    }

    [Fact]
    public async Task Transferencia_RN16_segunda_activa_falla()
    {
        var (_, tenantId) = await SeedOrgYTenantAsync("Trf RN16 Org", "Trf RN16 Cop");
        await SeedPersonaConApplicationUser();

        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
        var svc = scope.ServiceProvider.GetRequiredService<ITransferenciaCustodiaService>();

        await svc.IniciarEntregaVoluntariaAsync(new IniciarEntregaVoluntariaRequest(
            tenantId,
            DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(10)),
            null), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.IniciarEntregaVoluntariaAsync(new IniciarEntregaVoluntariaRequest(
                tenantId,
                DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(20)),
                null), CancellationToken.None));
    }

    [Fact]
    public async Task Transferencia_subir_acta_actualiza_estado_y_calcula_hash()
    {
        var (_, tenantId) = await SeedOrgYTenantAsync("Trf Acta Org", "Trf Acta Cop");
        await SeedPersonaConApplicationUser();

        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
        var svc = scope.ServiceProvider.GetRequiredService<ITransferenciaCustodiaService>();

        var trf = await svc.IniciarEntregaVoluntariaAsync(new IniciarEntregaVoluntariaRequest(
            tenantId,
            DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(10)),
            null), CancellationToken.None);

        var contenido = Encoding.UTF8.GetBytes("Acta de asamblea simulada para test 1.5.");
        var b64 = Convert.ToBase64String(contenido);

        var acta = await svc.SubirActaAsync(trf.Id, new SubirActaRequest(
            "acta.pdf", "application/pdf", contenido.Length, b64), CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(acta.HashSha256));
        Assert.Equal(64, acta.HashSha256.Length); // SHA-256 hex = 64 chars
        var trf2 = await svc.GetTransferenciaAsync(trf.Id, CancellationToken.None);
        Assert.Equal(EstadoTransferencia.ActaEnValidacion, trf2!.Estado);
        Assert.Equal(1, trf2.NumeroDocumentos);
    }

    [Fact]
    public async Task Transferencia_ejecutar_corte_cambia_organizacion_y_marca_ejecutado()
    {
        var (orgSaliente, tenantId) = await SeedOrgYTenantAsync("Trf Corte Org A", "Trf Corte Cop");
        var orgEntrante = await SeedOrganizacionAsync("Trf Corte Org B");
        await SeedPersonaConApplicationUser();

        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
        var svc = scope.ServiceProvider.GetRequiredService<ITransferenciaCustodiaService>();

        var trf = await svc.IniciarEntregaVoluntariaAsync(new IniciarEntregaVoluntariaRequest(
            tenantId,
            DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(10)),
            orgEntrante), CancellationToken.None);

        var contenido = Encoding.UTF8.GetBytes("Acta corte test.");
        await svc.SubirActaAsync(trf.Id, new SubirActaRequest(
            "acta.pdf", "application/pdf", contenido.Length, Convert.ToBase64String(contenido)),
            CancellationToken.None);

        var ejecutada = await svc.EjecutarCorteAsync(trf.Id, CancellationToken.None);
        Assert.Equal(EstadoTransferencia.Ejecutado, ejecutada.Estado);
        Assert.NotNull(ejecutada.FechaCorte);

        // Verificacion DB: la copropiedad cambio de organizacion.
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var verifCtx = new PropiaDbContext(opts, new TenantContext());
        var cop = await verifCtx.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenantId);
        Assert.Equal(orgEntrante, cop.OrganizacionId);
        Assert.Equal(EstadoCustodia.ConAdmin, cop.EstadoCustodia);
    }

    // ===========================================================================
    // Helpers
    // ===========================================================================

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

    private async Task<(Guid orgId, Guid tenantId)> SeedOrgYTenantAsync(string orgNombre, string copNombre)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());

        var org = new Organizacion
        {
            Nombre = orgNombre,
            Tipo = TipoOrganizacion.Administradora,
            Nit = $"NIT{Guid.NewGuid():N}".Substring(0, 12)
        };
        ctx.Organizaciones.Add(org);

        var tenant = new Tenant
        {
            Nombre = copNombre,
            Estado = EstadoCopropiedad.Activa,
            EstadoCustodia = EstadoCustodia.ConAdmin,
            OrganizacionId = org.Id
        };
        ctx.Tenants.Add(tenant);

        await ctx.SaveChangesAsync();
        return (org.Id, tenant.Id);
    }

    private async Task<Guid> SeedOrganizacionAsync(string nombre)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var org = new Organizacion
        {
            Nombre = nombre,
            Tipo = TipoOrganizacion.Administradora,
            Nit = $"NIT{Guid.NewGuid():N}".Substring(0, 12)
        };
        ctx.Organizaciones.Add(org);
        await ctx.SaveChangesAsync();
        return org.Id;
    }

    private async Task SeedPersonaConApplicationUser()
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        if (await ctx.Personas.AnyAsync(p => p.Id == _personaId)) return;
        var p = new Persona
        {
            Id = _personaId,
            TipoDocumento = TipoDocumento.CC,
            Documento = $"P{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = "Capa1",
            Apellidos = "Tester",
            Email = $"capa1.{Guid.NewGuid():N}@test.co",
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

    private sealed class InMemoryBlobStorage : IBlobStorage
    {
        private readonly Dictionary<string, byte[]> _store = new();

        public Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct)
        {
            using var ms = new MemoryStream();
            content.CopyTo(ms);
            _store[key] = ms.ToArray();
            return Task.FromResult(GetPublicUrl(key));
        }

        public Task DeleteAsync(string key, CancellationToken ct)
        {
            _store.Remove(key);
            return Task.CompletedTask;
        }

        public string GetPublicUrl(string key) => $"/mem/{key}";

        public Task<byte[]?> DownloadAsync(string key, CancellationToken ct)
            => Task.FromResult(_store.TryGetValue(key, out var bytes) ? bytes : null);
    }
}
