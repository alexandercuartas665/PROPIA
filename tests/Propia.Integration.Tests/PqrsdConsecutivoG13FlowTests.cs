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
using System.Security.Claims;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// G-13 (RN): el consecutivo de radicado no era concurrency-safe (leer-incrementar en memoria) -> dos
/// radicaciones en paralelo tomaban el mismo numero y chocaban con el UNIQUE (tenant_id, numero_radicado)
/// -> 500. Con el advisory lock transaccional por (tenant, anio), N radicaciones simultaneas producen N
/// numeros unicos sin excepcion.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class PqrsdConsecutivoG13FlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;
    private Guid _personaTestId;

    public PqrsdConsecutivoG13FlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        _personaTestId = Guid.NewGuid();
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddDataProtection();
        // Scoped (no Singleton) para que cada radicacion concurrente use SU propio contexto de tenant, como en produccion.
        sc.AddScoped<ITenantContext, TenantContext>();
        sc.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = BuildFakeHttpContext(_personaTestId) });
        sc.AddScoped<TenantConnectionInterceptor>();
        sc.AddDbContext<PropiaDbContext>((sp, opts) =>
            opts.UseNpgsql(_fx.AppConnectionString)
                .AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>()),
            ServiceLifetime.Scoped);
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
    public async Task Radicaciones_concurrentes_no_colisionan_en_el_consecutivo()
    {
        var tenantId = await SeedTenantAsync("PQRSD G13");
        await SeedPersonaAsync(tenantId);

        // Prepara catalogos/plazos (seed lazy) ANTES de la carga concurrente, para aislar la prueba al
        // consecutivo. El config de consecutivos NO se pre-siembra: su creacion tambien la serializa el lock.
        Guid catId;
        var (svc0, _, scope0) = Build(tenantId);
        using (scope0)
        {
            var cats = await svc0.ListarCategoriasAsync(CancellationToken.None);
            await svc0.ListarPlazosAsync(CancellationToken.None);
            catId = cats[0].Id;
        }

        const int N = 12;
        var radicaciones = Enumerable.Range(0, N).Select(_ => Task.Run(async () =>
        {
            var (svc, _, scope) = Build(tenantId);
            using (scope)
            {
                var x = await svc.RadicarAsync(new RadicarPqrsdRequest(
                    TipoPqrsd.Peticion, catId,
                    "Radicacion concurrente para validar que el consecutivo no colisiona bajo carga simultanea.",
                    false, null), CancellationToken.None);
                return x.NumeroRadicado;
            }
        })).ToArray();

        // Sin el lock, alguna de las N tareas reventaria con 500 (UNIQUE); Task.WhenAll propaga la excepcion.
        var radicados = await Task.WhenAll(radicaciones);

        Assert.Equal(N, radicados.Length);
        Assert.Equal(N, radicados.Distinct().Count());   // todos unicos: 0 colisiones

        // Ademas: son N consecutivos sin huecos (la secuencia no perdio ni repitio numeros).
        var seqs = radicados.Select(r => int.Parse(r[(r.LastIndexOf('-') + 1)..])).OrderBy(s => s).ToList();
        Assert.Equal(Enumerable.Range(seqs[0], N).ToList(), seqs);

        await CleanTenant(tenantId);
    }

    // ===================== Helpers (patron de PqrsdFlowTests) =====================

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

    private async Task SeedPersonaAsync(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        ctx.Personas.Add(new Persona
        {
            Id = _personaTestId,
            TipoDocumento = TipoDocumento.CC,
            Documento = $"P{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = "Test",
            Apellidos = "Radicador",
            Email = $"radic.{Guid.NewGuid():N}@test.co",
            PerfilIncompleto = false
        });
        await ctx.SaveChangesAsync();
    }

    private async Task CleanTenant(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE pqrsd_historial_estados DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_historial_estados WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE pqrsd_historial_estados ENABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_expedientes WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_consecutivo_configs WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_configuracion_plazos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_categorias WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id = {_personaTestId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }
}
