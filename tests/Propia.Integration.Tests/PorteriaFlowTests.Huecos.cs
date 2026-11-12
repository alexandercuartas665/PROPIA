using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Notificaciones;
using Propia.Application.Porteria;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Huecos de operacion de Porteria llenados en la replica (PROPUESTAS 9.2): re-notificar un paquete, entregar con
/// constancia de quien retira, devolver con motivo (y aviso al residente), alerta de seguridad (aviso inmediato a
/// administracion y vigilancia), novedad sobre una unidad (avisa al residente) y tarea de una novedad: dano sobre un
/// equipo -> intervencion de Mantenimiento; sobre una unidad -> tarea en Tareas.
/// </summary>
public partial class PorteriaFlowTests
{
    private FakeNotificacionDispatcher Noti => (FakeNotificacionDispatcher)_services.GetRequiredService<INotificacionDispatcher>();

    private async Task<Guid> SeedResidenteAsync(Guid tenantId, Guid unidadId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var p = new Persona { TipoDocumento = TipoDocumento.CC, Documento = $"R{Guid.NewGuid():N}"[..18], Nombres = "Maria", Apellidos = "Residente", Email = $"r.{Guid.NewGuid():N}@test.co" };
        ctx.Personas.Add(p);
        ctx.UnidadPersonas.Add(new UnidadPersona { TenantId = tenantId, UnidadId = unidadId, PersonaId = p.Id, Rol = RolUnidadPersona.Residente, Activo = true });
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    /// <summary>Usuario de la copropiedad con cargo de vigilancia (recibe las alertas de seguridad).</summary>
    private async Task<Guid> SeedVigilanteAsync(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var p = new Persona { TipoDocumento = TipoDocumento.CC, Documento = $"V{Guid.NewGuid():N}"[..18], Nombres = "Pedro", Apellidos = "Vigilante", Email = $"v.{Guid.NewGuid():N}@test.co" };
        ctx.Personas.Add(p);
        var ut = new UsuarioTenant { TenantId = tenantId, PersonaId = p.Id, Rol = "Operativo", Estado = EstadoUsuarioTenant.Activo };
        ctx.UsuariosTenant.Add(ut);
        ctx.UsuarioTenantCargos.Add(new UsuarioTenantCargo { TenantId = tenantId, UsuarioTenantId = ut.Id, Cargo = "Supervisor de vigilancia" });
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private async Task<Guid> SeedEquipoAsync(Guid tenantId, string nombre)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var e = new EquipoActivo { TenantId = tenantId, Nombre = nombre, Categoria = CategoriaEquipo.Otros, Estado = EstadoEquipoActivo.Operativo };
        ctx.EquiposActivos.Add(e);
        await ctx.SaveChangesAsync();
        return e.Id;
    }

    private async Task LimpiarHuecosAsync(Guid tenantId, params Guid[] personas)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE mantenimiento_bitacora DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE mantenimiento_historial_estado DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE tarea_historial DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM novedades_turno WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM mantenimiento_bitacora WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM mantenimiento_historial_estado WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM mantenimiento_intervenciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tarea_historial WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tarea_comentarios WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tarea_etiqueta_asignaciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tareas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tarea_estados WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE mantenimiento_bitacora ENABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE mantenimiento_historial_estado ENABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE tarea_historial ENABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM equipos_activos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM usuario_tenant_cargos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM usuarios_tenant WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidad_personas WHERE tenant_id = {tenantId}");
        foreach (var p in personas) await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id = {p}");
    }

