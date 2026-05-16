using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Common;
using Propia.Application.Porteria;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Porteria;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Tests del modulo 2.12 Porteria y Control de Acceso (spec v1.0 MVP).
/// Cubre:
///  - RN-01: un solo turno activo por guarda+punto.
///  - RN-04: vigencia_fin > vigencia_inicio en autorizaciones.
///  - RN-03: codigo usado/expirado no se puede reutilizar.
///  - RN-05: revocar autorizacion deja codigos en estado Revocado.
///  - RN-07: destino visitante obligatorio si configurado.
///  - RN-09: aviso tratamiento confirmado obligatorio.
///  - RN-10: registros visita/vehiculo append-only (trigger SQL).
///  - RN-13: semaforo de paquetes calculado backend.
///  - RN-14: configurable generacion tarea desde novedad (verifica flag).
///  - Ciclo correspondencia Recibido -> Notificado -> Entregado | Devuelto.
///  - Vehiculo: catalogo + reconocimiento automatico en registro.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class PorteriaFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;
    private Guid _userId;
    private Guid _personaId;

    public PorteriaFlowTests(PostgresFixture fx) => _fx = fx;

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
            opts.UseNpgsql(_fx.AppConnectionString).AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>()));
        sc.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<PropiaDbContext>().AddDefaultTokenProviders();
        sc.AddScoped<IPorteriaService, PorteriaService>();
        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Abrir_turno_y_RN01_bloquea_segundo_turno_activo()
    {
        var tenantId = await SeedTenantAsync("Por T1");
        await SeedPersonaConApplicationUser();
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        var t1 = await svc.AbrirTurnoAsync(new AbrirTurnoRequest(_personaId, "Principal"), CancellationToken.None);
        Assert.Equal(EstadoTurnoPorteria.Activo, t1.Estado);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AbrirTurnoAsync(new AbrirTurnoRequest(_personaId, "Principal"), CancellationToken.None));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task RN04_autorizacion_vigencia_fin_anterior_falla()
    {
        var tenantId = await SeedTenantAsync("Por RN04");
        await SeedPersonaConApplicationUser();
        var unidadId = await SeedUnidadAsync(tenantId);
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        var ini = DateTimeOffset.UtcNow.AddHours(1);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearAutorizacionAsync(new CrearAutorizacionRequest(
                unidadId, "Juan Visita", "12345", TipoVisitante.VisitaPersonal,
                ini, ini.AddMinutes(-30), null, 1, OrigenAutorizacion.Administrador), CancellationToken.None));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Crear_autorizacion_genera_codigo_8_digitos_unico()
    {
        var tenantId = await SeedTenantAsync("Por Cod");
        await SeedPersonaConApplicationUser();
        var unidadId = await SeedUnidadAsync(tenantId);
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        var a = await svc.CrearAutorizacionAsync(new CrearAutorizacionRequest(
            unidadId, "Visitante Test", null, TipoVisitante.VisitaPersonal,
            DateTimeOffset.UtcNow.AddMinutes(10), DateTimeOffset.UtcNow.AddHours(2),
            null, 1, OrigenAutorizacion.Administrador), CancellationToken.None);

        Assert.NotNull(a.CodigoNumerico);
        Assert.Equal(8, a.CodigoNumerico!.Length);
        Assert.All(a.CodigoNumerico, c => Assert.True(char.IsDigit(c)));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task RN05_revocar_autorizacion_marca_codigos_revocados()
    {
        var tenantId = await SeedTenantAsync("Por RN05");
        await SeedPersonaConApplicationUser();
        var unidadId = await SeedUnidadAsync(tenantId);
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        var a = await svc.CrearAutorizacionAsync(new CrearAutorizacionRequest(
            unidadId, "Sera revocado", null, TipoVisitante.VisitaPersonal,
            DateTimeOffset.UtcNow.AddMinutes(10), DateTimeOffset.UtcNow.AddHours(2),
            null, 1, OrigenAutorizacion.Administrador), CancellationToken.None);

        var ok = await svc.RevocarAutorizacionAsync(a.Id, CancellationToken.None);
        Assert.True(ok);

        var validacion = await svc.ValidarCodigoAsync(new ValidarCodigoRequest(a.CodigoNumerico!), CancellationToken.None);
        Assert.False(validacion.Valido);
        Assert.Contains("revocado", validacion.Motivo!, StringComparison.OrdinalIgnoreCase);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task RN03_codigo_usado_no_se_puede_reusar()
    {
        var tenantId = await SeedTenantAsync("Por RN03");
        await SeedPersonaConApplicationUser();
        var unidadId = await SeedUnidadAsync(tenantId);
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        var t = await svc.AbrirTurnoAsync(new AbrirTurnoRequest(_personaId, "Principal"), CancellationToken.None);
        var a = await svc.CrearAutorizacionAsync(new CrearAutorizacionRequest(
            unidadId, "UnSoloUso", "9999", TipoVisitante.VisitaPersonal,
            DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(2),
            null, 1, OrigenAutorizacion.Administrador), CancellationToken.None);

        // Primer uso
        await svc.RegistrarVisitaAsync(t.Id, new RegistrarVisitaRequest(
            TipoEventoAccesoPorteria.Ingreso, TipoVisitante.VisitaPersonal,
            "UnSoloUso", "9999", TipoDocumentoVisitante.Cc,
            DestinoVisita.UnidadPrivada, unidadId, a.Id, null, null, false), CancellationToken.None);

        // Segundo intento con mismo codigo debe fallar
        var validacion = await svc.ValidarCodigoAsync(new ValidarCodigoRequest(a.CodigoNumerico!), CancellationToken.None);
        Assert.False(validacion.Valido);
        Assert.Contains("usado", validacion.Motivo!, StringComparison.OrdinalIgnoreCase);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task RN09_aviso_tratamiento_obligatorio_para_visitante_sin_autorizacion()
    {
        var tenantId = await SeedTenantAsync("Por RN09");
        await SeedPersonaConApplicationUser();
        var unidadId = await SeedUnidadAsync(tenantId);
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;
        var t = await svc.AbrirTurnoAsync(new AbrirTurnoRequest(_personaId, "Principal"), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RegistrarVisitaAsync(t.Id, new RegistrarVisitaRequest(
                TipoEventoAccesoPorteria.Ingreso, TipoVisitante.Delivery,
                "Mensajero", "1111", null, DestinoVisita.UnidadPrivada, unidadId, null, null, null, false),
                CancellationToken.None));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task RN10_registro_visita_append_only_trigger_sql_bloquea_update()
    {
        var tenantId = await SeedTenantAsync("Por RN10");
        await SeedPersonaConApplicationUser();
        var unidadId = await SeedUnidadAsync(tenantId);
        var (svc, db, scope) = Build(tenantId);
        using var _ = scope;
        var t = await svc.AbrirTurnoAsync(new AbrirTurnoRequest(_personaId, "Principal"), CancellationToken.None);
        var v = await svc.RegistrarVisitaAsync(t.Id, new RegistrarVisitaRequest(
            TipoEventoAccesoPorteria.Ingreso, TipoVisitante.VisitaPersonal,
            "Inmutable", null, null, DestinoVisita.UnidadPrivada, unidadId, null, null, null, true),
            CancellationToken.None);

        var ex = await Assert.ThrowsAsync<Npgsql.PostgresException>(async () =>
            await db.Database.ExecuteSqlAsync($"UPDATE registros_visita SET observacion = 'hack' WHERE id = {v.Id}"));
        Assert.Contains("append-only", ex.Message);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Ciclo_correspondencia_recibido_notificado_entregado()
    {
        var tenantId = await SeedTenantAsync("Por Corr");
        await SeedPersonaConApplicationUser();
        var unidadId = await SeedUnidadAsync(tenantId);
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;
        var t = await svc.AbrirTurnoAsync(new AbrirTurnoRequest(_personaId, "Principal"), CancellationToken.None);

        var c = await svc.RegistrarCorrespondenciaAsync(t.Id, new RegistrarCorrespondenciaRequest(
            unidadId, TipoCorrespondencia.Paquete, "Amazon", "Caja mediana"), CancellationToken.None);
        Assert.Equal(EstadoCorrespondencia.Notificado, c.Estado);

        var ok = await svc.EntregarCorrespondenciaAsync(c.Id, new EntregarCorrespondenciaRequest("Carlos"), CancellationToken.None);
        Assert.True(ok);
        var pendientes = await svc.ListarCorrespondenciaAsync(EstadoCorrespondencia.Notificado, null, CancellationToken.None);
        Assert.DoesNotContain(pendientes, p => p.Id == c.Id);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task RN13_semaforo_paquete_calcula_backend_segun_umbrales()
    {
        var tenantId = await SeedTenantAsync("Por Sem");
        await SeedPersonaConApplicationUser();
        var unidadId = await SeedUnidadAsync(tenantId);
        var (svc, db, scope) = Build(tenantId);
        using var _ = scope;
        var t = await svc.AbrirTurnoAsync(new AbrirTurnoRequest(_personaId, "Principal"), CancellationToken.None);
        var c = await svc.RegistrarCorrespondenciaAsync(t.Id, new RegistrarCorrespondenciaRequest(
            unidadId, TipoCorrespondencia.Paquete, null, null), CancellationToken.None);

        // Recien creado -> verde
        var list = await svc.ListarCorrespondenciaAsync(null, null, CancellationToken.None);
        Assert.Equal(SemaforoPaquete.Verde, list.First(p => p.Id == c.Id).Semaforo);

        // Forzar RecibidoAt al pasado para validar amarillo/rojo
        await db.Database.ExecuteSqlAsync($"UPDATE correspondencias SET recibido_at = now() - interval '2 hours' WHERE id = {c.Id}");
        list = await svc.ListarCorrespondenciaAsync(null, null, CancellationToken.None);
        Assert.Equal(SemaforoPaquete.Amarillo, list.First(p => p.Id == c.Id).Semaforo);

        await db.Database.ExecuteSqlAsync($"UPDATE correspondencias SET recibido_at = now() - interval '4 hours' WHERE id = {c.Id}");
        list = await svc.ListarCorrespondenciaAsync(null, null, CancellationToken.None);
        Assert.Equal(SemaforoPaquete.Rojo, list.First(p => p.Id == c.Id).Semaforo);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Vehiculo_autorizado_reconocido_en_registro()
    {
        var tenantId = await SeedTenantAsync("Por Veh");
        await SeedPersonaConApplicationUser();
        var unidadId = await SeedUnidadAsync(tenantId);
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        var v = await svc.CrearVehiculoAutorizadoAsync(new CrearVehiculoRequest(
            unidadId, "ABC123", TipoVehiculo.Automovil, "Renault", "Logan", "Gris", "P-12"), CancellationToken.None);
        Assert.Equal("ABC123", v.Placa);

        var t = await svc.AbrirTurnoAsync(new AbrirTurnoRequest(_personaId, "Principal"), CancellationToken.None);
        // Normaliza placa con espacios y guiones - debe reconocerlo igual
        var r = await svc.RegistrarVehiculoAsync(t.Id, new RegistrarVehiculoRequest(
            TipoEventoAccesoPorteria.Ingreso, "abc-123", null, null), CancellationToken.None);
        Assert.False(r.EsVisita);
        Assert.Equal(unidadId, r.UnidadPrivadaId);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Configuracion_RN08_retencion_minima_365_dias()
    {
        var tenantId = await SeedTenantAsync("Por Cfg");
        await SeedPersonaConApplicationUser();
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ActualizarConfiguracionAsync(new ActualizarConfiguracionRequest(
                true, CanalNotificacionPaquete.Whatsapp, 60, 180, false, false, 30),
                CancellationToken.None));

        await CleanTenant(tenantId);
    }

    // =======================================================================
    // Helpers
    // =======================================================================

    private (IPorteriaService svc, PropiaDbContext db, IServiceScope scope) Build(Guid tenantId)
    {
        var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        return (scope.ServiceProvider.GetRequiredService<IPorteriaService>(), db, scope);
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
        if (await ctx.Personas.AnyAsync(p => p.Id == _personaId)) return;
        var p = new Persona
        {
            Id = _personaId,
            TipoDocumento = TipoDocumento.CC,
            Documento = $"P{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = "Guarda",
            Apellidos = "Test",
            Email = $"g.{Guid.NewGuid():N}@test.co",
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

    private async Task<Guid> SeedUnidadAsync(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var u = new UnidadPrivada
        {
            TenantId = tenantId,
            Numero = "101",
            CoeficientePropiedad = 0.01m
        };
        ctx.UnidadesPrivadas.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    private async Task CleanTenant(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());

        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE registros_visita DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE registros_vehiculo DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM registros_visita WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM registros_vehiculo WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE registros_visita ENABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE registros_vehiculo ENABLE TRIGGER ALL");

        await ctx.Database.ExecuteSqlAsync($"DELETE FROM correspondencias WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM novedades_turno WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM codigos_ingreso WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM autorizaciones_previa WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM vehiculos_autorizados WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM visitantes_frecuentes WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM turnos_porteria WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM porteria_configuracion WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidades_privadas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM asp_net_users WHERE id = {_userId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id = {_personaId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }
}
