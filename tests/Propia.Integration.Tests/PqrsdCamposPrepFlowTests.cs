using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Common;
using Propia.Application.Pqrsd;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Pqrsd;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Preparacion Fase 1 del Selector de Campos (PQRSD): el catalogo de campos del sistema
/// (<see cref="PqrsdCamposSistema"/>) y el aislamiento por tenant de las definiciones de campo propio, que
/// los endpoints estandar 'pqrsd-campos' exponen delegando en los servicios actuales.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class PqrsdCamposPrepFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;
    private Guid _personaTestId;

    public PqrsdCamposPrepFlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        _personaTestId = Guid.NewGuid();
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddDataProtection();
        sc.AddSingleton<ITenantContext, TenantContext>();
        sc.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = BuildFakeHttpContext(_personaTestId) });
        sc.AddScoped<TenantConnectionInterceptor>();
        sc.AddDbContext<PropiaDbContext>((sp, opts) =>
            opts.UseNpgsql(_fx.AppConnectionString)
                .AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>()));
        sc.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<PropiaDbContext>()
            .AddDefaultTokenProviders();
        sc.AddSingleton<Propia.Application.Notificaciones.INotificacionDispatcher, FakeNotificacionDispatcher>();
        sc.AddScoped<Propia.Application.Tareas.ITareasService, Propia.Infrastructure.Tareas.TareasService>();
        sc.AddSingleton<Propia.Application.Documents.IMembreteDocumentBuilder, Propia.Infrastructure.Documents.MembreteDocumentBuilder>();
        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void Catalogo_de_campos_del_sistema_cubre_las_9_columnas_del_tablero()
    {
        // Espejo exacto de _colsFijas de PqrsKanban.razor, en el mismo orden.
        var esperado = new[] { "radicado", "asunto", "tipo", "categoria", "estado", "semaforo", "radicador", "unidad", "vence" };
        Assert.Equal(esperado, PqrsdCamposSistema.Todos.Select(c => c.Clave).ToArray());

        // Todos visibles por defecto (hoy PqrsKanban muestra las 9 columnas fijas).
        Assert.All(PqrsdCamposSistema.Todos, c => Assert.True(c.VisiblePorDefecto));
        Assert.Equal(PqrsdCamposSistema.Todos.Select(c => c.Clave), PqrsdCamposSistema.ClavesVisiblesPorDefecto);

        // El radicado es el identificador (columna estructural fija que no se puede ocultar).
        Assert.True(PqrsdCamposSistema.Por("radicado")!.Fija);
        Assert.All(PqrsdCamposSistema.Todos.Where(c => c.Clave != "radicado"), c => Assert.False(c.Fija));
        Assert.Equal(TipoCampoTablero.Fecha, PqrsdCamposSistema.Por("vence")!.Tipo);
        Assert.Equal(TipoCampoTablero.Seleccion, PqrsdCamposSistema.Por("tipo")!.Tipo);
        Assert.Null(PqrsdCamposSistema.Por("no-existe"));
        Assert.Equal("pqrsd", PqrsdCamposSistema.Entidad);

        // Opcion (b): ningun campo del sistema de PQRSD tiene opciones editables por el gestor
        // (tipo/categoria/estado viven en sus propias tablas y no se tocan).
        Assert.All(PqrsdCamposSistema.Todos, c => Assert.False(c.OpcionesEditables));
    }

    [Fact]
    public async Task Campo_propio_pqrsd_se_aisla_por_tenant()
    {
        var tenantA = await SeedTenantAsync("PQRSD Campos A");
        var tenantB = await SeedTenantAsync("PQRSD Campos B");

        Guid campoId;
        var (svcA, _, scopeA) = Build(tenantA);
        using (scopeA)
        {
            var creado = await svcA.CrearCampoAsync(new GuardarCampoPqrsdRequest(
                Label: "Prioridad interna", Tipo: TipoCampoTablero.Seleccion, Opciones: "Alta\nMedia\nBaja",
                MostrarEnFiltro: true, Columna: 1, Descripcion: null, Requerido: false,
                ValorPorDefecto: null, PermiteVarios: false, CamposSuma: null), CancellationToken.None);
            campoId = creado.Id;

            var enA = await svcA.ListarCamposAsync(CancellationToken.None);
            Assert.Contains(enA, c => c.Id == campoId);   // el dueno lo ve
        }

        var (svcB, _, scopeB) = Build(tenantB);
        using (scopeB)
        {
            var enB = await svcB.ListarCamposAsync(CancellationToken.None);
            Assert.DoesNotContain(enB, c => c.Id == campoId);   // otro tenant NO
        }

        await CleanTenant(tenantA);
        await CleanTenant(tenantB);
    }

    // ===================== Helpers (mismo patron que PqrsdFlowTests) =====================

    private (IPqrsdService svc, PropiaDbContext db, IServiceScope scope) Build(Guid tenantId)
    {
        var scope = _services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        ctx.SetTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var http = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var noti = scope.ServiceProvider.GetRequiredService<Propia.Application.Notificaciones.INotificacionDispatcher>();
        var tareas = scope.ServiceProvider.GetRequiredService<Propia.Application.Tareas.ITareasService>();
        var membrete = scope.ServiceProvider.GetRequiredService<Propia.Application.Documents.IMembreteDocumentBuilder>();
        return (new PqrsdService(db, ctx, http, noti, tareas, membrete), db, scope);
    }

    private HttpContext BuildFakeHttpContext(Guid personaId)
    {
        var ctx = new DefaultHttpContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("user_id", Guid.NewGuid().ToString()),
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

    private async Task CleanTenant(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_campo_valores WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_campos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }
}
