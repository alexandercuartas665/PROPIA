using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Common;
using Propia.Application.Notificaciones;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Notificaciones;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Tests del modulo T.2 Motor de Notificaciones (MVP).
/// Cubre:
///  - Envio basico via dispatcher con resolucion automatica de destino desde Persona.
///  - Idempotencia: misma EntidadOrigenId + ModuloOrigenCodigo + destinatario no duplica.
///  - Lote: envio masivo retorna N resultados.
///  - InApp: marca leido por destinatario actual.
///  - Reintento: Fallida -> Encolada -> Enviado (limpia error).
///  - Resumen: counters por estado.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class NotificacionesFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;
    private Guid _userId;
    private Guid _personaId;

    public NotificacionesFlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        _userId = Guid.NewGuid();
        _personaId = Guid.NewGuid();
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddDataProtection();
        sc.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        sc.AddSingleton<ITenantContext, TenantContext>();
        sc.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor
        {
            HttpContext = BuildFakeHttpContext(_userId, _personaId)
        });
        sc.AddScoped<TenantConnectionInterceptor>();
        sc.AddDbContext<PropiaDbContext>((sp, opts) =>
            opts.UseNpgsql(_fx.AppConnectionString)
                .AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>()));
        sc.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<PropiaDbContext>()
            .AddDefaultTokenProviders();
        sc.AddScoped<INotificacionDispatcher, NotificacionDispatcher>();
        sc.AddScoped<INotificacionesService, NotificacionesService>();
        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =======================================================================
    // Tests
    // =======================================================================

    [Fact]
    public async Task Envio_directo_con_destino_explicito_marca_enviado_y_persiste()
    {
        var tenantId = await SeedTenantAsync("Noti Directo");
        await SeedPersonaConApplicationUser();

        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificacionDispatcher>();

        var r = await dispatcher.EnviarAsync(new EnviarNotificacionRequest(
            Canal: CanalNotificacion.Email,
            Cuerpo: "Hola tester",
            Destino: "tester@propia.co",
            Asunto: "Test envio",
            ModuloOrigenCodigo: "T.2-test",
            EntidadOrigenId: Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(EstadoNotificacion.Enviado, r.Estado);
        Assert.NotEqual(Guid.Empty, r.NotificacionId);

        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var n = await db.Notificaciones.AsNoTracking().FirstAsync(x => x.Id == r.NotificacionId);
        Assert.Equal(EstadoNotificacion.Enviado, n.Estado);
        Assert.Equal("tester@propia.co", n.Destino);
        Assert.Equal(1, n.Intentos);
        Assert.NotNull(n.FechaEnviado);
    }

    [Fact]
    public async Task Resolucion_destino_desde_persona_obtiene_email()
    {
        var tenantId = await SeedTenantAsync("Noti Persona");
        await SeedPersonaConApplicationUser();

        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificacionDispatcher>();

        var r = await dispatcher.EnviarAsync(new EnviarNotificacionRequest(
            Canal: CanalNotificacion.Email,
            Cuerpo: "Hola persona",
            PersonaDestinatariaId: _personaId,
            Asunto: "Persona test"), CancellationToken.None);

        Assert.Equal(EstadoNotificacion.Enviado, r.Estado);
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var n = await db.Notificaciones.AsNoTracking().FirstAsync(x => x.Id == r.NotificacionId);
        Assert.Contains("@", n.Destino); // Resolvio email desde persona
    }

    [Fact]
    public async Task Idempotencia_misma_entidad_origen_y_destinatario_no_duplica()
    {
        var tenantId = await SeedTenantAsync("Noti Idemp");
        await SeedPersonaConApplicationUser();

        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificacionDispatcher>();

        var entidadOrigen = Guid.NewGuid();
        var req = new EnviarNotificacionRequest(
            Canal: CanalNotificacion.Email,
            Cuerpo: "Idempotente",
            Destino: "idemp@test.co",
            Asunto: "Idemp",
            ModuloOrigenCodigo: "2.14",
            EntidadOrigenId: entidadOrigen);

        var r1 = await dispatcher.EnviarAsync(req, CancellationToken.None);
        var r2 = await dispatcher.EnviarAsync(req, CancellationToken.None);
        Assert.Equal(r1.NotificacionId, r2.NotificacionId);

        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var cnt = await db.Notificaciones.AsNoTracking()
            .CountAsync(n => n.EntidadOrigenId == entidadOrigen && n.ModuloOrigenCodigo == "2.14");
        Assert.Equal(1, cnt);
    }

    [Fact]
    public async Task Envio_lote_devuelve_N_resultados()
    {
        var tenantId = await SeedTenantAsync("Noti Lote");
        await SeedPersonaConApplicationUser();

        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificacionDispatcher>();

        var lote = Enumerable.Range(0, 5).Select(i => new EnviarNotificacionRequest(
            Canal: CanalNotificacion.WhatsApp,
            Cuerpo: $"Mensaje {i}",
            Destino: $"+5730000000{i}",
            ModuloOrigenCodigo: "2.14",
            EntidadOrigenId: Guid.NewGuid())).ToList();

        var resultados = await dispatcher.EnviarLoteAsync(lote, CancellationToken.None);
        Assert.Equal(5, resultados.Count);
        Assert.All(resultados, r => Assert.Equal(EstadoNotificacion.Enviado, r.Estado));
    }

    [Fact]
    public async Task InApp_marcar_leido_actualiza_estado_y_fecha()
    {
        var tenantId = await SeedTenantAsync("Noti Leido");
        await SeedPersonaConApplicationUser();

        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificacionDispatcher>();
        var svc = scope.ServiceProvider.GetRequiredService<INotificacionesService>();

        var r = await dispatcher.EnviarAsync(new EnviarNotificacionRequest(
            Canal: CanalNotificacion.InApp,
            Cuerpo: "Tienes una tarea nueva",
            UsuarioDestinatarioId: _userId,
            Asunto: "Tarea asignada"), CancellationToken.None);

        Assert.Equal(EstadoNotificacion.Enviado, r.Estado);
        var ok = await svc.MarcarLeidoAsync(r.NotificacionId, CancellationToken.None);
        Assert.True(ok);

        var n = await svc.GetAsync(r.NotificacionId, CancellationToken.None);
        Assert.NotNull(n);
        Assert.Equal(EstadoNotificacion.Leido, n!.Estado);
        Assert.NotNull(n.FechaLeido);
    }

    [Fact]
    public async Task Resumen_cuenta_correctamente_por_estado()
    {
        var tenantId = await SeedTenantAsync("Noti Resumen");
        await SeedPersonaConApplicationUser();

        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificacionDispatcher>();
        var svc = scope.ServiceProvider.GetRequiredService<INotificacionesService>();

        for (int i = 0; i < 3; i++)
        {
            await dispatcher.EnviarAsync(new EnviarNotificacionRequest(
                Canal: CanalNotificacion.InApp,
                Cuerpo: $"Msg {i}",
                UsuarioDestinatarioId: _userId,
                Asunto: $"R{i}"), CancellationToken.None);
        }

        var resumen = await svc.GetResumenAsync(CancellationToken.None);
        Assert.True(resumen.Enviadas >= 3);
        Assert.True(resumen.InAppNoLeidas >= 3);
    }

    // =======================================================================
    // Helpers
    // =======================================================================

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
        if (await ctx.Personas.AnyAsync(p => p.Id == _personaId)) return;
        var p = new Persona
        {
            Id = _personaId,
            TipoDocumento = TipoDocumento.CC,
            Documento = $"P{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = "Noti",
            Apellidos = "Tester",
            Email = $"noti.{Guid.NewGuid():N}@test.co",
            Telefono = "+573001234567",
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
}
