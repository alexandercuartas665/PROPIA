using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Common;
using Propia.Application.Reservas;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Reservas;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Tests del modulo 2.13 Reservas de Zonas Comunes (spec v1.0 MVP).
/// Cubre:
///  - Galeria solo zonas Reservable=true.
///  - RN-04: confirmacion automatica si hay disponibilidad y no requiere aprobacion.
///  - RN-05: no doble reserva (solape detectado al crear).
///  - RN-06: validacion de duracion min/max.
///  - RN-07: anticipacion min/max valida.
///  - RN-12: reglamento aceptado obligatorio si configurado.
///  - RN-13: admin puede cancelar cualquier reserva activa.
///  - RN-15: bloqueo manual impide reservar en el mismo periodo.
///  - Codigo unico RSV-YYYY-NNNNN secuencial.
///  - Tarifa: crea ReservaPago Pendiente cuando TieneTarifa=true.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ReservasFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;
    private Guid _userId;
    private Guid _personaId;

    public ReservasFlowTests(PostgresFixture fx) => _fx = fx;

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
        sc.AddSingleton<Propia.Application.Notificaciones.INotificacionDispatcher, FakeNotificacionDispatcher>();
        sc.AddScoped<IReservasService, ReservasService>();
        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Galeria_solo_devuelve_zonas_reservable_true()
    {
        var tenantId = await SeedTenantAsync("Res Gal");
        await SeedPersonaConApplicationUser();
        var (zonaResId, _) = await SeedZonaAsync(tenantId, "Salon Social", reservable: true);
        var (zonaNoResId, _) = await SeedZonaAsync(tenantId, "Lavanderia", reservable: false);

        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;
        var g = await svc.ListarGaleriaAsync(false, CancellationToken.None);
        Assert.Contains(g, z => z.ZonaComunId == zonaResId);
        Assert.DoesNotContain(g, z => z.ZonaComunId == zonaNoResId);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task RN04_reserva_se_confirma_automaticamente_sin_tarifa_ni_aprobacion()
    {
        var tenantId = await SeedTenantAsync("Res RN04");
        await SeedPersonaConApplicationUser();
        var unidadId = await SeedUnidadAsync(tenantId);
        var (zonaId, _) = await SeedZonaAsync(tenantId, "Salon Social", reservable: true);
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;

        await ConfigurarZonaConFranjasAsync(svc, zonaId);

        var manana = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var r = await svc.CrearReservaAsync(new CrearReservaRequest(
            zonaId, _personaId, unidadId, manana,
            new TimeOnly(10, 0), new TimeOnly(12, 0), true), CancellationToken.None);

        Assert.Equal(EstadoReserva.Confirmada, r.Estado);
        Assert.StartsWith($"RSV-{DateTime.UtcNow.Year}-", r.Codigo);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task RN05_no_doble_reserva_en_misma_franja()
    {
        var tenantId = await SeedTenantAsync("Res RN05");
        await SeedPersonaConApplicationUser();
        var unidadId = await SeedUnidadAsync(tenantId);
        var (zonaId, _) = await SeedZonaAsync(tenantId, "BBQ", reservable: true);
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;
        await ConfigurarZonaConFranjasAsync(svc, zonaId);

        var manana = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        await svc.CrearReservaAsync(new CrearReservaRequest(
            zonaId, _personaId, unidadId, manana,
            new TimeOnly(10, 0), new TimeOnly(12, 0), true), CancellationToken.None);

        // Mismo rango exacto - falla
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearReservaAsync(new CrearReservaRequest(
                zonaId, _personaId, unidadId, manana,
                new TimeOnly(10, 0), new TimeOnly(12, 0), true), CancellationToken.None));

        // Solape parcial - tambien falla
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearReservaAsync(new CrearReservaRequest(
                zonaId, _personaId, unidadId, manana,
                new TimeOnly(11, 0), new TimeOnly(13, 0), true), CancellationToken.None));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task RN06_duracion_fuera_de_rango_falla()
    {
        var tenantId = await SeedTenantAsync("Res RN06");
        await SeedPersonaConApplicationUser();
        var unidadId = await SeedUnidadAsync(tenantId);
        var (zonaId, _) = await SeedZonaAsync(tenantId, "Gym", reservable: true);
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;
        // duracion min 60, max 240 (defaults)
        await ConfigurarZonaConFranjasAsync(svc, zonaId);

        var manana = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        // Muy corta (15 min)
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearReservaAsync(new CrearReservaRequest(
                zonaId, _personaId, unidadId, manana,
                new TimeOnly(10, 0), new TimeOnly(10, 15), true), CancellationToken.None));

        // Muy larga (5 horas, max 4)
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearReservaAsync(new CrearReservaRequest(
                zonaId, _personaId, unidadId, manana,
                new TimeOnly(10, 0), new TimeOnly(15, 0), true), CancellationToken.None));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task RN07_anticipacion_minima_no_respetada_falla()
    {
        var tenantId = await SeedTenantAsync("Res RN07");
        await SeedPersonaConApplicationUser();
        var unidadId = await SeedUnidadAsync(tenantId);
        var (zonaId, _) = await SeedZonaAsync(tenantId, "Salon", reservable: true);
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;
        await ConfigurarZonaConFranjasAsync(svc, zonaId);

        // Reserva en el mismo dia con tiempo insuficiente (anticipacion minima 1h)
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var ahora = TimeOnly.FromDateTime(DateTime.UtcNow);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearReservaAsync(new CrearReservaRequest(
                zonaId, _personaId, unidadId, hoy,
                ahora.AddMinutes(10), ahora.AddMinutes(70), true), CancellationToken.None));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task RN12_reglamento_requerido_no_aceptado_falla()
    {
        var tenantId = await SeedTenantAsync("Res RN12");
        await SeedPersonaConApplicationUser();
        var unidadId = await SeedUnidadAsync(tenantId);
        var (zonaId, _) = await SeedZonaAsync(tenantId, "Sauna", reservable: true);
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;
        // Configurar con reglamento obligatorio
        await svc.GuardarConfigAsync(new GuardarConfigRequest(
            zonaId, false, null, true, 1, 30, 60, 240, 60,
            false, ModalidadCobroReserva.PorHora, 0, PoliticaReembolso.SinReembolso, 0,
            24, true, ComportamientoCancelacionTardia.ConPenalidad,
            "Aceptar uso responsable", true, true,
            new[] { new FranjaInput(DiaSemanaReserva.Lunes, new TimeOnly(8, 0), new TimeOnly(22, 0), true),
                    new FranjaInput(DiaSemanaReserva.Martes, new TimeOnly(8, 0), new TimeOnly(22, 0), true),
                    new FranjaInput(DiaSemanaReserva.Miercoles, new TimeOnly(8, 0), new TimeOnly(22, 0), true),
                    new FranjaInput(DiaSemanaReserva.Jueves, new TimeOnly(8, 0), new TimeOnly(22, 0), true),
                    new FranjaInput(DiaSemanaReserva.Viernes, new TimeOnly(8, 0), new TimeOnly(22, 0), true),
                    new FranjaInput(DiaSemanaReserva.Sabado, new TimeOnly(8, 0), new TimeOnly(22, 0), true),
                    new FranjaInput(DiaSemanaReserva.Domingo, new TimeOnly(8, 0), new TimeOnly(22, 0), true) }),
            CancellationToken.None);

        var manana = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearReservaAsync(new CrearReservaRequest(
                zonaId, _personaId, unidadId, manana,
                new TimeOnly(10, 0), new TimeOnly(12, 0), false /* reglamento no aceptado */), CancellationToken.None));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Tarifa_crea_reservaPago_pendiente()
    {
        var tenantId = await SeedTenantAsync("Res Tar");
        await SeedPersonaConApplicationUser();
        var unidadId = await SeedUnidadAsync(tenantId);
        var (zonaId, _) = await SeedZonaAsync(tenantId, "Salon Premium", reservable: true);
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;
        // Configurar con tarifa POR_HORA $50000
        await svc.GuardarConfigAsync(new GuardarConfigRequest(
            zonaId, false, null, true, 1, 30, 60, 240, 60,
            true, ModalidadCobroReserva.PorHora, 50000m, PoliticaReembolso.SinReembolso, 0,
            24, true, ComportamientoCancelacionTardia.ConPenalidad,
            null, false, true,
            new[] { new FranjaInput(DiaSemanaReserva.Lunes, new TimeOnly(8, 0), new TimeOnly(22, 0), true),
                    new FranjaInput(DiaSemanaReserva.Martes, new TimeOnly(8, 0), new TimeOnly(22, 0), true),
                    new FranjaInput(DiaSemanaReserva.Miercoles, new TimeOnly(8, 0), new TimeOnly(22, 0), true),
                    new FranjaInput(DiaSemanaReserva.Jueves, new TimeOnly(8, 0), new TimeOnly(22, 0), true),
                    new FranjaInput(DiaSemanaReserva.Viernes, new TimeOnly(8, 0), new TimeOnly(22, 0), true),
                    new FranjaInput(DiaSemanaReserva.Sabado, new TimeOnly(8, 0), new TimeOnly(22, 0), true),
                    new FranjaInput(DiaSemanaReserva.Domingo, new TimeOnly(8, 0), new TimeOnly(22, 0), true) }),
            CancellationToken.None);

        var manana = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var r = await svc.CrearReservaAsync(new CrearReservaRequest(
            zonaId, _personaId, unidadId, manana,
            new TimeOnly(10, 0), new TimeOnly(12, 0), true), CancellationToken.None);

        Assert.Equal(EstadoReserva.PendientePago, r.Estado);
        Assert.Equal(100000m, r.MontoPago);
        Assert.Equal(EstadoPagoReserva.Pendiente, r.EstadoPago);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task RN15_bloqueo_manual_impide_reservar()
    {
        var tenantId = await SeedTenantAsync("Res RN15");
        await SeedPersonaConApplicationUser();
        var unidadId = await SeedUnidadAsync(tenantId);
        var (zonaId, _) = await SeedZonaAsync(tenantId, "Cancha", reservable: true);
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;
        await ConfigurarZonaConFranjasAsync(svc, zonaId);

        var manana = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        await svc.CrearBloqueoAsync(new CrearBloqueoRequest(
            zonaId, TipoBloqueoZona.DiaCompleto, manana, manana, null, null,
            EtiquetaBloqueo.Mantenimiento, "Pintura", false), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearReservaAsync(new CrearReservaRequest(
                zonaId, _personaId, unidadId, manana,
                new TimeOnly(10, 0), new TimeOnly(12, 0), true), CancellationToken.None));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Cancelar_admin_cambia_estado_a_CanceladaAdmin()
    {
        var tenantId = await SeedTenantAsync("Res Canc");
        await SeedPersonaConApplicationUser();
        var unidadId = await SeedUnidadAsync(tenantId);
        var (zonaId, _) = await SeedZonaAsync(tenantId, "Salon", reservable: true);
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;
        await ConfigurarZonaConFranjasAsync(svc, zonaId);

        var manana = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var r = await svc.CrearReservaAsync(new CrearReservaRequest(
            zonaId, _personaId, unidadId, manana,
            new TimeOnly(14, 0), new TimeOnly(16, 0), true), CancellationToken.None);

        var ok = await svc.CancelarComoAdminAsync(r.Id, new CancelarReservaRequest("Evento privado"), CancellationToken.None);
        Assert.True(ok);
        var refresh = await svc.GetReservaAsync(r.Id, CancellationToken.None);
        Assert.Equal(EstadoReserva.CanceladaAdmin, refresh!.Estado);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Disponibilidad_devuelve_slots_con_estados_correctos()
    {
        var tenantId = await SeedTenantAsync("Res Disp");
        await SeedPersonaConApplicationUser();
        var unidadId = await SeedUnidadAsync(tenantId);
        var (zonaId, _) = await SeedZonaAsync(tenantId, "BBQ", reservable: true);
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;
        await ConfigurarZonaConFranjasAsync(svc, zonaId);

        var manana = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        await svc.CrearReservaAsync(new CrearReservaRequest(
            zonaId, _personaId, unidadId, manana,
            new TimeOnly(10, 0), new TimeOnly(12, 0), true), CancellationToken.None);

        var disp = await svc.CalcularDisponibilidadAsync(zonaId, manana, manana, CancellationToken.None);
        Assert.NotEmpty(disp.Slots);
        Assert.Contains(disp.Slots, s => s.HoraInicio == new TimeOnly(10, 0) && s.Estado == "OCUPADO");
        Assert.Contains(disp.Slots, s => s.HoraInicio == new TimeOnly(13, 0) && s.Estado == "DISPONIBLE");

        await CleanTenant(tenantId);
    }

    // =======================================================================
    // Helpers
    // =======================================================================

    private (IReservasService svc, PropiaDbContext db, IServiceScope scope) Build(Guid tenantId)
    {
        var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        return (scope.ServiceProvider.GetRequiredService<IReservasService>(), db, scope);
    }

    private static async Task ConfigurarZonaConFranjasAsync(IReservasService svc, Guid zonaId)
    {
        var franjas = Enum.GetValues<DiaSemanaReserva>()
            .Select(d => new FranjaInput(d, new TimeOnly(8, 0), new TimeOnly(22, 0), true)).ToList();
        await svc.GuardarConfigAsync(new GuardarConfigRequest(
            zonaId, false, null, true, 1, 30, 60, 240, 60,
            false, ModalidadCobroReserva.PorHora, 0, PoliticaReembolso.SinReembolso, 0,
            24, true, ComportamientoCancelacionTardia.ConPenalidad,
            null, false, true, franjas), CancellationToken.None);
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
            Documento = $"R{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = "Res",
            Apellidos = "Test",
            Email = $"r.{Guid.NewGuid():N}@test.co",
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
        var u = new UnidadPrivada { TenantId = tenantId, Numero = "201", CoeficientePropiedad = 0.01m };
        ctx.UnidadesPrivadas.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    private async Task<(Guid id, string nombre)> SeedZonaAsync(Guid tenantId, string nombre, bool reservable)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var z = new ZonaComun
        {
            TenantId = tenantId,
            Nombre = nombre,
            EsReservable = reservable,
            CapacidadPersonas = 30,
            Estado = EstadoZonaComunMantenimiento.Activa
        };
        ctx.ZonasComunes.Add(z);
        await ctx.SaveChangesAsync();
        return (z.Id, z.Nombre);
    }

    private async Task CleanTenant(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());

        await ctx.Database.ExecuteSqlAsync($"DELETE FROM reserva_pagos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM reservas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM reservas_recurrentes WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM zona_bloqueos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM zona_franjas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM zona_config_reserva WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM zonas_comunes WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidades_privadas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM asp_net_users WHERE id = {_userId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id = {_personaId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }
}