    [Fact]
    public async Task Paquete_renotificar_entregar_con_constancia_y_devolver_con_motivo_avisan_al_residente()
    {
        var tenantId = await SeedTenantAsync("Por Huecos Paq");
        await SeedPersonaConApplicationUser();
        var unidadId = await SeedUnidadAsync(tenantId);
        var residente = await SeedResidenteAsync(tenantId, unidadId);
        var (svc, db, scope) = Build(tenantId);
        using var _ = scope;
        var t = await svc.AbrirTurnoAsync(new AbrirTurnoRequest(_personaId, "Principal"), CancellationToken.None);
        Noti.Enviadas.Clear();

        // Registrar: el residente recibe el aviso en el app y por el canal configurado (WhatsApp por defecto).
        var c = await svc.RegistrarCorrespondenciaAsync(t.Id, new RegistrarCorrespondenciaRequest(unidadId, TipoCorrespondencia.Paquete, "Servientrega", null), CancellationToken.None);
        var alResidente = Noti.Enviadas.Where(n => n.PersonaDestinatariaId == residente && n.EntidadOrigenId == c.Id).ToList();
        Assert.Contains(alResidente, n => n.Canal == CanalNotificacion.InApp);
        Assert.Contains(alResidente, n => n.Canal == CanalNotificacion.WhatsApp && n.Cuerpo.Contains("Servientrega"));
        Assert.Equal(0, c.Recordatorios);

        // Re-notificar: cuenta recordatorios y reenvia; el semaforo sigue contando desde la recepcion.
        await db.Database.ExecuteSqlAsync($"UPDATE correspondencias SET recibido_at = now() - interval '2 hours' WHERE id = {c.Id}");
        db.ChangeTracker.Clear();   // cada request del API usa un contexto nuevo
        Noti.Enviadas.Clear();
        var r1 = (await svc.RenotificarCorrespondenciaAsync(c.Id, CancellationToken.None))!;
        var r2 = (await svc.RenotificarCorrespondenciaAsync(c.Id, CancellationToken.None))!;
        Assert.Equal(2, r2.Recordatorios);
        Assert.NotNull(r2.UltimoRecordatorioAt);
        Assert.Equal(SemaforoPaquete.Amarillo, r1.Semaforo);
        Assert.Contains(Noti.Enviadas, n => n.PersonaDestinatariaId == residente && n.Asunto!.StartsWith("Recordatorio"));
        Assert.Null(await svc.RenotificarCorrespondenciaAsync(Guid.NewGuid(), CancellationToken.None));

        // Entregar exige quien retira; guarda nombre y documento.
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.EntregarCorrespondenciaAsync(c.Id, new EntregarCorrespondenciaRequest("  "), CancellationToken.None));
        Assert.True(await svc.EntregarCorrespondenciaAsync(c.Id, new EntregarCorrespondenciaRequest(" Maria Gomez (apto 101) ", "52.123.456"), CancellationToken.None));
        var entregado = (await svc.ListarCorrespondenciaAsync(EstadoCorrespondencia.Entregado, null, CancellationToken.None)).Single(x => x.Id == c.Id);
        Assert.Equal(("Maria Gomez (apto 101)", "52.123.456"), (entregado.EntregadoA, entregado.EntregadoDoc));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RenotificarCorrespondenciaAsync(c.Id, CancellationToken.None));

        // Devolver con motivo libre: avisa al residente con el motivo.
        var c2 = await svc.RegistrarCorrespondenciaAsync(t.Id, new RegistrarCorrespondenciaRequest(unidadId, TipoCorrespondencia.Encomienda, "Coordinadora", null), CancellationToken.None);
        Noti.Enviadas.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DevolverCorrespondenciaAsync(c2.Id, new DevolverCorrespondenciaRequest(""), CancellationToken.None));
        Assert.True(await svc.DevolverCorrespondenciaAsync(c2.Id, new DevolverCorrespondenciaRequest("Direcci\u00f3n errada"), CancellationToken.None));
        Assert.Contains(Noti.Enviadas, n => n.PersonaDestinatariaId == residente && n.Cuerpo.Contains("Direcci\u00f3n errada"));

        // Con el canal "Ninguno" el residente solo recibe el aviso en el app.
        await svc.ActualizarConfiguracionAsync(new ActualizarConfiguracionRequest(true, CanalNotificacionPaquete.Ninguno, 60, 180, false, false, 365), CancellationToken.None);
        Noti.Enviadas.Clear();
        var c3 = await svc.RegistrarCorrespondenciaAsync(t.Id, new RegistrarCorrespondenciaRequest(unidadId, TipoCorrespondencia.Carta, null, null), CancellationToken.None);
        Assert.Equal(new[] { CanalNotificacion.InApp }, Noti.Enviadas.Where(n => n.PersonaDestinatariaId == residente).Select(n => n.Canal).ToArray());

        await LimpiarHuecosAsync(tenantId, residente);
        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Alerta_de_seguridad_avisa_inmediato_a_administracion_y_vigilancia_y_novedad_de_unidad_al_residente()
    {
        var tenantId = await SeedTenantAsync("Por Huecos Alerta");
        await SeedPersonaConApplicationUser();
        var unidadId = await SeedUnidadAsync(tenantId);
        var residente = await SeedResidenteAsync(tenantId, unidadId);
        var vigilante = await SeedVigilanteAsync(tenantId);
        var (svc, _, scope) = Build(tenantId);
        using var _ = scope;
        var t = await svc.AbrirTurnoAsync(new AbrirTurnoRequest(_personaId, "Principal"), CancellationToken.None);
        Noti.Enviadas.Clear();

        var a = await svc.AlertaSeguridadAsync(t.Id, new AlertaSeguridadRequest("Persona sospechosa en el parqueadero"), CancellationToken.None);
        Assert.Equal(TipoNovedadPorteria.Seguridad, a.Tipo);
        Assert.Equal(new[] { "administraci\u00f3n", "vigilancia" }, a.Avisados);
        var vig = Noti.Enviadas.Where(n => n.PersonaDestinatariaId == vigilante && n.EntidadOrigenId == a.Id).ToList();
        Assert.NotEmpty(vig);   // el vigilante (cargo) recibe la alerta
        Assert.All(vig, n => Assert.Equal(PrioridadNotificacion.Critica, n.Prioridad));
        Assert.Contains(Noti.Enviadas, n => n.PersonaDestinatariaId == _personaId && n.EntidadOrigenId == a.Id);   // guarda del turno activo
        Assert.DoesNotContain(Noti.Enviadas, n => n.PersonaDestinatariaId == residente);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AlertaSeguridadAsync(t.Id, new AlertaSeguridadRequest(" "), CancellationToken.None));
        Assert.Null(a.TareaGenerada);   // sin la opcion RN-14 de la copropiedad no se crea tarea

        // Novedad (no de seguridad) sobre una unidad: administracion + el residente; vigilancia no.
        Noti.Enviadas.Clear();
        var n = await svc.CrearNovedadAsync(t.Id, new CrearNovedadRequest("Fuga de agua en el balcon", false, TipoNovedadPorteria.General, DestinoNovedad.UnidadPrivada, unidadId), CancellationToken.None);
        Assert.Equal(new[] { "administraci\u00f3n", "el residente de la Unidad 101" }, n.Avisados);
        Assert.Contains(Noti.Enviadas, x => x.PersonaDestinatariaId == residente && x.EntidadOrigenId == n.Id);
        Assert.DoesNotContain(Noti.Enviadas, x => x.PersonaDestinatariaId == vigilante && x.Prioridad == PrioridadNotificacion.Critica);
        Assert.Null(n.TareaGenerada);

        // Destino de otra copropiedad: no se acepta.
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CrearNovedadAsync(t.Id,
            new CrearNovedadRequest("x", false, TipoNovedadPorteria.General, DestinoNovedad.UnidadPrivada, Guid.NewGuid()), CancellationToken.None));

        await LimpiarHuecosAsync(tenantId, residente, vigilante);
        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Novedad_con_tarea_dano_en_equipo_crea_intervencion_y_en_unidad_crea_tarea()
    {
        var tenantId = await SeedTenantAsync("Por Huecos Tarea");
        await SeedPersonaConApplicationUser();
        var unidadId = await SeedUnidadAsync(tenantId);
        var equipo = await SeedEquipoAsync(tenantId, "Ascensor Torre 2");
        var (svc, db, scope) = Build(tenantId);
        using var _ = scope;
        var t = await svc.AbrirTurnoAsync(new AbrirTurnoRequest(_personaId, "Principal"), CancellationToken.None);

        // RN-14: sin la opcion de la copropiedad, "Generar tarea" no crea nada.
        var sin = await svc.CrearNovedadAsync(t.Id, new CrearNovedadRequest("Rayon", true, TipoNovedadPorteria.Dano, DestinoNovedad.Equipo, equipo), CancellationToken.None);
        Assert.False(sin.GeneraTarea);
        Assert.Null(sin.IntervencionId);

        await svc.ActualizarConfiguracionAsync(new ActualizarConfiguracionRequest(true, CanalNotificacionPaquete.Ninguno, 60, 180, false, true, 365), CancellationToken.None);

        // Dano sobre un equipo -> intervencion correctiva en Mantenimiento (con su tarea).
        var d = await svc.CrearNovedadAsync(t.Id, new CrearNovedadRequest("Rayon en la puerta del ascensor", true, TipoNovedadPorteria.Dano, DestinoNovedad.Equipo, equipo), CancellationToken.None);
        Assert.NotNull(d.IntervencionId);
        Assert.StartsWith("Mantenimiento ", d.TareaGenerada);
        var iv = await db.MantenimientoIntervenciones.AsNoTracking().SingleAsync(x => x.Id == d.IntervencionId);
        Assert.Equal((TipoIntervencionMantenimiento.Correctivo, OrigenIntervencion.Porteria, d.Id, equipo), (iv.Tipo, iv.Origen, iv.OrigenReferenciaId, iv.ActivoId));
        Assert.StartsWith("Daño: Rayon en la puerta", iv.Titulo);
        Assert.Contains("Ascensor Torre 2", (await db.Tareas.AsNoTracking().SingleAsync(x => x.Id == d.TareaId)).Titulo);
        Assert.NotNull(d.TareaId);
        Assert.Equal(d.IntervencionId, (await svc.ListarNovedadesAsync(t.Id, null, CancellationToken.None)).Single(x => x.Id == d.Id).IntervencionId);

        // Mantenimiento sobre una unidad (Mantenimiento no maneja unidades) -> tarea en Tareas, vinculada a Porteria.
        var u = await svc.CrearNovedadAsync(t.Id, new CrearNovedadRequest("Humedad en el techo", true, TipoNovedadPorteria.Mantenimiento, DestinoNovedad.UnidadPrivada, unidadId), CancellationToken.None);
        Assert.Null(u.IntervencionId);
        Assert.StartsWith("Tareas ", u.TareaGenerada);
        var tarea = await db.Tareas.AsNoTracking().SingleAsync(x => x.Id == u.TareaId);
        Assert.Equal((OrigenModulo.Porteria, (Guid?)u.Id), (tarea.ModuloOrigenCodigo, tarea.ModuloOrigenEntidadId));
        Assert.Contains("[Porter\u00eda] Mantenimiento - 101", tarea.Titulo);

        // Alerta de seguridad con "Generar tarea" (marcada por defecto en el modal) -> tarea de seguimiento en Tareas.
        var al = await svc.AlertaSeguridadAsync(t.Id, new AlertaSeguridadRequest("Puerta del parqueadero forzada", GeneraTarea: true), CancellationToken.None);
        Assert.StartsWith("Tareas ", al.TareaGenerada);
        Assert.Equal((int)PrioridadTarea.Alta, (await db.Tareas.AsNoTracking().Include(x => x.PrioridadOpcion).SingleAsync(x => x.Id == al.TareaId)).PrioridadOpcion!.BaseValor);

        await LimpiarHuecosAsync(tenantId);
        await CleanTenant(tenantId);
    }
}
