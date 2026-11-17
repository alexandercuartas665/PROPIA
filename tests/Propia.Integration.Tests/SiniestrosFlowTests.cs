using Microsoft.EntityFrameworkCore;
using Propia.Application.Catalogos;
using Propia.Application.Historial;
using Propia.Application.Siniestros;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Historial;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Siniestros;
using Propia.Infrastructure.Tareas;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Modulo Siniestros (S1): flujo completo (consecutivo, elemento afectado, estados base, cambio de estado con
/// monto reconocido, actividad), validaciones, MERGE, estados editables y aislamiento por copropiedad
/// (HasQueryFilter + RLS FORCE contra PostgreSQL real con propia_app). S3: tarea de reparacion e historial
/// relacionado del elemento afectado.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SiniestrosFlowTests
{
    private readonly PostgresFixture _fx;
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateOnly Ayer = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

    public SiniestrosFlowTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Crear_numera_liga_elemento_siembra_estados_y_registra_actividad()
    {
        var t = await SeedTenantAsync("[SIN] flujo");
        try
        {
            var svc = BuildService(t);
            var zonaId = await SeedZonaAsync(t, "[SIN] Piscina");

            var s1 = await svc.CrearAsync(new CrearSiniestroRequest("Filtracion en cuarto de bombas", Ayer,
                TipoClave: "agua", ElementoTipo: ElementoSiniestro.Zona, ElementoId: zonaId, MontoEstimado: 1_500_000m), Ct);
            var s2 = await svc.CrearAsync(new CrearSiniestroRequest("Rotura de vidrio en porteria", Ayer,
                ElementoTipo: ElementoSiniestro.Otro, ElementoOtro: "Vidrio de la porteria"), Ct);

            var anio = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-5)).Year;
            Assert.Equal($"SIN-{anio}-0001", s1.Numero);
            Assert.Equal($"SIN-{anio}-0002", s2.Numero);
            Assert.Equal("[SIN] Piscina", s1.ElementoNombre);
            Assert.Equal("Da\u00f1o por agua", s1.TipoLabel);
            Assert.Equal("Vidrio de la porteria", s2.ElementoNombre);
            Assert.Equal(EstadoSiniestroClave.Reportado, s1.EstadoClave);

            var estados = await svc.ListEstadosAsync(Ct);
            Assert.Equal(6, estados.Count);
            Assert.Equal(new[] { "reportado", "tramite", "aprobado", "objetado", "pagado", "cerrado" }, estados.Select(e => e.Clave));
            Assert.Equal(2, estados.First(e => e.Clave == EstadoSiniestroClave.Reportado).SiniestrosCount);

            var actividad = await svc.ListComentariosAsync(s1.Id, Ct);
            Assert.Single(actividad);
            Assert.True(actividad[0].EsSistema);

            Assert.Equal(2, (await svc.ListAsync(Ct)).Count);
        }
        finally { await CleanupTenantAsync(t); }
    }

    [Fact]
    public async Task Pagado_y_cerrado_exigen_monto_reconocido_y_dejan_rastro()
    {
        var t = await SeedTenantAsync("[SIN] estados");
        try
        {
            var svc = BuildService(t);
            var s = await svc.CrearAsync(new CrearSiniestroRequest("Incendio en shut de basuras", Ayer, MontoReclamado: 2_000_000m), Ct);
            var estados = await svc.ListEstadosAsync(Ct);
            Guid E(string clave) => estados.First(e => e.Clave == clave).Id;

            Assert.True(await svc.CambiarEstadoAsync(s.Id, new CambiarEstadoSiniestroRequest(E(EstadoSiniestroClave.EnTramite)), Ct));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.CambiarEstadoAsync(s.Id, new CambiarEstadoSiniestroRequest(E(EstadoSiniestroClave.Pagado)), Ct));
            Assert.Contains("monto reconocido", ex.Message);

            Assert.True(await svc.CambiarEstadoAsync(s.Id, new CambiarEstadoSiniestroRequest(E(EstadoSiniestroClave.Pagado), MontoReconocido: 1_800_000m), Ct));
            var pagado = (await svc.ObtenerAsync(s.Id, Ct))!;
            Assert.Equal(1_800_000m, pagado.MontoReconocido);
            Assert.NotNull(pagado.FechaPago);
            Assert.Null(pagado.FechaCierre);

            Assert.True(await svc.CambiarEstadoAsync(s.Id, new CambiarEstadoSiniestroRequest(E(EstadoSiniestroClave.Cerrado)), Ct));
            Assert.NotNull((await svc.ObtenerAsync(s.Id, Ct))!.FechaCierre);

            // Un siniestro cerrado no puede quedar sin monto reconocido.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.ActualizarAsync(s.Id, new ActualizarSiniestroRequest(Limpiar: new[] { "montoreconocido" }), Ct));

            // Reabrir limpia la fecha de cierre.
            Assert.True(await svc.CambiarEstadoAsync(s.Id, new CambiarEstadoSiniestroRequest(E(EstadoSiniestroClave.EnTramite)), Ct));
            Assert.Null((await svc.ObtenerAsync(s.Id, Ct))!.FechaCierre);

            var eventos = (await svc.ListComentariosAsync(s.Id, Ct)).Where(c => c.EsSistema).ToList();
            Assert.Equal(5, eventos.Count);   // alta + 4 cambios de estado
            Assert.Contains(eventos, c => c.Texto.Contains("-> Pagado"));
        }
        finally { await CleanupTenantAsync(t); }
    }

    [Fact]
    public async Task Validaciones_de_fechas_elemento_tipo_y_poliza()
    {
        var t = await SeedTenantAsync("[SIN] validaciones");
        var otro = await SeedTenantAsync("[SIN] validaciones otro");
        try
        {
            var svc = BuildService(t);
            var zonaAjena = await SeedZonaAsync(otro, "[SIN] Zona ajena");
            var polizaId = await SeedPolizaAsync(t, new DateOnly(2020, 1, 1), new DateOnly(2020, 12, 31));

            async Task Falla(CrearSiniestroRequest r, string fragmento)
            {
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CrearAsync(r, Ct));
                Assert.Contains(fragmento, ex.Message);
            }

            await Falla(new CrearSiniestroRequest(" ", Ayer), "titulo");
            await Falla(new CrearSiniestroRequest("Futuro", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3))), "futura");
            await Falla(new CrearSiniestroRequest("Reporte antes", Ayer, FechaReporte: Ayer.AddDays(-5)), "reporte");
            await Falla(new CrearSiniestroRequest("Otro sin texto", Ayer, ElementoTipo: ElementoSiniestro.Otro), "Describe");
            await Falla(new CrearSiniestroRequest("Zona sin id", Ayer, ElementoTipo: ElementoSiniestro.Zona), "Elige");
            await Falla(new CrearSiniestroRequest("Zona ajena", Ayer, ElementoTipo: ElementoSiniestro.Zona, ElementoId: zonaAjena), "no existe");
            await Falla(new CrearSiniestroRequest("Tipo raro", Ayer, TipoClave: "no-existe"), "tipo");
            await Falla(new CrearSiniestroRequest("Fuera de vigencia", Ayer, PolizaId: polizaId), "vigencia");
            await Falla(new CrearSiniestroRequest("Monto negativo", Ayer, MontoEstimado: -1m), "negativos");

            Assert.Empty(await svc.ListAsync(Ct));
        }
        finally { await CleanupTenantAsync(t); await CleanupTenantAsync(otro); }
    }

    [Fact]
    public async Task Actualizar_es_merge_y_limpiar_borra_solo_lo_nombrado()
    {
        var t = await SeedTenantAsync("[SIN] merge");
        try
        {
            var svc = BuildService(t);
            var s = await svc.CrearAsync(new CrearSiniestroRequest("Dano electrico ascensor", Ayer,
                Descripcion: "Pico de tension", NumeroReclamacion: "RAD-77", MontoEstimado: 900_000m, Deducible: 100_000m), Ct);

            Assert.True(await svc.ActualizarAsync(s.Id, new ActualizarSiniestroRequest(Titulo: "Dano electrico ascensor torre 2"), Ct));
            var a = (await svc.ObtenerAsync(s.Id, Ct))!;
            Assert.Equal("Dano electrico ascensor torre 2", a.Titulo);
            Assert.Equal("Pico de tension", a.Descripcion);
            Assert.Equal("RAD-77", a.NumeroReclamacion);
            Assert.Equal(900_000m, a.MontoEstimado);

            Assert.True(await svc.ActualizarAsync(s.Id, new ActualizarSiniestroRequest(Limpiar: new[] { "deducible", "numeroreclamacion" }), Ct));
            var b = (await svc.ObtenerAsync(s.Id, Ct))!;
            Assert.Null(b.Deducible);
            Assert.Null(b.NumeroReclamacion);
            Assert.Equal(900_000m, b.MontoEstimado);
            Assert.Equal("Pico de tension", b.Descripcion);

            Assert.True(await svc.EliminarAsync(s.Id, Ct));
            Assert.Null(await svc.ObtenerAsync(s.Id, Ct));
        }
        finally { await CleanupTenantAsync(t); }
    }

    [Fact]
    public async Task Estados_base_no_se_eliminan_y_los_propios_reasignan_a_reportado()
    {
        var t = await SeedTenantAsync("[SIN] estados editables");
        try
        {
            var svc = BuildService(t);
            var estados = await svc.ListEstadosAsync(Ct);
            var reportado = estados.First(e => e.Clave == EstadoSiniestroClave.Reportado);
            var aprobado = estados.First(e => e.Clave == EstadoSiniestroClave.Aprobado);

            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.EliminarEstadoAsync(aprobado.Id, Ct));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.ActualizarEstadoAsync(reportado.Id, new ActualizarSiniestroEstadoRequest(Activo: false), Ct));

            // Renombrar un base conserva su clave (la regla sigue funcionando).
            Assert.True(await svc.ActualizarEstadoAsync(aprobado.Id, new ActualizarSiniestroEstadoRequest(Nombre: "Aprobado por aseguradora"), Ct));
            Assert.Equal(EstadoSiniestroClave.Aprobado, (await svc.ListEstadosAsync(Ct)).First(e => e.Id == aprobado.Id).Clave);

            var propio = await svc.CrearEstadoAsync(new CrearSiniestroEstadoRequest("Esperando peritaje", "#AA00AA"), Ct);
            Assert.Null(propio.Clave);
            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CrearEstadoAsync(new CrearSiniestroEstadoRequest("esperando PERITAJE", null), Ct));

            var s = await svc.CrearAsync(new CrearSiniestroRequest("Robo de bicicleta", Ayer, EstadoId: propio.Id), Ct);
            Assert.Equal(propio.Id, s.EstadoId);
            Assert.True(await svc.EliminarEstadoAsync(propio.Id, Ct));
            Assert.Equal(reportado.Id, (await svc.ObtenerAsync(s.Id, Ct))!.EstadoId);
        }
        finally { await CleanupTenantAsync(t); }
    }

    [Fact]
    public async Task Siniestros_y_sus_datos_no_se_ven_entre_copropiedades()
    {
        var tA = await SeedTenantAsync("[SIN] ISO A");
        var tB = await SeedTenantAsync("[SIN] ISO B");
        try
        {
            var svcA = BuildService(tA);
            var svcB = BuildService(tB);

            var sA = await svcA.CrearAsync(new CrearSiniestroRequest("[SIN] Siniestro A", Ayer), Ct);
            var campoA = await svcA.CrearCampoAsync(new CrearSiniestroCampoRequest("[SIN] Perito", TipoCampoTablero.Texto, null, null), Ct);
            Assert.True(await svcA.GuardarCampoValorAsync(sA.Id, campoA.Id, new GuardarSiniestroCampoValorRequest("Juan Perito"), Ct));
            await svcA.AgregarDocumentoAsync(sA.Id, "foto.jpg", "/mem/foto.jpg", Ct);
            await svcA.AgregarComentarioAsync(sA.Id, new AgregarComentarioSiniestroRequest("Llamamos a la aseguradora"), Ct);
            await svcA.CrearEstadoAsync(new CrearSiniestroEstadoRequest("[SIN] Estado A", null), Ct);

            await svcB.CrearAsync(new CrearSiniestroRequest("[SIN] Siniestro B", Ayer), Ct);

            // A ve lo suyo.
            var listaA = await svcA.ListAsync(Ct);
            Assert.Single(listaA);
            Assert.Equal(1, listaA[0].DocumentosCount);
            Assert.Equal(1, listaA[0].ComentariosCount);
            Assert.Contains(await svcA.ListTodosCamposValoresAsync(Ct), v => v.SiniestroId == sA.Id && v.Valor == "Juan Perito");

            // B no ve nada de A, ni por lista ni por id.
            var listaB = await svcB.ListAsync(Ct);
            Assert.Single(listaB);
            Assert.Equal("[SIN] Siniestro B", listaB[0].Titulo);
            Assert.Null(await svcB.ObtenerAsync(sA.Id, Ct));
            Assert.Empty(await svcB.ListDocumentosAsync(sA.Id, Ct));
            Assert.Empty(await svcB.ListComentariosAsync(sA.Id, Ct));
            Assert.Empty(await svcB.ListCamposAsync(Ct));
            Assert.Empty(await svcB.ListTodosCamposValoresAsync(Ct));
            Assert.DoesNotContain(await svcB.ListEstadosAsync(Ct), e => e.Nombre == "[SIN] Estado A");
            Assert.False(await svcB.ActualizarAsync(sA.Id, new ActualizarSiniestroRequest(Titulo: "hackeado"), Ct));
            Assert.False(await svcB.EliminarAsync(sA.Id, Ct));
            Assert.Equal("[SIN] Siniestro A", (await svcA.ObtenerAsync(sA.Id, Ct))!.Titulo);
        }
        finally { await CleanupTenantAsync(tA); await CleanupTenantAsync(tB); }
    }

    [Fact]
    public async Task Tarea_de_reparacion_y_siniestro_salen_en_el_historial_del_elemento()
    {
        var t = await SeedTenantAsync("[SIN] reparacion");
        try
        {
            var svc = BuildService(t);
            var zonaId = await SeedZonaAsync(t, "[SIN] Cuarto de bombas");
            var s = await svc.CrearAsync(new CrearSiniestroRequest("Filtracion de agua", Ayer,
                Descripcion: "Gotea el techo", ElementoTipo: ElementoSiniestro.Zona, ElementoId: zonaId), Ct);
            Assert.Null(s.TareaNumero);

            var conTarea = await svc.CrearTareaReparacionAsync(s.Id, Ct);
            Assert.NotNull(conTarea);
            Assert.NotNull(conTarea!.TareaId);
            Assert.StartsWith("T-", conTarea.TareaNumero);
            Assert.False(string.IsNullOrEmpty(conTarea.TareaEstado));

            // La tarea nace en el tablero General, ligada a la zona (historial) y al siniestro (Origen).
            await using (var db = AppDb(t))
            {
                var tarea = await db.Tareas.AsNoTracking().FirstAsync(x => x.Id == conTarea.TareaId);
                Assert.Equal(zonaId, tarea.OrigenEntidadId);
                Assert.Equal("zona", tarea.OrigenTipo);
                Assert.Equal(OrigenModulo.Siniestro, tarea.ModuloOrigenCodigo);
                Assert.Equal(s.Id, tarea.ModuloOrigenEntidadId);
                Assert.NotNull(tarea.TableroId);
                Assert.Contains("Filtracion de agua", tarea.Titulo);
            }

            // Una sola tarea vigente por siniestro.
            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CrearTareaReparacionAsync(s.Id, Ct));
            Assert.Contains((await svc.ListComentariosAsync(s.Id, Ct)), c => c.EsSistema && c.Texto.Contains(conTarea.TareaNumero!));

            // Historial de la zona: el siniestro y su tarea.
            await using (var db = AppDb(t))
            {
                var hist = await new HistorialRelacionadoService(db).GetAsync(TipoEntidadHistorial.ZonaComun, zonaId, Ct);
                Assert.Equal(1, hist.TotalSiniestros);
                Assert.Equal(1, hist.TotalTareas);
                var item = Assert.Single(hist.Items, i => i.Origen == OrigenHistorial.Siniestro);
                Assert.Equal(s.Numero, item.Codigo);
                Assert.Equal($"/siniestros#siniestro-{s.Id}", item.Ruta);
                // La fecha de ocurrencia se ve el mismo dia en hora de Colombia (no el anterior).
                Assert.Equal(Ayer, DateOnly.FromDateTime(item.Fecha.ToOffset(TimeSpan.FromHours(-5)).DateTime));
                // Otra zona no ve el siniestro.
                var otra = await new HistorialRelacionadoService(db).GetAsync(TipoEntidadHistorial.ZonaComun, Guid.NewGuid(), Ct);
                Assert.Equal(0, otra.TotalSiniestros);
            }

            Assert.Null(await svc.CrearTareaReparacionAsync(Guid.NewGuid(), Ct));
        }
        finally { await CleanupTenantAsync(t); }
    }

    // ----------------------------- infraestructura -----------------------------

    /// <summary>Lista "siniestro.tipo" fija para no depender del catalogo global sembrado.</summary>
    private sealed class OpcionesFijas : IOpcionesEfectivas
    {
        public Task<IReadOnlyList<OpcionEfectivaDto>> OpcionesAsync(string lista, bool incluirOcultas = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<OpcionEfectivaDto>>(lista == SiniestrosService.ListaTipo
                ? new[] { new OpcionEfectivaDto("agua", "Da\u00f1o por agua", 0, null, true, true), new OpcionEfectivaDto("incendio", "Incendio", 1, null, true, true) }
                : Array.Empty<OpcionEfectivaDto>());
    }

    private PropiaDbContext AppDb(Guid tenantId)
    {
        var tenantCtx = new TenantContext();
        tenantCtx.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .AddInterceptors(new TenantConnectionInterceptor(tenantCtx))
            .Options;
        return new PropiaDbContext(options, tenantCtx);
    }

    private ISiniestrosService BuildService(Guid tenantId)
    {
        var tenantCtx = new TenantContext();
        tenantCtx.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .AddInterceptors(new TenantConnectionInterceptor(tenantCtx))
            .Options;
        var db = new PropiaDbContext(options, tenantCtx);
        var http = new Microsoft.AspNetCore.Http.HttpContextAccessor();
        return new SiniestrosService(db, http, new OpcionesFijas(), new TareasService(db, tenantCtx, http, new FakeNotificacionDispatcher()));
    }

    private PropiaDbContext OwnerDb()
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        return new PropiaDbContext(options, new TenantContext());
    }

    private async Task<Guid> SeedTenantAsync(string nombre)
    {
        await using var ctx = OwnerDb();
        var t = new Tenant { Nombre = nombre, Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.SinAdmin };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        return t.Id;
    }

    private async Task<Guid> SeedZonaAsync(Guid tenantId, string nombre)
    {
        await using var db = AppDb(tenantId);
        var z = new ZonaComun { Nombre = nombre };
        db.ZonasComunes.Add(z);
        await db.SaveChangesAsync();
        return z.Id;
    }

    private async Task<Guid> SeedPolizaAsync(Guid tenantId, DateOnly inicio, DateOnly fin)
    {
        await using var db = AppDb(tenantId);
        var p = new Poliza { Aseguradora = "[SIN] Aseguradora", NumeroPoliza = "[SIN]-P1", FechaInicio = inicio, FechaFin = fin };
        db.Polizas.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    private async Task CleanupTenantAsync(Guid tenantId)
    {
        await using var ctx = OwnerDb();
        // Las tablas tienen RLS FORCE: el owner fija el tenant antes de borrar.
        await ctx.Database.OpenConnectionAsync();
        await ctx.Database.ExecuteSqlRawAsync($"SELECT set_config('app.tenant_id', '{tenantId}', false)");
        foreach (var tabla in new[] { "siniestro_campo_valores", "siniestro_campos", "siniestro_documentos", "siniestro_comentarios", "siniestros", "siniestro_estados", "polizas", "zonas_comunes" })
            await ctx.Database.ExecuteSqlRawAsync($"DELETE FROM {tabla} WHERE tenant_id = '{tenantId}'");
        // S3: la tarea de reparacion y el tablero General que se siembra al crearla (historial inmutable: sin triggers).
        await ctx.Database.ExecuteSqlRawAsync("ALTER TABLE tarea_historial DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlRawAsync($"DELETE FROM tarea_historial WHERE tenant_id = '{tenantId}'");
        await ctx.Database.ExecuteSqlRawAsync("ALTER TABLE tarea_historial ENABLE TRIGGER ALL");
        foreach (var tabla in new[] { "tarea_campo_valores", "tareas", "tarea_prioridades", "tablero_campos_config", "tablero_campos", "tablero_usuarios", "tarea_estados", "tableros" })
            await ctx.Database.ExecuteSqlRawAsync($"DELETE FROM {tabla} WHERE tenant_id = '{tenantId}'");
        await ctx.Database.ExecuteSqlRawAsync($"DELETE FROM tenants WHERE id = '{tenantId}'");
    }
}
