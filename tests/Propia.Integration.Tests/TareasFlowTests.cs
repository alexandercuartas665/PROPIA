using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Common;
using Propia.Application.Tareas;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Tareas;
using System.Security.Claims;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>Tests del modulo 2.10 Tareas y Proyectos (spec v1.0 MVP).</summary>
[Collection(nameof(PostgresCollection))]
public class TareasFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;

    public TareasFlowTests(PostgresFixture fx) => _fx = fx;

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
    public async Task Seed_lazy_de_6_estados_base_incluye_Pendiente_Completada_Cancelada()
    {
        var tenantId = await SeedTenantAsync("Tareas Seed");
        var (svc, _, _) = Build(tenantId);

        var estados = await svc.ListarEstadosAsync(CancellationToken.None);

        Assert.Equal(6, estados.Count);
        Assert.Contains(estados, e => e.Nombre == EstadoTareaBase.Pendiente && !e.EsTerminal);
        Assert.Contains(estados, e => e.Nombre == EstadoTareaBase.Completada && e.EsTerminal);
        Assert.Contains(estados, e => e.Nombre == EstadoTareaBase.Cancelada && e.EsTerminal);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Crear_tarea_genera_numero_y_arranca_Pendiente_con_historial()
    {
        var tenantId = await SeedTenantAsync("Tareas Crear");
        var (svc, _, _) = Build(tenantId);

        var t = await svc.CrearTareaAsync(new CrearTareaRequest(
            "Limpiar tanque", "Mantenimiento mensual del tanque", PrioridadTarea.Alta,
            null, null, null, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), null, null), CancellationToken.None);

        Assert.StartsWith($"T-{DateTime.UtcNow.Year}-", t.NumeroTarea);
        Assert.Equal(EstadoTareaBase.Pendiente, t.Estado.Nombre);
        Assert.Equal(PrioridadTarea.Alta, t.Prioridad);
        Assert.Single(t.Historial);
        Assert.Equal(TipoEventoTarea.Creada, t.Historial[0].TipoEvento);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Cambiar_estado_a_Completada_marca_fecha_completada_y_registra_historial()
    {
        var tenantId = await SeedTenantAsync("Tareas Completar");
        var (svc, _, _) = Build(tenantId);

        var t = await svc.CrearTareaAsync(new CrearTareaRequest(
            "T", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);
        var estados = await svc.ListarEstadosAsync(CancellationToken.None);
        var completada = estados.First(e => e.Nombre == EstadoTareaBase.Completada);

        await svc.CambiarEstadoAsync(t.Id, new CambiarEstadoRequest(completada.Id, null), CancellationToken.None);

        var d = await svc.GetTareaAsync(t.Id, CancellationToken.None);
        Assert.Equal(EstadoTareaBase.Completada, d!.Estado.Nombre);
        Assert.NotNull(d.FechaCompletada);
        Assert.Contains(d.Historial, h => h.TipoEvento == TipoEventoTarea.EstadoCambiado);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Cancelar_requiere_motivo_explicito()
    {
        var tenantId = await SeedTenantAsync("Tareas Cancelar");
        var (svc, _, _) = Build(tenantId);

        var t = await svc.CrearTareaAsync(new CrearTareaRequest(
            "T", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);
        var cancelada = (await svc.ListarEstadosAsync(CancellationToken.None)).First(e => e.Nombre == EstadoTareaBase.Cancelada);

        // Sin motivo -> error
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CambiarEstadoAsync(t.Id, new CambiarEstadoRequest(cancelada.Id, null), CancellationToken.None));

        // Con motivo -> ok
        await svc.CambiarEstadoAsync(t.Id, new CambiarEstadoRequest(cancelada.Id, "No es necesario"), CancellationToken.None);
        var d = await svc.GetTareaAsync(t.Id, CancellationToken.None);
        Assert.Equal(EstadoTareaBase.Cancelada, d!.Estado.Nombre);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Numero_es_unico_y_secuencial_dentro_del_tenant()
    {
        var tenantId = await SeedTenantAsync("Tareas Numero");
        var (svc, _, _) = Build(tenantId);

        var t1 = await svc.CrearTareaAsync(new CrearTareaRequest("A", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);
        var t2 = await svc.CrearTareaAsync(new CrearTareaRequest("B", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);
        var t3 = await svc.CrearTareaAsync(new CrearTareaRequest("C", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);

        Assert.Equal(3, new[] { t1.NumeroTarea, t2.NumeroTarea, t3.NumeroTarea }.Distinct().Count());
        Assert.EndsWith("0003", t3.NumeroTarea);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Tarea_padre_e_hijos_se_relacionan_y_la_ficha_lista_subtareas()
    {
        var tenantId = await SeedTenantAsync("Tareas Padre");
        var (svc, _, _) = Build(tenantId);

        var padre = await svc.CrearTareaAsync(new CrearTareaRequest(
            "Proyecto pintura", null, PrioridadTarea.Alta, null, null, null, null, null, null), CancellationToken.None);
        var h1 = await svc.CrearTareaAsync(new CrearTareaRequest(
            "Comprar pintura", null, PrioridadTarea.Normal, null, null, null, null, padre.Id, null), CancellationToken.None);
        var h2 = await svc.CrearTareaAsync(new CrearTareaRequest(
            "Pintar fachada", null, PrioridadTarea.Normal, null, null, null, null, padre.Id, null), CancellationToken.None);

        var detalle = await svc.GetTareaAsync(padre.Id, CancellationToken.None);
        Assert.Equal(2, detalle!.Subtareas.Count);
        Assert.Contains(detalle.Subtareas, s => s.Id == h1.Id);
        Assert.Contains(detalle.Subtareas, s => s.Id == h2.Id);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Historial_es_append_only_trigger_bloquea_update_y_delete()
    {
        var tenantId = await SeedTenantAsync("Tareas Audit");
        var (svc, db, _) = Build(tenantId);

        var t = await svc.CrearTareaAsync(new CrearTareaRequest(
            "T", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);
        var h = await db.TareaHistorial.AsNoTracking().FirstAsync(x => x.TareaId == t.Id);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlAsync($"UPDATE tarea_historial SET descripcion = 'alterado' WHERE id = {h.Id}"));
        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlAsync($"DELETE FROM tarea_historial WHERE id = {h.Id}"));

        await CleanTenant(tenantId);
    }

    // ===================== Dependencias (Fase 2) =====================

    [Fact]
    public async Task Dependencias_agregar_listar_y_remover()
    {
        var tenantId = await SeedTenantAsync("Tareas Deps");
        var (svc, db, _) = Build(tenantId);

        var pre = await svc.CrearTareaAsync(new CrearTareaRequest(
            "Predecesora", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);
        var suc = await svc.CrearTareaAsync(new CrearTareaRequest(
            "Sucesora", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);

        var dep = await svc.AgregarDependenciaAsync(suc.Id,
            new AgregarDependenciaRequest(pre.Id, TipoDependenciaTarea.Bloqueante),
            CancellationToken.None);

        Assert.Equal(pre.Id, dep.DependeDeTareaId);
        Assert.Equal(TipoDependenciaTarea.Bloqueante, dep.Tipo);

        var lista = await svc.ListarDependenciasAsync(suc.Id, CancellationToken.None);
        Assert.Single(lista);

        var ok = await svc.RemoverDependenciaAsync(suc.Id, dep.Id, CancellationToken.None);
        Assert.True(ok);
        var lista2 = await svc.ListarDependenciasAsync(suc.Id, CancellationToken.None);
        Assert.Empty(lista2);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Dependencias_evita_ciclo()
    {
        var tenantId = await SeedTenantAsync("Tareas Ciclo");
        var (svc, _, _) = Build(tenantId);

        var a = await svc.CrearTareaAsync(new CrearTareaRequest(
            "A", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);
        var b = await svc.CrearTareaAsync(new CrearTareaRequest(
            "B", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);

        // A depende de B
        await svc.AgregarDependenciaAsync(a.Id, new AgregarDependenciaRequest(b.Id), CancellationToken.None);
        // Ahora intentar B depende de A debe fallar (ciclo)
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AgregarDependenciaAsync(b.Id, new AgregarDependenciaRequest(a.Id), CancellationToken.None));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Dependencias_misma_tarea_falla()
    {
        var tenantId = await SeedTenantAsync("Tareas Self");
        var (svc, _, _) = Build(tenantId);
        var t = await svc.CrearTareaAsync(new CrearTareaRequest(
            "T", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AgregarDependenciaAsync(t.Id, new AgregarDependenciaRequest(t.Id), CancellationToken.None));
        await CleanTenant(tenantId);
    }

    // ===================== Bulk actions (Fase 2) =====================

    [Fact]
    public async Task Bulk_cambiar_estado_aplica_en_lote_y_marca_completada()
    {
        var tenantId = await SeedTenantAsync("Tareas Bulk");
        var (svc, db, _) = Build(tenantId);

        var estados = await svc.ListarEstadosAsync(CancellationToken.None);
        var completada = estados.First(e => e.EsTerminal && e.Nombre == "Completada");
        var t1 = await svc.CrearTareaAsync(new CrearTareaRequest(
            "T1", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);
        var t2 = await svc.CrearTareaAsync(new CrearTareaRequest(
            "T2", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);

        var res = await svc.BulkCambiarEstadoAsync(
            new BulkCambiarEstadoRequest(new[] { t1.Id, t2.Id }, completada.Id, "Bulk MCP"),
            CancellationToken.None);
        Assert.Equal(2, res.Solicitados);
        Assert.Equal(2, res.Aplicados);

        var actualizadas = await db.Tareas.AsNoTracking()
            .Where(t => t.Id == t1.Id || t.Id == t2.Id).ToListAsync();
        Assert.All(actualizadas, t => Assert.NotNull(t.FechaCompletada));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Bulk_cambiar_prioridad_aplica_en_lote()
    {
        var tenantId = await SeedTenantAsync("Tareas Bulk Prio");
        var (svc, db, _) = Build(tenantId);
        var t1 = await svc.CrearTareaAsync(new CrearTareaRequest(
            "T1", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);
        var t2 = await svc.CrearTareaAsync(new CrearTareaRequest(
            "T2", null, PrioridadTarea.Baja, null, null, null, null, null, null), CancellationToken.None);

        var res = await svc.BulkCambiarPrioridadAsync(
            new BulkCambiarPrioridadRequest(new[] { t1.Id, t2.Id }, PrioridadTarea.Urgente),
            CancellationToken.None);
        Assert.Equal(2, res.Aplicados);
        var actualizadas = await db.Tareas.AsNoTracking()
            .Where(t => t.Id == t1.Id || t.Id == t2.Id).ToListAsync();
        Assert.All(actualizadas, t => Assert.Equal(PrioridadTarea.Urgente, t.Prioridad));
        await CleanTenant(tenantId);
    }

    // ===================== Helpers =====================

    private (ITareasService svc, PropiaDbContext db, IServiceScope scope) Build(Guid tenantId)
    {
        var scope = _services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        ctx.SetTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var http = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var noti = scope.ServiceProvider.GetRequiredService<Propia.Application.Notificaciones.INotificacionDispatcher>();
        return (new TareasService(db, ctx, http, noti), db, scope);
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

    private async Task CleanTenant(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE tarea_historial DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tarea_historial WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE tarea_historial ENABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tarea_comentarios WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tarea_etiqueta_asignaciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tareas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tarea_etiquetas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tarea_estados WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }
}
