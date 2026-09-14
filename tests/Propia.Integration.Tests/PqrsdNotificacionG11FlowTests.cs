using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Common;
using Propia.Application.Notificaciones;
using Propia.Application.Pqrsd;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Pqrsd;
using System.Security.Claims;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// G-11 (privacidad): <c>NotificarAdminsTenantAsync</c> notifica SOLO a la administracion (Rol=Administrador),
/// no a los residentes; y el texto literal (justificacion de tutela / motivo de prorroga) NO viaja en el
/// cuerpo del aviso, aunque SI queda en la traza del historial (auditoria completa).
/// </summary>
[Collection(nameof(PostgresCollection))]
public class PqrsdNotificacionG11FlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;
    private Guid _actorAdminPersonaId;   // el que actua (admin) = persona del http fake

    public PqrsdNotificacionG11FlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        _actorAdminPersonaId = Guid.NewGuid();
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddDataProtection();
        sc.AddSingleton<ITenantContext, TenantContext>();
        sc.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = BuildFakeHttpContext(_actorAdminPersonaId) });
        sc.AddScoped<TenantConnectionInterceptor>();
        sc.AddDbContext<PropiaDbContext>((sp, opts) =>
            opts.UseNpgsql(_fx.AppConnectionString)
                .AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>()));
        sc.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<PropiaDbContext>()
            .AddDefaultTokenProviders();
        sc.AddSingleton<INotificacionDispatcher, FakeNotificacionDispatcher>();
        sc.AddScoped<Propia.Application.Tareas.ITareasService, Propia.Infrastructure.Tareas.TareasService>();
        sc.AddSingleton<Propia.Application.Documents.IMembreteDocumentBuilder, Propia.Infrastructure.Documents.MembreteDocumentBuilder>();
        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Tutela_notifica_solo_a_admins_y_sin_justificacion_literal()
    {
        var tenantId = await SeedTenantAsync("PQRSD G11");
        // El actor es admin; ademas un residente activo que NO debe recibir el aviso.
        await SeedPersonaAsync(_actorAdminPersonaId, "Admin", "PH");
        var residentePersonaId = Guid.NewGuid();
        await SeedPersonaAsync(residentePersonaId, "Resi", "Dente");
        await SeedUsuarioTenantAsync(tenantId, _actorAdminPersonaId, "Administrador");
        await SeedUsuarioTenantAsync(tenantId, residentePersonaId, "Residente");

        var (svc, db, scope) = Build(tenantId);
        using (scope)
        {
            var fake = (FakeNotificacionDispatcher)scope.ServiceProvider.GetRequiredService<INotificacionDispatcher>();
            var cats = await svc.ListarCategoriasAsync(CancellationToken.None);
            var x = await svc.RadicarAsync(new RadicarPqrsdRequest(
                TipoPqrsd.Peticion, cats[0].Id,
                "Solicitud que tarda y el radicador anuncia accion de tutela ante el juez de turno.",
                false, null), CancellationToken.None);

            fake.Enviadas.Clear();   // aislar: solo nos importan los avisos que dispara la tutela
            const string justificacion = "Radicador notifico tutela ante Juez 12 Civil, radicado 2026-XYZ-0001";
            await svc.ActivarTutelaAsync(x.Id, new ActivarTutelaRequest(justificacion), CancellationToken.None);

            // (a) destinatarios: el admin SI; el residente NUNCA.
            var destinatarios = fake.Enviadas.Select(e => e.PersonaDestinatariaId).ToList();
            Assert.NotEmpty(destinatarios);
            Assert.Contains(_actorAdminPersonaId, destinatarios);
            Assert.DoesNotContain(residentePersonaId, destinatarios);

            // (b) el cuerpo del aviso NO lleva la justificacion literal (privacidad).
            Assert.All(fake.Enviadas, e => Assert.DoesNotContain(justificacion, e.Cuerpo));

            // (c) pero la traza del historial SI la conserva (auditoria).
            var notas = await db.PqrsdHistorialEstados.AsNoTracking()
                .Where(h => h.ExpedienteId == x.Id).Select(h => h.Nota).ToListAsync();
            Assert.Contains(notas, n => n != null && n.Contains(justificacion));
        }

        await CleanTenant(tenantId, residentePersonaId);
    }

    // ===================== Helpers (patron de PqrsdFlowTests) =====================

    private (IPqrsdService svc, PropiaDbContext db, IServiceScope scope) Build(Guid tenantId)
    {
        var scope = _services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        ctx.SetTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var http = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var noti = scope.ServiceProvider.GetRequiredService<INotificacionDispatcher>();
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

    private async Task SeedPersonaAsync(Guid personaId, string nombres, string apellidos)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        ctx.Personas.Add(new Persona
        {
            Id = personaId,
            TipoDocumento = TipoDocumento.CC,
            Documento = $"P{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = nombres,
            Apellidos = apellidos,
            Email = $"{nombres.ToLowerInvariant()}.{Guid.NewGuid():N}@test.co",
            PerfilIncompleto = false
        });
        await ctx.SaveChangesAsync();
    }

    private async Task SeedUsuarioTenantAsync(Guid tenantId, Guid personaId, string rol)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        ctx.UsuariosTenant.Add(new UsuarioTenant
        {
            TenantId = tenantId,
            PersonaId = personaId,
            Rol = rol,
            Estado = EstadoUsuarioTenant.Activo
        });
        await ctx.SaveChangesAsync();
    }

    private async Task CleanTenant(Guid tenantId, Guid residentePersonaId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE pqrsd_historial_estados DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_historial_estados WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE pqrsd_historial_estados ENABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_adjuntos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_expedientes WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_configuracion_plazos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM pqrsd_categorias WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM usuarios_tenant WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id = {_actorAdminPersonaId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id = {residentePersonaId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }
}
