using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Common;
using Propia.Application.Comunicaciones;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Comunicaciones;
using Propia.Infrastructure.Persistence;
using System.Security.Claims;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Tests del modulo 2.14 Comunicaciones (spec v1.0 MVP).
/// Cubre: ciclo Borrador -> Enviado, seed plantillas globales, segmentacion
/// con autorizacion datos (RN-01) y whatsapp (RN-02), inmutabilidad post-envio
/// (RN-04), acuse silencioso via SECURITY DEFINER, append-only acuses, RN-06
/// plantillas globales, RN-07 reenvio no crea nuevo comunicado.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ComunicacionesFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;
    private Guid _userId;
    private Guid _personaId;
    public ComunicacionesFlowTests(PostgresFixture fx) => _fx = fx;

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
        sc.AddSingleton<Propia.Application.Notificaciones.INotificacionDispatcher,
            FakeNotificacionDispatcher>();
        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }
    public Task DisposeAsync() => Task.CompletedTask;

    // =======================================================================
    // Tests
    // =======================================================================

    [Fact]
    public async Task Seed_global_tiene_7_plantillas_PropIA()
    {
        var tenantId = await SeedTenantAsync("Com Seed");
        await SeedPersonaConApplicationUser(tenantId);
        var (svc, _, _) = Build(tenantId);

        var globales = await svc.ListarPlantillasAsync(true, CancellationToken.None);
        Assert.True(globales.Count >= 7);
        Assert.All(globales, p => Assert.True(p.EsGlobal));
        Assert.Contains(globales, p => p.Nombre == "Aviso de corte de agua");
        Assert.Contains(globales, p => p.Nombre == "Comunicado de emergencia");

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Plantilla_global_no_se_puede_editar_ni_eliminar_RN06()
    {
        var tenantId = await SeedTenantAsync("Com RN06");
        await SeedPersonaConApplicationUser(tenantId);
        var (svc, _, _) = Build(tenantId);

        var globales = await svc.ListarPlantillasAsync(true, CancellationToken.None);
        var alguna = globales.First();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ActualizarPlantillaTenantAsync(alguna.Id, new ActualizarPlantillaRequest(
                "Hack", TipoComunicadoBase.Circular, "x", "x", false), CancellationToken.None));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.DesactivarPlantillaTenantAsync(alguna.Id, CancellationToken.None));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Crear_borrador_valida_longitudes_minimas_de_asunto_y_cuerpo()
    {
        var tenantId = await SeedTenantAsync("Com Vall");
        await SeedPersonaConApplicationUser(tenantId);
        var (svc, _, _) = Build(tenantId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearBorradorAsync(new CrearBorradorRequest(null, TipoComunicadoBase.Circular,
                "abc", "Cuerpo suficientemente largo para validar", null, false), CancellationToken.None));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearBorradorAsync(new CrearBorradorRequest(null, TipoComunicadoBase.Circular,
                "Asunto valido", "corto", null, false), CancellationToken.None));

        var ok = await svc.CrearBorradorAsync(new CrearBorradorRequest(null, TipoComunicadoBase.AvisoOperativo,
            "Mantenimiento programado", "<p>Habra corte de agua el lunes a las 6am.</p>", null, false), CancellationToken.None);
        Assert.Equal(EstadoComunicado.Borrador, ok.Estado);
        Assert.StartsWith("Mantenimiento programado", ok.CuerpoTextoPlano);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Preview_destinatarios_filtra_por_autorizacion_y_whatsapp_RN01_RN02()
    {
        var tenantId = await SeedTenantAsync("Com Preview");
        await SeedPersonaConApplicationUser(tenantId);

        // 3 personas vinculadas al tenant:
        //  A: autoriza datos + tiene WhatsApp -> elegible
        //  B: NO autoriza datos -> excluida (RN-01)
        //  C: autoriza pero sin WhatsApp -> excluida (RN-02)
        await SeedPersonaVinculadaAsync(tenantId, "Ana", "Garcia", autoriza: true, whatsapp: "+573001111111");
        await SeedPersonaVinculadaAsync(tenantId, "Beto", "Lopez", autoriza: false, whatsapp: "+573002222222");
        await SeedPersonaVinculadaAsync(tenantId, "Carla", "Mora", autoriza: true, whatsapp: null);

        var (svc, _, _) = Build(tenantId);

        var c = await svc.CrearBorradorAsync(new CrearBorradorRequest(null, TipoComunicadoBase.Circular,
            "Aviso general", "<p>Comunicado de prueba para todos</p>", null, false), CancellationToken.None);
        await svc.AgregarSegmentoAsync(c.Id, new AgregarSegmentoRequest(TipoSegmento.Broadcast, "{}"), CancellationToken.None);

        var preview = await svc.PreviewDestinatariosAsync(c.Id, CancellationToken.None);
        Assert.Equal(1, preview.Total);             // Solo Ana
        Assert.Equal(1, preview.TotalSinWhatsapp);  // Carla
        Assert.Equal(1, preview.TotalSinAutorizacion); // Beto

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Enviar_inmediato_marca_destinatarios_Entregado_y_simula_T2()
    {
        var tenantId = await SeedTenantAsync("Com Send");
        await SeedPersonaConApplicationUser(tenantId);
        await SeedPersonaVinculadaAsync(tenantId, "Ana", "Garcia", autoriza: true, whatsapp: "+573001111111");
        await SeedPersonaVinculadaAsync(tenantId, "Diana", "Ruiz", autoriza: true, whatsapp: "+573004444444");
        var (svc, db, _) = Build(tenantId);

        var c = await svc.CrearBorradorAsync(new CrearBorradorRequest(null, TipoComunicadoBase.Circular,
            "Bienvenida a la comunidad", "<p>Les damos la bienvenida con gusto.</p>", null, false), CancellationToken.None);
        await svc.AgregarSegmentoAsync(c.Id, new AgregarSegmentoRequest(TipoSegmento.Broadcast, "{}"), CancellationToken.None);

        await svc.EnviarAsync(c.Id, new EnviarRequest(null), CancellationToken.None);

        var actualizado = await svc.GetComunicadoAsync(c.Id, CancellationToken.None);
        Assert.Equal(EstadoComunicado.Enviado, actualizado!.Estado);
        Assert.Equal(2, actualizado.TotalDestinatarios);
        Assert.Equal(2, actualizado.TotalEntregados);

        var destinatarios = await db.ComunicadoDestinatarios.AsNoTracking()
            .Where(d => d.ComunicadoId == c.Id).ToListAsync();
        Assert.All(destinatarios, d => Assert.Equal(EstadoEntregaDestinatario.Entregado, d.EstadoEntrega));
        Assert.All(destinatarios, d => Assert.NotEqual(Guid.Empty, d.Token));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Programar_requiere_fecha_5min_en_futuro()
    {
        var tenantId = await SeedTenantAsync("Com Prog");
        await SeedPersonaConApplicationUser(tenantId);
        await SeedPersonaVinculadaAsync(tenantId, "Ana", "Garcia", autoriza: true, whatsapp: "+573001111111");
        var (svc, _, _) = Build(tenantId);

        var c = await svc.CrearBorradorAsync(new CrearBorradorRequest(null, TipoComunicadoBase.AvisoOperativo,
            "Aviso programado", "<p>Aviso a programar para envio futuro.</p>", null, false), CancellationToken.None);
        await svc.AgregarSegmentoAsync(c.Id, new AgregarSegmentoRequest(TipoSegmento.Broadcast, "{}"), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.EnviarAsync(c.Id, new EnviarRequest(DateTimeOffset.UtcNow.AddMinutes(2)), CancellationToken.None));

        var ok = await svc.EnviarAsync(c.Id, new EnviarRequest(DateTimeOffset.UtcNow.AddHours(1)), CancellationToken.None);
        Assert.True(ok);
        var det = await svc.GetComunicadoAsync(c.Id, CancellationToken.None);
        Assert.Equal(EstadoComunicado.Programado, det!.Estado);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Comunicado_Enviado_no_se_puede_editar_RN04_trigger_inmutabilidad()
    {
        var tenantId = await SeedTenantAsync("Com RN04");
        await SeedPersonaConApplicationUser(tenantId);
        await SeedPersonaVinculadaAsync(tenantId, "Ana", "Garcia", autoriza: true, whatsapp: "+573001111111");
        var (svc, db, _) = Build(tenantId);

        var c = await svc.CrearBorradorAsync(new CrearBorradorRequest(null, TipoComunicadoBase.Circular,
            "Asunto original", "<p>Cuerpo original del comunicado.</p>", null, false), CancellationToken.None);
        await svc.AgregarSegmentoAsync(c.Id, new AgregarSegmentoRequest(TipoSegmento.Broadcast, "{}"), CancellationToken.None);
        await svc.EnviarAsync(c.Id, new EnviarRequest(null), CancellationToken.None);

        // Service rechaza la edicion
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ActualizarBorradorAsync(c.Id, new ActualizarBorradorRequest(
                TipoComunicadoBase.Circular, "Cambio", "<p>Texto cambiado</p>", null, false), CancellationToken.None));

        // Trigger SQL tambien lo bloquea si alguien intenta UPDATE directo
        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlAsync($"UPDATE comunicados SET asunto = 'hack' WHERE id = {c.Id}"));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Acuse_publico_via_token_es_silencioso_e_idempotente()
    {
        var tenantId = await SeedTenantAsync("Com Acuse");
        await SeedPersonaConApplicationUser(tenantId);
        await SeedPersonaVinculadaAsync(tenantId, "Ana", "Garcia", autoriza: true, whatsapp: "+573001111111");
        var (svc, db, _) = Build(tenantId);

        var c = await svc.CrearBorradorAsync(new CrearBorradorRequest(null, TipoComunicadoBase.AvisoOperativo,
            "Mantenimiento ascensor", "<p>El mantenimiento sera el lunes proximo.</p>", null, true), CancellationToken.None);
        await svc.AgregarSegmentoAsync(c.Id, new AgregarSegmentoRequest(TipoSegmento.Broadcast, "{}"), CancellationToken.None);
        await svc.EnviarAsync(c.Id, new EnviarRequest(null), CancellationToken.None);

        var dest = await db.ComunicadoDestinatarios.AsNoTracking().FirstAsync(d => d.ComunicadoId == c.Id);

        // Endpoint publico - simulamos no tener tenant en contexto
        var publicoScope = _services.CreateScope();
        var publicoCtx = publicoScope.ServiceProvider.GetRequiredService<ITenantContext>();
        publicoCtx.SetTenant(Guid.Empty);  // sin tenant explicito (vacio simula caller publico)
        publicoCtx.GetType();  // dummy: workaround porque no hay clear-tenant
        var publicoDb = publicoScope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var publicoHttp = publicoScope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var svcPublico = new ComunicacionesService(publicoDb, publicoCtx, publicoHttp, _services.GetRequiredService<Propia.Application.Notificaciones.INotificacionDispatcher>());

        var view = await svcPublico.AbrirVistaPublicaAsync(dest.Token, DispositivoAcuse.Mobile, CancellationToken.None);
        Assert.NotNull(view);
        Assert.Equal("Mantenimiento ascensor", view!.Asunto);

        // Segundo acceso no duplica el acuse (idempotente)
        var view2 = await svcPublico.AbrirVistaPublicaAsync(dest.Token, DispositivoAcuse.Desktop, CancellationToken.None);
        Assert.NotNull(view2);

        var acuses = await svc.ListarAcusesAsync(c.Id, CancellationToken.None);
        Assert.Equal(1, acuses.Confirmados);
        Assert.Equal(0, acuses.Pendientes);
        Assert.Equal(100m, acuses.TasaApertura);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Acuse_es_append_only_trigger_bloquea_update_y_delete()
    {
        var tenantId = await SeedTenantAsync("Com Append");
        await SeedPersonaConApplicationUser(tenantId);
        await SeedPersonaVinculadaAsync(tenantId, "Ana", "Garcia", autoriza: true, whatsapp: "+573001111111");
        var (svc, db, _) = Build(tenantId);

        var c = await svc.CrearBorradorAsync(new CrearBorradorRequest(null, TipoComunicadoBase.Circular,
            "Cuerpo append", "<p>Cuerpo del comunicado de prueba.</p>", null, true), CancellationToken.None);
        await svc.AgregarSegmentoAsync(c.Id, new AgregarSegmentoRequest(TipoSegmento.Broadcast, "{}"), CancellationToken.None);
        await svc.EnviarAsync(c.Id, new EnviarRequest(null), CancellationToken.None);
        var dest = await db.ComunicadoDestinatarios.AsNoTracking().FirstAsync(d => d.ComunicadoId == c.Id);

        // Crear acuse via endpoint publico
        var pubScope = _services.CreateScope();
        var pubCtx = pubScope.ServiceProvider.GetRequiredService<ITenantContext>();
        pubCtx.SetTenant(Guid.Empty);
        var pubDb = pubScope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var pubHttp = pubScope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        await new ComunicacionesService(pubDb, pubCtx, pubHttp, _services.GetRequiredService<Propia.Application.Notificaciones.INotificacionDispatcher>())
            .AbrirVistaPublicaAsync(dest.Token, DispositivoAcuse.Mobile, CancellationToken.None);

        var acuseId = await db.ComunicadoAcuses.AsNoTracking()
            .Where(a => a.DestinatarioId == dest.Id).Select(a => a.Id).FirstAsync();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlAsync($"UPDATE comunicado_acuses SET dispositivo = 1 WHERE id = {acuseId}"));
        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlAsync($"DELETE FROM comunicado_acuses WHERE id = {acuseId}"));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Reenvio_a_pendientes_no_crea_nuevo_comunicado_RN07()
    {
        var tenantId = await SeedTenantAsync("Com Reenv");
        await SeedPersonaConApplicationUser(tenantId);
        await SeedPersonaVinculadaAsync(tenantId, "Ana", "Garcia", autoriza: true, whatsapp: "+573001111111");
        await SeedPersonaVinculadaAsync(tenantId, "Eva", "Tobon", autoriza: true, whatsapp: "+573005555555");
        var (svc, db, _) = Build(tenantId);

        var c = await svc.CrearBorradorAsync(new CrearBorradorRequest(null, TipoComunicadoBase.Circular,
            "Convocatoria reunion", "<p>Reunion del consejo el sabado.</p>", null, true), CancellationToken.None);
        await svc.AgregarSegmentoAsync(c.Id, new AgregarSegmentoRequest(TipoSegmento.Broadcast, "{}"), CancellationToken.None);
        await svc.EnviarAsync(c.Id, new EnviarRequest(null), CancellationToken.None);

        // 1 de 2 abre el comunicado
        var primero = await db.ComunicadoDestinatarios.AsNoTracking()
            .Where(d => d.ComunicadoId == c.Id).OrderBy(d => d.PersonaId).Select(d => d.Token).FirstAsync();
        await svc.AbrirVistaPublicaAsync(primero, DispositivoAcuse.Mobile, CancellationToken.None);

        var totalAntes = await db.Comunicados.CountAsync();
        var pendientes = await svc.ReenviarAPendientesAsync(c.Id, CancellationToken.None);
        var totalDespues = await db.Comunicados.CountAsync();

        Assert.Equal(1, pendientes);
        Assert.Equal(totalAntes, totalDespues);  // No se creo nuevo comunicado

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Cancelar_solo_aplica_a_Borrador_o_Programado()
    {
        var tenantId = await SeedTenantAsync("Com Cancel");
        await SeedPersonaConApplicationUser(tenantId);
        await SeedPersonaVinculadaAsync(tenantId, "Ana", "Garcia", autoriza: true, whatsapp: "+573001111111");
        var (svc, _, _) = Build(tenantId);

        var c = await svc.CrearBorradorAsync(new CrearBorradorRequest(null, TipoComunicadoBase.Circular,
            "A cancelar", "<p>Comunicado para cancelar antes de enviar.</p>", null, false), CancellationToken.None);
        Assert.True(await svc.CancelarAsync(c.Id, new CancelarRequest("Error"), CancellationToken.None));
        var det = await svc.GetComunicadoAsync(c.Id, CancellationToken.None);
        Assert.Equal(EstadoComunicado.Cancelado, det!.Estado);

        // Otro ya enviado
        var c2 = await svc.CrearBorradorAsync(new CrearBorradorRequest(null, TipoComunicadoBase.Circular,
            "Para enviar", "<p>Sera enviado y luego no se podra cancelar.</p>", null, false), CancellationToken.None);
        await svc.AgregarSegmentoAsync(c2.Id, new AgregarSegmentoRequest(TipoSegmento.Broadcast, "{}"), CancellationToken.None);
        await svc.EnviarAsync(c2.Id, new EnviarRequest(null), CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CancelarAsync(c2.Id, new CancelarRequest("tarde"), CancellationToken.None));

        await CleanTenant(tenantId);
    }

    // =======================================================================
    // Helpers
    // =======================================================================

    private (IComunicacionesService svc, PropiaDbContext db, IServiceScope scope) Build(Guid tenantId)
    {
        var scope = _services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        ctx.SetTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var http = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        return (new ComunicacionesService(db, ctx, http, _services.GetRequiredService<Propia.Application.Notificaciones.INotificacionDispatcher>()), db, scope);
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
            Documento = $"C{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = "Admin",
            Apellidos = "Com",
            Email = $"com.{Guid.NewGuid():N}@test.co",
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

    /// <summary>Crea Persona + vinculo Activo al tenant + opcionalmente contacto WhatsApp.</summary>
    private async Task<Guid> SeedPersonaVinculadaAsync(Guid tenantId, string nombres, string apellidos, bool autoriza, string? whatsapp)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var p = new Persona
        {
            TipoDocumento = TipoDocumento.CC,
            Documento = $"V{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = nombres,
            Apellidos = apellidos,
            Email = $"{nombres.ToLower()}.{Guid.NewGuid():N}@test.co",
            AceptoTratamientoDatos = autoriza,
            PerfilIncompleto = false
        };
        ctx.Personas.Add(p);
        ctx.DirectorioVinculos.Add(new DirectorioVinculo
        {
            TenantId = tenantId,
            EntidadTipo = EntidadDirectorio.Persona,
            EntidadId = p.Id,
            FechaDesde = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)),
            Estado = EstadoVinculo.Activo
        });
        if (!string.IsNullOrEmpty(whatsapp))
        {
            ctx.DirectorioContactos.Add(new DirectorioContacto
            {
                TenantId = tenantId,
                EntidadTipo = EntidadDirectorio.Persona,
                EntidadId = p.Id,
                Tipo = TipoContacto.Whatsapp,
                Valor = whatsapp,
                Activo = true
            });
        }
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private async Task CleanTenant(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());

        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE comunicado_acuses DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE comunicados DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM comunicado_acuses WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM comunicado_destinatarios WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM comunicado_adjuntos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM comunicado_segmentos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM comunicados WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM comunicado_plantillas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE comunicado_acuses ENABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE comunicados ENABLE TRIGGER ALL");

        await ctx.Database.ExecuteSqlAsync($"DELETE FROM directorio_etiquetas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM directorio_contactos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM directorio_vinculos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id != {_personaId} AND id IN (SELECT entidad_id FROM directorio_vinculos WHERE tenant_id = {tenantId})");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM asp_net_users WHERE id = {_userId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id = {_personaId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }
}
